using UnityEngine;

// 섭취 편의 표시 (기획서 (8) '섭취 편의 기능 추가').
//
// 기획서 3가지 중 이 스크립트가 담당하는 것:
//   ① 섭취 가능 상태가 되면 '섭취가 시전되는 거리'(PC의 섭취 사정거리)를 대상 주변에 원으로 표시
//   ② 섭취 전용 원형 콜라이더 추가 + 그 위에 마우스 커서가 올라가면 원형 링 이미지 표시
//   (③ '대시 쿨타임 동안 섭취가 안 되는 문제'는 PlayerController.TryDashOrEat에서 이미 해결됨)
//
// 이펙트 에셋이 아직 없어서 원/링 스프라이트는 런타임에 만들어 쓴다
// (HitboxVisualizer·ExplosionRangeIndicator와 같은 방식). 나중에 진짜 이미지가 나오면
// Range Sprite / Hover Sprite 칸에 넣으면 그걸 대신 쓴다.
//
// ★ 섭취 전용 콜라이더를 '자식'에 만드는 이유
//   몬스터 본체에 콜라이더를 하나 더 붙이면 MonsterBase가 Awake에서 잡아두는
//   bodyCollider(GetComponent<Collider2D>)가 이 콜라이더로 바뀔 수 있어서 접촉 판정이 망가진다.
//   자식으로 두고, PlayerController.GetMouseTarget이 GetComponentInParent로 주인을 찾는다.
//
// MonsterBase가 자동으로 붙여주므로 몬스터는 인스펙터 작업이 필요 없다.
// 회복셀·다크셀 잔재처럼 직접 만든 IConsumable 오브젝트엔 손으로 붙이면 된다.
[DisallowMultipleComponent]
public class ConsumeIndicator : MonoBehaviour
{
    [Header("섭취 전용 콜라이더")]
    [Tooltip("마우스로 집기 쉬우라고 따로 두는 원형 트리거의 반지름. " +
             "0이면 몬스터 몸통 크기에 맞춰 자동 계산한다 (거미균처럼 큰 몬스터도 알아서 커짐)")]
    public float consumeColliderRadius = 0f;
    [Tooltip("자동 계산할 때 몸통 대비 배율")]
    public float autoRadiusMultiplier = 1.05f;
    [Tooltip("끄면 콜라이더를 만들지 않는다 (이미 손으로 만들어둔 경우)")]
    public bool createConsumeCollider = true;

    [Header("사정거리 원")]
    [Tooltip("비우면 런타임에 만든 임시 원을 쓴다")]
    public Sprite rangeSprite;
    public Color rangeColor = new Color(0.55f, 0.85f, 1f, 0.35f);
    [Tooltip("사정거리 원이 보이기 시작하는 거리. 0이면 사정거리의 2.5배")]
    public float showDistance = 0f;

    [Header("커서 올렸을 때 링")]
    public Sprite hoverSprite;
    public Color hoverColor = new Color(1f, 0.45f, 0.5f, 0.9f);
    [Tooltip("링 크기 배율 (몬스터 몸통 지름 기준). 1이면 몸통에 딱 맞는다")]
    public float hoverScale = 1f;

    [Header("표시")]
    public int sortingOrder = 45;

    CircleCollider2D consumeCollider;
    SpriteRenderer rangeView;
    SpriteRenderer hoverView;
    IConsumable owner;

    static Sprite sharedRing;      // 테두리만 있는 원 (사정거리 표시용)
    static Sprite sharedThickRing; // 두꺼운 링 (커서 올렸을 때)

    void Awake()
    {
        owner = GetComponent<IConsumable>();
        if (owner == null)
        {
            Debug.LogWarning($"[{name}] ConsumeIndicator는 IConsumable(몬스터·회복셀 등)에만 붙일 수 있습니다.", this);
            enabled = false;
            return;
        }

        if (createConsumeCollider) BuildConsumeCollider();
        BuildViews();
    }

    // 섭취 전용 원형 콜라이더 — 클릭 판정만 담당하는 트리거.
    // ★ 레이어를 주인과 똑같이 맞춰야 한다. PlayerController.GetMouseTarget이
    //   monsterMask로 OverlapPoint를 쏘기 때문에, 기본 레이어(Default)로 두면 아예 안 잡힌다.
    void BuildConsumeCollider()
    {
        GameObject go = new GameObject("ConsumeCollider");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;

        consumeCollider = go.AddComponent<CircleCollider2D>();
        consumeCollider.isTrigger = true;
        consumeCollider.radius = ResolveLocalRadius();
    }

