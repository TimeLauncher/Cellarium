using System.Collections;
using UnityEngine;

// 거미균 (엘리트): 벽/천장에 붙어 이동 가능, 거리에 따라 '베기'(근접) 또는 '덮치기'(점프 기습) 시전
// surfaceNormal이 위쪽(Vector2.up)이 아니면 벽/천장에 붙어있는 것으로 간주 — 이 경우 덮치기 우선
//
// 기획서 A12~A13 (1) 거미균: 베기·덮치기를 침식 거미균과 같은 방식으로 바꿨다.
//   - 베기: 앞쪽 박스 범위(크기·위치 조절), 준비시간 → 판정 → 후딜레이
//   - 덮치기: 준비시간 동안 제자리 → 시전 순간 PC가 있던 자리 '아래 바닥'으로 뛰어 착지 → 후딜레이
//     (예전엔 준비시간과 상관없이 즉시 뛰었고, PC가 공중에 있으면 허공·벽에 멈춰 붙었다)
//   - 쿨타임은 패턴이 끝난 뒤부터 돈다
// 침식 거미균(ErosionSpiderGerm)은 이 클래스를 상속해 패턴을 더 얹는다.
//
// 애니메이션이 아직 없어서 공격 범위를 반투명 판으로 보여준다 (준비 = 보라, 판정 = 빨강).
// 애니메이터를 붙이면 패턴 시작 때 Slash / Pounce 트리거를 건다 (파라미터가 있을 때만).
public class SpiderGerm : MonsterBase
{
    [Header("벽 타기")]
    public Vector2 surfaceNormal = Vector2.up; // 시작 방향. 실제로는 닿은 면에 맞춰 자동 갱신됨
    public float stickRayDistance = 1f;
    public float surfaceSearchDistance = 3f;   // 붙을 면을 잃었을 때 주변을 훑는 거리
    [Tooltip("몸(콜라이더) 바깥면과 붙은 면 사이 간격")]
    public float surfaceOffset = 0.2f;
    public LayerMask surfaceMask;              // 미사용 — 맵이 레이어로 안 나뉘어 있어 CastSurface로 판정

    // ★ 예전엔 '몸 중심'을 면에서 surfaceOffset(0.2)만큼 띄우려 했다. 몸 콜라이더가 그보다 크면(크기 1 x 스케일 2 = 반 높이 1)
    //   물리가 몸을 밀어내서 목표 위치에 영영 못 닿고 → '아직 안 붙음'으로 판정돼 순찰·추적 이동을 아예 안 했다.
    //   ('순찰 작동 X 고정된 위치에서 가만히 대기'의 원인) 이제 몸 반 두께를 더해서 띄운다.
    [Tooltip("켜면 몸 콜라이더의 반 두께만큼 더 띄워 면에 붙인다. 끄면 예전처럼 몸 중심을 Surface Offset만큼만 띄운다")]
    public bool fitBodyToSurface = true;

    [Header("베기 (근접)")]
    public float slashRange = 2.5f;
    public float slashCooldown = 2.5f;
    public float slashDamage = 50f;
    [Tooltip("준비시간 (범위가 보라색으로 보이는 시간)")]
    public float slashWindup = 1f;
    [Tooltip("판정 유지 시간")]
    public float slashActiveTime = 0.2f;
    [Tooltip("후딜레이")]
    public float slashRecovery = 1f;
    [Tooltip("공격 범위 (가로, 세로)")]
    public Vector2 slashBoxSize = new Vector2(3f, 3f);
    [Tooltip("범위 중심 위치 (앞쪽 거리, 위쪽 높이). 앞쪽은 바라보는 방향으로 자동 반전")]
    public Vector2 slashBoxOffset = new Vector2(2f, 1f);

