using UnityEngine;

// 부유균: 느린 속도로 체공하며 원거리 공격으로 플레이어 동선을 방해, 사망 시 자폭
public class FloaterGerm : MonsterBase
{
    [Header("체공/교전")]
    public float engageRange = 8f; // 이 범위 이내면 추적을 멈추고 제자리에서 공격

    [Header("원거리 공격")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 6f;
    public float projectileDamage = 50f;
    public float fireWindup = 0.5f;   // 발사 준비시간 (애니메이션 붙기 전 임시)
    public float fireRecovery = 0.3f; // 발사 후 여유시간

    [Header("자폭")]
    public float selfDestructWindup = 1.5f;
    public float selfDestructRadius = 1.5f;
    public float selfDestructDamage = 100f;
    public bool showExplosionRange = true; // 이펙트 에셋 나오기 전까지 자폭 범위를 화면에 원으로 표시

    [Header("추적 준비")]
    public float chaseReadyDuration = 0.5f;

    private bool isChasePreparing;
    private float chaseReadyTimer;
    private bool wasTargetDetected;

    private bool isDetonating;

    private bool detectionPrepared;

    // 자폭형이라 언제든 섭취 불가 (사망해도 회복셀처럼 먹을 수 없음)
    public override bool IsConsumable => false;

    protected override void Awake()
    {
        base.Awake();
        avoidLedges = false; // 공중/벽면을 이동하므로 낭떠러지 감지 불필요
        if (rb != null) rb.gravityScale = 0f;
    }

    protected override void Update()
    {
        if (isDetonating)
        {
            UpdateDetection();

            if (animator != null)
            {
                animator.SetBool("move", false);
                animator.SetBool("ChaseReady", false);
            }

            return;
        }

        base.Update();

        UpdateChasePreparation();
        UpdateFloaterAnimator();
    }
    private void UpdateChasePreparation()
    {
        bool hasTarget = target != null;

        // 플레이어를 처음 감지한 순간, 거리와 무관하게 준비 시작
        if (hasTarget && !wasTargetDetected)
        {
            isChasePreparing = true;
            detectionPrepared = false;
            chaseReadyTimer = chaseReadyDuration;

            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        if (isChasePreparing)
        {
            chaseReadyTimer -= Time.deltaTime;

            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            if (chaseReadyTimer <= 0f)
            {
                isChasePreparing = false;
                detectionPrepared = true;
            }
        }

        // 플레이어를 완전히 잃으면 다음 감지를 위해 초기화
        if (!hasTarget)
        {
            isChasePreparing = false;
            detectionPrepared = false;
            chaseReadyTimer = 0f;
        }

        wasTargetDetected = hasTarget;
    }

    private void UpdateFloaterAnimator()
    {
        if (animator == null || rb == null)
            return;

        bool isMoving =
            !IsDead &&
            !isAttacking &&
            !isDetonating &&
            !isChasePreparing &&
            rb.linearVelocity.sqrMagnitude > 0.01f;

        animator.SetBool("move", isMoving);
        animator.SetBool("ChaseReady", isChasePreparing);
    }
    protected override void FaceDirection(float dirX)
    {
        if (Mathf.Abs(dirX) <= 0.01f)
            return;

        base.FaceDirection(dirX);

        if (spr != null)
            spr.flipX = dirX > 0f;
    }

    protected override void UpdateBehavior()
    {
        if (isAttacking)
            return;

        if (isChasePreparing || !detectionPrepared)
            return;

        if (
            target != null &&
            attackCooldownTimer <= 0f &&
            Vector2.Distance(transform.position, target.position) <= engageRange
        )
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
        SpawnProjectile(dir);
    }

    protected void SpawnProjectile(Vector2 direction)
    {
        Vector3 pos = firePoint != null ? firePoint.position : transform.position;
        GameObject proj = Instantiate(projectilePrefab, pos, Quaternion.identity);
        AcidProjectile ap = proj.GetComponent<AcidProjectile>();
        if (ap != null) ap.Init(direction, projectileSpeed, projectileDamage);
    }

    protected override void UpdateMovement()
    {
        if (rb == null || IsDead)
            return;

        // 넉백/공격후 딜레이 중엔 속도를 건드리지 않는다. 이 아래 분기들이 매 FixedUpdate마다
        // linearVelocity를 하드 세팅하므로, 이 가드가 없으면 ApplyKnockback이 넣은 속도가
        // 다음 물리 프레임에 그대로 지워져 넉백이 아예 안 걸린다.
        if (MovementSuppressed()) return;

        if (isChasePreparing || !detectionPrepared)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (
            target != null &&
            Vector2.Distance(transform.position, target.position) > engageRange
        )
        {
            Vector2 dir =
                ((Vector2)(target.position - transform.position)).normalized;

            rb.linearVelocity = dir * moveSpeed;
            FaceDirection(dir.x);
            return;
        }

        rb.linearVelocity = Vector2.zero;

    }

    protected override void OnDeath()
    {
        if (isDetonating) return;
        isDetonating = true;
        isAttacking = false;
        CancelInvoke(nameof(FireProjectile));
        CancelInvoke(nameof(StopAttack));
        DisableHitbox();

        // 자폭 준비 동안 폭발 범위를 미리 보여준다 (FloaterSpreaderGerm도 이 OnDeath를 상속하므로 함께 적용됨)
        if (showExplosionRange)
            gameObject.AddComponent<ExplosionRangeIndicator>().Begin(selfDestructRadius, selfDestructWindup);

        if (animator != null) animator.SetTrigger("SelfDestruct");
        Invoke(nameof(Detonate), selfDestructWindup);
    }

    protected virtual void Detonate()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, selfDestructRadius, playerMask);
        foreach (var hit in hits)
        {
            PlayerController pc = hit.GetComponent<PlayerController>();
            // 자폭은 공격 판정 — 대시 중이어도 맞는다
            if (pc != null) pc.TakeDamage(selfDestructDamage, default, 0f, DamageSource.Attack);
        }

        // 자폭형은 섭취가 불가능해서(IsConsumable = false) OnConsumed도 OnConsumableTimeout도
        // 영영 안 불린다. 셀을 주는 시점은 '터지는 순간'뿐이다.
        DropCells();

        Destroy(gameObject);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        // 자폭 폭발 범위 (Scene 뷰 미리보기)
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, selfDestructRadius);
    }
}
