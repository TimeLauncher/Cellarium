using UnityEngine;

// 일반비행균: 느린 속도로 체공하며 조우 시 추적, 근접 시 공중 돌진
public class FlyingGerm : MonsterBase
{
    // 추적 속도(chaseSpeed)는 MonsterBase로 옮겼다 (전 몬스터 공통 항목). 비행균은 체크와 상관없이 항상 그 값으로 추적한다.

    // 기획서 A12~A13 (3) 비행균: 계속 다가와 몸으로 부딪히지 않고 PC와 거리를 둔다
    [Header("거리 유지")]
    [Tooltip("켜면 PC에게 끝까지 붙지 않고 아래 거리를 유지하려 한다. 끄면 예전처럼 PC 위치로 곧장 쫓아간다")]
    public bool keepDistance = true;
    [Tooltip("n: PC와 이 거리(타일)까지만 다가온다. 이보다 가까워지면 오히려 바깥으로 물러난다. " +
             "돌진 사거리(Dive Attack Range)보다 크면 돌진을 못 하니 같거나 작게 둘 것")]
    [Min(0.5f)] public float keepDistanceRange = 3f;
    [Tooltip("(실험) 켜면 n~a 사이에 있을 때 PC 쪽으로 오지 않고 그 띠 안을 돌아다닌다. 끄면 n 거리에서 멈춰 떠 있는다")]
    public bool wanderInBand = true;
    [Tooltip("a: 배회하는 띠의 바깥 경계(타일). 이보다 멀면 다가온다")]
    [Min(0.5f)] public float wanderOuterRange = 6f;
    [Tooltip("배회 중 새 목적지를 고르는 간격(초)")]
    [Min(0.1f)] public float wanderRetargetInterval = 1.2f;
    [Tooltip("배회할 때 속도. 0이면 Move Speed")]
    [Min(0f)] public float wanderSpeed = 0f;

    [Header("공중 돌진")]
    public float diveAttackRange = 3f; // 이 거리 이내면 돌진 시전
    public float diveSpeed = 8f;
    public float diveWindup = 0.3f;
    public float diveMaxDuration = 1f; // 모션 미완성 대비 안전장치 (추후 애니메이션 이벤트로 대체 가능)

    // Fix 문서 '일반비행균의 돌진 모션을 돌진 방향으로 재생되도록 수정'.
    // 좌우 반전(FaceDirection)만으로는 위/아래 대각선 돌진이 표현되지 않고, 방향별 클립도 아직 없다.
    // 클립이 나오기 전까지는 스프라이트를 돌진 방향으로 기울여서 방향을 보여준다 (PC 대시와 같은 방식).
    [Header("돌진 모션 방향")]
    [Tooltip("돌진하는 동안 스프라이트를 돌진 방향으로 기울인다. 방향별 돌진 클립이 생기면 꺼도 된다")]
    public bool rotateOnDive = true;
    [Range(0f, 90f)]
    [Tooltip("기울일 수 있는 최대 각도. 90이면 돌진 방향을 그대로 본다")]
    public float diveRotationMaxAngle = 70f;
    [Tooltip("스프라이트가 든 자식 오브젝트를 넣으면 그것만 돌린다. 비우면 본체를 돌린다(콜라이더도 같이 돈다)")]
    public Transform diveVisualRoot;

    [Header("바라보는 방향")]
    [Tooltip("PC와의 가로 거리가 이보다 가까우면 좌우를 바꾸지 않는다. " +
             "PC 바로 위/아래에서 x가 조금만 흔들려도 좌우가 계속 뒤집혀 엉뚱한 쪽을 보며 날아가던 문제 방지")]
    [Min(0f)] public float faceDeadZone = 0.3f;

    private Vector2 diveDir;
    private bool isDiving;

    protected override void Update()
    {
        base.Update();

        if (animator == null || rb == null)
            return;

        bool isMoving =
            !IsDead &&
            !isAttacking &&
            rb.linearVelocity.sqrMagnitude > 0.01f;

        animator.SetBool("move", isMoving);
        animator.SetBool("isDiving", isDiving);
    }
    protected override void FaceDirection(float dirX)
    {
        if (Mathf.Abs(dirX) <= 0.01f)
            return;

        base.FaceDirection(dirX);

        if (spr != null)
            spr.flipX = dirX > 0f;
    }

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
        // 거의 수직 돌진이면 좌우는 지금 보던 쪽을 유지한다 (아주 작은 x로 좌우가 뒤집히지 않게)
        if (Mathf.Abs(target.position.x - transform.position.x) > faceDeadZone * 0.5f)
            FaceDirection(diveDir.x); // 돌진 모션이 돌진 방향으로 재생되도록 (UpdateMovement는 공격 중 방향을 안 잡는다)
        ApplyDiveRotation(diveDir); // 위/아래 대각선까지 방향을 맞춘다
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
            Vector2 toTarget = target.position - transform.position;
            float dist = toTarget.magnitude;
            float dx = toTarget.x;

