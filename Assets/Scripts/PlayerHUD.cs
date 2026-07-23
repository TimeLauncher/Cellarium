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

    [Header("분열 연속 바 자동 생성 (칸 아래에 함께 표시)")]
    public bool autoBuildFissionBar = true;                         // 분열 칸 아래에 지속 상승하는 바도 자동 생성
    public float fissionBarHeight = 8f;                             // 바 두께
    public float fissionBarYOffset = 6f;                            // 칸 아래로 띄우는 간격
    public Color fissionBarBgColor = new Color(0f, 0f, 0f, 0.5f);   // 바 배경색
    public Color fissionBarFillColor = new Color(0.35f, 0.7f, 1f);  // 바 채움색

    private bool notchesBuilt;
    private RectTransform fissionBarFillRT; // 자동 생성한 바의 채움 부분 (게이지 비율로 가로 폭 조절)
    private float fissionBarWidth;

    [Header("대시 쿨다운 (Image - Filled 타입)")]
    public Image dashCooldownFill;

    [Header("분열 가능 횟수 - 본체 아이콘")]
    public Image mainBodyIcon;
    public Sprite mainBodyDefaultSprite;      // 조종 중일 때
    public Sprite mainBodyUncontrolledSprite; // 비조종일 때

    [Header("분열 가능 횟수 - 분열체 아이콘 (좌→우, 소모는 우측부터)")]
    public Image[] fissionSlotIcons;
    public Sprite slotDefaultSprite;      // 미사용
    public Sprite slotControlledSprite;   // 조종 중인 분열체
    public Sprite slotUncontrolledSprite; // 존재하지만 비조종인 분열체

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

        // 분열 게이지는 항상 본체(allPlayers[0]) 기준
        PlayerController mainBody = manager.allPlayers.Count > 0 ? manager.allPlayers[0] : controlled;
        UpdateFissionGauge(mainBody);

        UpdateFissionIcons(manager);
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

        // 자동 생성한 연속 바: 현재/최대 비율로 가로 폭 조절 (지속 상승하는 바 형태)
        if (fissionBarFillRT != null && player.MaxFissionGauge > 0f)
        {
            float frac = Mathf.Clamp01(player.CurrentFissionGauge / player.MaxFissionGauge);
            Vector2 s = fissionBarFillRT.sizeDelta;
            s.x = fissionBarWidth * frac;
            fissionBarFillRT.sizeDelta = s;
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

    // 이미지 에셋이 없을 때 데모용으로 색깔 사각형 칸을 자동 생성 (플레이어 최대 수치를 알아야 개수가 정해져서 첫 Update 때 1회 생성)
    void EnsureNotchesBuilt(PlayerController hpRef, PlayerController fissionRef)
    {
        if (!autoBuildNotches || notchesBuilt) return;

        if (hpNotchParent != null && hpPerNotch > 0f)
            hpNotches = BuildNotchRow(hpNotchParent, Mathf.CeilToInt(hpRef.maxHp / hpPerNotch), hpNotchColor);

        if (fissionNotchParent != null && fissionPerNotch > 0f)
        {
            int count = Mathf.CeilToInt(fissionRef.MaxFissionGauge / fissionPerNotch);
            fissionNotches = BuildNotchRow(fissionNotchParent, count, fissionNotchColor);
            if (autoBuildFissionBar)
                BuildFissionBar(fissionNotchParent, count);
        }

        notchesBuilt = true;
    }

    // 분열 칸 아래에 지속 상승하는 연속 바(배경 + 채움)를 생성. 채움은 UpdateFissionGauge에서 폭 조절
    void BuildFissionBar(RectTransform parent, int notchCount)
    {
        notchCount = Mathf.Max(1, notchCount);
        fissionBarWidth = notchCount * notchSize.x + (notchCount - 1) * notchSpacing; // 칸 행과 같은 전체 폭
        float y = -(notchSize.y * 0.5f + fissionBarYOffset + fissionBarHeight * 0.5f);

        MakeBarPart("FissionBarBg", parent, fissionBarBgColor, fissionBarWidth, y);
        Image fill = MakeBarPart("FissionBarFill", parent, fissionBarFillColor, 0f, y);
        fissionBarFillRT = fill.rectTransform;
    }

    Image MakeBarPart(string name, RectTransform parent, Color color, float width, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(width, fissionBarHeight);
        rt.anchoredPosition = new Vector2(0f, y);

        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    // 부모 밑에 count개의 색깔 사각형 Image를 좌→우로 생성해 반환
    Image[] BuildNotchRow(RectTransform parent, int count, Color color)
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
            img.color = color; // 스프라이트 없이 단색 사각형으로 렌더됨
            arr[i] = img;
        }
        return arr;
    }

    void UpdateDashCooldown(PlayerController player)
    {
        if (dashCooldownFill == null) return;
        dashCooldownFill.fillAmount = player.DashCooldownProgress;
    }

    void UpdateFissionIcons(PlayerManager manager)
    {
        if (manager.allPlayers.Count == 0) return;

        if (mainBodyIcon != null)
        {
            bool mainControlled = manager.currentPlayer == manager.allPlayers[0];
            mainBodyIcon.sprite = mainControlled ? mainBodyDefaultSprite : mainBodyUncontrolledSprite;
        }

        if (fissionSlotIcons == null) return;

        int cloneCount = manager.allPlayers.Count - 1; // 본체를 제외한 분열체 수
        int totalSlots = fissionSlotIcons.Length;

        for (int i = 0; i < totalSlots; i++)
        {
            if (fissionSlotIcons[i] == null) continue;

            // 분열 가능 횟수를 소모할 때 우측부터 채워지도록 인덱스를 뒤집어서 매핑
            int fromRight = totalSlots - 1 - i;
            if (fromRight >= cloneCount)
            {
                fissionSlotIcons[i].sprite = slotDefaultSprite;
                continue;
            }

            PlayerController clone = manager.allPlayers[1 + fromRight];
            fissionSlotIcons[i].sprite = clone == manager.currentPlayer ? slotControlledSprite : slotUncontrolledSprite;
        }
    }

    // 재화 시스템 완성 전까지 외부(재화 매니저 등)에서 호출해 갱신
    public void SetCurrency(int cellCount, int darkCellCount)
    {
        if (cellAmountText != null) cellAmountText.text = cellCount.ToString();
        if (darkCellAmountText != null) darkCellAmountText.text = darkCellCount.ToString();
    }
}
