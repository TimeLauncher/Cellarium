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

    [Header("공격")]
    public float attackRange = 1.5f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    public float knockbackForce = 5f;
    public float stunDuration = 0.3f;

    [Header("히트박스")]
    public Collider2D attackHitbox; // Inspector에서 자식 오브젝트의 Collider2D 연결
    public bool showHitbox = true;  // 이펙트 에셋 나오기 전까지 공격 범위를 화면에 표시

    [Header("공격 임시 타이머 (애니메이션 붙기 전까지만 사용)")]
    public float attackWindup = 0.2f;     // 판정 켜지는 시점
    public float attackActiveTime = 0.3f; // 판정 유지 시간

    [Header("섭취 대기 시간")]
    public float consumableLifetime = 5f; // 섭취 가능 상태로 이 시간 동안 방치되면 자동 소멸

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
    private Color baseColor;
    private float consumableTimer;
    private bool wasContactingPlayer;
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

            // 이펙트 에셋이 없으므로 히트박스를 눈에 보이게 해준다 (인스펙터 작업 불필요)
            hitboxView = attackHitbox.GetComponent<HitboxVisualizer>();
            if (showHitbox && hitboxView == null)
                hitboxView = attackHitbox.gameObject.AddComponent<HitboxVisualizer>();
        }

        if (spr != null) baseColor = spr.color; // 인스펙터에서 구분용으로 지정한 색 보존
    }

    protected virtual void Update()
    {
        if (spr != null)
            spr.color = IsDead ? Color.yellow : baseColor;

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

        UpdateBehavior();
    }

    protected virtual void FixedUpdate()
    {
        if (IsDead) return;
        UpdateMovement();
    }

    // 몸통 접촉 데미지 — 물리 충돌은 꺼져있으므로(PlayerManager에서 레이어 무시) 수동 겹침 체크로 대체.
    // 진입 시점에만 1회 발동, TakeDamage의 무적프레임이 이후 연속 데미지를 자연히 막아줌.
    protected virtual void CheckContactDamage()
    {
        if (bodyCollider == null) return;
        Collider2D hit = Physics2D.OverlapBox(bodyCollider.bounds.center, bodyCollider.bounds.size, 0f, playerMask);
        if (hit != null)
        {
            if (!wasContactingPlayer)
            {
                PlayerController pc = hit.GetComponent<PlayerController>();
                if (pc != null)
                {
                    Vector2 knockDir = ((Vector2)(pc.transform.position - transform.position)).normalized;
                    if (knockDir.sqrMagnitude < 0.001f) knockDir = Vector2.up;
                    pc.TakeDamage(attackDamage, knockDir * knockbackForce, stunDuration);
                }
            }
            wasContactingPlayer = true;
        }
        else
        {
            wasContactingPlayer = false;
        }
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

    // 섭취 가능 상태로 방치되어 시간 초과된 경우 — 기본은 그냥 소멸
    protected virtual void OnConsumableTimeout()
    {
        Destroy(gameObject);
    }

    // IConsumable — 몬스터를 섭취했을 때(기존 섭취 회복 수치 그대로 재현)
    public virtual void OnConsumed(PlayerController consumer)
    {
        consumer.RestoreFromConsume(100f, 100f);
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
        if (isAttacking)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        if (target != null)
        {
            MoveTowardsTarget();
        }
        else
        {
            Patrol();
        }
    }

    protected virtual void MoveTowardsTarget()
    {
        float dir = target.position.x - transform.position.x;
        FaceDirection(dir);

        // 쫓아가더라도 낭떠러지 앞에서는 멈춘다
        if (!HasGroundAhead(Mathf.Sign(dir)))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(Mathf.Sign(dir) * moveSpeed, rb.linearVelocity.y);
    }

    // 정해진 구간을 반복 이동 + 주기적으로 정지 (기획서 "기본 행동" 공통 패턴)
    // patrolDistanceVariance/patrolPauseVariance가 0보다 크면 구간마다 랜덤한 거리/정지시간을 다시 뽑음
    protected virtual void Patrol()
    {
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
        if (spr != null && Mathf.Abs(dirX) > 0.01f)
            spr.flipX = dirX < 0;
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

            Vector2 knockDir = ((Vector2)(pc.transform.position - transform.position)).normalized;
            if (knockDir.sqrMagnitude < 0.001f) knockDir = Vector2.up;

            pc.TakeDamage(HitboxDamage, knockDir * knockbackForce, stunDuration);
            hitPlayerThisAttack = true; // 중복 타격은 이 플래그로 막는다.
            // 여기서 DisableHitbox()를 부르면 켜진 같은 프레임에 꺼져서 판정 표시가 안 보이므로 끄지 않는다.
            break;
        }
    }

    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;
        currentHp = Mathf.Max(0f, currentHp - amount);
        if (IsDead)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            OnDeath();
        }
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
