using UnityEngine;

// 다크셀 잔재: 좌클릭 섭취 시 사라지며 특수 능력 획득.
// 획득 연출 및 실제 능력 잠금/해금(A02 구역: 분열/분열회수/분열체조종)은 추후 구현 예정 — 지금은 훅만 남겨둠
[RequireComponent(typeof(Collider2D))]
public class DarkCellResidue : MonoBehaviour, IConsumable
{
    [TextArea]
    public string abilityDescription = "분열 / 분열 회수 / 분열체 조종";

    public bool IsConsumable => true;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public void OnConsumed(PlayerController consumer)
    {
        Debug.Log($"다크셀 잔재 섭취! 특수 능력 획득: {abilityDescription} (획득 연출/실제 잠금 로직은 추후 구현)");
    }
}
