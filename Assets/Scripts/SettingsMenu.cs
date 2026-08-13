using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 설정 화면 (기획서 (7) '설정 기능').
//
// 기획서: "ESC 입력 이후 'Pause' 화면에서 '설정' 선택 시 설정 UI로 전환 / 플레이 정지 상태"
//   → 일시정지 메뉴의 '설정' 버튼 OnClick에 이 컴포넌트의 Open() 을 연결하면 된다.
//
// ★ UI를 인스펙터에 안 꽂아도 동작한다 (DialogueManager와 같은 방식).
//   참조가 비어 있으면 런타임에 임시 설정창을 만들어 쓴다. 디자인이 나오면
//   Settings Panel 과 각 Text를 연결하기만 하면 자동 생성은 꺼진다.
//
// ★ 일시정지 메뉴(PauseMenu)와의 ESC 충돌 처리
//   PauseMenu도 Update에서 ESC를 읽기 때문에, 설정창에서 ESC를 누르면 설정창이 닫히면서
//   일시정지까지 같이 풀려버린다. 설정창이 열려 있는 동안에는 PauseMenu 컴포넌트를
//   잠시 꺼두는 방식으로 막는다 (팀원 스크립트를 고치지 않아도 되게).
//
// ★ 배치 위치 주의
//   일시정지 패널의 자식으로 두면 안 된다. PauseMenu가 그 패널을 SetActive(false)로 끄는 순간
//   이 컴포넌트의 Update도 같이 멈춰서 ESC로 설정창을 닫을 수 없게 된다.
//   Canvas 밖의 별도 빈 오브젝트에 붙일 것.
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.isOpen;

    [Header("UI 연결 (비우면 임시 UI를 자동 생성)")]
    [Tooltip("설정창 전체를 감싸는 오브젝트. 열렸을 때만 활성화된다")]
    public GameObject settingsPanel;

    [Header("값 표시용 Text (연결한 것만 갱신된다)")]
    public Text resolutionText;
    public Text fullscreenText;
    public Text masterVolumeText;
    public Text bgmVolumeText;
    public Text sfxVolumeText;
    public Text brightnessText;

    [Header("입력")]
    public KeyCode closeKey = KeyCode.Escape;

    [Tooltip("이 키로 설정창을 바로 연다. None이면 버튼이나 코드로만 열린다. " +
             "일시정지 메뉴가 없는 테스트/데모 씬에서 확인용으로 쓸 것 — " +
             "실제 게임에서는 일시정지 화면의 '설정' 버튼에 Open()을 연결한다")]
    public KeyCode openKey = KeyCode.None;
    [Tooltip("자동 생성 UI에서 항목 사이를 오르내리는 키(방향키도 같이 동작)")]
    public KeyCode upKey = KeyCode.W;
    public KeyCode downKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;

    [Tooltip("볼륨/밝기를 ◀ ▶ 한 번에 얼마나 바꿀지")]
    [Range(0.05f, 0.5f)] public float step = 0.1f;

    bool isOpen;
    bool builtOwnUI;
    int openedFrame = -1;          // 연 프레임의 키 입력이 곧바로 '닫기'로 먹히는 것 방지
    float savedTimeScale = 1f;
    int cursor;                    // 자동 생성 UI에서 선택 중인 줄
    PauseMenu suppressedPauseMenu; // 열려 있는 동안 꺼둔 일시정지 메뉴

    // 설정 항목 한 줄 — 자동 생성 UI와 키보드 조작이 같은 목록을 공유한다
    class Option
    {
        public string label;
        public System.Func<string> value;
        public System.Action prev;
        public System.Action next;
        public Text labelText;
        public Text valueText;
    }

    readonly List<Option> options = new List<Option>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        BuildOptionList();

        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 설정창을 연 채로 씬이 바뀌면 시간이 멈춘 채 남는다
        if (isOpen) RestoreTime();
    }

    void BuildOptionList()
    {
        options.Clear();

        options.Add(new Option
        {
            label = "해상도",
            value = () => GameSettings.ResolutionLabel(GameSettings.ResolutionIndex),
            prev = PreviousResolution,
            next = NextResolution,
        });

        options.Add(new Option
        {
            label = "전체화면",
            value = () => GameSettings.Fullscreen ? "켜짐" : "꺼짐",
            prev = ToggleFullscreen,
            next = ToggleFullscreen,
        });

        options.Add(new Option
        {
            label = "전체 볼륨",
            value = () => Percent(GameSettings.MasterVolume),
            prev = () => GameSettings.MasterVolume -= step,
            next = () => GameSettings.MasterVolume += step,
        });

        options.Add(new Option
        {
            label = "배경음",
            value = () => Percent(GameSettings.BgmVolume),
            prev = () => GameSettings.BgmVolume -= step,
            next = () => GameSettings.BgmVolume += step,
        });

        options.Add(new Option
        {
            label = "효과음",
            value = () => Percent(GameSettings.SfxVolume),
            prev = () => GameSettings.SfxVolume -= step,
            next = () => GameSettings.SfxVolume += step,
        });

        options.Add(new Option
        {
            label = "화면 밝기",
            value = () => Percent(GameSettings.Brightness),
            prev = () => GameSettings.Brightness -= step,
            next = () => GameSettings.Brightness += step,
        });
    }

    static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

    // ── 열기 / 닫기 ───────────────────────────────────────────────────

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        EnsureUI();

        // 기획서: '플레이 정지 상태'.
        // 일시정지 메뉴에서 들어온 경우 이미 0이므로, 닫을 때 되돌릴 수 있게 원래 값을 기억한다.
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        SuppressPauseMenu(true);

        if (settingsPanel != null) settingsPanel.SetActive(true);
        cursor = 0;
        openedFrame = Time.frameCount;
        Refresh();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (settingsPanel != null) settingsPanel.SetActive(false);

        GameSettings.Save();
        RestoreTime();
        SuppressPauseMenu(false);
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    void RestoreTime()
    {
        // 일시정지 메뉴 위에서 열렸으면 0으로 되돌아가 일시정지가 유지된다
        Time.timeScale = savedTimeScale;
    }

    // 설정창이 열려 있는 동안 PauseMenu의 Update(ESC 처리)를 멈춘다
    void SuppressPauseMenu(bool suppress)
    {
        if (suppress)
        {
            PauseMenu pm = FindFirstObjectByType<PauseMenu>();
            if (pm != null && pm.enabled)
            {
                pm.enabled = false;
                suppressedPauseMenu = pm;
            }
        }
        else if (suppressedPauseMenu != null)
        {
            suppressedPauseMenu.enabled = true;
            suppressedPauseMenu = null;
        }
    }

    // ── 입력 ──────────────────────────────────────────────────────────

    void Update()
    {
        if (!isOpen)
        {
            if (openKey != KeyCode.None && Input.GetKeyDown(openKey)) Open();
            return;
        }

        // 연 프레임에는 닫기 키를 안 읽는다 (openKey와 closeKey가 같아도 바로 안 닫히도록)
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        if (options.Count == 0) return;

        if (Input.GetKeyDown(upKey) || Input.GetKeyDown(KeyCode.UpArrow))
            MoveCursor(-1);
        else if (Input.GetKeyDown(downKey) || Input.GetKeyDown(KeyCode.DownArrow))
            MoveCursor(1);
        else if (Input.GetKeyDown(leftKey) || Input.GetKeyDown(KeyCode.LeftArrow))
            { options[cursor].prev?.Invoke(); Refresh(); }
        else if (Input.GetKeyDown(rightKey) || Input.GetKeyDown(KeyCode.RightArrow))
            { options[cursor].next?.Invoke(); Refresh(); }
    }

    void MoveCursor(int delta)
    {
        cursor = (cursor + delta + options.Count) % options.Count;
        Refresh();
    }

    // ── 버튼용 공개 메서드 (직접 만든 UI에서 OnClick으로 연결) ────────

    public void NextResolution()
    {
        int count = GameSettings.ResolutionList.Count;
        GameSettings.ResolutionIndex = Mathf.Min(count - 1, GameSettings.ResolutionIndex + 1);
        Refresh();
    }

    public void PreviousResolution()
    {
        GameSettings.ResolutionIndex = Mathf.Max(0, GameSettings.ResolutionIndex - 1);
        Refresh();
    }

    public void ToggleFullscreen()
    {
        GameSettings.Fullscreen = !GameSettings.Fullscreen;
        Refresh();
    }

    // Slider.onValueChanged 에 연결할 용도 (0~1)
    public void SetMasterVolume(float v) { GameSettings.MasterVolume = v; Refresh(); }
    public void SetBgmVolume(float v) { GameSettings.BgmVolume = v; Refresh(); }
    public void SetSfxVolume(float v) { GameSettings.SfxVolume = v; Refresh(); }
    public void SetBrightness(float v) { GameSettings.Brightness = v; Refresh(); }

    public void ResetToDefaults()
    {
        GameSettings.ResetToDefaults();
        Refresh();
    }

    // ── 표시 갱신 ─────────────────────────────────────────────────────

    public void Refresh()
    {
        if (resolutionText != null) resolutionText.text = GameSettings.ResolutionLabel(GameSettings.ResolutionIndex);
        if (fullscreenText != null) fullscreenText.text = GameSettings.Fullscreen ? "켜짐" : "꺼짐";
        if (masterVolumeText != null) masterVolumeText.text = Percent(GameSettings.MasterVolume);
        if (bgmVolumeText != null) bgmVolumeText.text = Percent(GameSettings.BgmVolume);
        if (sfxVolumeText != null) sfxVolumeText.text = Percent(GameSettings.SfxVolume);
        if (brightnessText != null) brightnessText.text = Percent(GameSettings.Brightness);

        // 자동 생성 UI
        for (int i = 0; i < options.Count; i++)
        {
            Option o = options[i];
            if (o.valueText != null) o.valueText.text = "◀  " + o.value() + "  ▶";
            if (o.labelText != null)
                o.labelText.color = (i == cursor) ? new Color(1f, 0.85f, 0.35f) : Color.white;
        }
    }

    // ── 임시 UI 자동 생성 ─────────────────────────────────────────────

    void EnsureUI()
    {
        if (builtOwnUI) return;
        if (settingsPanel != null) return; // 디자인된 창이 연결돼 있음

        builtOwnUI = true;
        Debug.LogWarning("[설정] 설정창 UI가 연결되지 않아 임시 UI를 생성합니다. " +
                         "디자인이 나오면 SettingsMenu의 Settings Panel 과 각 Text를 연결하세요.");

        Font font = GetDefaultFont();

        GameObject canvasGo = new GameObject("SettingsCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600; // 대화창(500)보다 위
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        settingsPanel = new GameObject("Panel");
        settingsPanel.transform.SetParent(canvasGo.transform, false);
        Image bg = settingsPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        RectTransform rt = settingsPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(900f, 560f);

        BuildText(settingsPanel.transform, font, "Title", 44, TextAnchor.MiddleCenter,
                  new Vector2(0f, -30f), new Vector2(860f, 60f)).text = "설정";

        float y = -120f;
        foreach (Option o in options)
        {
            o.labelText = BuildText(settingsPanel.transform, font, o.label + "Label", 30, TextAnchor.MiddleLeft,
                                    new Vector2(-200f, y), new Vector2(320f, 46f));
            o.labelText.text = o.label;

            o.valueText = BuildText(settingsPanel.transform, font, o.label + "Value", 30, TextAnchor.MiddleCenter,
                                    new Vector2(200f, y), new Vector2(420f, 46f));
            y -= 60f;
        }

        BuildText(settingsPanel.transform, font, "Help", 22, TextAnchor.MiddleCenter,
                  new Vector2(0f, -500f), new Vector2(860f, 40f)).text =
            "W/S 항목 이동    A/D 값 변경    ESC 닫기 (자동 저장)";

        settingsPanel.SetActive(false);
    }

    Text BuildText(Transform parent, Font font, string goName, int size, TextAnchor anchor,
                   Vector2 anchoredPos, Vector2 size2)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        Text t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size2;

        return t;
    }

    // Unity 6에는 내장 Arial이 없다 (LegacyRuntime.ttf로 대체됨)
    static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }
}
