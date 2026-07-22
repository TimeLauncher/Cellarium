using UnityEngine;

// 가시: PC 접촉 시 체력 감소 + 접촉 반대 방향으로 넉백. 솔리드 콜라이더라 위에 서있을 수 있음
public class SpikeHazard : MonoBehaviour
{
    public float damage = 50f;
    public float knockbackForce = 10f;

    void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerController pc = collision.gameObject.GetComponent<PlayerController>();
        if (pc == null) return;

        Vector2 knockDir = ((Vector2)(pc.transform.position - transform.position)).normalized;
        if (knockDir.sqrMagnitude < 0.001f) knockDir = Vector2.up;
        pc.TakeDamage(damage, knockDir * knockbackForce, 0f);
    }
}
