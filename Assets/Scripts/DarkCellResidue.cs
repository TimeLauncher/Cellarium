using UnityEngine;

// 다크셀 잔재: 좌클릭 섭취 시 사라지며 특수 능력(분열 / 분열 회수 / 분열체 조종)을 획득한다.
// 섭취하면 PlayerManager.UnlockFission()을 호출해 잠겨 있던 분열 계열 능력을 전부 해금.
[RequireComponent(typeof(Collider2D))]
public class DarkCellResidue : MonoBehaviour, IConsumable
{
    [TextArea]
    public string abilityDescription = "분열 / 분열 회수 / 분열체 조종";

    [Tooltip("부활 후에도 '이미 섭취함'을 기억할 때 쓰는 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    public bool IsConsumable => true;

    string id;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // 이미 섭취한 잔재는 리로드된 씬에서 다시 나타나지 않게 즉시 치운다
        id = WorldState.MakeId(this, persistentId);
        if (WorldState.Has(WorldCategory.DarkCell, id))
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    public void OnConsumed(PlayerController consumer)
    {
        // 분열/분열 회수(R)/분열체 조종(숫자키)을 한꺼번에 해금 (잠금 게이트는 PlayerManager.fissionUnlocked 하나로 통일)
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.UnlockFission();

        WorldState.Record(WorldCategory.DarkCell, id);
        Debug.Log($"다크셀 잔재 섭취! 특수 능력 획득: {abilityDescription}");
    }
}
