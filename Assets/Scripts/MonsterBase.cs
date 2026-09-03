using UnityEngine;

public class MonsterBase : MonoBehaviour, IConsumable
{
    [Header("스탯")]
    public float maxHp = 100f;
    public float moveSpeed = 3f;
    [Range(0f, 1f)]
    public float consumeThreshold = 0.25f; // HP 몇 % 이하면 섭취 가능

    [Header("감지")]
    public float detectionRange = 6f;
    public float detectionExpandMultiplier = 3f; // 탐지 이후 탐지범위 배율
    public LayerMask playerMask;
    public bool blockSightByTerrain = true; // 지형에 가로막히면 감지 취소
    public LayerMask obstructionMask;       // 미사용 — 맵이 레이어로 안 나뉘어 있어 CastSurface로 판정

    [Header("낭떠러지 감지")]
    public bool avoidLedges = true;      // 지상형만 true. 비행/벽타기형은 Awake에서 끔
    public float ledgeCheckAhead = 0.3f; // 진행 방향 앞쪽 이만큼 지점의 발밑을 검사
    public float ledgeCheckDepth = 1.2f; // 이 깊이까지 바닥이 없으면 낭떠러지로 판단

    [Header("순찰")]
    public float patrolDistance = 3f;
    public float patrolPauseDuration = 1f;
    public float patrolDistanceVariance = 0f; // 0이면 기존과 동일(고정 거리), >0이면 구간마다 랜덤화
    public float patrolPauseVariance = 0f;

    [Header("추격 제한")]
    public float maxLeashDistance = 8f; // 순찰 원점에서 이 거리(타일) 이상 벗어나면 추격을 포기하고 원점으로 복귀 (0 = 무제한)

    [Header("행동 간 딜레이")]
    public float postAttackPause = 0.5f;   // 공격이 끝난 뒤 이만큼 정지 후 다시 이동 (공격 직후 홱 도는 느낌 완화)
    public float turnPauseDuration = 0.5f; // 추적 중 PC가 반대편으로 넘어가 이동 방향이 급전환될 때 정지 시간

    [Header("피격 넉백 (대시 등)")]
    public float knockbackResistance = 1f;    // 밀려나는 정도 배율 (0이면 아예 안 밀림)
    public float knockbackRecoverTime = 0.2f; // 이 시간 동안은 AI 이동이 넉백 속도를 덮어쓰지 않음
    public bool knockbackDuringAttack = false; // 공격 모션 중에도 넉백/정지에 영향받을지 (기본 false = 공격 중엔 안 밀리고 안 멈춤). 몬스터별로 조정

    [Header("공격")]
    public float attackRange = 1.5f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    public float knockbackForce = 5f;
    [Tooltip("넉백에 섞는 위쪽 성분 비율. 0이면 지금처럼 옆으로만 밀리고, 클수록 위로 띄우는 포물선이 된다 (0.6 정도가 대시 넉백과 같은 느낌)")]
    public float knockbackUpRatio = 0.5f;
    [Tooltip("켜면 몬스터보다 아래에 있는 플레이어도 위로 띄운다. 끄면 실제 상하 위치 관계를 그대로 따른다")]
    public bool knockbackAlwaysUp = true;
    public float stunDuration = 0.3f;

    [Header("히트박스")]
    public Collider2D attackHitbox; // Inspector에서 자식 오브젝트의 Collider2D 연결
    public bool showHitbox = false; // 공격 범위를 화면에 표시 (개발용 — 기획 요청으로 기본값 끔)
    public bool attackFrontOnly = true; // 바라보는 앞쪽만 공격 판정(뒤통수 안 때림). 히트박스도 방향 따라 뒤집힘

    [Header("공격 임시 타이머 (애니메이션 붙기 전까지만 사용)")]
    public float attackWindup = 0.2f;     // 판정 켜지는 시점
    public float attackActiveTime = 0.3f; // 판정 유지 시간

    [Header("섭취 대기 시간")]
    public float consumableLifetime = 5f; // 섭취 가능 상태로 이 시간 동안 방치되면 자동 소멸

    [Tooltip("섭취 가능 상태가 되면 사정거리 원을 표시하고, " +
             "마우스로 집기 쉬운 섭취 전용 원형 콜라이더를 자동으로 붙인다")]
    public bool showConsumeIndicator = true;

