using UnityEngine;

// 시야 제한 영역(EV_VisionArea) — BoxCollider2D 범위가 영역이다.
// 기획서 기능 세 가지를 각각 켜고 끌 수 있다:
//   ① hideUntilEntered      PC가 들어오기 전까지 영역을 까맣게 가린다
//   ② revealOnFirstEntry    한 번 들어오면 ①을 영구 해제한다 (끄면 나갈 때마다 다시 가려진다)
//   ③ darkenOutsideInside   PC가 영역 안에 있는 동안 영역 바깥 전체를 어둡게 한다
// 벽 파괴 등 외부 조건으로 풀 때는 Reveal()을 부른다 (CoagulatedWall이 사용).
// 암막은 런타임에 만든 스프라이트라 아트가 따로 필요 없다.
[RequireComponent(typeof(BoxCollider2D))]
public class VisionArea : MonoBehaviour
{
    [Header("기능 On/Off")]
    [Tooltip("① PC가 영역에 닿기 전까지 영역을 암시야로 표시")]
    public bool hideUntilEntered = true;
    [Tooltip("② 최초 입장 이후 ①을 영구 해제. 끄면 PC가 나갈 때마다 다시 가려진다")]
    public bool revealOnFirstEntry = true;
    [Tooltip("③ PC가 영역 안에 있을 때 영역 바깥을 암시야로 표시")]
    public bool darkenOutsideInside = false;
    [Tooltip("켜면 PC가 닿아도 ①이 풀리지 않는다 — 벽 파괴(Reveal) 같은 외부 조건으로만 풀 때 사용")]
    public bool revealOnlyByTrigger = false;

    [Header("연출")]
    public Color darkColor = Color.black;
    [Tooltip("③ 바깥 암시야의 진하기 (0~1)")]
    [Range(0f, 1f)] public float outsideDarkness = 0.85f;
    [Tooltip("암막이 켜지고 꺼지는 시간(초)")]
    [Min(0.01f)] public float fadeDuration = 0.4f;
    [Tooltip("비우면 가장 위쪽 Sorting Layer를 쓴다")]
    public string sortingLayerName = "";
    public int sortingOrder = 500;
    [Tooltip("③ 바깥 암막이 영역 밖으로 뻗는 거리. 화면보다 넉넉하면 된다")]
    public float outsideExtent = 60f;

    [Tooltip("해제 기록용 식별자. 비우면 계층 경로로 자동 생성")]
    public string persistentId = "";

    public bool Revealed { get; private set; }
    public bool PlayerInside { get; private set; }

    BoxCollider2D area;
    SpriteRenderer cover;                        // ①
    readonly SpriteRenderer[] outside = new SpriteRenderer[4]; // ③ 상하좌우
    float coverAlpha, outsideAlpha;
    string id;
    static Sprite pixel;

    void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
    }

    void Start()
    {
        id = WorldState.MakeId(this, persistentId);
        Revealed = WorldState.Has(WorldCategory.Vision, id);

        cover = MakeQuad("VisionCover");
        for (int i = 0; i < outside.Length; i++) outside[i] = MakeQuad("VisionOutside" + i);
        LayoutQuads();

        coverAlpha = CoverTarget() ? 1f : 0f; // 시작부터 가려져 있어야 한다 (페이드 인 금지)
        Apply();
    }

    void Update()
    {
        PlayerInside = ControlledPlayerInside();

        if (PlayerInside && hideUntilEntered && revealOnFirstEntry && !revealOnlyByTrigger && !Revealed)
            Reveal();

        float step = Time.deltaTime / fadeDuration;
        coverAlpha = Mathf.MoveTowards(coverAlpha, CoverTarget() ? 1f : 0f, step);
        outsideAlpha = Mathf.MoveTowards(outsideAlpha, darkenOutsideInside && PlayerInside ? outsideDarkness : 0f, step);
        Apply();
    }

    // 외부 조건(벽 파괴 등)으로 ①을 영구 해제
    public void Reveal()
    {
        if (Revealed) return;
        Revealed = true;
        WorldState.Record(WorldCategory.Vision, id ?? WorldState.MakeId(this, persistentId));
    }

    bool CoverTarget()
    {
        if (!hideUntilEntered || Revealed) return false;
        return revealOnlyByTrigger || !PlayerInside;
    }

    bool ControlledPlayerInside()
    {
        var m = PlayerManager.Instance;
        var pc = m != null ? m.currentPlayer : null;
        if (pc == null) return false;
        return GimmickPhysics.PlayersIn(area).Contains(pc);
    }

    void OnDisable()
    {
        SetAlpha(cover, 0f);
        foreach (var q in outside) SetAlpha(q, 0f);
    }

    void OnEnable()
    {
        if (cover != null) Apply();
    }

    void OnDestroy()
    {
        if (cover != null) Destroy(cover.gameObject);
        foreach (var q in outside) if (q != null) Destroy(q.gameObject);
    }

    void Apply()
    {
        SetAlpha(cover, coverAlpha);
        foreach (var q in outside) SetAlpha(q, outsideAlpha);
    }

    void SetAlpha(SpriteRenderer r, float a)
    {
        if (r == null) return;
        r.enabled = a > 0.001f;
        r.color = new Color(darkColor.r, darkColor.g, darkColor.b, a);
    }

    // 암막은 월드 좌표에 고정한다 (이 오브젝트의 회전/스케일과 무관하게 콜라이더 범위를 덮도록)
    void LayoutQuads()
    {
        Bounds b = area.bounds;
        Place(cover, b.center, b.size);

        float e = outsideExtent;
        // 위/아래는 좌우 끝까지, 좌/우는 영역 높이만큼 — 겹치지 않아야 모서리가 두 배로 진해지지 않는다
        Place(outside[0], new Vector3(b.center.x, b.max.y + e * 0.5f), new Vector2(b.size.x + e * 2f, e));
        Place(outside[1], new Vector3(b.center.x, b.min.y - e * 0.5f), new Vector2(b.size.x + e * 2f, e));
        Place(outside[2], new Vector3(b.min.x - e * 0.5f, b.center.y), new Vector2(e, b.size.y));
        Place(outside[3], new Vector3(b.max.x + e * 0.5f, b.center.y), new Vector2(e, b.size.y));
    }

    static void Place(SpriteRenderer r, Vector3 center, Vector2 size)
    {
        r.transform.position = new Vector3(center.x, center.y, 0f);
        r.transform.rotation = Quaternion.identity;
        r.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    SpriteRenderer MakeQuad(string name)
    {
        // 부모 회전/스케일에 휘둘리지 않게 씬 루트에 둔다. 정리는 OnDestroy에서.
        var go = new GameObject(name + " (" + gameObject.name + ")");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = Pixel();
        r.sortingOrder = sortingOrder;
        r.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? TopSortingLayer() : sortingLayerName;
        r.enabled = false;
        return r;
    }

    static string TopSortingLayer()
    {
        var layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].name : "Default";
    }

    static Sprite Pixel()
    {
        if (pixel != null) return pixel;
        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return pixel;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawCube(box.bounds.center, box.bounds.size);
        Gizmos.color = new Color(0.6f, 0.4f, 1f);
        Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
    }
}
