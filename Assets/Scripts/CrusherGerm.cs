using UnityEngine;

// 분쇄균: 일자 형태의 지형에서 빠르게 압박하는 몬스터
// 순찰/추적은 MonsterBase 기본 구현 사용, 공격만 '돌진'으로 교체
public class CrusherGerm : MonsterBase
{
    [Header("돌진")]
    public float dashWindup = 0.5f;    // 준비 자세 시간 (애니메이션 이벤트로 대체 예정, 임시 타이머)
    public float dashSpeed = 6f;       // 초당 6타일(대략)
    public float dashMaxDuration = 3.5f; // 이 시간 안에 벽에 부딪히지 않으면 시전 중지
    public LayerMask wallMask; // 미사용 — 맵이 레이어로 안 나뉘어 있어 접촉 방향으로 벽을 판정
    public float wallKnockback = 0.5f; // 벽 충돌 시 넉백 거리
    public float wallStunDuration = 2.5f;

    private float dashDir;
    private float dashTimer;
    private bool isDashing; // 준비 자세(0.5초) 이후 실제 돌진 구간
    private bool isStunned;
    private float stunTimer;

    protected override void Update()
    {
        base.Update();

        if (animator == null)
            return;

        bool isMoving =
            !IsDead &&
            !isAttacking &&
            !isStunned &&
            rb != null &&
            Mathf.Abs(rb.linearVelocity.x) > 0.05f;

        animator.SetBool("move", isMoving);
        animator.SetBool("IsDead", IsDead);
        animator.SetBool("isDashing", isDashing);
        animator.SetBool("isStunned", isStunned);
    }
    protected override void UpdateBehavior()
    {
        if (isAttacking || isStunned) return;

        // 분쇄균은 attackRange가 아니라 탐지 범위 내 조우 시 바로 돌진 시전
        if (target == null || attackCooldownTimer > 0f) return;

        // 낭떠러지 쪽으로는 돌진을 시작하지 않는다.
        // (시작만 하고 첫 프레임에 중단되면 준비 자세만 반복해서 잡는 것처럼 보임)
        float dir = Mathf.Sign(target.position.x - transform.position.x);
        if (!HasGroundAhead(dir)) return;

        TryStartAttack();
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
        if (MovementSuppressed()) return;

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
            if (dashTimer > dashMaxDuration || !HasGroundAhead(dashDir))
            {
                // 시간 초과했거나 발밑이 끊기면 돌진 중단 (맵 밖으로 뛰어내리지 않도록)
                EndDash();
                return;
            }

            rb.linearVelocity = new Vector2(dashDir * dashSpeed, rb.linearVelocity.y);
            return;
        }

        base.UpdateMovement();
    }

    // 돌진 방향을 정면으로 막아선 수직면이면 '벽에 박았다'로 본다 (레이어 대신 접촉 방향으로 판정).
    // 바닥(법선이 위)이나 플레이어/다른 몬스터는 제외.
    bool IsWallHit(Collision2D collision)
    {
        if (collision.collider == null)
            return false;

        if (collision.collider.GetComponentInParent<PlayerController>() != null)
            return false;

        if (collision.collider.GetComponentInParent<MonsterBase>() != null)
            return false;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 normal = contact.normal;

            // 바닥이나 경사면 제외
            // 수직벽에 가까운 법선만 인정
            if (Mathf.Abs(normal.x) < 0.9f)
                continue;

            if (Mathf.Abs(normal.y) > 0.2f)
                continue;

            // 돌진 방향 정면에 있는 벽인지 확인
            bool isFrontWall =
                dashDir > 0f
                    ? normal.x < -0.9f
                    : normal.x > 0.9f;

            if (isFrontWall)
                return true;
        }

        return false;
    }

    void EndDash()
    {
        CancelInvoke(nameof(StartDash));
        isAttacking = false;
        isDashing = false;
        ShowTelegraph(false);
        DisableHitbox();
        actionPauseTimer = postAttackPause; // 돌진 종료 후 잠깐 멈췄다가 움직이도록
    }

    public override void StopAttack()
    {
        EndDash();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleWallCollision(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryHandleWallCollision(collision);
    }

    private void TryHandleWallCollision(Collision2D collision)
    {
        if (!isDashing)
            return;

        if (!IsWallHit(collision))
            return;

        EndDash();

        isStunned = true;
        stunTimer = wallStunDuration;

        rb.linearVelocity = new Vector2(
            -dashDir * wallKnockback,
            rb.linearVelocity.y
        );
    }
    protected override void FaceDirection(float dirX)
    {
        if (Mathf.Abs(dirX) <= 0.01f)
            return;

        base.FaceDirection(dirX);

        // 원본 이미지가 왼쪽을 바라보는 기준
        if (spr != null)
            spr.flipX = dirX > 0f;
    }

}