    [Header("덮치기 (점프 기습)")]
    public float pounceMinRange = 3f;
    public float pounceMaxRange = 6f;
    [Tooltip("준비시간 (착지 지점이 보라색으로 보이는 시간)")]
    public float pounceWindup = 1f;
    [Tooltip("체공 시간 (점프 높이와 무관하게 고정)")]
    public float pounceAirTime = 0.5f;
    public float pounceCooldown = 6f;
    public float pounceDamage = 50f;
    [Tooltip("착지 지점 공격 범위 (반지름)")]
    public float pounceLandRadius = 1f;
    [Tooltip("점프 높이 (최소값)")]
    public float pounceHeight = 2f;
    [Tooltip("점프 높이 최대값. Pounce Height보다 작으면 항상 Pounce Height")]
    public float pounceHeightMax = 0f;
    [Tooltip("후딜레이")]
    public float pounceRecovery = 0.4f;

    [Header("공격 범위 표시")]
    [Tooltip("공격 범위를 반투명 판으로 표시 (애니메이션·이펙트가 나오기 전 임시)")]
    public bool showAttackAreas = true;

    protected Vector2 Tangent => new Vector2(-surfaceNormal.y, surfaceNormal.x);

    protected enum MoveMode { Crawl, Airborne }
    protected MoveMode moveMode = MoveMode.Crawl;
    protected Coroutine patternRoutine;
    protected Vector2 lastTargetPos;

    protected float slashTimer;
    protected float pounceTimerCd;

    private bool isAttachedToSurface;
    private bool hasSurfaceNearby = true;
    protected float bodyHalfThickness; // 몸 중심에서 발바닥(붙는 면 쪽)까지 거리

    protected SpriteRenderer telegraphArea;
    protected SpriteRenderer secondaryArea;

    // ★ 노랑은 쓰지 않는다 — 죽은 몬스터(섭취 가능)가 노랗게 변해서, 예고 판이 몸에 겹치면 '먹을 수 있는 상태'로 보인다
    protected static readonly Color TelegraphColor = new Color(0.75f, 0.3f, 1f, 0.2f);
    protected static readonly Color ActiveColor = new Color(1f, 0.25f, 0.15f, 0.55f);

    // 컴포넌트를 처음 붙였을 때 기획서 수치로 채운다 (A12~A13 수치 조정 값 메모)
    protected virtual void Reset()
    {
        maxHp = 300f;
        slashCooldown = 2.5f;
        pounceDamage = 50f;
        pounceLandRadius = 1f;
    }

    protected override void Awake()
    {
        base.Awake();
        avoidLedges = false; // 공중/벽면을 이동하므로 낭떠러지 감지 불필요
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
        bodyHalfThickness = MeasureBodyHalfThickness();
        AlignToSurface();
    }

    // 몸 콜라이더의 '아래쪽 반 두께'(로컬 기준). 회전과 상관없이 붙는 면 쪽 두께로 쓴다
    float MeasureBodyHalfThickness()
    {
        if (!fitBodyToSurface || bodyCollider == null) return 0f;
        float sy = Mathf.Abs(transform.lossyScale.y);
        switch (bodyCollider)
        {
            case BoxCollider2D box: return Mathf.Max(0f, (box.size.y * 0.5f - box.offset.y) * sy);
            case CapsuleCollider2D cap: return Mathf.Max(0f, (cap.size.y * 0.5f - cap.offset.y) * sy);
            case CircleCollider2D cir: return Mathf.Max(0f, cir.radius * Mathf.Abs(transform.lossyScale.x) - cir.offset.y * sy);
            default: return Mathf.Max(0f, transform.position.y - bodyCollider.bounds.min.y);
        }
    }

    // 면에서 몸 중심까지 띄울 거리
    protected float SurfaceGap => surfaceOffset + bodyHalfThickness;

    protected void AlignToSurface()
    {
        float angle = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        if (slashTimer > 0f) slashTimer -= dt;
        if (pounceTimerCd > 0f) pounceTimerCd -= dt;
        if (target != null) lastTargetPos = target.position;
    }
    // ── 행동 선택 ────────────────────────────────────────────────

