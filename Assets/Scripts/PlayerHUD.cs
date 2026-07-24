using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("체력 - 칸 형식 (한 칸 = 50)")]
    public Image[] hpNotches;      // 왼→오 순서로 배치. 최대 체력에 맞춰 maxHp/50 칸까지만 표시
    public Sprite hpFilledSprite;  // 채워진 칸 스프라이트 (없으면 색으로 대체)
    public Sprite hpEmptySprite;   // 빈 칸 스프라이트
    public float hpPerNotch = 50f; // 한 칸이 나타내는 체력
    public Slider hpSlider;        // (선택) 예전 슬라이더 방식도 계속 갱신

    [Header("분열 게이지 - 바 + 100당 칸")]
    public Slider fissionGauge;             // 지속 상승하는 바 형태 게이지
    public Image[] fissionNotches;          // 100 게이지마다 1칸씩 활성화
    public Sprite fissionNotchOnSprite;     // 활성화된 칸 (없으면 색으로 대체)
    public Sprite fissionNotchOffSprite;    // 비활성 칸
    public float fissionPerNotch = 100f;    // 한 칸이 나타내는 분열 게이지

    [Header("칸 자동 생성 (이미지 에셋 없이 데모용)")]
    public bool autoBuildNotches = false;         // 켜면 아래 부모 밑에 색깔 사각형 칸을 런타임에 생성
    public RectTransform hpNotchParent;           // 체력 칸을 생성할 부모 (Canvas 아래 빈 오브젝트)
    public RectTransform fissionNotchParent;      // 분열 칸을 생성할 부모
    public Vector2 notchSize = new Vector2(24f, 24f);
    public float notchSpacing = 4f;
    public Color hpNotchColor = new Color(0.9f, 0.25f, 0.25f);      // 체력 칸 색 (빨강 계열)
    public Color fissionNotchColor = new Color(0.35f, 0.7f, 1f);    // 분열 칸 색 (하늘색 계열)

    [Header("분열 연속 바 자동 생성 (칸 뒤에 겹쳐 표시)")]
    public bool autoBuildFissionBar = true;                         // 분열 칸 뒤에 지속 상승하는 바를 겹쳐 생성 (칸이 앞, 바가 뒤)
    public float fissionBarHeight = 24f;                            // 바 두께 (칸 높이와 같게 두면 배경 바처럼 보임)
    public float fissionBarXOffset = 0f;                            // 칸 왼쪽 기준 바의 X 위치 조정 (좌표 이동)
    public float fissionBarYOffset = 0f;                            // 칸 중심 기준 바의 Y 위치 조정 (0 = 칸과 같은 높이)
    public Color fissionBarBgColor = new Color(0f, 0f, 0f, 0.5f);   // 바 배경색 (스프라이트 없을 때)
    public Color fissionBarFillColor = new Color(0.35f, 0.7f, 1f);  // 바 채움색 (스프라이트 없을 때)
    public Sprite fissionBarBgSprite;    // 바 배경 스프라이트 (예: FissionBarEmpty). 없으면 색 사각형
    public Sprite fissionBarFillSprite;  // 바 채움 스프라이트 (예: fissionBar). Filled(가로)로 게이지 비율만큼 채움

    private int builtHpNotchCount = -1;      // 마지막으로 생성한 체력 칸 수 (최대체력 바뀌면 다시 만듦)
    private int builtFissionNotchCount = -1; // 마지막으로 생성한 분열 칸 수
    private Image fissionBarFillImg;        // 자동 생성한 바의 채움 Image
    private RectTransform fissionBarFillRT; // 채움의 RectTransform (스프라이트 없을 때 폭으로 조절)
    private RectTransform fissionBarBgRT;   // 배경 바의 RectTransform (좌표 실시간 반영용)
    private float fissionBarWidth;

    [Header("대시 쿨다운 (Image - Filled 타입)")]
    public Image dashCooldownFill;

    [Header("분열 가능 횟수 - 본체 아이콘")]
    public Image mainBodyIcon;
    public Sprite mainBodyDefaultSprite;      // 조종 중일 때
    public Sprite mainBodyUncontrolledSprite; // 비조종일 때

    [Header("분열 가능 횟수 - 분열체 아이콘 (좌→우, 소모는 우측부터)")]
    public Image[] fissionSlotIcons;
    [Tooltip("분열체가 없는 빈 슬롯에 쓸 이미지. 비워두면 빈 슬롯은 숨겨짐(안 보임)")]
    public Sprite slotDefaultSprite;      // 빈 슬롯 (분열체 없음)
    [Tooltip("분열체가 있고 지금 조종 중일 때")]
    public Sprite slotControlledSprite;   // 조종 중인 분열체
    [Tooltip("분열체가 있지만 지금 조종하지 않을 때")]
    public Sprite slotUncontrolledSprite; // 존재하지만 비조종인 분열체
    [Range(0f, 1f)] public float uncontrolledSlotAlpha = 0.4f; // 스프라이트가 없을 때만, 비조종 슬롯을 흐리게 하는 투명도

    [Header("보유 재화")]
    public Image cellIcon;
    public Text cellAmountText;
    public Image darkCellIcon;
    public Text darkCellAmountText;

    void Update()
    {
        PlayerManager manager = PlayerManager.Instance;
        if (manager == null) return;

        PlayerController controlled = manager.currentPlayer;
        if (controlled == null) return;

        PlayerController mainBodyRef = manager.allPlayers.Count > 0 ? manager.allPlayers[0] : controlled;
        EnsureNotchesBuilt(controlled, mainBodyRef);

        // 체력·대시는 현재 조종 중인 캐릭터 기준
        UpdateHp(controlled);
        UpdateDashCooldown(controlled);

        // 본체 아이콘은 분열 해금/분열체 유무와 무관하게 항상 표시 ("이게 나(본체)"라는 표시)
        UpdateMainBodyIcon(manager);

        // 분열 능력 해금 전(A02 획득 전)엔 분열 게이지/가능횟수(분열체 슬롯) UI만 숨긴다
        bool fissionUnlocked = manager.fissionUnlocked;
        SetFissionUIVisible(fissionUnlocked);

        if (fissionUnlocked)
        {
            // 분열 게이지는 항상 본체(allPlayers[0]) 기준
            PlayerController mainBody = manager.allPlayers.Count > 0 ? manager.allPlayers[0] : controlled;
            UpdateFissionGauge(mainBody);

            UpdateFissionIcons(manager);
        }
    }

    private bool fissionUIVisible = true;

    // 분열 관련 UI(게이지 바/칸, 분열 가능 횟수 아이콘)를 한꺼번에 켜고 끈다.
    // autoBuild로 만든 칸/바는 fissionNotchParent 자식이라 부모만 꺼도 같이 숨겨진다.
    void SetFissionUIVisible(bool on)
    {
        if (fissionUIVisible == on) return; // 상태 바뀔 때만 토글
        fissionUIVisible = on;

        if (fissionGauge != null) fissionGauge.gameObject.SetActive(on);
        if (fissionNotchParent != null) fissionNotchParent.gameObject.SetActive(on);
        if (fissionNotches != null)
            foreach (var n in fissionNotches)
                if (n != null) n.gameObject.SetActive(on);
        // 본체 아이콘(mainBodyIcon)은 여기서 끄지 않는다 — 항상 표시(UpdateMainBodyIcon가 담당)
        if (fissionSlotIcons != null)
            foreach (var s in fissionSlotIcons)
                if (s != null) s.gameObject.SetActive(on);
    }

    void UpdateHp(PlayerController player)
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = player.maxHp;
            hpSlider.value = player.CurrentHp;
        }

        // 한 칸 = 50 체력. 남은 체력이 걸쳐 있는 칸까지 채움(Ceil), 최대 체력을 넘는 칸은 숨김
        if (hpNotches != null && hpNotches.Length > 0 && hpPerNotch > 0f)
        {
            int filled = Mathf.CeilToInt(player.CurrentHp / hpPerNotch);
            int maxNotch = Mathf.CeilToInt(player.maxHp / hpPerNotch);
            for (int i = 0; i < hpNotches.Length; i++)
            {
                if (hpNotches[i] == null) continue;
                bool exists = i < maxNotch;
                hpNotches[i].enabled = exists;
                if (!exists) continue;
                SetNotch(hpNotches[i], i < filled, hpFilledSprite, hpEmptySprite);
            }
        }
    }

    void UpdateFissionGauge(PlayerController player)
    {
        if (fissionGauge != null)
        {
            fissionGauge.maxValue = player.MaxFissionGauge;
            fissionGauge.value = player.CurrentFissionGauge;
        }

        // 100 게이지가 찰 때마다 한 칸씩 활성화(Floor), 최대 게이지를 넘는 칸은 숨김
        if (fissionNotches != null && fissionNotches.Length > 0 && fissionPerNotch > 0f)
        {
            int active = Mathf.FloorToInt(player.CurrentFissionGauge / fissionPerNotch);
            int maxNotch = Mathf.CeilToInt(player.MaxFissionGauge / fissionPerNotch);
            for (int i = 0; i < fissionNotches.Length; i++)
            {
                if (fissionNotches[i] == null) continue;
                bool exists = i < maxNotch;
                fissionNotches[i].enabled = exists;
                if (!exists) continue;
                SetNotch(fissionNotches[i], i < active, fissionNotchOnSprite, fissionNotchOffSprite);
            }
        }

        // 바 좌표(X/Y)를 매 프레임 반영 — 인스펙터에서 실행 중에 옮겨도 바로 적용됨
        if (fissionBarBgRT != null) fissionBarBgRT.anchoredPosition = new Vector2(fissionBarXOffset, fissionBarYOffset);
        if (fissionBarFillRT != null)
        {
            Vector2 p = fissionBarFillRT.anchoredPosition;
            p.x = fissionBarXOffset; p.y = fissionBarYOffset;
            fissionBarFillRT.anchoredPosition = p;
        }

        // 자동 생성한 연속 바: 현재/최대 비율만큼 채움 (지속 상승하는 바 형태)
        if (fissionBarFillImg != null && player.MaxFissionGauge > 0f)
        {
            float frac = Mathf.Clamp01(player.CurrentFissionGauge / player.MaxFissionGauge);
            if (fissionBarFillImg.type == Image.Type.Filled)
                fissionBarFillImg.fillAmount = frac;               // 스프라이트: fillAmount로 채움
            else if (fissionBarFillRT != null)
            {
                Vector2 s = fissionBarFillRT.sizeDelta;            // 색 사각형: 폭으로 채움
                s.x = fissionBarWidth * frac;
                fissionBarFillRT.sizeDelta = s;
            }
        }
    }

    // 칸 하나를 켬/끔 상태로 표시. 스프라이트가 지정돼 있으면 스프라이트 교체, 없으면 임시로 투명도 조절
    void SetNotch(Image notch, bool on, Sprite onSprite, Sprite offSprite)
    {
        if (onSprite != null && offSprite != null)
        {
            notch.sprite = on ? onSprite : offSprite;
        }
        else
        {
            Color c = notch.color;
            c.a = on ? 1f : 0.2f;
            notch.color = c;
        }
    }

    // 최대 수치에 맞춰 칸을 자동 생성. 최대체력/최대게이지가 바뀌면(보스 처치 등) 그 줄만 다시 만든다.
    // 스프라이트(Filled/On)가 지정돼 있으면 단색 사각형 대신 그 스프라이트로 생성 → 실제 아트 그대로 사용 가능.
    void EnsureNotchesBuilt(PlayerController hpRef, PlayerController fissionRef)
    {
        if (!autoBuildNotches) return;

        if (hpNotchParent != null && hpPerNotch > 0f)
        {
            int need = Mathf.Max(0, Mathf.CeilToInt(hpRef.maxHp / hpPerNotch));
            if (need != builtHpNotchCount)
            {
                ClearChildren(hpNotchParent);
                hpNotches = BuildNotchRow(hpNotchParent, need, hpNotchColor, hpFilledSprite);
                builtHpNotchCount = need;
            }
        }

        if (fissionNotchParent != null && fissionPerNotch > 0f)
        {
            int need = Mathf.Max(0, Mathf.CeilToInt(fissionRef.MaxFissionGauge / fissionPerNotch));
            if (need != builtFissionNotchCount)
            {
                ClearChildren(fissionNotchParent);
                fissionBarFillImg = null;
                fissionBarFillRT = null;
                // 바를 먼저 생성해 뒤에 깔고(=먼저 그려짐), 칸을 나중에 생성해 앞에 올린다 (UI는 형제 순서대로 그려짐)
                if (autoBuildFissionBar)
                    BuildFissionBar(fissionNotchParent, need);
                fissionNotches = BuildNotchRow(fissionNotchParent, need, fissionNotchColor, fissionNotchOnSprite);
                builtFissionNotchCount = need;
            }
        }
    }

    // 자동 생성했던 칸/바를 지운다 (다시 만들기 전 정리). Destroy는 프레임 끝에 반영됨.
    void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // 분열 칸 아래에 지속 상승하는 연속 바(배경 + 채움)를 생성. 채움은 UpdateFissionGauge에서 폭 조절
    void BuildFissionBar(RectTransform parent, int notchCount)
    {
        notchCount = Mathf.Max(1, notchCount);
        fissionBarWidth = notchCount * notchSize.x + (notchCount - 1) * notchSpacing; // 칸 행과 같은 전체 폭
        float y = fissionBarYOffset; // 칸과 같은 높이(중심)에 겹쳐 배치 — 칸이 앞에서 덮음

        fissionBarBgRT = MakeBarPart("FissionBarBg", parent, fissionBarBgColor, fissionBarBgSprite, fissionBarWidth, y).rectTransform;

        // 채움: 스프라이트가 있으면 폭은 꽉 채운 뒤 Filled(가로)로 fillAmount 조절(스프라이트가 안 찌그러짐),
        //       없으면 폭(sizeDelta)을 줄여 색 사각형으로 표현
        Image fill = MakeBarPart("FissionBarFill", parent, fissionBarFillColor, fissionBarFillSprite,
            fissionBarFillSprite != null ? fissionBarWidth : 0f, y);
        if (fissionBarFillSprite != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
        }
        fissionBarFillImg = fill;
        fissionBarFillRT = fill.rectTransform;
    }

    Image MakeBarPart(string name, RectTransform parent, Color color, Sprite sprite, float width, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(width, fissionBarHeight);
        rt.anchoredPosition = new Vector2(fissionBarXOffset, y);

        Image img = go.GetComponent<Image>();
        if (sprite != null) img.sprite = sprite; // 스프라이트면 색은 흰색 유지(원본 그대로)
        else img.color = color;
        return img;
    }

    // 부모 밑에 count개의 칸 Image를 좌→우로 생성해 반환.
    // sprite가 있으면 그 스프라이트로(색은 흰색=원본), 없으면 단색 사각형으로 렌더한다.
    // 채움/빈칸 전환은 매 프레임 SetNotch가 처리하므로 여기선 초기 모양만 준다.
    Image[] BuildNotchRow(RectTransform parent, int count, Color color, Sprite sprite)
    {
        count = Mathf.Max(0, count);
        Image[] arr = new Image[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"Notch{i}", typeof(RectTransform), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = notchSize;
            rt.anchoredPosition = new Vector2(i * (notchSize.x + notchSpacing), 0f);

            Image img = go.GetComponent<Image>();
            if (sprite != null)
                img.sprite = sprite;  // 실제 스프라이트로 렌더 (색은 기본 흰색이라 원본 그대로)
            else
                img.color = color;    // 스프라이트 없으면 단색 사각형
            arr[i] = img;
        }
        return arr;
    }

    void UpdateDashCooldown(PlayerController player)
    {
        if (dashCooldownFill == null) return;
        dashCooldownFill.fillAmount = player.DashCooldownProgress;
    }

    // 본체 아이콘은 분열 해금/분열체 유무와 무관하게 매 프레임 갱신 (항상 표시)
    void UpdateMainBodyIcon(PlayerManager manager)
    {
        if (mainBodyIcon == null || manager.allPlayers.Count == 0) return;

        bool mainControlled = manager.currentPlayer == manager.allPlayers[0];
        if (mainBodyDefaultSprite != null && mainBodyUncontrolledSprite != null)
            mainBodyIcon.sprite = mainControlled ? mainBodyDefaultSprite : mainBodyUncontrolledSprite;
        // 조종 중이 아니면 색(투명도)을 줄여 선택된 개체만 선명하게 (스프라이트로 구분되면 굳이 흐리게 안 함)
        bool hasStateSprites = mainBodyDefaultSprite != null && mainBodyUncontrolledSprite != null;
        SetIconAlpha(mainBodyIcon, (mainControlled || hasStateSprites) ? 1f : uncontrolledSlotAlpha);
    }

    void UpdateFissionIcons(PlayerManager manager)
    {
        if (manager.allPlayers.Count == 0) return;
        if (fissionSlotIcons == null) return;

        int cloneCount = manager.allPlayers.Count - 1;        // 현재 존재하는 분열체 수 (= 소모된 칸)
        int capacity = Mathf.Max(0, manager.maxFissionCount); // 최대 분열 가능 횟수 = 켜둘 칸 수
        int totalSlots = fissionSlotIcons.Length;

        for (int i = 0; i < totalSlots; i++)
        {
            if (fissionSlotIcons[i] == null) continue;

            // 분열 가능 횟수를 소모할 때 우측부터 채워지도록 인덱스를 뒤집어서 매핑
            int fromRight = totalSlots - 1 - i;

            // 최대 분열 횟수를 넘는 여분 칸은 사용하지 않으므로 숨긴다
            if (fromRight >= capacity)
            {
                fissionSlotIcons[i].enabled = false;
                continue;
            }

            fissionSlotIcons[i].enabled = true;

            bool hasClone = fromRight < cloneCount;
            if (!hasClone)
            {
                // 아직 분열하지 않은 '분열 가능' 칸 — 켜진 상태로 계속 표시한다
                // (한 번 분열해도 남은 분열 가능 횟수만큼은 그대로 켜져 있음 — 꺼지지 않음)
                if (slotDefaultSprite != null) fissionSlotIcons[i].sprite = slotDefaultSprite;
                SetIconAlpha(fissionSlotIcons[i], 1f);
                continue;
            }

            // 분열체가 있는(소모된) 칸 — 조종 중이면 controlled, 아니면 uncontrolled 스프라이트
            PlayerController clone = manager.allPlayers[1 + fromRight];
            bool controlled = clone == manager.currentPlayer;
            Sprite s = controlled ? slotControlledSprite : slotUncontrolledSprite;
            if (s != null) fissionSlotIcons[i].sprite = s; // 각 상태 스프라이트는 독립적으로 검사 (한쪽만 넣어도 동작)

            // 스프라이트로 상태가 구분되면 alpha는 1, 스프라이트가 없을 때만 비조종 칸을 흐리게
            float a = (s != null) ? 1f : (controlled ? 1f : uncontrolledSlotAlpha);
            SetIconAlpha(fissionSlotIcons[i], a);
        }
    }

    void SetIconAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    // 재화 시스템 완성 전까지 외부(재화 매니저 등)에서 호출해 갱신
    public void SetCurrency(int cellCount, int darkCellCount)
    {
        if (cellAmountText != null) cellAmountText.text = cellCount.ToString();
        if (darkCellAmountText != null) darkCellAmountText.text = darkCellCount.ToString();
    }
}
