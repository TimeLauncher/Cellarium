using UnityEngine;

// 분쇄균: 일자 형태의 지형에서 빠르게 압박하는 몬스터
// 순찰/추적은 MonsterBase 기본 구현 사용, 공격만 '돌진'으로 교체
public class CrusherGerm : MonsterBase
{
    [Header("돌진")]
    public float dashWindup = 0.5f;    // 준비 자세 시간 (애니메이션 이벤트로 대체 예정, 임시 타이머)
    public float dashSpeed = 6f;       // 초당 6타일(대략)
    public float dashMaxDuration = 3.5f; // 이 시간 안에 벽에 부딪히지 않으면 시전 중지
    public LayerMask wallMask;
    public float wallKnockback = 0.5f; // 벽 충돌 시 넉백 거리
    public float wallStunDuration = 2.5f;

    private float dashDir;
    private float dashTimer;
    private bool isDashing; // 준비 자세(0.5초) 이후 실제 돌진 구간
    private bool isStunned;
    private float stunTimer;

    protected override void UpdateBehavior()
    {
        if (isAttacking || isStunned) return;

        // 분쇄균은 attackRange가 아니라 탐지 범위 내 조우 시 바로 돌진 시전
        if (target != null && attackCooldownTimer <= 0f)
        {
            TryStartAttack();
        }
    }

    protected override void TryStartAttack()
    {
        dashDir = Mathf.Sign(target.position.x - transform.position.x);
        FaceDirection(dashDir);
        isAttacking = true;
        isDashing = false;
        attackCooldownTimer = attackCooldown;
        if (animator != null) animator.SetTrigger("Attack");

        if (!HasAnimatorController)
            Invoke(nameof(StartDash), dashWindup); // 임시: 애니메이션 이벤트 대신 타이머로 준비 자세 종료 처리
    }

    // 애니메이션 이벤트 (지금은 위 Invoke 타이머가 대신 호출)
    public void StartDash()
    {
        if (!isAttacking || IsDead) return;
        isDashing = true;
        dashTimer = 0f;
        EnableHitbox();
    }

    protected override void UpdateMovement()
    {
        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            if (stunTimer <= 0f) isStunned = false;
            return;
        }

        if (isAttacking)
        {
            if (!isDashing)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // 준비 자세 중 정지
                return;
            }

            dashTimer += Time.fixedDeltaTime;
            if (dashTimer > dashMaxDuration)
            {
                EndDash();
                return;
            }

            rb.linearVelocity = new Vector2(dashDir * dashSpeed, rb.linearVelocity.y);
            return;
        }

        base.UpdateMovement();
    }

    void EndDash()
    {
        CancelInvoke(nameof(StartDash));
        isAttacking = false;
        isDashing = false;
        DisableHitbox();
    }

    public override void StopAttack()
    {
        EndDash();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDashing) return;
        if (((1 << collision.gameObject.layer) & wallMask) == 0) return;

        EndDash();
        isStunned = true;
        stunTimer = wallStunDuration;
        rb.linearVelocity = new Vector2(-dashDir * wallKnockback, rb.linearVelocity.y);
    }
}
