using UnityEngine;

// 거미균 (엘리트): 벽/천장에 붙어 이동 가능, 거리에 따라 '베기'(근접) 또는 '덮치기'(점프 기습) 시전
// surfaceNormal이 위쪽(Vector2.up)이 아니면 벽/천장에 붙어있는 것으로 간주 — 이 경우 덮치기 우선
public class SpiderGerm : MonsterBase
{
    [Header("벽 타기")]
    public Vector2 surfaceNormal = Vector2.up; // 시작 방향. 실제로는 닿은 면에 맞춰 자동 갱신됨
    public float stickRayDistance = 1f;
    public float surfaceSearchDistance = 3f;   // 붙을 면을 잃었을 때 주변을 훑는 거리
    public float surfaceOffset = 0.2f;         // 표면에서 띄울 간격 (몸이 박히면 늘릴 것)
    public LayerMask surfaceMask;              // 미사용 — 맵이 레이어로 안 나뉘어 있어 CastSurface로 판정

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
    private bool isAttachedToSurface;
    private bool hasSurfaceNearby = true;
    private Vector2 pounceStart;
    private Vector2 pounceLandTarget;
    private float pounceTimer;

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
        ShowTelegraph(true); // 베는 범위를 준비 동작 동안 미리 표시
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

        // 붙을 면을 아예 못 찾으면(허공에 착지한 경우 등) 중력으로 떨어뜨린다.
        // 안 그러면 gravityScale이 0이라 공중에 그대로 멈춰버림
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

    // 현재 붙어 있는 면에 몸을 붙인다. 면을 잃으면 주변을 훑어 새 면을 찾아 그쪽으로 이동한다.
    void StickToSurface()
    {
        // 바로 아래(현재 법선 반대)에 면이 없으면 주변 탐색 결과를 그대로 사용한다.
        // 탐색 결과를 stickRayDistance로 다시 검사하면 멀리 있는 벽에는 영원히 못 붙는다.
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
    bool TryFindSurface(out RaycastHit2D best)
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

    public override void StopAttack()
    {
        CancelInvoke(nameof(BeginPounceJump));
        CancelInvoke(nameof(EnableHitbox));
        CancelInvoke(nameof(StopAttack));
        isAttacking = false;
        isPouncing = false;
        ShowTelegraph(false);
        DisableHitbox();
    }

    // 히트박스로 들어가는 공격은 베기뿐 (덮치기는 착지 시 OverlapCircle로 별도 판정)
    protected override float HitboxDamage => slashDamage;
}
