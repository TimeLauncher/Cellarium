using UnityEngine;

// 회복셀: 좌클릭 섭취 시 사라지며 체력 100(2칸) 회복. PC 접촉 판정 없음(섭취로만 상호작용)
[RequireComponent(typeof(Collider2D))]
public class HealCell : MonoBehaviour, IConsumable
{
    public float healAmount = 100f;

    public bool IsConsumable => true;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public void OnConsumed(PlayerController consumer)
    {
        consumer.RestoreFromConsume(healAmount, 0f);
    }
}
