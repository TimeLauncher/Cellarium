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

    private bool isDetonating;

    protected override void Awake()
    {
        base.Awake();
        if (rb != null) rb.gravityScale = 0f;
    }

    protected override void Update()
    {
        if (isDetonating)
        {
            UpdateDetection(); // 위치 갱신 정도만, 그 외 행동/판정 없음
            return;
        }
        base.Update();
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
        // isDetonating일 때는 IsConsumable이 true라 MonsterBase.FixedUpdate가 이 메서드를 호출하지 않음
        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (target != null && Vector2.Distance(transform.position, target.position) > engageRange)
        {
            Vector2 dir = ((Vector2)(target.position - transform.position)).normalized;
            rb.linearVelocity = dir * moveSpeed;
            FaceDirection(dir.x);
            return;
        }

        rb.linearVelocity = Vector2.zero; // 교전 범위 내에서는 체공 유지
    }

    protected override void OnDeath()
    {
        if (isDetonating) return;
        isDetonating = true;
        isAttacking = false;
        CancelInvoke(nameof(FireProjectile));
        CancelInvoke(nameof(StopAttack));
        DisableHitbox();
        if (animator != null) animator.SetTrigger("SelfDestruct");
        Invoke(nameof(Detonate), selfDestructWindup);
    }

    protected virtual void Detonate()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, selfDestructRadius, playerMask);
        foreach (var hit in hits)
        {
            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc != null) pc.TakeDamage(selfDestructDamage);
        }
        Destroy(gameObject);
    }
}
