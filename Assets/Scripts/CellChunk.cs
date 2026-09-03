using UnityEngine;

// 셀 덩어리: 섭취 불필요, PC 접촉 시 자동으로 사라지며 보유 셀 증가. 움직임을 방해하는 판정 없음
[RequireComponent(typeof(Collider2D))]
public class CellChunk : MonoBehaviour
{
    public int cellAmount = 50; // 지역/상황별로 인스펙터에서 조정

    [Tooltip("부활 후에도 '이미 먹음'을 기억할 때 쓰는 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    [Header("드랍 연출 (몬스터가 떨군 셀에만 쓰임)")]
    // ★ 섭취 직후 몬스터 자리에 셀을 만들면 플레이어가 이미 그 자리에 겹쳐 있어서
    //   생기는 즉시 흡수된다 — "셀이 나왔다"는 게 화면에 전혀 안 보인다.
    //   그래서 잠깐 튀어나오는 동안은 못 먹게 막는다.
    [Tooltip("생성 후 이 시간 동안은 접촉해도 획득되지 않는다. 씬에 손으로 놓은 덩어리는 0으로 둘 것")]
    public float pickupDelay = 0f;

    [Tooltip("튀어나오는 연출에 쓰는 중력 (Launch로 던져졌을 때만)")]
    public float popGravity = 14f;

    [Tooltip("튀어나오면서 가로로 이동할 수 있는 최대 거리. 벽 판정이 없어도 이만큼만 간다")]
    public float maxPopDistanceX = 1.2f;

    [Tooltip("튀어나오는 도중 이 레이어에 막히면 그 자리에서 멈춘다. 비워두면(Nothing) 기본값을 쓴다.\n" +
             "★ 이 프로젝트의 타일맵 콜라이더는 Default(0)에 있으므로 Default를 빼면 벽을 그냥 통과한다")]
    public LayerMask popBlockMask;

    // Default(0) | wall(6) | ground(8). 이 프로젝트는 지형 레이어를 신뢰할 수 없어서
    // (지면 타일맵 콜라이더가 Default에 있다) Default를 반드시 포함해야 한다.
    const int DefaultBlockMask = (1 << 0) | (1 << 6) | (1 << 8);
    const float PopSkin = 0.05f;

    // 튀어나오는 연출에서 실제로 움직일 오브젝트.
    // 팀원이 만든 Bigcell 프리팹처럼 CellChunk가 자식에 붙어 있는 구조에서는
    // 자기 자신만 움직이면 껍데기(빛/파티클)가 제자리에 남는다. Spawn이 루트를 넣어준다.
    [System.NonSerialized] public Transform popRoot;

    float aliveTimer;
    Vector2 popVelocity;
    bool popping;
    float popFloorY;
    float popTravelX;   // 지금까지 가로로 이동한 거리

    // ★ 몬스터가 죽으면서 떨군 셀(기획서 (4))은 '이미 먹었는지'를 기억하면 안 된다.
    //   씬에 손으로 놓은 덩어리와 달리, 부활로 씬이 리로드되면 몬스터도 되살아나 다시 떨구기 때문에
    //   기록해두면 두 번째부터는 생기자마자 스스로 사라져버린다.
    //   Awake보다 먼저 값을 넣을 방법이 없어서(Instantiate가 Awake를 즉시 부른다)
    //   생성 직전에 이 플래그를 세우고 Awake가 바로 소비한다.
    public static bool NextIsRuntimeDrop;

    string id;
    bool isRuntimeDrop;

    Transform PopRoot => popRoot != null ? popRoot : transform;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (NextIsRuntimeDrop)
        {
            NextIsRuntimeDrop = false;
            isRuntimeDrop = true;
            return; // 몬스터 드랍분은 WorldState를 쓰지 않는다
        }

        // 이미 먹은 덩어리는 리로드된 씬에서 되살아나지 않게 즉시 치운다
        id = WorldState.MakeId(this, persistentId);
        if (WorldState.Has(WorldCategory.Pickup, id))
        {
            gameObject.SetActive(false); // Destroy는 프레임 끝에 처리되므로 먼저 꺼서 트리거를 막는다
            Destroy(gameObject);
        }
    }

    void Update()
    {
        aliveTimer += Time.deltaTime;

        if (!popping) return;

        Transform root = PopRoot;
        Vector3 step = (Vector3)(popVelocity * Time.deltaTime);

        // ★ 가로 이동만 지형 검사를 한다 (세로는 아래 popFloorY로 이미 막혀 있다).
        //   벽 옆에서 죽은 몬스터가 떨군 셀이 벽을 뚫고 나가 못 먹게 되는 것을 막는다.
        if (Mathf.Abs(step.x) > 0.0001f)
        {
            if (popTravelX + Mathf.Abs(step.x) > maxPopDistanceX || IsBlockedX(root.position, step.x))
            {
                popVelocity.x = 0f;
                step.x = 0f;
            }
            else
            {
                popTravelX += Mathf.Abs(step.x);
            }
        }

        root.position += step;
        popVelocity.y -= popGravity * Time.deltaTime;

        // 던져진 높이까지 도로 내려오면 연출 끝. 지형 충돌을 따로 보지 않아도
        // 시작 높이에서 멈추므로 바닥을 뚫고 들어가지 않는다.
        if (popVelocity.y < 0f && root.position.y <= popFloorY)
        {
            Vector3 p = root.position;
            p.y = popFloorY;
            root.position = p;
            popping = false;
        }
    }

    // 몬스터가 떨굴 때 살짝 튀어나오는 연출. 프리팹에 Rigidbody2D가 있으면 그쪽을 쓴다.
    public void Launch(Vector2 velocity)
    {
        Transform root = PopRoot;

        Rigidbody2D body = root.GetComponent<Rigidbody2D>();
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
        {
            body.linearVelocity = velocity;
            return;
        }

        popVelocity = velocity;
        popFloorY = root.position.y;
        popTravelX = 0f;
        popping = true;
    }

    // 가로 방향이 지형에 막혔는지.
    // ★ 이 프로젝트는 지형 레이어를 신뢰할 수 없다(지면 타일맵 콜라이더가 Default에 있고,
    //   ground(8) 레이어를 쓰는 콜라이더는 몇 개뿐이다). 그래서 마스크를 넓게 두고
    //   트리거·자기 자신·몬스터·플레이어는 코드에서 걸러낸다.
    bool IsBlockedX(Vector3 from, float stepX)
    {
        int mask = popBlockMask.value != 0 ? popBlockMask.value : DefaultBlockMask;
        Vector2 dir = new Vector2(Mathf.Sign(stepX), 0f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(from, dir, Mathf.Abs(stepX) + PopSkin, mask);

        foreach (RaycastHit2D h in hits)
        {
            Collider2D c = h.collider;
            if (c == null || c.isTrigger) continue;                        // 획득/상호작용 판정은 지형이 아니다
            if (c.transform.IsChildOf(PopRoot)) continue;                  // 자기 자신
            if (c.GetComponentInParent<MonsterBase>() != null) continue;   // 시체에 걸려 멈추지 않게
            if (c.GetComponentInParent<PlayerController>() != null) continue;
            return true;
        }
        return false;
    }

    void OnTriggerEnter2D(Collider2D other) => TryPickup(other);

    // ★ Enter만으로는 부족하다 — 획득 딜레이가 끝나는 시점엔 플레이어가 이미 겹쳐 있어서
    //   더 이상 '진입'이 일어나지 않는다. 겹쳐 있는 동안 계속 확인해야 딜레이 후에 먹힌다.
    void OnTriggerStay2D(Collider2D other) => TryPickup(other);

    void TryPickup(Collider2D other)
    {
        if (aliveTimer < pickupDelay) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        if (PlayerManager.Instance != null)
            PlayerManager.Instance.AddCell(cellAmount);

        if (!isRuntimeDrop) WorldState.Record(WorldCategory.Pickup, id);
        Destroy(PopRoot.gameObject); // 프리팹 구조상 껍데기(빛/파티클)가 부모일 수 있어 루트째 지운다
    }

    // ── 셀 덩어리 생성 공용 창구 ──────────────────────────────────────
    //
    // 셀이 어떤 모습으로 나올지를 한 군데서 정한다. 고르는 순서는 HitEffect와 같다:
    //   ① 인스펙터에 꽂은 프리팹 (몬스터별로 다른 셀을 쓰고 싶을 때)
    //   ② Assets/Resources/Effects/CellDrop.prefab  ← 기본. 진짜 셀 이미지가 여기 들어있다
    //   ③ 그것도 없으면 런타임 흰 동그라미 (에셋이 하나도 없을 때만)
    //
    // ★ ②가 Resources 아래에 있는 이유: 씬마다 몬스터가 28마리 넘게 깔려 있는데
    //   전부 인스펙터에 프리팹을 꽂게 하면 한 마리라도 빠뜨리면 그 몬스터만 흰 동그라미를 떨군다.
    const string DefaultPrefabPath = "Effects/CellDrop";

    static GameObject defaultPrefab;
    static bool defaultPrefabSearched;

    static GameObject DefaultPrefab()
    {
        if (!defaultPrefabSearched)
        {
            defaultPrefabSearched = true;
            defaultPrefab = Resources.Load<GameObject>(DefaultPrefabPath);
        }
        return defaultPrefab;
    }

    // position 자리에 셀 덩어리 하나를 만든다. 획득 기록(WorldState)은 남기지 않는다 —
    // 몬스터 드랍·이벤트 보상은 씬을 리로드하면 다시 생겨야 하기 때문.
    // sortingRef: 정렬 레이어를 물려줄 스프라이트(보통 떨군 몬스터). 프리팹을 쓸 땐 건드리지 않는다.
    public static CellChunk Spawn(Vector3 position, int amount, GameObject prefab = null,
                                  SpriteRenderer sortingRef = null)
    {
        GameObject source = prefab != null ? prefab : DefaultPrefab();

        if (source == null)
            return SpawnRuntime(position, amount, sortingRef);

        NextIsRuntimeDrop = true;
        GameObject go = Instantiate(source, position, Quaternion.identity);
        NextIsRuntimeDrop = false; // 프리팹이 비활성이라 Awake가 안 돈 경우 대비

        // 팀원이 만든 Bigcell 프리팹처럼 CellChunk가 자식에 붙어 있는 구조도 받아준다
        CellChunk chunk = go.GetComponentInChildren<CellChunk>(true);
        if (chunk == null)
        {
            Debug.LogWarning($"[CellChunk] 셀 프리팹 '{source.name}'에 CellChunk 컴포넌트가 없습니다.", source);
            Destroy(go);
            return SpawnRuntime(position, amount, sortingRef);
        }

        chunk.popRoot = go.transform;   // 튀어오르기/제거는 루트 기준
        chunk.cellAmount = amount;

        // ★ 떨군 몬스터의 정렬 레이어를 물려준다.
        //   이 프로젝트엔 Default 말고 'New Layer 1/2'도 쓰이고 있어서, 레이어가 다르면
        //   Order in Layer를 아무리 높여도 무시된다 — 몬스터는 보이는데 셀만 맵 뒤에 숨는다.
        if (sortingRef != null)
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerID = sortingRef.sortingLayerID;
                sr.sortingOrder = sortingRef.sortingOrder + 1; // 시체보다 앞에
            }

        return chunk;
    }

    // 셀 스프라이트가 아직 없어서(에셋 미제작) 런타임에 임시 셀 덩어리를 만든다.
    // HitboxVisualizer·ExplosionRangeIndicator와 같은 "에셋 없이 일단 보이게" 방식.
    // 진짜 프리팹이 나오면 MonsterBase의 Cell Chunk Prefab 칸에 넣으면 이건 안 쓰인다.
    // sortingRef: 떨군 몬스터의 SpriteRenderer. 정렬 레이어를 그대로 물려받아 몬스터가 보이는 곳이면
    //   셀도 보이게 한다. ★ Sorting Layer가 다르면 Order in Layer는 완전히 무시되므로
    //   (이 프로젝트엔 Default 말고 New Layer 1/2도 있다) Order만 높게 줘서는 안 된다.
    public static CellChunk SpawnRuntime(Vector3 position, int amount, SpriteRenderer sortingRef = null,
                                         float radius = 0.22f)
    {
        GameObject go = new GameObject("CellDrop");
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDropSprite();
        sr.color = new Color(1f, 0.92f, 0.45f, 1f);

        if (sortingRef != null)
        {
            sr.sortingLayerID = sortingRef.sortingLayerID;
            sr.sortingOrder = sortingRef.sortingOrder + 1; // 시체보다 앞에
        }
        else
        {
            sr.sortingOrder = 50;
        }

        go.transform.localScale = Vector3.one * (radius * 2f);

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f; // 스프라이트가 지름 1유닛이라 스케일과 함께 실제 radius가 된다

        NextIsRuntimeDrop = true;
        CellChunk chunk = go.AddComponent<CellChunk>(); // AddComponent가 Awake를 즉시 부른다
        NextIsRuntimeDrop = false;

        chunk.cellAmount = amount;
        return chunk;
    }

    static Sprite dropSprite;

    static Sprite GetDropSprite()
    {
        if (dropSprite != null) return dropSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        Vector2 center = new Vector2(r, r);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                // 가장자리를 살짝 부드럽게
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        dropSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return dropSprite;
    }
}
