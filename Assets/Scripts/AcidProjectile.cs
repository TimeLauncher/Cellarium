using UnityEngine;

// 부유균/부유산포균/소형거미균의 '산성 뱉기' 투사체 공용 스크립트
[RequireComponent(typeof(Rigidbody2D))]
public class AcidProjectile : MonoBehaviour
{
    public float lifeTime = 5f;
    public LayerMask obstacleMask; // 지형 등 오브젝트에 닿으면 제거

    private float damage;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        Destroy(gameObject, lifeTime);
    }

    public void Init(Vector2 direction, float speed, float dmg)
    {
        damage = dmg;
        Vector2 velocity = direction.normalized * speed;
        rb.linearVelocity = velocity;

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg+180f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & obstacleMask) != 0)
            Destroy(gameObject);
    }
}