    // 반지름을 안 정해뒀으면 몸통 콜라이더 크기에서 뽑는다.
    // 고정값(0.7 등)으로 두면 스케일이 2배인 거미균에선 몸의 절반도 안 덮여서
    // "분명 몬스터를 클릭했는데 대시가 나간다"가 된다.
    float ResolveLocalRadius()
    {
        if (consumeColliderRadius > 0f) return consumeColliderRadius;

        Collider2D body = FindBodyCollider();
        if (body == null) return 0.7f;

        Vector3 lossy = transform.lossyScale;
        float scale = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));

        // bounds는 월드 크기라 로컬로 되돌린다 (자식 콜라이더의 radius는 로컬 값)
        Vector3 e = body.bounds.extents;
        return Mathf.Max(e.x, e.y) / scale * autoRadiusMultiplier;
    }

    // 섭취 전용 트리거 자신은 제외하고, 몸통(비트리거)을 우선 고른다
    Collider2D FindBodyCollider()
    {
        Collider2D fallback = null;

        foreach (Collider2D c in GetComponentsInChildren<Collider2D>())
        {
            if (c == null || c == consumeCollider) continue;
            if (!c.isTrigger) return c;
            if (fallback == null) fallback = c;
        }

        return fallback;
    }

    void BuildViews()
    {
        if (sharedRing == null) sharedRing = MakeRingSprite(128, 0.94f);
        if (sharedThickRing == null) sharedThickRing = MakeRingSprite(128, 0.80f);

        rangeView = MakeView("ConsumeRangeView", rangeSprite != null ? rangeSprite : sharedRing, rangeColor);
        hoverView = MakeView("ConsumeHoverView", hoverSprite != null ? hoverSprite : sharedThickRing, hoverColor);
    }

    SpriteRenderer MakeView(string goName, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;
        return sr;
    }

    void LateUpdate()
    {
        if (owner == null || rangeView == null) return;

        // 섭취 가능 상태가 아니면 전부 숨긴다 (살아 있는 몬스터엔 아무것도 안 뜬다)
        if (!owner.IsConsumable)
        {
            rangeView.enabled = false;
            hoverView.enabled = false;
            return;
        }

        PlayerController player = CurrentPlayer();
        if (player == null)
        {
            rangeView.enabled = false;
            hoverView.enabled = false;
            return;
        }

        float range = player.consumeRange;
        float appearAt = showDistance > 0f ? showDistance : range * 2.5f;
        float distance = Vector2.Distance(transform.position, player.transform.position);

        // 사정거리 원: 플레이어가 근처에 왔을 때만 (멀리 있는 시체들까지 전부 원을 그리면 화면이 지저분해진다)
        bool showRange = distance <= appearAt;
        rangeView.enabled = showRange;

        if (showRange)
        {
            ScaleToWorldDiameter(rangeView.transform, range * 2f);

            // 사정거리 안에 실제로 들어와 '지금 섭취 가능'하면 진하게
            Color c = rangeColor;
            c.a = distance <= range ? rangeColor.a : rangeColor.a * 0.45f;
            rangeView.color = c;
        }

        // 커서 링 — 판정 범위와 그리는 크기를 일부러 다르게 둔다.
        //   판정(clickRadius): 몸통 + 여유(consumePickRadius). 넉넉해야 "눌렀는데 대시가 나가는" 일이 없다.
        //   표시(ringRadius):  몸통 크기. 여유까지 그리면 원이 사정거리 원만큼 커져서 뭐가 뭔지 알 수 없다.
        // 링은 '판정' 기준으로 켜지므로, 링이 보이는데 대시가 나가는 상황은 여전히 없다.
        bool hovering = showRange && IsMouseOver(ClickableWorldRadius(player));
        hoverView.enabled = hovering;

        if (hovering)
            ScaleToWorldDiameter(hoverView.transform, BodyWorldRadius() * 2f * hoverScale);
    }

    // 몬스터 몸통 크기 (링을 그리는 기준)
    float BodyWorldRadius()
    {
        float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        return consumeCollider != null
            ? consumeCollider.radius * scale
            : (consumeColliderRadius > 0f ? consumeColliderRadius : 0.7f) * scale;
    }

    // 지금 커서를 올렸을 때 섭취로 인정되는 월드 반경 (몸통 + 커서 여유)
    float ClickableWorldRadius(PlayerController player)
    {
        return BodyWorldRadius() + Mathf.Max(0f, player.consumePickRadius);
    }

    bool IsMouseOver(float clickRadius)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector2 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        return Vector2.Distance(mouse, transform.position) <= clickRadius;
    }

    // 스프라이트는 지름 1유닛으로 만들었으므로 원하는 월드 지름으로 맞춘다.
    // 부모(몬스터) 스케일이 1이 아닐 수 있어 lossyScale로 보정한다 —
    // 안 하면 스케일 2배인 거미균에서 원이 2배로 크게 나온다.
    void ScaleToWorldDiameter(Transform t, float worldDiameter)
    {
        Vector3 lossy = transform.lossyScale;
        float sx = Mathf.Approximately(lossy.x, 0f) ? 1f : worldDiameter / lossy.x;
        float sy = Mathf.Approximately(lossy.y, 0f) ? 1f : worldDiameter / lossy.y;
        t.localScale = new Vector3(sx, sy, 1f);
    }

    static PlayerController CurrentPlayer()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.currentPlayer != null)
            return PlayerManager.Instance.currentPlayer;
        return null;
    }

    // 테두리만 있는 원을 런타임에 생성. innerRatio가 클수록 얇은 테두리가 된다.
    // (pixelsPerUnit = size 라서 지름이 정확히 1유닛)
    static Sprite MakeRingSprite(int size, float innerRatio)
    {
        Texture2D tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
        float outer = size * 0.5f;
        float inner = outer * innerRatio;
        Vector2 center = new Vector2(outer, outer);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                bool onRing = d <= outer && d >= inner;

                // 가장자리 계단을 부드럽게 (테두리가 얇아 안 하면 눈에 띄게 지저분하다)
                float alpha = onRing ? Mathf.Clamp01(Mathf.Min(outer - d, d - inner) + 0.5f) : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
