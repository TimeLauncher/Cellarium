using UnityEngine;

// 소형거미균: 벽면을 빠르게 타고 다니며 투사체로 플레이어 동선을 방해
// surfaceNormal = 부착된 벽/천장의 바깥쪽 방향. 배치할 벽에 맞춰 Inspector에서 설정
//   바닥에 서는 벽면 없이 항상 벽/천장에만 붙어 다니므로 지면 이동은 지원하지 않음
public class SmallSpiderGerm : MonsterBase
{
    [Header("벽 타기")]
    public Vector2 surfaceNormal = Vector2.right; // 시작 방향. 실제로는 닿은 면에 맞춰 자동 갱신됨
    public float stickRayDistance = 1f;
    public float surfaceSearchDistance = 3f;      // 붙을 면을 잃었을 때 주변을 훑는 거리
    public float surfaceOffset = 0.2f;            // 표면에서 띄울 간격 (몸이 박히면 늘릴 것)
    public LayerMask surfaceMask;                 // 미사용 — 맵이 레이어로 안 나뉘어 있어 CastSurface로 판정

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

    private bool isAttachedToSurface;
    private bool hasSurfaceNearby = true;

    protected override void Awake()
    {
        base.Awake();
        avoidLedges = false; // 공중/벽면을 이동하므로 낭떠러지 감지 불필요
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
        if (IsDead || target == null || projectilePrefab == null) return;
        Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
        Vector3 pos = firePoint != null ? firePoint.position : transform.position;
        GameObject proj = Instantiate(projectilePrefab, pos, Quaternion.identity);
        AcidProjectile ap = proj.GetComponent<AcidProjectile>();
        if (ap != null) ap.Init(dir, projectileSpeed, projectileDamage);
    }

    protected override void UpdateMovement()
    {
        StickToSurface();

        // 붙을 면을 아예 못 찾으면 중력으로 떨어뜨린다 (gravityScale이 0이라 공중에 멈춰버리는 것 방지)
        if (!hasSurfaceNearby)
        {
            rb.gravityScale = 1f;
            return;
        }
        rb.gravityScale = 0f;

        // 아직 면에 다 붙지 않았으면 접선 이동을 멈추고 붙는 것만 우선 (벽에서 떨어진 채 떠다니는 것 방지)
        if (!isAttachedToSurface || isAttacking)
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
        if (patrolDir > 0 && offset >= currentPatrolLegDistance)
        {
            patrolDir = -1;
            RerollPatrolLeg();
        }
        else if (patrolDir < 0 && offset <= -currentPatrolLegDistance)
        {
            patrolDir = 1;
            RerollPatrolLeg();
        }

        rb.linearVelocity = Tangent * patrolDir * moveSpeed;
    }

    // 현재 붙어 있는 면에 몸을 붙인다. 면을 잃으면 주변을 훑어 새 면을 찾아 그쪽으로 이동한다.
    protected void StickToSurface()
    {
        // 탐색 결과를 stickRayDistance로 다시 검사하면 멀리 있는 벽에는 영원히 못 붙으므로 그대로 사용한다
        if (!CastSurface(transform.position, -surfaceNormal, stickRayDistance, out RaycastHit2D hit)
            && !TryFindSurface(out hit))
        {
            isAttachedToSurface = false;
            hasSurfaceNearby = false;
            return;
        }
        hasSurfaceNearby = true;

        surfaceNormal = hit.normal; // 실제 면 법선으로 갱신 → 모서리를 돌아도 자연스럽게 따라감
        AlignToSurface();

        Vector2 targetPos = hit.point + surfaceNormal * surfaceOffset;
        transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime * 2f);

        isAttachedToSurface = Vector2.Distance(transform.position, targetPos) <= surfaceOffset;
    }

    // 아래/좌/우/위 중 가장 가까운 지형을 찾아 그 면을 반환
    protected bool TryFindSurface(out RaycastHit2D best)
    {
        best = default;
        float bestDist = float.MaxValue;
        bool found = false;

        Vector2[] dirs = { Vector2.down, Vector2.left, Vector2.right, Vector2.up };
        foreach (var d in dirs)
        {
            if (CastSurface(transform.position, d, surfaceSearchDistance, out RaycastHit2D h) && h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }
}
