using UnityEngine;

// 셀 덩어리: 섭취 불필요, PC 접촉 시 자동으로 사라지며 보유 셀 증가. 움직임을 방해하는 판정 없음
[RequireComponent(typeof(Collider2D))]
public class CellChunk : MonoBehaviour
{
    public int cellAmount = 50; // 지역/상황별로 인스펙터에서 조정

    [Tooltip("부활 후에도 '이미 먹음'을 기억할 때 쓰는 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    // ★ 몬스터가 죽으면서 떨군 셀(기획서 (4))은 '이미 먹었는지'를 기억하면 안 된다.
    //   씬에 손으로 놓은 덩어리와 달리, 부활로 씬이 리로드되면 몬스터도 되살아나 다시 떨구기 때문에
    //   기록해두면 두 번째부터는 생기자마자 스스로 사라져버린다.
    //   Awake보다 먼저 값을 넣을 방법이 없어서(Instantiate가 Awake를 즉시 부른다)
    //   생성 직전에 이 플래그를 세우고 Awake가 바로 소비한다.
    public static bool NextIsRuntimeDrop;

    string id;
    bool isRuntimeDrop;

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

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        if (PlayerManager.Instance != null)
            PlayerManager.Instance.AddCell(cellAmount);

        // TODO(임시): 셀 드랍 확인용 로그. 동작 확인되면 이 줄과 MonsterBase.DropCells의 로그를 지울 것
        Debug.Log($"[셀 획득] +{cellAmount} (보유 {(PlayerManager.Instance != null ? PlayerManager.Instance.cellCurrency : 0)})");

        if (!isRuntimeDrop) WorldState.Record(WorldCategory.Pickup, id);
        Destroy(gameObject);
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