    protected override void UpdateBehavior()
    {
        if (isAttacking || patternRoutine != null || target == null) return;

        float dist = Vector2.Distance(transform.position, target.position);
        bool onWallOrCeiling = surfaceNormal.y < 0.7f;
        bool canSlash = dist <= slashRange && slashTimer <= 0f;
        bool canPounce = dist >= pounceMinRange && dist <= pounceMaxRange && pounceTimerCd <= 0f;

        // 벽/천장에 붙어있을 경우 덮치기를 베기보다 우선 시전
        if (onWallOrCeiling && canPounce)
            StartPattern(PouncePattern(pounceWindup, pounceRecovery, true));
        else if (canSlash)
            StartPattern(SlashPattern());
        else if (canPounce)
            StartPattern(PouncePattern(pounceWindup, pounceRecovery, true));
    }

    protected void StartPattern(IEnumerator body)
    {
        patternRoutine = StartCoroutine(RunPattern(body));
    }

    IEnumerator RunPattern(IEnumerator body)
    {
        isAttacking = true;
        yield return body;
        isAttacking = false;
        patternRoutine = null;
    }

    // ── 베기: 앞쪽 박스 ──────────────────────────────────────────

    protected IEnumerator SlashPattern()
    {
        FaceTarget();
        Trigger("Slash");

        Vector2 center = LocalToWorld(new Vector2(slashBoxOffset.x * facingDir, slashBoxOffset.y));
        float angle = transform.eulerAngles.z;
        ShowBox(ref telegraphArea, center, slashBoxSize, angle, TelegraphColor);
        yield return new WaitForSeconds(slashWindup);

        ShowBox(ref telegraphArea, center, slashBoxSize, angle, ActiveColor);
        float t = 0f;
        bool hit = false;
        while (t < Mathf.Max(slashActiveTime, 0.05f))
        {
            if (!hit) hit = HitBox(center, slashBoxSize, angle, slashDamage);
            t += Time.deltaTime;
            yield return null;
        }
        Hide(telegraphArea);

        slashTimer = slashCooldown;
        yield return new WaitForSeconds(slashRecovery);
    }

    // ── 덮치기: 준비 → 점프 → 시전 순간 PC가 있던 자리 아래 바닥으로 내려찍기 ─────────

    protected IEnumerator PouncePattern(float windup, float recovery, bool useCooldown)
    {
        FaceTarget();
        Trigger("Pounce");

        // 시전 순간의 PC 위치 (공중이면 그 아래 바닥) — 반드시 바닥에 착지한다
        Vector2 land = GroundBelow(TargetPos()) + Vector2.up * SurfaceGap;
        ShowCircle(ref telegraphArea, land, pounceLandRadius, TelegraphColor);
        yield return new WaitForSeconds(windup);

        // 공중에선 몸을 똑바로 세운다
        surfaceNormal = Vector2.up;
        AlignToSurface();
        moveMode = MoveMode.Airborne;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; }

        Vector2 start = transform.position;
        float height = Random.Range(pounceHeight, Mathf.Max(pounceHeight, pounceHeightMax));
        float airTime = Mathf.Max(0.05f, pounceAirTime);
        float t = 0f;
        while (t < airTime)
        {
            t += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(t / airTime);
            transform.position = Vector2.Lerp(start, land, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * height);
            yield return new WaitForFixedUpdate();
        }
        transform.position = land;
        moveMode = MoveMode.Crawl;

        ShowCircle(ref telegraphArea, land, pounceLandRadius, ActiveColor);
        HitCircle(land, pounceLandRadius, pounceDamage);
        yield return new WaitForSeconds(0.12f);
        Hide(telegraphArea);