            if (keepDistance && dist < Mathf.Max(wanderInBand ? wanderOuterRange : keepDistanceRange, keepDistanceRange))
            {
                ChaseKeepingDistance(toTarget, dist);
                if (Mathf.Abs(dx) > faceDeadZone) FaceDirection(dx);
                return;
            }
            hasWanderPoint = false;

            // 추적 중 PC가 반대편으로 넘어가 이동 방향이 급전환되면 잠깐 멈췄다가 따라간다 (QA: 0.5초 내외)
            // 바로 위/아래에서 x가 살짝 흔들리는 것까지 '반대편으로 넘어감'으로 치면 멈칫+뒤돌기를 반복한다
            int hdir = dx > faceDeadZone ? 1 : (dx < -faceDeadZone ? -1 : 0);
            if (UpdateTurnPause(hdir))
            {
                rb.linearVelocity = Vector2.zero;
                if (Mathf.Abs(dx) > faceDeadZone) FaceDirection(dx);
                return;
            }

            rb.linearVelocity = toTarget.normalized * chaseSpeed;
            if (Mathf.Abs(dx) > faceDeadZone) FaceDirection(dx);
        }
        else
        {
            lastChaseDir = 0;
            hasWanderPoint = false;
            Patrol();
        }
    }

    bool hasWanderPoint;
    Vector2 wanderOffset;   // PC 기준 배회 목적지 (PC가 움직이면 같이 따라간다)
    float wanderTimer;

    // n 안이면 바깥으로 물러나고, n~a 사이면 띠 안을 돌아다니거나(배회) 그 자리에 떠 있는다
    void ChaseKeepingDistance(Vector2 toTarget, float dist)
    {
        if (dist < keepDistanceRange)
        {
            hasWanderPoint = false;
            Vector2 away = dist > 0.01f ? -toTarget / dist : Vector2.up;
            rb.linearVelocity = away * chaseSpeed;
            return;
        }

        if (!wanderInBand)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        wanderTimer -= Time.fixedDeltaTime;
        Vector2 targetPos = target.position;
        Vector2 dest = targetPos + wanderOffset;
        if (!hasWanderPoint || wanderTimer <= 0f || Vector2.Distance(transform.position, dest) < 0.3f)
        {
            // 띠 안의 무작위 지점. 지금 있는 쪽 반원에서 고른다 — PC를 가로질러 반대편으로 넘어가면 부딪힌다
            Vector2 side = -toTarget.normalized;
            float angle = Random.Range(-75f, 75f);
            Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * side);
            float outer = Mathf.Max(keepDistanceRange, wanderOuterRange);
            wanderOffset = dir * Random.Range(keepDistanceRange, outer);
            wanderTimer = wanderRetargetInterval;
            hasWanderPoint = true;
            dest = targetPos + wanderOffset;
        }

        Vector2 move = dest - (Vector2)transform.position;
        float speed = wanderSpeed > 0f ? wanderSpeed : moveSpeed;
        rb.linearVelocity = move.magnitude > 0.05f ? move.normalized * speed : Vector2.zero;
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
        if (Mathf.Abs(toOrigin.x) > faceDeadZone) FaceDirection(toOrigin.x);
    }

    // 돌진 방향으로 기울이기 (좌우는 FaceDirection이 이미 처리했으므로 여기서는 위/아래 기울기만 만든다)
    //
    // ★ 기타 메모 '돌진 모션 방향이 이상하게 출력됨':
    //   예전엔 PC 대시와 똑같이 'flipX면 각도 반전'으로 계산했다. 그런데 비행균 그림은 원래 왼쪽을 보고 있어서
    //   flipX의 의미가 PC와 반대다(위 FaceDirection 참고: 오른쪽을 볼 때 flipX = true).
    //   그 결과 위로 돌진하면 머리가 아래로, 아래로 돌진하면 위로 기울었다.
    //   그림 방향과 상관없이 '지금 바라보는 쪽(facingDir)'으로 판단한다 — 오른쪽을 보면 반시계(+)가 머리 위쪽.
    void ApplyDiveRotation(Vector2 dir)
    {
        if (!rotateOnDive) return;
        Transform pivot = diveVisualRoot != null ? diveVisualRoot : transform;

        float tilt = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;
        tilt = Mathf.Clamp(tilt, -diveRotationMaxAngle, diveRotationMaxAngle);
        if (facingDir < 0) tilt = -tilt;

        pivot.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    void ClearDiveRotation()
    {
        Transform pivot = diveVisualRoot != null ? diveVisualRoot : transform;
        pivot.localRotation = Quaternion.identity;
    }

    public override void StopAttack()
    {
        ClearDiveRotation();
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
