using UnityEngine;

// 일반비행균: 느린 속도로 체공하며 조우 시 추적, 근접 시 공중 돌진
public class FlyingGerm : MonsterBase
{
    [Header("추적")]
    public float chaseSpeed = 3f; // 초당 3타일

    [Header("공중 돌진")]
    public float diveAttackRange = 3f; // 이 거리 이내면 돌진 시전
    public float diveSpeed = 8f;
    public float diveWindup = 0.3f;
    public float diveMaxDuration = 1f; // 모션 미완성 대비 안전장치 (추후 애니메이션 이벤트로 대체 가능)

    private Vector2 diveDir;
    private bool isDiving;

    protected override void Awake()
    {
        base.Awake();
        avoidLedges = false; // 공중/벽면을 이동하므로 낭떠러지 감지 불필요
        if (rb != null) rb.gravityScale = 0f;
    }

    protected override void UpdateBehavior()
    {
        if (isAttacking) return;

        if (target != null && attackCooldownTimer <= 0f &&
            Vector2.Distance(transform.position, target.position) <= diveAttackRange)
        {
            TryStartAttack();
        }
    }

    protected override void TryStartAttack()
    {
        diveDir = ((Vector2)(target.position - transform.position)).normalized;
        isAttacking = true;
        isDiving = false;
        if (animator != null) animator.SetTrigger("Attack");

        if (!HasAnimatorController)
        {
            Invoke(nameof(StartDive), diveWindup);
            Invoke(nameof(StopAttack), diveWindup + diveMaxDuration);
        }
    }

    void StartDive()
    {
        if (!isAttacking || IsDead) return;
        isDiving = true;
        EnableHitbox();
    }

    protected override void UpdateMovement()
    {
        if (MovementSuppressed()) return;

        if (isAttacking)
        {
            rb.linearVelocity = isDiving ? diveDir * diveSpeed : Vector2.zero;
            return;
        }

        // 추격 제한: 원점에서 너무 멀어지면 복귀 (원점까지 다 돌아온 뒤 다시 추격)
        if (returningHome)
        {
            ReturnToOrigin();
            return;
        }
        if (IsBeyondLeash())
        {
            returningHome = true;
            ReturnToOrigin();
            return;
        }

        if (target != null)
        {
            // 추적 중 PC가 반대편으로 넘어가 이동 방향이 급전환되면 잠깐 멈췄다가 따라간다 (QA: 0.5초 내외)
            float dx = target.position.x - transform.position.x;
            int hdir = dx > 0.02f ? 1 : (dx < -0.02f ? -1 : 0);
            if (hdir != 0 && lastChaseDir != 0 && hdir != lastChaseDir)
                turnPauseTimer = turnPauseDuration;
            if (hdir != 0) lastChaseDir = hdir;

            if (turnPauseTimer > 0f)
            {
                rb.linearVelocity = Vector2.zero;
                FaceDirection(dx);
                return;
            }

            Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
            rb.linearVelocity = dir * chaseSpeed;
            FaceDirection(dir.x);
        }
        else
        {
            lastChaseDir = 0;
            Patrol();
        }
    }

    // 비행형은 중력이 없으므로 x/y 모두 원점 방향으로 복귀
    protected override void ReturnToOrigin()
    {
        Vector2 toOrigin = patrolOrigin - (Vector2)transform.position;
        if (toOrigin.magnitude <= 0.2f)
        {
            returningHome = false;
            rb.linearVelocity = Vector2.zero;
            return;
        }
        rb.linearVelocity = toOrigin.normalized * chaseSpeed;
        FaceDirection(toOrigin.x);
    }

    public override void StopAttack()
    {
        CancelInvoke(nameof(StartDive));
        CancelInvoke(nameof(StopAttack));
        isAttacking = false;
        isDiving = false;
        ShowTelegraph(false);
        DisableHitbox();
        attackCooldownTimer = attackCooldown; // 공격이 끝난 시점부터 쿨다운 시작
        actionPauseTimer = postAttackPause;   // 돌진 종료 후 잠깐 멈췄다가 움직이도록
    }
}
