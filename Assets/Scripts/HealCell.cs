using System.Collections;
using UnityEngine;

// 회복셀: 좌클릭 섭취 시 사라지며 체력 100(2칸) 회복. PC 접촉 판정 없음(섭취로만 상호작용)
// 섭취 대상은 PlayerController가 Monster 레이어에서 찾으므로 이 오브젝트도 Monster 레이어에 둘 것
[RequireComponent(typeof(Collider2D))]
public class HealCell : MonoBehaviour, IConsumable
{
    public float healAmount = 100f;

    [Tooltip("DropTo로 떨어뜨릴 때 낙하 속도 (침식 거미균 회복 오브젝트 드랍 등)")]
    public float dropSpeed = 12f;

    bool dropping;

    // 떨어지는 동안엔 먹을 수 없다
    public bool IsConsumable => !dropping;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public void OnConsumed(PlayerController consumer)
    {
        consumer.RestoreFromConsume(healAmount, 0f);
    }

    // 지금 위치에서 landAt까지 떨어진다 (트리거 콜라이더라 물리로는 바닥에 못 서므로 직접 옮긴다)
    public void DropTo(Vector2 landAt)
    {
        StopAllCoroutines();
        StartCoroutine(DropRoutine(landAt));
    }

    IEnumerator DropRoutine(Vector2 landAt)
    {
        dropping = true;
        while (Vector2.Distance(transform.position, landAt) > 0.01f)
        {
            transform.position = Vector2.MoveTowards(transform.position, landAt, Mathf.Max(0.1f, dropSpeed) * Time.deltaTime);
            yield return null;
        }
        transform.position = landAt;
        dropping = false;
    }
}
