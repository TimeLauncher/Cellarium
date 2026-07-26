using UnityEngine;

// 셀 덩어리: 섭취 불필요, PC 접촉 시 자동으로 사라지며 보유 셀 증가. 움직임을 방해하는 판정 없음
[RequireComponent(typeof(Collider2D))]
public class CellChunk : MonoBehaviour
{
    public int cellAmount = 50; // 지역/상황별로 인스펙터에서 조정

    [Tooltip("부활 후에도 '이미 먹음'을 기억할 때 쓰는 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    string id;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

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

        WorldState.Record(WorldCategory.Pickup, id);
        Destroy(gameObject);
    }
}