    [Header("피격 이펙트")]
    [Tooltip("맞은 자리에 터지는 이펙트. Prefab을 비워두면 임시 스파크가 런타임에 만들어진다")]
    public HitEffect.Settings hitEffect = new HitEffect.Settings
    {
        scale = 0.9f,
        lifetime = 0.3f,
        fallbackColor = new Color(1f, 0.85f, 0.35f, 1f),
    };

    [Tooltip("맞은 순간 스프라이트를 이 색으로 물들인다. 곱하기 틴트라 흰색이면 아무 변화가 없다")]
    public Color hitFlashColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Tooltip("피격 시 스프라이트가 물드는 시간. 0이면 색 변화 없음")]
    public float hitFlashDuration = 0.12f;

    [Header("셀 드랍")]
    // 기본값은 전 몬스터 공통(총량 10 ÷ 5개 = 개당 2)이라 씬에 넣지 않고 여기 둔다.
    // 씬마다 박아두면 몬스터를 새로 놓을 때마다 빠뜨리고, 씬 저장 사고로 날아가기도 한다.
    // 몬스터별로 다르게 하고 싶으면 그 몬스터 인스펙터에서만 값을 바꾸면 된다.
    [Tooltip("처치 시 떨어뜨리는 셀 총량. 0이면 드랍하지 않는다 (10 = 개당 2 × 5개)")]
    [Range(0, 100)] public int cellDropTotal = 10;

    [Tooltip("총량을 몇 덩어리로 나눠 뿌릴지. 덩어리 하나당 셀 = 총량 ÷ 개수 (나머지는 앞쪽 덩어리에 1씩 붙는다)")]
    [Range(1, 10)] public int cellDropCount = 5;

    [Tooltip("떨어뜨릴 셀 프리팹. 비우면 임시 셀(노란 동그라미)을 런타임에 만든다")]
    public GameObject cellChunkPrefab;

    [Tooltip("떨어진 셀이 이 시간 동안은 획득되지 않는다. 0이면 섭취한 자리에서 즉시 흡수돼 셀이 보이지 않는다")]
    public float cellPickupDelay = 0.45f;

    [Tooltip("셀이 튀어오르는 속도")]
    public float cellPopUpSpeed = 4f;

    [Tooltip("셀이 좌우로 흩어지는 속도")]
    public float cellPopSideSpeed = 2.5f;

    protected float currentHp;
    protected float attackCooldownTimer;
    protected bool isAttacking;
    protected Rigidbody2D rb;
    protected SpriteRenderer spr;
    protected Animator animator;
    protected Collider2D bodyCollider;
    protected Transform target;
    protected bool hasDetectedPlayer;

    protected Vector2 patrolOrigin;
    protected int patrolDir = 1;
    protected float patrolPauseTimer;
    protected float currentPatrolLegDistance;
    protected float actionPauseTimer; // 공격 후 잠깐 멈추는 타이머
    protected float knockbackTimer;   // 넉백으로 밀려나는 동안 AI 이동을 멈추는 타이머
    protected bool returningHome;      // 추격 제한을 넘어 원점으로 복귀 중
    protected int lastChaseDir;        // 추적 중 마지막 이동 방향 (급전환 감지용)
    protected float turnPauseTimer;    // 이동 방향 급전환 시 잠깐 멈추는 타이머
    protected int facingDir = 1;       // 바라보는 방향(+1 오른쪽 / -1 왼쪽). 히트박스 방향/앞쪽 판정에 사용
    private float hitboxBaseOffsetX;   // 히트박스 자식의 원래 로컬 x 오프셋 크기 (방향 따라 부호만 바꿔줌)
    private Color baseColor;
    private float hitFlashTimer;
    private float consumableTimer;
    private bool hitPlayerThisAttack;
    protected HitboxVisualizer hitboxView;

    // 공격 준비 동작 중 '여기를 공격한다'는 예고 표시 on/off
    protected void ShowTelegraph(bool on)
    {
        if (hitboxView != null) hitboxView.SetTelegraph(on);
    }

    // 내부 "죽었는가" 판정 (AI 정지, 공격 중단 등에 사용)
    protected bool IsDead => currentHp <= 0f;

    // 플레이어가 섭취 가능한지 묻는 공개 게이트 — 자폭형 등은 override로 항상 false 가능
    public virtual bool IsConsumable => IsDead;

    // Animator에 실제 컨트롤러가 연결돼 있는지 — false면 애니메이션 이벤트 대신 타이머로 임시 대체
    protected bool HasAnimatorController => animator != null && animator.runtimeAnimatorController != null;

    // 탐지 이후 이탈 전까지는 확장된 범위를 계속 사용 (기획서 몬스터 공통 사항)
    protected float EffectiveDetectionRange => hasDetectedPlayer ? detectionRange * detectionExpandMultiplier : detectionRange;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        bodyCollider = GetComponent<Collider2D>();
        currentHp = maxHp;
        patrolOrigin = transform.position;
        currentPatrolLegDistance = patrolDistance;

        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
            hitboxBaseOffsetX = Mathf.Abs(attackHitbox.transform.localPosition.x); // 방향 뒤집기용 기준 오프셋 저장

            // 이펙트 에셋이 없으므로 히트박스를 눈에 보이게 해준다 (인스펙터 작업 불필요)
            hitboxView = attackHitbox.GetComponent<HitboxVisualizer>();
            if (showHitbox && hitboxView == null)
                hitboxView = attackHitbox.gameObject.AddComponent<HitboxVisualizer>();
        }

        // 기획서 (8): 섭취 사정거리 원 + 섭취 전용 콜라이더 + 커서 링 (인스펙터 작업 불필요).
        // ★ 반드시 bodyCollider를 잡은 뒤에 붙일 것 — ConsumeIndicator가 콜라이더를 하나 더 만든다.
        //   (자식에 만들긴 하지만, 순서를 지켜야 나중에 방식이 바뀌어도 안전하다)
        if (showConsumeIndicator && GetComponent<ConsumeIndicator>() == null)
            gameObject.AddComponent<ConsumeIndicator>();

        if (spr != null) baseColor = spr.color; // 인스펙터에서 구분용으로 지정한 색 보존
    }

    protected virtual void Update()
    {
        // ★ 색은 여기서 매 프레임 덮어쓰므로 피격 반짝임도 반드시 이 안에서 처리해야 한다.
        //   밖에서 spr.color를 직접 바꾸면 다음 프레임에 그대로 지워진다.
        if (hitFlashTimer > 0f) hitFlashTimer -= Time.deltaTime;

        if (spr != null)
            spr.color = IsDead ? Color.yellow : (hitFlashTimer > 0f ? hitFlashColor : baseColor);

        if (IsDead)
        {
            // 섭취 불가능한 타입(자폭형 등)은 자체 사망 처리(타이머/이펙트)를 쓰므로 여기선 건드리지 않음
            if (IsConsumable)
            {
                consumableTimer += Time.deltaTime;
                if (consumableTimer >= consumableLifetime)
                    OnConsumableTimeout();
            }
            return;
        }

        CheckContactDamage();
        PollAttackHitbox();
        UpdateDetection();

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;
        if (actionPauseTimer > 0f)
            actionPauseTimer -= Time.deltaTime;
        if (knockbackTimer > 0f)
            knockbackTimer -= Time.deltaTime;
        if (turnPauseTimer > 0f)
            turnPauseTimer -= Time.deltaTime;

        // 넉백/공격후 딜레이 중이거나 원점 복귀 중엔 새 공격을 걸지 않는다
        if (knockbackTimer <= 0f && actionPauseTimer <= 0f && !returningHome)
            UpdateBehavior();
    }

    protected virtual void FixedUpdate()
    {
        if (IsDead) return;
        UpdateMovement();
    }

    // 몸통 접촉 데미지 — 물리 충돌은 꺼져있으므로(PlayerManager에서 레이어 무시) 수동 겹침 체크로 대체.
    // QA (5): 겹쳐 있는 동안 매 프레임 판정한다. TakeDamage가 무적 중엔 무시하므로 무적시간이
    // 재피격 간격을 알아서 rate-limit 해준다 → "겹쳐진 상태에서도 피격 및 넉백 유지(무적시간 제외)".
    protected virtual void CheckContactDamage()
    {
        if (bodyCollider == null) return;
        Collider2D hit = Physics2D.OverlapBox(bodyCollider.bounds.center, bodyCollider.bounds.size, 0f, playerMask);
        if (hit == null) return;

        PlayerController pc = hit.GetComponent<PlayerController>();
        if (pc == null) return;

        // 몸통에 겹쳐서 주는 피해 — 대시로 들이받는 동안은 이것만 면역된다
        pc.TakeDamage(attackDamage, KnockbackVector(pc.transform.position), stunDuration, DamageSource.Contact);
    }

    // 플레이어에게 줄 넉백 속도. 좌우 방향은 몬스터→플레이어 기준으로 잡고,
    // 거기에 knockbackUpRatio만큼 위쪽 성분을 섞어 포물선으로 띄운다.
    // (전엔 두 위치를 그대로 normalize해서 서로 비슷한 높이일 때 사실상 x축으로만 밀렸다)
    protected Vector2 KnockbackVector(Vector3 playerPos)
    {
        float dx = playerPos.x - transform.position.x;
        float dy = playerPos.y - transform.position.y;

        Vector2 dir = new Vector2(Mathf.Abs(dx) < 0.001f ? 0f : Mathf.Sign(dx), 0f);

        // 위로 띄우는 성분. AlwaysUp이면 아래에 있는 플레이어도 위로, 아니면 실제 상하 관계를 따른다
        float up = knockbackAlwaysUp ? 1f : (Mathf.Abs(dy) < 0.001f ? 1f : Mathf.Sign(dy));
        dir += Vector2.up * (up * knockbackUpRatio);

        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        return dir.normalized * knockbackForce;
    }

    // 지형 표면 감지 — 맵이 wall/ground 레이어로 나뉘어 있지 않아 레이어 대신 필터링으로 판정한다.
    // (몬스터/플레이어/트리거는 지형이 아니므로 제외, 가장 가까운 것을 반환)
    protected bool CastSurface(Vector2 origin, Vector2 dir, float dist, out RaycastHit2D result)
    {
        result = default;
        if (dir.sqrMagnitude < 0.0001f) return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir.normalized, dist);
        float best = float.MaxValue;
        bool found = false;

        foreach (var h in hits)
        {
            Collider2D c = h.collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.GetComponent<MonsterBase>() != null) continue;
            if (c.GetComponent<PlayerController>() != null) continue;

            if (h.distance < best)
            {
                best = h.distance;
                result = h;
                found = true;
            }
        }
        return found;
    }

    // 진행 방향 앞쪽에 디딜 바닥이 있는지 (없으면 낭떠러지 → 떨어지지 않도록 방향 전환/정지)
    protected bool HasGroundAhead(float dirX)
    {
        if (!avoidLedges) return true;
        if (bodyCollider == null || Mathf.Abs(dirX) < 0.01f) return true;

        Bounds b = bodyCollider.bounds;
        Vector2 origin = new Vector2(
            dirX > 0 ? b.max.x + ledgeCheckAhead : b.min.x - ledgeCheckAhead,
            b.min.y + 0.05f);

        return CastSurface(origin, Vector2.down, ledgeCheckDepth, out _);
    }

    // 섭취 가능 상태로 방치되어 시간 초과된 경우 — 시체가 스스로 정리된다.
    // 안 먹었어도 셀은 나온다 (먹어야만 주면 놓친 몬스터의 보상이 통째로 증발한다).
    protected virtual void OnConsumableTimeout()
    {
        DropCells();
        Destroy(gameObject);
    }

    // IConsumable — 몬스터를 섭취했을 때(기존 섭취 회복 수치 그대로 재현)
    public virtual void OnConsumed(PlayerController consumer)
    {
        consumer.RestoreFromConsume(100f, 100f);

        // ★ 셀은 죽자마자가 아니라 '섭취하고 나서' 나온다.
        //   섭취 안 하고 방치해 consumableLifetime으로 사라지면 셀도 안 나온다(의도된 동작).
        DropCells();
    }

    protected virtual void UpdateDetection()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, EffectiveDetectionRange, playerMask);

        // 시야 차단도 레이어로 판정하지 않는다 — 맵 지형이 Default에 있어서 wall 레이어로는 거의 아무것도 못 막음.
        // CastSurface가 트리거/몬스터/플레이어를 걸러주므로 '사이를 막고 있는 지형'만 남는다.
        if (hit != null && blockSightByTerrain)
        {
            Vector2 dir = (Vector2)hit.transform.position - (Vector2)transform.position;
            if (CastSurface(transform.position, dir, dir.magnitude, out _))
                hit = null;
        }

        if (hit != null)
        {
            target = hit.transform;
            hasDetectedPlayer = true;
        }
        else
        {
            target = null;
            hasDetectedPlayer = false;
        }
    }

    // 순찰→추적→공격 판단. 공격 트리거 조건만 다른 타입은 이 메서드만 override
    protected virtual void UpdateBehavior()
    {
        if (isAttacking) return;

        if (target != null && attackCooldownTimer <= 0f &&
            Vector2.Distance(transform.position, target.position) <= attackRange)
        {
            TryStartAttack();
        }
    }

    protected virtual void UpdateMovement()
    {
        if (MovementSuppressed()) return;

        if (isAttacking)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 추격 제한: 원점에서 너무 멀어지면 복귀. 원점에 다 돌아올 때까지는 다시 쫓지 않는다(경계선 진동 방지)
        if (returningHome)
        {
            ReturnToOrigin();
        }
        else if (target != null && !IsBeyondLeash())
        {
            MoveTowardsTarget();
        }
        else if (IsBeyondLeash())
        {
            returningHome = true;
            ReturnToOrigin();
        }
        else
        {
            Patrol();
        }
    }

    // 넉백/공격후 딜레이 중이면 AI 이동을 멈춘다. 넉백 중엔 속도를 건드리지 않아 그대로 밀려남.
    protected bool MovementSuppressed()
    {
        if (knockbackTimer > 0f) return true;
        if (actionPauseTimer > 0f)
        {
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return true;
        }
        return false;
    }

    // 대시 등에 맞았을 때 밀려남 (knockbackResistance가 0이면 안 밀림)
    // 죽어서 섭취 가능 상태가 되면 AI가 멈춰 속도를 못 지워 마찰 없이 미끄러지므로 넉백을 걸지 않는다
    public virtual void ApplyKnockback(Vector2 force)
    {
        if (rb == null || knockbackResistance <= 0f || IsDead) return;
        // 공격 모션 중엔 넉백으로 밀려나지도, (knockbackTimer로) 멈추지도 않는다 — 공격이 끊기지 않게.
        // 몬스터마다 다르게 하려면 인스펙터에서 knockbackDuringAttack 체크
        if (isAttacking && !knockbackDuringAttack) return;
        rb.linearVelocity = force * knockbackResistance;
        knockbackTimer = knockbackRecoverTime;
    }

    protected bool IsBeyondLeash()
    {
        if (maxLeashDistance <= 0f) return false;
        return Mathf.Abs(transform.position.x - patrolOrigin.x) >= maxLeashDistance;
    }

    // 순찰 원점으로 걸어서 복귀. 다 돌아오면 복귀 상태 해제
    protected virtual void ReturnToOrigin()
    {
        float dx = patrolOrigin.x - transform.position.x;
        if (Mathf.Abs(dx) <= 0.2f)
        {
            returningHome = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float dir = Mathf.Sign(dx);
        FaceDirection(dir);
        if (!HasGroundAhead(dir))
        {
            // 발밑이 끊겨 있으면 더 못 감 — 그 자리에서 복귀 종료
            returningHome = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }
        rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
    }

    protected virtual void MoveTowardsTarget()
    {
        float dx = target.position.x - transform.position.x;
        int dir = dx > 0.02f ? 1 : (dx < -0.02f ? -1 : 0);

        // 추적 중 PC가 반대편으로 넘어가 이동 방향이 급전환되면 잠깐 멈췄다가 따라간다 (QA: 0.5초 내외)
        if (dir != 0 && lastChaseDir != 0 && dir != lastChaseDir)
            turnPauseTimer = turnPauseDuration;
        if (dir != 0) lastChaseDir = dir;

        FaceDirection(dx);

        if (turnPauseTimer > 0f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 쫓아가더라도 낭떠러지 앞에서는 멈춘다
        if (!HasGroundAhead(Mathf.Sign(dx)))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(Mathf.Sign(dx) * moveSpeed, rb.linearVelocity.y);
    }

    // 정해진 구간을 반복 이동 + 주기적으로 정지 (기획서 "기본 행동" 공통 패턴)
    // patrolDistanceVariance/patrolPauseVariance가 0보다 크면 구간마다 랜덤한 거리/정지시간을 다시 뽑음
    protected virtual void Patrol()
    {
        lastChaseDir = 0; // 추적을 멈췄으니 방향 기록 초기화 (재추적 시 잘못된 급전환 판정 방지)

        if (patrolPauseTimer > 0f)
        {
            patrolPauseTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float offset = transform.position.x - patrolOrigin.x;
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
        else if (!HasGroundAhead(patrolDir))
        {
            // 순찰 구간이 남았어도 발밑이 끊기면 되돌아간다
            patrolDir = -patrolDir;
            RerollPatrolLeg();
        }

        rb.linearVelocity = new Vector2(patrolDir * moveSpeed, rb.linearVelocity.y);
        FaceDirection(patrolDir);
    }

    protected void RerollPatrolLeg()
    {
        currentPatrolLegDistance = Mathf.Max(0.1f, patrolDistance + Random.Range(-patrolDistanceVariance, patrolDistanceVariance));
        patrolPauseTimer = Mathf.Max(0f, patrolPauseDuration + Random.Range(-patrolPauseVariance, patrolPauseVariance));
    }

    protected virtual void FaceDirection(float dirX)
    {
        if (Mathf.Abs(dirX) <= 0.01f) return;

        facingDir = dirX < 0 ? -1 : 1;
        if (spr != null) spr.flipX = dirX < 0;

        // 공격 히트박스(자식)를 바라보는 방향으로 옮겨준다 — 스프라이트만 뒤집으면 히트박스는
        // 고정된 쪽에 남아 뒤통수를 때리게 되므로 로컬 x 부호를 방향에 맞춘다.
        if (attackHitbox != null && hitboxBaseOffsetX > 0.0001f)
        {
            Vector3 lp = attackHitbox.transform.localPosition;
            lp.x = facingDir * hitboxBaseOffsetX;
            attackHitbox.transform.localPosition = lp;
        }
    }

    // 근접 접촉형 공격 시작 (몸통박치기 등). 다른 패턴(돌진/원거리/점프)은 서브클래스에서 override
    protected virtual void TryStartAttack()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        ShowTelegraph(true);
        if (animator != null) animator.SetTrigger("Attack");

        if (!HasAnimatorController)
        {
            Invoke(nameof(EnableHitbox), attackWindup);
            Invoke(nameof(StopAttack), attackWindup + attackActiveTime);
        }
    }

    // 애니메이션 이벤트로 호출 — 판정 시작 프레임
    public void EnableHitbox()
    {
        if (IsDead) return; // 타이머 대기 중 사망한 경우 뒤늦게 켜지는 것 방지
        ShowTelegraph(false); // 예고 끝, 이제 실제 판정
        hitPlayerThisAttack = false;
        if (hitboxView != null) hitboxView.FlashActive();
        if (attackHitbox != null) attackHitbox.enabled = true;
    }

    // 애니메이션 이벤트로 호출 — 판정 끝 프레임
    public void DisableHitbox()
    {
        if (attackHitbox != null) attackHitbox.enabled = false;
    }

    // 애니메이션 이벤트로 호출 — 공격 모션 마지막 프레임
    public virtual void StopAttack()
    {
        CancelInvoke(nameof(EnableHitbox));
        CancelInvoke(nameof(StopAttack));
        isAttacking = false;
        ShowTelegraph(false);
        DisableHitbox();
        actionPauseTimer = postAttackPause; // 공격 직후 잠깐 멈췄다가 움직이도록
    }

    // 이 공격에서 적용할 피해량 — 패턴별로 다른 타입(거미균 베기 등)은 override
    protected virtual float HitboxDamage => attackDamage;

    // 히트박스 판정도 겹침 검사로 한다.
    // 히트박스가 monster 레이어에 있고 player↔monster 충돌이 꺼져 있어서
    // OnTriggerEnter2D는 아예 호출되지 않기 때문 (겹침 쿼리는 레이어 매트릭스를 무시함).
    protected virtual void PollAttackHitbox()
    {
        if (hitPlayerThisAttack) return;
        if (attackHitbox == null || !attackHitbox.enabled) return;

        Bounds b = attackHitbox.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, 0f, playerMask);
        foreach (var h in hits)
        {
            PlayerController pc = h.GetComponent<PlayerController>();
            if (pc == null) continue;

            // 앞쪽만 때리기: 바라보는 방향 반대편(뒤통수)에 있는 플레이어는 무시
            if (attackFrontOnly)
            {
                float relX = pc.transform.position.x - transform.position.x;
                if (Mathf.Abs(relX) > 0.05f && Mathf.Sign(relX) != facingDir) continue;
            }

            // 공격 히트박스 — 접촉이 아니라 공격이므로 대시 중에도 그대로 맞는다
            pc.TakeDamage(HitboxDamage, KnockbackVector(pc.transform.position), stunDuration, DamageSource.Attack);
            hitPlayerThisAttack = true; // 중복 타격은 이 플래그로 막는다.
            // 여기서 DisableHitbox()를 부르면 켜진 같은 프레임에 꺼져서 판정 표시가 안 보이므로 끄지 않는다.
            break;
        }
    }

    // hitDirection: 공격이 날아온 방향(대시 진행 방향 등). 이펙트를 그쪽으로 뻗게 하는 데만 쓴다
    public virtual void TakeDamage(float amount, Vector2 hitDirection = default)
    {
        if (IsDead) return;
        currentHp = Mathf.Max(0f, currentHp - amount);

        PlayHitEffect(hitDirection);

        if (IsDead)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;

            // ★ 여기서는 셀을 떨구지 않는다. 드랍 시점은 세 가지다:
            //     ① 섭취했을 때            → OnConsumed()
            //     ② 안 먹고 시체가 사라질 때 → OnConsumableTimeout()
            //     ③ 자폭형이 터졌을 때      → FloaterGerm.Detonate()
            //   즉 '죽는 것'이 아니라 '시체가 정리되는 것'이 조건이다.
            OnDeath();
        }
    }

    // 맞은 자리에 이펙트를 띄우고 스프라이트를 잠깐 물들인다.
    // 죽은 뒤(노란 시체)엔 색을 건드리지 않으므로 이펙트만 나간다.
    protected void PlayHitEffect(Vector2 hitDirection)
    {
        Vector3 pos = bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        HitEffect.Play(hitEffect, pos, hitDirection, spr);

        if (hitFlashDuration > 0f)
            hitFlashTimer = hitFlashDuration;
    }

    // 셀 드랍. 몸통(피격) 콜라이더 범위 안에 무작위로 흩뿌린다.
    protected virtual void DropCells()
    {
        if (cellDropTotal <= 0 || cellDropCount <= 0) return;

        Bounds area = bodyCollider != null
            ? bodyCollider.bounds
            : new Bounds(transform.position, Vector3.one);

        int perChunk = cellDropTotal / cellDropCount;
        int remainder = cellDropTotal - perChunk * cellDropCount; // 나머지는 앞쪽 덩어리에 1씩 얹는다

        for (int i = 0; i < cellDropCount; i++)
        {
            int amount = perChunk + (i < remainder ? 1 : 0);
            if (amount <= 0) continue; // 총량이 개수보다 적으면 뒤쪽 덩어리는 안 만든다

            Vector3 pos = new Vector3(
                Random.Range(area.min.x, area.max.x),
                Random.Range(area.min.y, area.max.y),
                transform.position.z);

            SpawnCellChunk(pos, amount);
        }
    }

    void SpawnCellChunk(Vector3 position, int amount)
    {
        // 어떤 모습으로 나올지는 CellChunk.Spawn이 정한다
        // (인스펙터 프리팹 → Resources/Effects/CellDrop → 런타임 임시 원 순).
        // 몬스터의 정렬 레이어를 물려줘야 맵/배경 뒤에 숨지 않는다.
        CellChunk chunk = CellChunk.Spawn(position, amount, cellChunkPrefab, spr);
        if (chunk == null) return;

        // 섭취 직후엔 플레이어가 몬스터 자리에 겹쳐 있어서 그냥 두면 생기는 즉시 흡수된다.
        // 잠깐 튀어오르는 동안 못 먹게 막아야 "셀이 나왔다"가 화면에 보인다.
        chunk.pickupDelay = cellPickupDelay;
        chunk.Launch(new Vector2(Random.Range(-cellPopSideSpeed, cellPopSideSpeed),
                                 Random.Range(cellPopUpSpeed * 0.6f, cellPopUpSpeed)));
    }

    // 사망(체력 0) 시점 훅 — 자폭 등 특수 사망 처리가 필요한 타입에서 override
    protected virtual void OnDeath() { }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 판정 박스: 켜져있으면 노란 채움, 꺼져있으면 회색 테두리만
        if (attackHitbox is BoxCollider2D box)
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            if (box.enabled)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                Gizmos.DrawCube(box.offset, box.size);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(box.offset, box.size);
            }
            else
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireCube(box.offset, box.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
