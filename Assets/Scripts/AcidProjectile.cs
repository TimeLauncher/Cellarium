using UnityEngine;

// 부유균/부유산포균/소형거미균의 '산성 뱉기' 투사체 공용 스크립트
[RequireComponent(typeof(Rigidbody2D))]
public class AcidProjectile : MonoBehaviour
{
    public float lifeTime = 5f;

    // ★ 레이어로 지형을 거르지 않는다.
    //   이 프로젝트는 맵 지형이 ground(8)/wall(6)이 아니라 전부 Default(0)에 있다.
    //   (Heart A03 등의 오브젝트가 전부 m_Layer: 0) 그래서 obstacleMask=320(ground+wall)이
    //   걸리는 게 하나도 없어서 투사체가 타일을 그대로 통과했다.
    //   플레이어의 지면/벽 판정이 이미 레이어를 버리고 기하학 판정을 쓰고 있으므로 여기도 맞춘다.
    [Tooltip("비워두면(기본) 트리거·몬스터·투사체를 뺀 모든 솔리드 콜라이더에 부딪혀 사라진다. " +
             "값을 넣으면 그 레이어에만 부딪힌다(예전 방식)")]
    public LayerMask obstacleMask;

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
            // 몬스터의 원거리 공격이므로 대시 접촉 무적으로는 못 막는다
            pc.TakeDamage(damage, default, 0f, DamageSource.Attack);
            Destroy(gameObject);
            return;
        }

        if (IsObstacle(other))
            Destroy(gameObject);
    }

    // 지형으로 칠 것인가. 마스크가 설정돼 있으면 예전처럼 레이어로 판정하고,
    // 비어 있으면 기하학적으로 — '솔리드이고, 쏜 쪽(몬스터)도 플레이어도 다른 투사체도 아닌 것'.
    bool IsObstacle(Collider2D other)
    {
        if (obstacleMask.value != 0)
            return ((1 << other.gameObject.layer) & obstacleMask) != 0;

        if (other.isTrigger) return false;                              // 감지범위·상호작용 트리거는 통과
        if (other.GetComponent<MonsterBase>() != null) return false;    // 쏜 몬스터와 동료 몬스터는 통과
        if (other.GetComponent<AcidProjectile>() != null) return false; // 투사체끼리는 통과
        if (other.GetComponent<PlayerController>() != null) return false; // 위에서 이미 처리됨

        return true;
    }
}
