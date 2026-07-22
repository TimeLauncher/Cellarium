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
    public LayerMask obstructionMask; // 이 레이어(벽 등)에 가로막히면 감지 취소 — 인스펙터에서 wall 레이어로 설정 필요

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
        if (attackHitbox != null) attackHitbox.enabled = false;
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
        if (hit != null && obstructionMask.value != 0)
        {
            Vector2 dir = (Vector2)hit.transform.position - (Vector2)transform.position;
            RaycastHit2D block = Physics2D.Raycast(transform.position, dir.normalized, dir.magnitude, obstructionMask);
            if (block.collider != null) hit = null; // 벽 등에 가로막히면 감지 취소
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
        rb.linearVelocity = new Vector2(Mathf.Sign(dir) * moveSpeed, rb.linearVelocity.y);
        FaceDirection(dir);
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
        DisableHitbox();
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAttacking) return;
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            Vector2 knockDir = ((Vector2)(other.transform.position - transform.position)).normalized;
            pc.TakeDamage(attackDamage, knockDir * knockbackForce, stunDuration);
            DisableHitbox(); // 한 번만 히트
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