        if (useCooldown) pounceTimerCd = pounceCooldown;
        yield return new WaitForSeconds(recovery);
    }

    // ── 이동 ────────────────────────────────────────────────────

    protected override void UpdateMovement()
    {
        // 점프·벽면 이동 중엔 패턴 코루틴이 위치를 직접 옮긴다
        if (moveMode == MoveMode.Airborne)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
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
        // 공격·넉백·공격 후딜·PC 인지 중에도 기어 다니지 않는다
        if (!isAttachedToSurface || isAttacking || HoldPosition || knockbackTimer > 0f || actionPauseTimer > 0f || IsNoticing)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (target != null)
        {
            float along = Vector2.Dot(target.position - transform.position, Tangent);
            int dirAlong = along > 0.05f ? 1 : (along < -0.05f ? -1 : 0);
            if (dirAlong == 0 || UpdateTurnPause(dirAlong))
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            rb.linearVelocity = Tangent * dirAlong * ChaseMoveSpeed;
            FaceAlong(dirAlong);
        }
        else
        {
            lastChaseDir = 0;
            PatrolAlongSurface();
        }
    }

    // 면을 따라 기어가는 방향(+1/-1)을 화면 좌우로 바꿔서 바라본다. 바닥이 아니면 Tangent의 x 성분 기준
    void FaceAlong(int dirAlong)
    {
        float x = (Tangent * dirAlong).x;
        if (Mathf.Abs(x) > 0.3f) FaceDirection(x);
    }

    // true면 면에 붙어만 있고 기어 다니지 않는다 (침식 거미균의 전투 돌입 전 대기 등)
    protected virtual bool HoldPosition => false;

    // 바닥이면 좌우, 벽이면 상하로 순찰한다 (붙은 면을 따라가는 축 = Tangent)
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
        FaceAlong(patrolDir);
    }

    // 현재 붙어 있는 면에 몸을 붙인다. 면을 잃으면 주변을 훑어 새 면을 찾아 그쪽으로 이동한다.
    void StickToSurface()
    {
        // 바로 아래(현재 법선 반대)에 면이 없으면 주변 탐색 결과를 그대로 사용한다.
        // 탐색 결과를 stickRayDistance로 다시 검사하면 멀리 있는 벽에는 영원히 못 붙는다.
        if (!CastSurface(transform.position, -surfaceNormal, stickRayDistance + bodyHalfThickness, out RaycastHit2D hit)
            && !TryFindSurface(out hit))
        {
            isAttachedToSurface = false;
            hasSurfaceNearby = false;
            return;
        }
        hasSurfaceNearby = true;

        surfaceNormal = hit.normal; // 실제 면 법선으로 갱신 → 모서리를 돌아도 자연스럽게 따라감
        AlignToSurface();

        Vector2 targetPos = hit.point + surfaceNormal * SurfaceGap;
        transform.position = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime * 2f);

        isAttachedToSurface = Vector2.Distance(transform.position, targetPos) <= surfaceOffset + 0.05f;
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
            if (CastSurface(transform.position, d, surfaceSearchDistance + bodyHalfThickness, out RaycastHit2D h) && h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }

    // 패턴을 중간에 끊는다 (애니메이션 이벤트 StopAttack이 남아 있어도 안전하게)
    public override void StopAttack()
    {
        if (patternRoutine != null) StopCoroutine(patternRoutine);
        patternRoutine = null;
        isAttacking = false;
        moveMode = MoveMode.Crawl;
        ShowTelegraph(false);
        DisableHitbox();
        Hide(telegraphArea);
        Hide(secondaryArea);
    }

    // 히트박스(attackHitbox)는 더 이상 쓰지 않는다 — 베기는 SlashPattern의 박스 판정으로 한다
    protected override float HitboxDamage => slashDamage;

    // ── 사망 ────────────────────────────────────────────────────

    protected override void OnDeath()
    {
        base.OnDeath();
        StopAttack();

        // 벽·공중에서 죽으면 바닥으로 떨어뜨린다 (그래야 섭취할 수 있다)
        surfaceNormal = Vector2.up;
        AlignToSurface();
        if (rb != null) rb.gravityScale = 1f;
    }

    protected virtual void OnDestroy()
    {
        if (telegraphArea != null) Destroy(telegraphArea.gameObject);
        if (secondaryArea != null) Destroy(secondaryArea.gameObject);
    }

    // ── 공용 ────────────────────────────────────────────────────

    protected void FaceTarget() => FaceDirection(TargetPos().x - transform.position.x);

    protected Vector2 TargetPos() => target != null ? (Vector2)target.position : lastTargetPos;

    protected Vector2 BodyCenter() => bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;

    // 몸 기준 좌표(앞=x, 위=y) → 월드. 벽·천장에 붙어 회전해 있어도 몸 방향을 따른다 (스케일은 무시)
    protected Vector2 LocalToWorld(Vector2 local)
        => (Vector2)transform.position + (Vector2)transform.right * local.x + (Vector2)transform.up * local.y;

    protected Vector2 GroundBelow(Vector2 from)
    {
        if (CastSurface(from + Vector2.up * 0.5f, Vector2.down, 40f, out RaycastHit2D hit))
            return hit.point;
        return from;
    }

    protected bool HitBox(Vector2 center, Vector2 size, float angle, float damage)
    {
        foreach (var h in Physics2D.OverlapBoxAll(center, size, angle, playerMask))
            if (TryDamage(h, damage)) return true;
        return false;
    }

    protected bool HitCircle(Vector2 center, float radius, float damage)
    {
        foreach (var h in Physics2D.OverlapCircleAll(center, radius, playerMask))
            if (TryDamage(h, damage)) return true;
        return false;
    }

    bool TryDamage(Collider2D h, float damage)
    {
        PlayerController pc = h.GetComponent<PlayerController>();
        if (pc == null) return false;
        // 공격 판정 — 대시 접촉 무적으로는 못 막는다
        pc.TakeDamage(damage, KnockbackVector(pc.transform.position), stunDuration, DamageSource.Attack);
        return true;
    }

    protected void Trigger(string name)
    {
        if (!HasAnimatorController) return;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(name);
                return;
            }
    }

    // ── 범위 표시 (임시) ─────────────────────────────────────────

    protected void ShowBox(ref SpriteRenderer area, Vector2 center, Vector2 size, float angle, Color color)
    {
        if (!showAttackAreas) return;
        area = EnsureArea(area);
        area.sprite = SquareSprite();
        area.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
        area.transform.localScale = new Vector3(size.x, size.y, 1f);
        area.color = color;
        area.gameObject.SetActive(true);
    }

    protected void ShowCircle(ref SpriteRenderer area, Vector2 center, float radius, Color color)
    {
        if (!showAttackAreas) return;
        area = EnsureArea(area);
        area.sprite = CircleSprite();
        area.transform.SetPositionAndRotation(center, Quaternion.identity);
        area.transform.localScale = Vector3.one * (radius * 2f);
        area.color = color;
        area.gameObject.SetActive(true);
    }

    protected static void Hide(SpriteRenderer area)
    {
        if (area != null) area.gameObject.SetActive(false);
    }

    SpriteRenderer EnsureArea(SpriteRenderer area)
    {
        if (area != null) return area;
        var go = new GameObject(name + "_AttackArea");
        var sr = go.AddComponent<SpriteRenderer>();
        // 몸 뒤에 깐다 — 앞에 그리면 거미 몸이 판 색으로 물든다
        if (spr != null) { sr.sortingLayerID = spr.sortingLayerID; sr.sortingOrder = spr.sortingOrder - 1; }
        else sr.sortingOrder = 50;
        return sr;
    }

    static Sprite squareSprite, circleSprite;

    protected static Sprite SquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 1x1 유닛
        return squareSprite;
    }

    protected static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int n = 64;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n); // 지름 1 유닛
        return circleSprite;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        // 베기 범위 (오른쪽을 볼 때 기준)
        Gizmos.color = new Color(1f, 0.3f, 0.2f);
        Vector3 c = transform.position + transform.right * slashBoxOffset.x + transform.up * slashBoxOffset.y;
        Gizmos.matrix = Matrix4x4.TRS(c, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, slashBoxSize);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
