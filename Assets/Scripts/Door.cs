using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 문: 연결된 스위치(Required Buttons)가 전부 활성화되면 개방.
// 스위치 2개짜리 문 등 '필요 개수'는 Required Buttons에 연결한 스위치 수로 정해진다.
// - 각 스위치의 눌림 상태는 문에 붙은 표시등(그 스위치 색)으로 보여준다.
// - 개방 시 사라지지 않고 위로 슬라이드되어 열린다.
public class Door : MonoBehaviour
{
    [Header("열리는 조건 (스위치 여러 개면 전부 눌려야 함)")]
    public ButtonSwitch[] requiredButtons;
    public Text requiredCountText; // "활성화 수/필요 수" 표시 (선택)
    public bool isOpen;
    [Tooltip("켜면 모든 스위치를 '동시에' 밟아야 열린다(분열체 퍼즐). 스위치들의 Latch를 자동으로 꺼서 momentary로 만든다. 한 번 동시에 열리면 계속 열린 채 유지")]
    public bool requireSimultaneous = false;

    // 표시등 하나의 켜짐/꺼짐 이미지 한 쌍
    [System.Serializable]
    public struct IndicatorSprites
    {
        public Sprite on;   // 켜진(눌림) 이미지
        public Sprite off;  // 꺼진 이미지
    }

    [Header("스위치 표시등")]
    [Tooltip("★각 표시등의 켜짐/꺼짐 이미지를 넣는 곳. Required Buttons와 같은 순서(스위치 2개면 여기도 2개). 여기 이미지가 최우선")]
    public IndicatorSprites[] indicatorImages;
    [Tooltip("표시등 오브젝트를 직접 배치했을 때만 연결(비워두면 자동 생성). Required Buttons와 같은 순서")]
    public SpriteRenderer[] switchIndicators;
    [Tooltip("표시등별 이미지가 없을 때 쓸 문 공통 켜짐/꺼짐 이미지 (선택)")]
    public Sprite indicatorOnSprite;
    public Sprite indicatorOffSprite;
    [Header("이미지가 아예 없을 때만 쓰는 색 대체")]
    public Color indicatorOffColor = new Color(0.25f, 0.25f, 0.25f);
    public bool useButtonColorWhenOn = true;
    public Color indicatorOnColor = Color.cyan;

    [Header("표시등 자동 생성/배치")]
    [Tooltip("켜면 Required Buttons 개수만큼 문 앞(sorting order 위)에 표시등을 자동 생성한다")]
    public bool autoBuildIndicators = true;
    [Tooltip("표시등을 세로로 쌓아 배치(끄면 가로로 나란히)")]
    public bool indicatorVertical = true;
    public Vector2 indicatorAreaOffset = Vector2.zero; // 문 중심 기준 표시등 줄의 시작 위치
    public float indicatorSpacing = 0.4f;              // 표시등 사이 간격
    public float indicatorScale = 0.3f;                // 표시등 크기(월드 유닛)

    [Header("표시등 그리는 순서")]
    [Tooltip("끄면 '문 기준 상대'(Indicator Sorting Offset). 켜면 아래 값을 그대로 쓴다 — 맵 타일보다 뒤로 보내려면 켜고 음수를 넣을 것")]
    public bool indicatorAbsoluteSorting = false;
    [Tooltip("Absolute일 때 쓰는 Order in Layer. 맵 타일(보통 0)보다 뒤에 두려면 -10 등 음수")]
    public int indicatorSortingOrder = -10;
    [Tooltip("Absolute일 때 쓸 Sorting Layer 이름. 비우면 문과 같은 레이어를 쓴다")]
    public string indicatorSortingLayer = "";
    public int indicatorSortingOffset = 1;             // 상대 모드에서 문보다 이만큼 앞에 그린다

    [Header("개방 연출 (사라지지 않고 위로 열림)")]
    [Tooltip("위로 올라가는 거리 (문 높이만큼 주면 천장으로 들어가듯 열림)")]
    public float openSlideDistance = 2f;
    public float openSlideDuration = 0.6f;
    public Vector2 openSlideDir = Vector2.up;

    [Header("진행 색 표시 (실제 문 스프라이트가 있으면 보통 끔)")]
    public bool tintDoorByProgress = false;
    public Color closedColor = Color.white;
    public Color readyColor = Color.cyan;

    private Collider2D col;
    private SpriteRenderer spr;
    private int lastActiveCount = -1;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spr = GetComponentInChildren<SpriteRenderer>(); // 문 스프라이트가 자식에 있어도 잡히도록

