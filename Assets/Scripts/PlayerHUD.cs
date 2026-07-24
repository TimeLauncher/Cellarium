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
    public float notchSpacing = 4f;              // 분열 칸 사이 간격
    [Tooltip("체력 칸 사이 간격. 음수로 주면 칸끼리 겹쳐진다. 0이면 딱 붙음")]
    public float hpNotchSpacing = 0f;            // 체력 칸 간격 (분열과 분리 — 좁게 붙이려면 0 이하)
    public Color hpNotchColor = new Color(0.9f, 0.25f, 0.25f);      // 체력 칸 색 (빨강 계열)
    public Color fissionNotchColor = new Color(0.35f, 0.7f, 1f);    // 분열 칸 색 (하늘색 계열)

    [Header("분열 연속 바 자동 생성 (칸 뒤에 겹쳐 표시)")]
    public bool autoBuildFissionBar = true;                         // 분열 칸 뒤에 지속 상승하는 바를 겹쳐 생성 (칸이 앞, 바가 뒤)
    public float fissionBarHeight = 24f;                            // 바 두께 (칸 높이와 같게 두면 배경 바처럼 보임)
    [Tooltip("분열 바 전체 길이(px). 0이면 칸 수에 맞춰 자동. 값을 주면 그 길이로 늘어나고 칸도 같은 길이에 맞춰 균등 분할된다(바-칸 정렬 유지). Play 중 인스펙터에서 바로 반영됨")]
    public float fissionBarLength = 0f;                            // 바 길이 직접 지정 (0 = 자동)
    public float fissionBarXOffset = 0f;                            // 칸 왼쪽 기준 바의 X 위치 조정 (좌표 이동)
    public float fissionBarYOffset = 0f;                            // 칸 중심 기준 바의 Y 위치 조정 (0 = 칸과 같은 높이)
    public Color fissionBarBgColor = new Color(0f, 0f, 0f, 0.5f);   // 바 배경색 (스프라이트 없을 때)
    public Color fissionBarFillColor = new Color(0.35f, 0.7f, 1f);  // 바 채움색 (스프라이트 없을 때)
    public Sprite fissionBarBgSprite;    // 바 배경 스프라이트 (예: FissionBarEmpty). 없으면 색 사각형
    public Sprite fissionBarFillSprite;  // 바 채움 스프라이트 (예: fissionBar). Filled(가로)로 게이지 비율만큼 채움

    private int builtHpNotchCount = -1;      // 마지막으로 생성한 체력 칸 수 (최대체력 바뀌면 다시 만듦)
    private int builtFissionNotchCount = -1; // 마지막으로 생성한 분열 칸 수
    private float builtFissionBarLength = -1f; // 마지막으로 생성한 바 길이 (인스펙터에서 바꾸면 다시 만듦)
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

    [Header("분열 가능 횟수 아이콘 - 개수별 자동 배치")]
    [Tooltip("켜면 Max Fission Count 개수만큼 아이콘을 본체 아래에 좌우 대칭 아치로 자동 배치한다. 개수(N)에 따라 위치가 자동으로 바뀜 — 1개=바로 밑, 2개=좌우 둘, 3개=아치. 끄면 씬에 배치한 위치/기존 방식")]
    public bool autoLayoutFissionSlots = true;
    [Tooltip("본체 아이콘 기준, 가운데 칸이 내려오는 세로 거리(깊이)")]
    public float fissionSlotDropY = 52f;
    [Tooltip("칸 사이 좌우 간격")]
    public float fissionSlotSpacingX = 42f;
    [Tooltip("바깥 칸이 위로 올라오는 아치 곡률(0이면 일자로 나란히, 클수록 U자로 휨)")]
    public float fissionSlotArc = 22f;

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

        // 100 게이지가 찰 때마다 한 칸씩 활성화(Floor), 최대 게이지를 넘는 칸은 숨김.
        // 빈 칸(Off)은 계속 보여준다 — "이만큼 차야 분열 가능"이라는 표시. 바가 그 칸까지 차면 On으로 켜진다.
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
                hpNotches = BuildNotchRow(hpNotchParent, need, hpNotchColor, hpFilledSprite, hpNotchSpacing, notchSize.x);
                builtHpNotchCount = need;
            }
        }

        if (fissionNotchParent != null && fissionPerNotch > 0f)
        {
            int need = Mathf.Max(0, Mathf.CeilToInt(fissionRef.MaxFissionGauge / fissionPerNotch));
            // 칸 수(최대 게이지 증가)나 바 길이가 바뀌면 다시 만든다 — 길이는 Play 중 인스펙터 조정도 반영
            if (need != builtFissionNotchCount || !Mathf.Approximately(fissionBarLength, builtFissionBarLength))
            {
                ClearChildren(fissionNotchParent);
                fissionBarFillImg = null;
                fissionBarFillRT = null;

                float totalW = FissionRowWidth(need);                         // 바 = 칸 행 전체 폭 (자동 or 지정 길이)
                int notches = Mathf.Max(1, need);
                // 칸은 원래 크기(notchSize)를 그대로 유지하고, 바 길이에 맞춰 '간격'만 벌려 퍼뜨린다 → 이미지 안 깨짐.
                // 자동 폭일 땐 이 값이 notchSpacing 그대로라 기존 배치와 동일.
                float spacing = notches > 1 ? (totalW - notches * notchSize.x) / (notches - 1) : notchSpacing;

                // 바를 먼저 생성해 뒤에 깔고(=먼저 그려짐), 칸을 나중에 생성해 앞에 올린다 (UI는 형제 순서대로 그려짐)
                if (autoBuildFissionBar)
                    BuildFissionBar(fissionNotchParent, totalW);
                fissionNotches = BuildNotchRow(fissionNotchParent, need, fissionNotchColor, fissionNotchOnSprite, spacing, notchSize.x);
                builtFissionNotchCount = need;
                builtFissionBarLength = fissionBarLength;
            }
        }
    }

    // 자동 생성했던 칸/바를 지운다 (다시 만들기 전 정리). Destroy는 프레임 끝에 반영됨.
    void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // 분열 칸 행의 전체 폭. fissionBarLength>0이면 그 길이, 아니면 칸 크기/간격으로 자동 산출
    float FissionRowWidth(int notchCount)
    {
        if (fissionBarLength > 0f) return fissionBarLength;
        notchCount = Mathf.Max(1, notchCount);
        return notchCount * notchSize.x + (notchCount - 1) * notchSpacing;
    }

    // 분열 칸 아래에 지속 상승하는 연속 바(배경 + 채움)를 생성. 채움은 UpdateFissionGauge에서 폭 조절
    void BuildFissionBar(RectTransform parent, float totalWidth)
    {
        fissionBarWidth = totalWidth; // 칸 행과 같은 전체 폭
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
    Image[] BuildNotchRow(RectTransform parent, int count, Color color, Sprite sprite, float spacing, float cellWidth)
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
            rt.sizeDelta = new Vector2(cellWidth, notchSize.y);
            rt.anchoredPosition = new Vector2(i * (cellWidth + spacing), 0f);

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
        int capacity = Mathf.Max(0, manager.maxFissionCount); // 데이터상 최대 분열 가능 횟수 = 켜둘 칸 수
        int totalSlots = fissionSlotIcons.Length;

        // 자동 배치일 때 위치 기준점: 본체 아이콘 위치 (없으면 원점)
        Vector2 anchor = mainBodyIcon != null ? mainBodyIcon.rectTransform.anchoredPosition : Vector2.zero;
        int shown = Mathf.Min(capacity, totalSlots); // 실제로 보일 칸 수 — 이 수로 대칭 아치를 만든다

        for (int i = 0; i < totalSlots; i++)
        {
            if (fissionSlotIcons[i] == null) continue;

            // rank: 0 = 가장 먼저 채워지는 칸.
            // 자동 배치 → 앞에서부터(0번=본체 바로 밑) capacity개 표시, 소모는 앞에서부터.
            // 기존 방식 → 우측부터 표시/소모(fromRight).
            int rank;
            bool exists;
            if (autoLayoutFissionSlots)
            {
                exists = i < capacity;
                rank = i;
            }
            else
            {
                rank = totalSlots - 1 - i;
                exists = rank < capacity;
            }

            fissionSlotIcons[i].enabled = exists;
            if (!exists) continue;

            // 개수(shown)에 맞춰 위치를 자동 계산해 배치 (1개=바로 밑, 2개=좌우 둘, 3개=아치)
            if (autoLayoutFissionSlots)
                fissionSlotIcons[i].rectTransform.anchoredPosition = anchor + FissionSlotPos(i, shown);

            bool hasClone = rank < cloneCount;
            if (!hasClone)
            {
                // 아직 분열하지 않은 '분열 가능' 칸 — 켜진 상태로 계속 표시한다
                if (slotDefaultSprite != null) fissionSlotIcons[i].sprite = slotDefaultSprite;
                SetIconAlpha(fissionSlotIcons[i], 1f);
                continue;
            }

            // 분열체가 있는(소모된) 칸 — 조종 중이면 controlled, 아니면 uncontrolled 스프라이트
            PlayerController clone = manager.allPlayers[1 + rank];
            bool controlled = clone == manager.currentPlayer;
            Sprite s = controlled ? slotControlledSprite : slotUncontrolledSprite;
            if (s != null) fissionSlotIcons[i].sprite = s; // 각 상태 스프라이트는 독립적으로 검사 (한쪽만 넣어도 동작)

            // 스프라이트로 상태가 구분되면 alpha는 1, 스프라이트가 없을 때만 비조종 칸을 흐리게
            float a = (s != null) ? 1f : (controlled ? 1f : uncontrolledSlotAlpha);
            SetIconAlpha(fissionSlotIcons[i], a);
        }
    }

    // 개수(count)에 따라 i번째 슬롯의 위치(본체 기준 오프셋)를 자동 계산.
    // 본체 아래 가운데를 기준으로 좌우 대칭으로 펼치고, 바깥 칸은 아치처럼 살짝 위로 올린다.
    // count가 바뀌면(개수 조절) 전체 위치가 같이 재배치된다.
    Vector2 FissionSlotPos(int i, int count)
    {
        if (count <= 1) return new Vector2(0f, -fissionSlotDropY); // 1개면 본체 바로 밑
        float half = (count - 1) * 0.5f;
        float t = (i - half) / half;                              // -1(왼끝)~0(가운데)~1(오른끝)
        float x = (i - half) * fissionSlotSpacingX;               // 가운데 기준 좌우 대칭
        float y = -fissionSlotDropY + fissionSlotArc * (t * t);   // 가운데가 가장 아래, 바깥이 위로(아치)
        return new Vector2(x, y);
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
