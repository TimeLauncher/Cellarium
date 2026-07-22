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
        if (isAttacking)
        {
            rb.linearVelocity = isDiving ? diveDir * diveSpeed : Vector2.zero;
            return;
        }

        if (target != null)
        {
            Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
            rb.linearVelocity = dir * chaseSpeed;
            FaceDirection(dir.x);
        }
        else
        {
            Patrol();
        }
    }

    public override void StopAttack()
    {
        CancelInvoke(nameof(StartDive));
        CancelInvoke(nameof(StopAttack));
        isAttacking = false;
        isDiving = false;
        DisableHitbox();
        attackCooldownTimer = attackCooldown; // 공격이 끝난 시점부터 쿨다운 시작
    }
}
