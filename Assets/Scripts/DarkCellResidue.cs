using UnityEngine;

// 다크셀 잔재: 좌클릭 섭취 시 사라지며 특수 능력(분열 / 분열 회수 / 분열체 조종)을 획득한다.
// 섭취하면 PlayerManager.UnlockFission()을 호출해 잠겨 있던 분열 계열 능력을 전부 해금.
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
        // 분열/분열 회수(R)/분열체 조종(숫자키)을 한꺼번에 해금 (잠금 게이트는 PlayerManager.fissionUnlocked 하나로 통일)
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.UnlockFission();
        Debug.Log($"다크셀 잔재 섭취! 특수 능력 획득: {abilityDescription}");
    }
}
