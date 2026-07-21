using UnityEngine;

// 소형거미균: 벽면을 빠르게 타고 다니며 투사체로 플레이어 동선을 방해
// surfaceNormal = 부착된 벽/천장의 바깥쪽 방향. 배치할 벽에 맞춰 Inspector에서 설정
//   바닥에 서는 벽면 없이 항상 벽/천장에만 붙어 다니므로 지면 이동은 지원하지 않음
public class SmallSpiderGerm : MonsterBase
{
    [Header("벽 타기")]
    public Vector2 surfaceNormal = Vector2.right;
    public float stickRayDistance = 1f;
    public LayerMask surfaceMask;

    [Header("교전")]
    public float engageRange = 8f;

    [Header("원거리 공격")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 8f;
    public float projectileDamage = 50f;
    public float fireWindup = 0.3f;   // 발사 준비시간 (애니메이션 붙기 전 임시)
    public float fireRecovery = 0.3f; // 발사 후 여유시간

    protected Vector2 Tangent => new Vector2(-surfaceNormal.y, surfaceNormal.x);

    protected override void Awake()
    {
        base.Awake();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
        AlignToSurface();
    }

    protected void AlignToSurface()
    {
        float angle = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    protected override void UpdateBehavior()
    {
        if (isAttacking) return;

        if (target != null && attackCooldownTimer <= 0f &&
            Vector2.Distance(transform.position, target.position) <= engageRange)
        {
            TryStartAttack();
        }
    }

    protected override void TryStartAttack()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        if (animator != null) animator.SetTrigger("Attack");

        if (!HasAnimatorController)
        {
            Invoke(nameof(FireProjectile), fireWindup);
            Invoke(nameof(StopAttack), fireWindup + fireRecovery);
        }
    }

    public override void StopAttack()
    {
        CancelInvoke(nameof(FireProjectile));
        base.StopAttack();
    }

    // 애니메이션 이벤트 (지금은 위 Invoke 타이머가 대신 호출)
    public void FireProjectile()
    {
        if (IsConsumable || target == null || projectilePrefab == null) return;
        Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
        Vector3 pos = firePoint != null ? firePoint.position : transform.position;
        GameObject proj = Instantiate(projectilePrefab, pos, Quaternion.identity);
        AcidProjectile ap = proj.GetComponent<AcidProjectile>();
        if (ap != null) ap.Init(dir, projectileSpeed, projectileDamage);
    }

    protected override void UpdateMovement()
    {
        StickToSurface();

        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (target != null)
        {
            float dirAlong = Mathf.Sign(Vector2.Dot(target.position - transform.position, Tangent));
            rb.linearVelocity = Tangent * dirAlong * moveSpeed;
        }
        else
        {
            PatrolAlongSurface();
        }
    }

    protected void PatrolAlongSurface()
    {
        if (patrolPauseTimer > 0f)
        {
            patrolPauseTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float offset = Vector2.Dot((Vector2)transform.position - patrolOrigin, Tangent);
        if (patrolDir > 0 && offset >= patrolDistance)
        {
            patrolDir = -1;
            patrolPauseTimer = patrolPauseDuration;
        }
        else if (patrolDir < 0 && offset <= -patrolDistance)
        {
            patrolDir = 1;
            patrolPauseTimer = patrolPauseDuration;
        }

        rb.linearVelocity = Tangent * patrolDir * moveSpeed;
    }

    protected void StickToSurface()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, -surfaceNormal, stickRayDistance, surfaceMask);
        if (hit.collider != null)
        {
            Vector2 targetPos = hit.point + surfaceNormal * 0.05f;
            transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime * 2f);
        }
    }
}
