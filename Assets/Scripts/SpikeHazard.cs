using UnityEngine;

// 가시: PC 접촉 시 체력 감소 + 접촉 반대 방향으로 넉백. 솔리드 콜라이더라 위에 서있을 수 있음
public class SpikeHazard : MonoBehaviour
{
    public float damage = 50f;
    public float knockbackForce = 10f;

    void OnCollisionEnter2D(Collision2D collision) => Hit(collision.gameObject);

    // QA (5): 가시에 접촉 상태를 유지해도 계속 피격. TakeDamage가 무적 중엔 무시하므로
    // 무적시간이 재피격 간격을 알아서 rate-limit 해준다.
    void OnCollisionStay2D(Collision2D collision) => Hit(collision.gameObject);

    void Hit(GameObject other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        Vector2 knockDir = ((Vector2)(pc.transform.position - transform.position)).normalized;
        if (knockDir.sqrMagnitude < 0.001f) knockDir = Vector2.up;
        pc.TakeDamage(damage, knockDir * knockbackForce, 0f);
    }
}
