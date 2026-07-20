using UnityEngine;

// 부유산포균 (엘리트): 부유균과 동일한 체공/자폭 골격에 3발 산탄 + 자폭 시 부유균 스폰 추가
public class FloaterSpreaderGerm : FloaterGerm
{
    [Header("산탄")]
    public float spreadAngle = 30f; // 중심 기준 위아래 각도

    [Header("자폭 스폰")]
    public GameObject floaterGermPrefab;
    public int spawnCount = 3;
    public float spawnSpacing = 2f;

    public float spreadWindup = 0.8f; // 산탄 준비시간 (애니메이션 붙기 전 임시)

    protected override void TryStartAttack()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        if (animator != null) animator.SetTrigger("Attack");

        if (!HasAnimatorController)
        {
            Invoke(nameof(FireSpreadProjectiles), spreadWindup);
            Invoke(nameof(StopAttack), spreadWindup + fireRecovery);
        }
    }

    public override void StopAttack()
    {
        CancelInvoke(nameof(FireSpreadProjectiles));
        base.StopAttack();
    }

    // 애니메이션 이벤트 (지금은 위 Invoke 타이머가 대신 호출) — FloaterGerm.FireProjectile 대신 이걸 연결
    public void FireSpreadProjectiles()
    {
        if (IsConsumable || target == null || projectilePrefab == null) return;

        Vector2 baseDir = ((Vector2)(target.position - transform.position)).normalized;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        for (int i = -1; i <= 1; i++)
        {
            float angle = (baseAngle + i * spreadAngle) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpawnProjectile(dir);
        }
    }

    protected override void Detonate()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, selfDestructRadius, playerMask);
        foreach (var hit in hits)
        {
            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc != null) pc.TakeDamage(selfDestructDamage);
        }

        if (floaterGermPrefab != null && spawnCount > 0)
        {
            float angleStep = 360f / spawnCount;
            for (int i = 0; i < spawnCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnSpacing;
                Instantiate(floaterGermPrefab, (Vector2)transform.position + offset, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }
}
