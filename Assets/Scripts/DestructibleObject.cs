using UnityEngine;

// 체력이 있어 PC의 공격(대시)으로 부서지는 오브젝트의 공통 부모.
// 응고 조직 벽(CoagulatedWall), 몬스터 둥지(MonsterNest)가 이걸 상속한다.
//
// ★ MonsterBase를 상속하지 않은 이유: MonsterBase는 순찰/추적 AI와 '체력 0 = 섭취 가능 시체'가 붙어 있다.
//   벽·둥지는 움직이지 않고 체력 0이면 즉시 사라져야 하므로 체력·피격·셀 드랍만 따로 둔다.
// ★ 레이어는 상관없다 — PlayerController의 대시가 레이어 마스크와 별개로 이 컴포넌트를 찾는다.
//   (벽은 PC를 막아야 하는데 Monster 레이어는 PC와 충돌하지 않으므로, 지형 레이어에 두면 된다)
public class DestructibleObject : MonoBehaviour
{
    [Header("체력")]
    [Min(1f)] public float maxHp = 100f;

    [Tooltip("파괴 기록용 식별자. 비우면 계층 경로로 자동 생성된다 (오브젝트를 옮기면 바뀌므로 확정되면 적어둘 것)")]
    public string persistentId = "";

    [Header("피격 연출")]
    public HitEffect.Settings hitEffect = new HitEffect.Settings
    {
        scale = 0.9f,
        lifetime = 0.3f,
        fallbackColor = new Color(1f, 0.85f, 0.35f, 1f),
    };
    public Color hitFlashColor = new Color(1f, 0.45f, 0.45f, 1f);
    [Min(0f)] public float hitFlashDuration = 0.12f;

    [Header("셀 드랍 (몬스터와 동일)")]
    [Tooltip("파괴 시 떨어뜨리는 셀 총량. 0이면 드랍하지 않는다")]
    [Min(0)] public int cellDropTotal = 0;
    [Tooltip("총량을 몇 덩어리로 나눠 뿌릴지")]
    [Min(1)] public int cellDropCount = 5;
    [Tooltip("비우면 기본 셀(Resources/Effects/CellDrop)을 쓴다")]
    public GameObject cellChunkPrefab;
    public float cellPickupDelay = 0.45f;
    public float cellPopUpSpeed = 4f;
    public float cellPopSideSpeed = 2.5f;

    public float CurrentHp { get; private set; }
    public bool IsDestroyed { get; private set; }

    protected SpriteRenderer spr;
    Color baseColor = Color.white;
    float flashTimer;
    string id;

    protected virtual void Awake()
    {
        spr = GetComponent<SpriteRenderer>();
        if (spr != null) baseColor = spr.color;
        CurrentHp = maxHp;
    }

    protected virtual void Start()
    {
        id = WorldState.MakeId(this, persistentId);
        if (WorldState.Has(WorldCategory.Destructible, id))
        {
            IsDestroyed = true;
            OnAlreadyDestroyed();
        }
    }

    protected virtual void Update()
    {
        if (flashTimer <= 0f || spr == null) return;
        flashTimer -= Time.deltaTime;
        spr.color = flashTimer > 0f ? hitFlashColor : baseColor;
    }

    public Bounds HitBounds
    {
        get
        {
            var c = GetComponent<Collider2D>();
            return c != null ? c.bounds : new Bounds(transform.position, Vector3.one);
        }
    }

    public virtual void TakeDamage(float amount, Vector2 hitDirection = default)
    {
        if (IsDestroyed || amount <= 0f) return;

        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        HitEffect.Play(hitEffect, HitBounds.center, hitDirection, spr);
        if (spr != null && hitFlashDuration > 0f)
        {
            flashTimer = hitFlashDuration;
            spr.color = hitFlashColor;
        }

        if (CurrentHp > 0f) return;

        IsDestroyed = true;
        WorldState.Record(WorldCategory.Destructible, id ?? WorldState.MakeId(this, persistentId));
        DropCells();
        OnDestroyed();
    }

    // 체력이 0이 된 순간. 기본은 오브젝트 제거.
    protected virtual void OnDestroyed()
    {
        Destroy(gameObject);
    }

    // 씬 로드 시 이미 부서진 기록이 있을 때. 드랍·연출 없이 조용히 사라진다.
    protected virtual void OnAlreadyDestroyed()
    {
        Destroy(gameObject);
    }

    void DropCells()
    {
        if (cellDropTotal <= 0 || cellDropCount <= 0) return;

        Bounds area = HitBounds;
        int perChunk = cellDropTotal / cellDropCount;
        int remainder = cellDropTotal - perChunk * cellDropCount;

        for (int i = 0; i < cellDropCount; i++)
        {
            int amount = perChunk + (i < remainder ? 1 : 0);
            if (amount <= 0) continue;

            Vector3 pos = new Vector3(Random.Range(area.min.x, area.max.x),
                                      Random.Range(area.min.y, area.max.y),
                                      transform.position.z);
            CellChunk chunk = CellChunk.Spawn(pos, amount, cellChunkPrefab, spr);
            if (chunk == null) continue;
            chunk.pickupDelay = cellPickupDelay;
            chunk.Launch(new Vector2(Random.Range(-cellPopSideSpeed, cellPopSideSpeed),
                                     Random.Range(cellPopUpSpeed * 0.6f, cellPopUpSpeed)));
        }
    }
}
