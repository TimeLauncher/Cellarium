using UnityEngine;

// 거미균 (엘리트): 벽/천장에 붙어 이동 가능, 거리에 따라 '베기'(근접) 또는 '덮치기'(점프 기습) 시전
// surfaceNormal이 위쪽(Vector2.up)이 아니면 벽/천장에 붙어있는 것으로 간주 — 이 경우 덮치기 우선
public class SpiderGerm : MonsterBase
{
    [Header("벽 타기")]
    public Vector2 surfaceNormal = Vector2.up;
    public float stickRayDistance = 1f;
    public LayerMask surfaceMask;

    [Header("베기 (근접)")]
    public float slashRange = 2.5f;
    public float slashCooldown = 3.5f;
    public float slashDamage = 50f;
    public float slashWindup = 1f;        // 준비시간 (애니메이션 붙기 전 임시)
    public float slashActiveTime = 0.2f;  // 판정 유지 시간

    [Header("덮치기 (점프 기습)")]
    public float pounceMinRange = 3f;
    public float pounceMaxRange = 6f;
    public float pounceWindup = 1f;
    public float pounceAirTime = 0.5f; // 점프 높이와 무관하게 고정
    public float pounceCooldown = 6f;
    public float pounceDamage = 100f;
    public float pounceLandRadius = 1.5f;
    public float pounceHeight = 2f; // 시각적 포물선 높이 (정확한 값은 모션 작업 시 조정)

    protected Vector2 Tangent => new Vector2(-surfaceNormal.y, surfaceNormal.x);

    private float slashCooldownTimer;
    private float pounceCooldownTimer;
    private bool isPouncing;
    private Vector2 pounceStart;
    private Vector2 pounceLandTarget;
    private float pounceTimer;

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

    void AlignToSurface()
    {
        float angle = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    protected override void Update()
    {
        base.Update();
        if (slashCooldownTimer > 0f) slashCooldownTimer -= Time.deltaTime;
        if (pounceCooldownTimer > 0f) pounceCooldownTimer -= Time.deltaTime;
    }

    protected override void UpdateBehavior()
    {
        if (isAttacking || target == null) return;

        float dist = Vector2.Distance(transform.position, target.position);
        bool onWallOrCeiling = !Mathf.Approximately(surfaceNormal.y, 1f);
        bool canSlash = dist <= slashRange && slashCooldownTimer <= 0f;
        bool canPounce = dist >= pounceMinRange && dist <= pounceMaxRange && pounceCooldownTimer <= 0f;

        // 벽/천장에 붙어있을 경우 덮치기를 베기보다 우선 시전
        if (onWallOrCeiling && canPounce)
            StartPounce();
        else if (canSlash)
            StartSlash();
        else if (canPounce)
            StartPounce();
    }

    void StartSlash()
    {
        isAttacking = true;
        slashCooldownTimer = slashCooldown;
        FaceDirection(target.position.x - transform.position.x);
        if (animator != null) animator.SetTrigger("Slash");

        if (!HasAnimatorController)
        {
            Invoke(nameof(EnableHitbox), slashWindup);
            Invoke(nameof(StopAttack), slashWindup + slashActiveTime);
        }
    }

    void StartPounce()
    {
        isAttacking = true;
        isPouncing = true;
        pounceCooldownTimer = pounceCooldown;
        pounceStart = transform.position;
        pounceLandTarget = target.position;
        pounceTimer = 0f;
        if (animator != null) animator.SetTrigger("Pounce");
        Invoke(nameof(BeginPounceJump), pounceWindup);
    }

    void BeginPounceJump()
    {
        if (!isPouncing) return;
        pounceTimer = 0f;
    }

    protected override void UpdateMovement()
    {
        if (isPouncing)
        {
            UpdatePounceJump();
            return;
        }

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

    void UpdatePounceJump()
    {
        pounceTimer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(pounceTimer / pounceAirTime);
        Vector2 flatPos = Vector2.Lerp(pounceStart, pounceLandTarget, t);
        float arc = Mathf.Sin(t * Mathf.PI) * pounceHeight;
        transform.position = flatPos + Vector2.up * arc;

        if (t >= 1f) LandPounce();
    }

    void LandPounce()
    {
        isPouncing = false;
        surfaceNormal = Vector2.up; // 착지 후 바닥 기준으로 전환
        AlignToSurface();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pounceLandRadius, playerMask);
        foreach (var hit in hits)
        {
            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc != null)
            {
                Vector2 knockDir = ((Vector2)(hit.transform.position - transform.position)).normalized;
                pc.TakeDamage(pounceDamage, knockDir * knockbackForce, stunDuration);
            }
        }

        StopAttack();
    }

    void PatrolAlongSurface()
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

    void StickToSurface()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, -surfaceNormal, stickRayDistance, surfaceMask);
        if (hit.collider != null)
        {
            Vector2 targetPos = hit.point + surfaceNormal * 0.05f;
            transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime * 2f);
        }
    }

    public override void StopAttack()
    {
        CancelInvoke(nameof(BeginPounceJump));
        CancelInvoke(nameof(EnableHitbox));
        CancelInvoke(nameof(StopAttack));
        isAttacking = false;
        isPouncing = false;
        DisableHitbox();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAttacking || isPouncing) return; // 베기만 일반 히트박스 트리거 사용 (덮치기는 착지 시 별도 판정)
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            Vector2 knockDir = ((Vector2)(other.transform.position - transform.position)).normalized;
            pc.TakeDamage(slashDamage, knockDir * knockbackForce, stunDuration);
            DisableHitbox();
        }
    }
}
