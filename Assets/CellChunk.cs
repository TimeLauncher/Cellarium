using UnityEngine;

// 셀 덩어리: 섭취 불필요, PC 접촉 시 자동으로 사라지며 보유 셀 증가. 움직임을 방해하는 판정 없음
[RequireComponent(typeof(Collider2D))]
public class CellChunk : MonoBehaviour
{
    public int cellAmount = 50; // 지역/상황별로 인스펙터에서 조정

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        if (PlayerManager.Instance != null)
            PlayerManager.Instance.AddCell(cellAmount);

        Destroy(gameObject);
    }
}
