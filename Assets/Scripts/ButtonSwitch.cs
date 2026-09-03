using UnityEngine;

// 버튼: PC가 위에 올라가 접촉해 있는 동안 활성화.
// 충돌/트리거 이벤트를 쓰지 않고 매 프레임 겹침 검사를 한다 —
// 레이어 충돌 매트릭스(player↔monster 무시 등)나 Is Trigger 설정에 상관없이 항상 동작시키기 위함.
public class ButtonSwitch : MonoBehaviour
{
    [Header("감지")]
    public float detectMargin = 0.15f; // 버튼 위에 '올라선' 상태도 잡히도록 감지 범위를 살짝 넓힘
    [Tooltip("한 번 눌리면 발을 떼도 계속 눌린 상태로 유지 (해제하면 밟는 동안만 활성)")]
    public bool latch = true;

    [Header("눌림 표시 (스프라이트 없으면 색으로 대체)")]
    public Sprite normalSprite;
    public Sprite pressedSprite;
    public Color pressedColor = Color.cyan;
    public float pressDepth = 0.08f; // 밟으면 이만큼 내려가 눌린 느낌을 줌

    [Header("그리는 순서")]
    [Tooltip("켜면 아래 값으로 Order in Layer를 덮어쓴다. 맵 타일(보통 0)보다 뒤에 두려면 음수")]
    public bool overrideSortingOrder = true;
    public int sortingOrder = -10;
    [Tooltip("비우면 현재 Sorting Layer를 그대로 둔다")]
    public string sortingLayer = "";

    [Header("문 표시등 이미지 (이 스위치 색의 표시등 스프라이트)")]
    [Tooltip("이 스위치가 문에 켤 표시등 이미지. 색깔별로 다른 이미지를 넣으면 문 표시등이 그 이미지로 바뀐다")]
    public Sprite indicatorOnSprite;   // 눌렸을 때 (색깔별 켜진 이미지)
    public Sprite indicatorOffSprite;  // 안 눌렸을 때 (꺼진 이미지, 없으면 off일 때 표시등 숨김)

    public bool IsActive { get; private set; }

    private Collider2D col;
    private SpriteRenderer spr;
    private Color baseColor = Color.white;
    private Vector3 basePos;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spr = GetComponent<SpriteRenderer>();
        if (spr != null) baseColor = spr.color;
        basePos = transform.position;

        if (spr != null && overrideSortingOrder)
        {
            if (!string.IsNullOrEmpty(sortingLayer)) spr.sortingLayerName = sortingLayer;
            spr.sortingOrder = sortingOrder;
        }

        if (col == null)
            Debug.LogWarning($"[{name}] Collider2D가 없습니다. 버튼이 눌림을 감지할 수 없습니다.", this);
    }

    void Start()
    {
        // 밟고 설 수 있어야 하므로 솔리드 콜라이더 + 플레이어와 충돌 가능한 레이어여야 한다.
        // (PlayerManager가 Awake에서 player↔monster 충돌을 끄므로 이 검사는 Start에서 한다)
        if (col != null && col.isTrigger)
            Debug.LogWarning($"[{name}] Collider2D가 Is Trigger 상태라 플레이어가 통과합니다. 체크를 해제하세요.", this);

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0 && Physics2D.GetIgnoreLayerCollision(gameObject.layer, playerLayer))
            Debug.LogWarning($"[{name}] 이 오브젝트의 레이어({LayerMask.LayerToName(gameObject.layer)})는 Player와 충돌이 꺼져 있어 밟고 설 수 없습니다. Default 또는 Ground 레이어로 옮기세요.", this);
    }

    void Update()
    {
        // 래치 모드: 한 번 눌린 뒤엔 계속 눌린 상태로 둔다 (발을 떼도 해제 안 됨)
        if (latch && IsActive) return;

        bool pressed = DetectPlayer();
        if (pressed == IsActive) return;

        IsActive = pressed;

        if (spr != null)
        {
            if (pressedSprite != null && normalSprite != null)
                spr.sprite = IsActive ? pressedSprite : normalSprite;
            else
                spr.color = IsActive ? pressedColor : baseColor;
        }

        // 밟으면 살짝 내려가고 떼면 원위치 (스프라이트가 없어도 눌린 게 보이도록)
        transform.position = IsActive ? basePos + Vector3.down * pressDepth : basePos;

        Debug.Log($"버튼 {name}: {(IsActive ? "눌림" : "해제")}");
    }

    bool DetectPlayer()
    {
        Vector2 center, size;
        if (col != null)
        {
            center = col.bounds.center;
            size = (Vector2)col.bounds.size + Vector2.one * detectMargin;
        }
        else
        {
            center = transform.position;
            size = Vector2.one * (0.5f + detectMargin);
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.GetComponent<PlayerController>() != null) return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(c.bounds.center, (Vector3)((Vector2)c.bounds.size + Vector2.one * detectMargin));
    }
}