        if (requiredButtons == null || requiredButtons.Length == 0)
            Debug.LogWarning($"[{name}] Required Buttons가 비어 있습니다. 스위치를 연결해야 문이 열립니다.", this);

        // 동시 입력 모드: 스위치들을 momentary(래치 해제)로 강제 → 전부 '동시에' 밟혀야 열림 (분열체 퍼즐)
        if (requireSimultaneous && requiredButtons != null)
            foreach (var b in requiredButtons)
                if (b != null) b.latch = false;

        // 표시등을 스위치 개수만큼 확보 (배열 크기만 잡아두고 비워둔 칸도 채워 생성)
        EnsureIndicators();

        RefreshIndicators(); // 시작 시 표시등 초기화 (전부 꺼짐)
    }

    void Update()
    {
        if (isOpen) return;

        int activeCount = 0;
        foreach (var b in requiredButtons)
            if (b != null && b.IsActive) activeCount++;

        // 표시등은 각 스위치 상태를 매 프레임 반영 (스위치가 개별로 켜/꺼질 수 있으므로)
        RefreshIndicators();

        if (activeCount != lastActiveCount)
        {
            lastActiveCount = activeCount;

            if (requiredCountText != null)
                requiredCountText.text = $"{activeCount}/{requiredButtons.Length}";

            if (tintDoorByProgress && spr != null && requiredButtons.Length > 0)
                spr.color = Color.Lerp(closedColor, readyColor, (float)activeCount / requiredButtons.Length);

            Debug.Log($"[{name}] 스위치 {activeCount}/{requiredButtons.Length} 활성화");
        }

        if (requiredButtons.Length > 0 && activeCount >= requiredButtons.Length)
            Open();
    }

    // 각 표시등을 대응하는 스위치의 눌림 상태로 갱신.
    // 색깔별 표시등 이미지가 있으면 그 이미지를 그대로 보여준다(코드가 색을 바꾸지 않음).
    // 우선순위: 스위치별 표시등 이미지 → 문 공통 표시등 이미지 → (이미지 전혀 없을 때만) 색으로 대체.
    void RefreshIndicators()
    {
        if (switchIndicators == null) return;
        for (int i = 0; i < switchIndicators.Length; i++)
        {
            SpriteRenderer ind = switchIndicators[i];
            if (ind == null) continue;

            ButtonSwitch btn = (requiredButtons != null && i < requiredButtons.Length) ? requiredButtons[i] : null;
            bool on = btn != null && btn.IsActive;

            // 이 상태에서 쓸 이미지 고르기 (Door의 Indicator Images → 스위치별 → 문 공통 순)
            Sprite onSpr  = IndOn(i);
            Sprite offSpr = IndOff(i);
            Sprite chosen = on ? onSpr : offSpr;

            if (chosen != null)
            {
                // 색깔별 이미지 그대로 표시 (색 tint 안 함)
                ind.enabled = true;
                ind.sprite = chosen;
                ind.color = Color.white;
            }
            else if (onSpr != null)
            {
                // 이미지 방식인데 현재 상태(주로 off)용 이미지가 없으면 표시등을 숨긴다
                ind.enabled = false;
            }
            else
            {
                // 이미지가 아예 없을 때만 색으로 대체 (기존 방식)
                ind.enabled = true;
                if (on)
                    ind.color = (useButtonColorWhenOn && btn != null) ? btn.pressedColor : indicatorOnColor;
                else
                    ind.color = indicatorOffColor;
            }
        }
    }

    // 스위치 개수만큼 표시등을 확보한다 (문 앞 sorting order 위에, 세로/가로로 배치).
    // 배열 크기만 잡아두고 비워둔 칸(null)이나 부족한 칸도 채워서 생성 → "표시등이 안 만들어지던" 문제 해결.
    // 이미 들어있는(수동 배치한) 표시등은 건드리지 않는다.
    void EnsureIndicators()
    {
        if (!autoBuildIndicators || requiredButtons == null || requiredButtons.Length == 0) return;
        int n = requiredButtons.Length;

        // 배열이 없거나 스위치 수보다 작으면 늘리고 기존 항목은 보존
        if (switchIndicators == null || switchIndicators.Length < n)
        {
            var arr = new SpriteRenderer[n];
            if (switchIndicators != null)
                for (int k = 0; k < switchIndicators.Length && k < n; k++) arr[k] = switchIndicators[k];
            switchIndicators = arr;
        }

        int created = 0;
        for (int i = 0; i < n; i++)
        {
            if (switchIndicators[i] != null)
            {
                ApplyIndicatorSorting(switchIndicators[i]); // 수동 배치한 것도 정렬은 인스펙터 설정을 따르게
                continue;
            }
            switchIndicators[i] = CreateIndicator(i, n);
            created++;
        }
        if (created > 0) Debug.Log($"[{name}] 표시등 {created}개 자동 생성 (스위치 {n}개)");
    }

    // 표시등 i의 켜짐/꺼짐 이미지 선택: Door의 Indicator Images → 스위치별 → 문 공통 순
    Sprite IndOn(int i)
    {
        if (indicatorImages != null && i < indicatorImages.Length && indicatorImages[i].on != null) return indicatorImages[i].on;
        ButtonSwitch btn = (requiredButtons != null && i < requiredButtons.Length) ? requiredButtons[i] : null;
        if (btn != null && btn.indicatorOnSprite != null) return btn.indicatorOnSprite;
        return indicatorOnSprite;
    }
    Sprite IndOff(int i)
    {
        if (indicatorImages != null && i < indicatorImages.Length && indicatorImages[i].off != null) return indicatorImages[i].off;
        ButtonSwitch btn = (requiredButtons != null && i < requiredButtons.Length) ? requiredButtons[i] : null;
        if (btn != null && btn.indicatorOffSprite != null) return btn.indicatorOffSprite;
        return indicatorOffSprite;
    }

    SpriteRenderer CreateIndicator(int i, int n)
    {
        GameObject go = new GameObject($"Indicator{i}");
        go.transform.SetParent(transform, false);

        float total = (n - 1) * indicatorSpacing;
        float along = -total * 0.5f + i * indicatorSpacing;
        // 세로면 위→아래로(i=0이 위), 가로면 왼→오
        go.transform.localPosition = indicatorVertical
            ? new Vector3(indicatorAreaOffset.x, indicatorAreaOffset.y - along, 0f)
            : new Vector3(indicatorAreaOffset.x + along, indicatorAreaOffset.y, 0f);
        go.transform.localScale = Vector3.one * indicatorScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        // 초기 스프라이트: off 이미지 우선(시작은 꺼짐), 없으면 on, 그래도 없으면 기본 사각형. RefreshIndicators가 상태에 맞게 보정
        sr.sprite = IndOff(i) != null ? IndOff(i)
                  : IndOn(i) != null ? IndOn(i)
                  : FallbackSquare();
        ApplyIndicatorSorting(sr);
        return sr;
    }

    // 표시등의 Sorting Layer / Order를 정한다.
    // 자동 생성한 것뿐 아니라 수동 배치한 표시등에도 적용해야 인스펙터 설정이 실제로 반영된다.
    void ApplyIndicatorSorting(SpriteRenderer sr)
    {
        if (sr == null) return;

        if (indicatorAbsoluteSorting)
        {
            if (!string.IsNullOrEmpty(indicatorSortingLayer))
                sr.sortingLayerName = indicatorSortingLayer;
            else if (spr != null)
                sr.sortingLayerID = spr.sortingLayerID;

            sr.sortingOrder = indicatorSortingOrder;
            return;
        }

        // 상대 모드: 문과 같은 레이어에서 문보다 offset만큼 앞
        if (spr != null)
        {
            sr.sortingLayerID = spr.sortingLayerID;
            sr.sortingOrder = spr.sortingOrder + indicatorSortingOffset;
        }
        else
        {
            sr.sortingOrder = indicatorSortingOffset;
        }
    }

    // 표시등 스프라이트를 안 넣었을 때 쓰는 기본 흰 사각형 (색으로 tint해서 표시)
    private static Sprite _fallbackSquare;
    static Sprite FallbackSquare()
    {
        if (_fallbackSquare == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            _fallbackSquare = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
        }
        return _fallbackSquare;
    }

    void Open()
    {
        isOpen = true;
        if (col != null) col.enabled = false; // 열리는 즉시 통과 가능
        RefreshIndicators();                  // 열린 뒤에도 표시등은 켜진 상태로 남김
        Debug.Log($"[{name}] 문 개방!");

        // 사라지지 않고 위로 슬라이드 (스프라이트가 있고 이동값이 유효할 때)
        if (spr != null && openSlideDistance != 0f && openSlideDuration > 0f && openSlideDir.sqrMagnitude > 0.0001f)
            StartCoroutine(SlideOpen());
    }

    IEnumerator SlideOpen()
    {
        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(openSlideDir.normalized * openSlideDistance);
        float t = 0f;
        while (t < openSlideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / openSlideDuration);
            transform.position = Vector3.Lerp(start, end, k);
            yield return null;
        }
        transform.position = end;
    }
}
