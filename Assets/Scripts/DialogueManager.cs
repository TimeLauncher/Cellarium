using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 대화 UI 전체를 담당한다 (기획서 (1) 'NPC 상호작용'의 ※대화 이벤트 부분).
//
// 흐름
//   NpcInteractable 이 E키로 Play(data) 호출
//   → PC 조작 잠금(PlayerInputLock) + 대화창 팝업 + 직업/이름 즉시 표시
//   → 본문을 한 글자씩 타이핑
//   → 지정키: 타이핑 중이면 전체 표시로 스킵 / 다 표시됐으면 다음 줄 or 종료
//   → 종료 시 대화창 닫고 조작 잠금 해제
//
// ★ UI를 인스펙터에 안 꽂아도 동작한다:
//   대화창 캔버스가 아직 없어서 코드를 확인할 수 없는 상황을 피하려고, 참조가 비어 있으면
//   런타임에 임시 대화창을 직접 만들어 쓴다 (SavePoint가 안내 이미지를 자동 생성하는 것과 같은 방식).
//   디자인이 나오면 씬에 제대로 만든 캔버스를 이 컴포넌트 필드에 연결하기만 하면 자동 생성은 꺼진다.
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI 연결 (비우면 임시 UI를 자동 생성)")]
    [Tooltip("대화창 전체를 감싸는 오브젝트. 대화 중에만 활성화된다")]
    public GameObject dialoguePanel;
    public Text jobText;
    public Text nameText;
    public Text bodyText;
    [Tooltip("기획서 ④: 지정키 입력이 가능한 상태일 때만 대화 UI 하단에 표시되는 아이콘")]
    public GameObject continueIcon;

    [Header("타이핑 연출")]
    [Tooltip("한 글자가 찍히는 간격(초). 작을수록 빠르다. 0이면 즉시 전부 표시")]
    public float charInterval = 0.03f;
    [Tooltip("켜면 Time.timeScale의 영향을 받지 않는다 (일시정지 중에도 타이핑이 진행됨)")]
    public bool useUnscaledTime = true;

    [Header("지정키")]
    [Tooltip("기획서: 스페이스 바, E, 마우스 좌클릭. 필요하면 여기서 추가/변경")]
    public KeyCode[] advanceKeys = { KeyCode.Space, KeyCode.E, KeyCode.Return };
    public bool advanceOnLeftClick = true;

    // 대화 상태
    DialogueData current;
    int lineIndex;
    bool isPlaying;
    bool isTyping;
    bool lineComplete;      // 현재 줄이 전부 표시됐는가 (= 지정키로 넘어갈 수 있는 상태)
    string currentFullText = "";
    System.Action onComplete;

    // ★ E키로 대화를 시작한 그 프레임에 같은 E키가 '다음으로'까지 먹어버리면
    //   첫 줄이 순식간에 스킵된다. 시작한 프레임에는 지정키 입력을 무시한다.
    int startedFrame = -1;

    bool lockAcquired;      // PlayerInputLock을 우리가 걸었는지 (짝을 정확히 맞추기 위함)
    bool builtOwnUI;

    public static bool IsPlaying => Instance != null && Instance.isPlaying;

    void Awake()
    {
        // 씬에 직접 배치한 것이 있으면 그쪽이 우선. 중복은 버린다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        HidePanel();
    }

    void OnDestroy()
    {
        // 대화 중에 씬이 리로드되면(사망 등) 조작 잠금이 남아 부활 후 못 움직인다.
        if (lockAcquired)
        {
            PlayerInputLock.Release();
            lockAcquired = false;
        }
        if (Instance == this) Instance = null;
    }

    // 씬에 DialogueManager가 없어도 대화가 되도록, 없으면 만들어서 돌려준다.
    public static DialogueManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        DialogueManager found = FindFirstObjectByType<DialogueManager>();
        if (found != null) { Instance = found; return Instance; }

        GameObject go = new GameObject("DialogueManager (auto)");
        return go.AddComponent<DialogueManager>();
    }

    // ── 외부 진입점 ────────────────────────────────────────────────

    public void Play(DialogueData data, System.Action onDialogueComplete = null)
    {
        if (data == null || data.IsEmpty)
        {
            Debug.LogWarning("[대화] 표시할 대사가 없습니다 (DialogueData가 비어 있음)");
            onDialogueComplete?.Invoke();
            return;
        }

        if (isPlaying) return; // 이미 대화 중이면 무시 (중첩 방지)

        current = data;
        onComplete = onDialogueComplete;
        lineIndex = 0;
        isPlaying = true;
        startedFrame = Time.frameCount;

        EnsureUI();

        // 기획서 ①: 대화 UI 팝업 + PC 조작 불가능 상태
        if (!lockAcquired)
        {
            PlayerInputLock.Acquire();
            lockAcquired = true;
        }

        ShowPanel();
        ShowLine(lineIndex);
    }

    // 외부에서 강제로 끊어야 할 때 (씬 전환 연출 등)
    public void StopDialogue()
    {
        if (!isPlaying) return;
        EndDialogue();
    }

    // ── 진행 ──────────────────────────────────────────────────────

    void Update()
    {
        if (!isPlaying) return;
        if (Time.frameCount == startedFrame) return; // 시작 프레임의 E키가 그대로 넘김으로 먹히는 것 방지
        if (!AdvancePressed()) return;

        if (isTyping)
        {
            // 기획서 ③: 지정키로 텍스트 표시를 스킵 — 남은 글자를 즉시 전부 표시
            SkipTyping();
            return;
        }

        if (!lineComplete) return;

        // 기획서 ④: 다 표시됐으면 '다음 대화' 혹은 '대화 종료'
        if (lineIndex + 1 < current.LineCount)
        {
            lineIndex++;
            ShowLine(lineIndex);
        }
        else
        {
            EndDialogue();
        }
    }

    bool AdvancePressed()
    {
        if (advanceOnLeftClick && Input.GetMouseButtonDown(0)) return true;

        if (advanceKeys != null)
            foreach (KeyCode k in advanceKeys)
                if (Input.GetKeyDown(k)) return true;

        return false;
    }

    void ShowLine(int index)
    {
        // 기획서 ②: 직업 및 이름은 창이 뜰 때 바로 표시, 본문만 한 글자씩
        if (jobText != null) jobText.text = current.JobOf(index);
        if (nameText != null) nameText.text = current.SpeakerOf(index);

        currentFullText = current.lines[index].text ?? "";

        float interval = current.lines[index].charIntervalOverride > 0f
            ? current.lines[index].charIntervalOverride
            : charInterval;

        StopAllCoroutines();
        lineComplete = false;
        SetContinueIcon(false);

        if (interval <= 0f || currentFullText.Length == 0)
        {
            SkipTyping();
            return;
        }

        StartCoroutine(TypeRoutine(interval));
    }

    IEnumerator TypeRoutine(float interval)
    {
        isTyping = true;
        if (bodyText != null) bodyText.text = "";

        float timer = 0f;
        int shown = 0;

        while (shown < currentFullText.Length)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            // 프레임이 길어도(저사양·씬 로드 직후) 글자 수가 밀리지 않도록 한 프레임에 여러 글자 처리
            while (timer >= interval && shown < currentFullText.Length)
            {
                timer -= interval;
                shown++;
            }

            if (bodyText != null) bodyText.text = currentFullText.Substring(0, shown);
            yield return null;
        }

        FinishLine();
    }

    void SkipTyping()
    {
        StopAllCoroutines();
        FinishLine();
    }

    void FinishLine()
    {
        isTyping = false;
        lineComplete = true;

        if (bodyText != null) bodyText.text = currentFullText;
        else Debug.Log($"[대화] {current.SpeakerOf(lineIndex)}: {currentFullText}"); // UI가 없을 때 확인용

        SetContinueIcon(true); // 지정키 입력이 가능한 상태 → 하단 아이콘 표시
    }

    void EndDialogue()
    {
        StopAllCoroutines();

        isPlaying = false;
        isTyping = false;
        lineComplete = false;
        current = null;

        HidePanel();

        // 기획서 ④: 대화 종료 시 PC 조작 가능 및 대화 UI 종료
        if (lockAcquired)
        {
            PlayerInputLock.Release();
            lockAcquired = false;
        }

        System.Action cb = onComplete;
        onComplete = null;
        cb?.Invoke();
    }

    // ── UI ────────────────────────────────────────────────────────

    void ShowPanel()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
    }

    void HidePanel()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        SetContinueIcon(false);
    }

    void SetContinueIcon(bool visible)
    {
        if (continueIcon != null && continueIcon.activeSelf != visible)
            continueIcon.SetActive(visible);
    }

    // 인스펙터에 아무것도 안 꽂혀 있으면 임시 대화창을 만든다.
    // 하나라도 꽂혀 있으면(디자인 적용됨) 손대지 않는다.
    void EnsureUI()
    {
        if (builtOwnUI) return;
        if (dialoguePanel != null || bodyText != null) return;

        builtOwnUI = true;
        Debug.LogWarning("[대화] 대화창 UI가 연결되지 않아 임시 UI를 생성합니다. " +
                         "디자인이 나오면 DialogueManager의 Dialogue Panel / Job Text / Name Text / Body Text / Continue Icon 을 연결하세요.");

        Font font = GetDefaultFont();

        GameObject canvasGo = new GameObject("DialogueCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // HUD보다 위
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 기획서 ②: '화면 상단' 다이얼로그 창
        dialoguePanel = new GameObject("Panel");
        dialoguePanel.transform.SetParent(canvasGo.transform, false);
        Image bg = dialoguePanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        RectTransform panelRt = dialoguePanel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -40f);
        panelRt.sizeDelta = new Vector2(1200f, 300f);

        jobText = BuildText(dialoguePanel.transform, font, "JobText", 28, TextAnchor.MiddleCenter,
                            new Vector2(0f, -20f), new Vector2(1100f, 40f));
        nameText = BuildText(dialoguePanel.transform, font, "NameText", 36, TextAnchor.MiddleCenter,
                             new Vector2(0f, -62f), new Vector2(1100f, 48f));
        bodyText = BuildText(dialoguePanel.transform, font, "BodyText", 30, TextAnchor.UpperLeft,
                             new Vector2(0f, -130f), new Vector2(1100f, 140f));

        continueIcon = BuildText(dialoguePanel.transform, font, "ContinueIcon", 30, TextAnchor.MiddleCenter,
                                 new Vector2(0f, -272f), new Vector2(60f, 40f)).gameObject;
        continueIcon.GetComponent<Text>().text = "▼";

        dialoguePanel.SetActive(false);
        SetContinueIcon(false);
    }

    Text BuildText(Transform parent, Font font, string name, int size, TextAnchor anchor,
                   Vector2 anchoredPos, Vector2 size2)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Text t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
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

    // Unity 6에는 내장 Arial이 없다 (LegacyRuntime.ttf로 대체됨).
    static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }
}
