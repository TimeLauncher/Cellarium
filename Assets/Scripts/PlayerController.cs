using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 8f;

    [Header("점프")]
    public float jumpPower = 10f;
    public float jumpBuffer = 0.12f;
    public int maxJumps = 2;
    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 2f;
    public float ascendMultiplier = 1f;

    [Header("내려찍기")]
    public float slamForce = 40f;
    public float slamPreDelay = 0.2f;
    public float slamPostDelay = 0.2f;

    [Header("일반 대시 (좌클릭)")]
    public float dashSpeed = 20f;
    public float dashDistance = 3f;
    public float dashCooldown = 0.5f;
    public float dashExitPreserve = 0.35f;
    public bool allowAirDash = true;
    public int maxAirDash = 1;
    public float dashAttackDamage = 34f;
    public float dashKnockbackSpeed = 7f;
    public float dashKnockbackUpRatio = 0.6f; // 넉백에 섞는 위쪽 성분 비율 — 클수록 포물선이 높아짐
    public float dashInvincibleTime = 0.4f;   // 대시로 박은 뒤 튕겨나오는 동안 접촉 데미지 면역
    public float dashMonsterKnockback = 8f;   // 대시로 맞은 몬스터가 밀려나는 힘 (몬스터별 knockbackResistance로 배율 조절, 0이면 안 밀림)

    [Header("분열 대시 (우클릭 홀드→뗌)")]
    public float fissionDashSpeed = 20f;
    public float fissionDashDuration = 0.2f;
    public float fissionDashHoldDuration = 0.3f; // 이 시간 이상 누르고 떼야 발동 (톡 누르면 취소)

    [Header("지면 감지")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
    public LayerMask groundMask; // 미사용 — 지면도 레이어가 아니라 접촉 방향으로 판정(CheckGrounded)

    [Header("분열 시스템")]
    public GameObject playerPrefab;
    public float fissionHoldDuration = 0.5f;

    [Header("섭취")]
    public float consumeRange = 2f;
    public LayerMask monsterMask;
    public float consumeMoveSpeed = 15f; // 섭취 대상 위치로 러지하는 속도

    [Header("벽타기")]
    public float wallCheckDistance = 0.3f;
    public float wallSlideSpeed = 1.5f;
    public float wallJumpX = 6f;
    public float wallJumpY = 10f;
    public LayerMask wallMask; // 현재 미사용 — 맵이 wall/ground로 나뉘어 있지 않아 기하학적 판정(CheckWallSide)을 씀

    [Header("체력")]
    public float maxHp = 100f;
    public float knockbackDuration = 0.25f; // 이 시간 동안은 이동 입력이 넉백 속도를 덮어쓰지 않음
    public float invincibleDuration = 0.5f; // 피격 후 무적 시간 (경직 시간에 더해짐)

    [Header("분열 게이지")]
    public float maxFissionGauge = 100f;
    public float fissionGaugeRecoverRate = 10f;

    [Header("사망/부활")]
    public float deathMotionDuration = 3f;                 // 사망 모션 길이(2~4초). 이 동안 조작 불가·무적
    public Vector3 defaultRespawnPosition = Vector3.zero;  // 세이브포인트가 없을 때 부활 위치 (A00 중앙). 인스펙터에서 설정

    [Header("제어")]
    public bool isControlled = false;
    public bool isClone = false;

    [Header("표시")]
    [Range(0f, 1f)] public float uncontrolledAlpha = 0.5f; // 조종 중이 아닌 개체의 투명도
    public Color invincibleBlinkColor = Color.white;       // 무적 중 깜빡일 색
    public float invincibleBlinkInterval = 0.08f;

    [HideInInspector] public float thrownTimer = 0f;

    private Rigidbody2D rb;
    private SpriteRenderer spr;
    private Animator animator;

    private float moveX;
    private bool isGrounded;
    private bool wasGrounded;
    private float jumpBufferTimer;
    private int jumpsLeft;
    private bool isSlamming;

    // 일반 대시
    private bool isNormalDashing;
    private float normalDashCooldownTimer;
    private int airDashLeft;

    // 분열 대시
    private bool isDashReady;
    private bool isFissionDashing;
    private float fissionDashTimer;
    private float fissionDashHoldTimer;
    private float fissionHoldTimer;

    // 섭취
    private bool isConsuming;

    // 체력
    private float currentHp;
    private float currentFissionGauge;
    private bool isStunned;
    private bool isInvincible;
    private bool isDead;
    private float knockbackTimer;
    private float dashInvincibleTimer;
    private Color baseColor = Color.white;

    // 분열 능력이 해금됐는지 (A02에서 획득 전까지 잠김). 매니저가 없으면 개발 편의상 허용
    bool FissionUnlocked => PlayerManager.Instance == null || PlayerManager.Instance.fissionUnlocked;

    public float CurrentHp => currentHp;
    public float CurrentFissionGauge => currentFissionGauge;
    public float MaxFissionGauge => maxFissionGauge;
    public float FissionHoldProgress => fissionHoldTimer > 0f ? fissionHoldTimer / fissionHoldDuration : 0f;
    public float FissionDashHoldProgress => isDashReady ? Mathf.Clamp01(fissionDashHoldTimer / fissionDashHoldDuration) : 0f;
    public float DashCooldownProgress => normalDashCooldownTimer > 0f ? normalDashCooldownTimer / dashCooldown : 0f;

    // 벽타기
    private bool isOnWall;
    private bool wasOnWall;
    private int wallDir;
    private float wallJumpTimer;
    private int lastWallJumpDir;

    private Collider2D col;
    private PhysicsMaterial2D noFrictionMat;
    private PhysicsMaterial2D originalMat;
    private OneWayPlatformTile currentOneWayPlatform;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        col = GetComponent<Collider2D>();
        originalMat = col != null ? col.sharedMaterial : null;
        noFrictionMat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
    }

    void Start()
    {
        spr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (spr != null) baseColor = spr.color; // 스프라이트 원래 색 보존 (강제로 흰색/초록으로 덮어쓰지 않기 위함)
        jumpsLeft = maxJumps;
        airDashLeft = maxAirDash;
        currentHp = maxHp;
        currentFissionGauge = maxFissionGauge;

        if (PlayerManager.Instance != null)
            PlayerManager.Instance.RegisterPlayer(this);
    }

    void OnDestroy()
    {
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.UnregisterPlayer(this);
    }

    void Update()
    {
        if (isDead) return; // 사망 모션 중엔 입력·물리 판정 모두 정지

        if (thrownTimer > 0f)
            thrownTimer -= Time.deltaTime;

        if (dashInvincibleTimer > 0f)
            dashInvincibleTimer -= Time.deltaTime;

        // 지면 감지 — groundCheck가 비어있으면 콜라이더 발밑을 기준점으로 사용 (중앙에서 재면 거의 항상 false가 됨)
        Vector3 checkPos = groundCheck != null ? groundCheck.position
            : (col != null ? new Vector3(col.bounds.center.x, col.bounds.min.y, transform.position.z) : transform.position);
        isGrounded = CheckGrounded(checkPos);
        if (!wasGrounded && isGrounded)
        {
            jumpsLeft = maxJumps;
            lastWallJumpDir = 0;
            airDashLeft = maxAirDash;
        }
        wasGrounded = isGrounded;

        // 벽 감지 (레이어 무관 — 맵이 wall/ground로 나뉘어 있지 않아 기하학적으로 판정)
        if (CheckWallSide(1))       { isOnWall = true; wallDir =  1; }
        else if (CheckWallSide(-1)) { isOnWall = true; wallDir = -1; }
        else                        { isOnWall = false; wallDir = 0; lastWallJumpDir = 0; }

        if (!wasOnWall && isOnWall && !isGrounded)
            airDashLeft = maxAirDash;
        wasOnWall = isOnWall;

        // 분열 대시 타이머
        if (isFissionDashing)
        {
            fissionDashTimer -= Time.deltaTime;
            if (fissionDashTimer <= 0f)
            {
                isFissionDashing = false;
                rb.gravityScale = 1f;
            }
        }

        // 분열 게이지 자동 회복 (본체만, 분열체가 맵에 있으면 회복 안 됨)
        if (!isClone)
        {
            bool hasClones = PlayerManager.Instance != null && PlayerManager.Instance.allPlayers.Count > 1;
            if (!hasClones)
                currentFissionGauge = Mathf.Min(maxFissionGauge, currentFissionGauge + fissionGaugeRecoverRate * Time.deltaTime);
        }

        if (!isControlled) return;

        if (isStunned)
        {
            moveX = 0f;
            return;
        }

        moveX = Input.GetAxisRaw("Horizontal");

        bool isWallSliding = isOnWall && !isGrounded && wallJumpTimer <= 0f &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));

        if (normalDashCooldownTimer > 0f)
            normalDashCooldownTimer -= Time.deltaTime;

        // 점프 버퍼
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBuffer;

        // S+Space: 관통 타일 위면 아래로 통과, 그 외 공중이면 내려찍기 (통과가 우선)
        if (Input.GetKey(KeyCode.S) && Input.GetButtonDown("Jump") && !IsActionLocked())
        {
            if (currentOneWayPlatform != null)
            {
                jumpBufferTimer = 0f;
                currentOneWayPlatform.DropThrough(col);
            }
            else if (!isGrounded && !isSlamming)
            {
                jumpBufferTimer = 0f;
                Slam();
            }
        }

        // 분열 (홀드 0.5초, 분열체/분열대시 중 불가, 분열 능력 해금 전엔 불가)
        if (!isClone && !isFissionDashing && FissionUnlocked)
        {
            if (Input.GetKey(KeyCode.Q))
            {
                fissionHoldTimer += Time.deltaTime;
                if (fissionHoldTimer >= fissionHoldDuration)
                {
                    fissionHoldTimer = 0f;
                    Fission();
                }
            }
            if (Input.GetKeyUp(KeyCode.Q))
                fissionHoldTimer = 0f;
        }

        // 좌클릭: 마우스 커서 위치 몬스터 있으면 섭취, 없으면 일반 대시
        if (Input.GetMouseButtonDown(0) && !IsActionLocked())
            TryDashOrEat();

        // 분열 대시 준비: 우클릭 누르면 (일반 대시 중엔 불가, 분열 능력 해금 전엔 불가)
        if (Input.GetMouseButtonDown(1) && !isNormalDashing && !isClone && FissionUnlocked)
        {
            isFissionDashing = false;
            isDashReady = true;
            fissionDashHoldTimer = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        if (isDashReady && Input.GetMouseButton(1))
            fissionDashHoldTimer += Time.deltaTime;

        // 분열 대시 시전: 충분히 누르고 있다가 떼야 발동. 짧게 누르면 취소
        if (Input.GetMouseButtonUp(1) && isDashReady && !isClone)
        {
            isDashReady = false;

            if (fissionDashHoldTimer >= fissionDashHoldDuration)
                FissionDash();
            else
                Debug.Log("분열 대시 취소 (더 길게 누르고 떼야 함)");

            fissionDashHoldTimer = 0f;
        }

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        // 점프 (벽점프 우선)
        if (jumpBufferTimer > 0f && !isDashReady && !IsActionLocked())
        {
            if (isWallSliding && wallDir != lastWallJumpDir && !isClone)
            {
                rb.linearVelocity = new Vector2(-wallDir * wallJumpX, wallJumpY);
                jumpsLeft = maxJumps - 1;
                lastWallJumpDir = wallDir;
                wallJumpTimer = 0.25f;
                if (animator != null) animator.Play("jumpstart", 0, 0f);
                jumpBufferTimer = 0f;
            }
            else if (jumpsLeft > 0 && !isSlamming)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
                jumpsLeft--;
                if (animator != null) animator.Play("jumpstart", 0, 0f);
                jumpBufferTimer = 0f;
            }
        }

        if (spr != null && Mathf.Abs(moveX) > 0.01f)
            spr.flipX = (moveX < 0f);

        if (animator != null)
        {
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetFloat("yVelocity", rb.linearVelocity.y);
            animator.SetBool("move", Mathf.Abs(moveX) > 0.01f);
        }
    }

    // 색 처리는 한 곳에서만 — 조종 여부는 투명도로, 무적은 깜빡임으로 표현한다.
    // (예전엔 PlayerManager가 흰색/초록으로 직접 칠해서 스프라이트 원래 색이 날아갔음)
    void LateUpdate()
    {
        if (spr == null) return;

        Color c = baseColor;

        if (isInvincible && Mathf.FloorToInt(Time.time / Mathf.Max(0.01f, invincibleBlinkInterval)) % 2 == 0)
            c = invincibleBlinkColor;

        if (!isControlled)
            c.a = baseColor.a * uncontrolledAlpha;

        spr.color = c;
    }

    void FixedUpdate()
    {
        if (isDead) { rb.linearVelocity = Vector2.zero; return; } // 사망 모션 중 완전 정지

        bool isWallSlidingNow = isOnWall && !isGrounded && !isSlamming && wallJumpTimer <= 0f && isControlled && !isClone &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));
        if (col != null)
            col.sharedMaterial = isWallSlidingNow ? noFrictionMat : originalMat;

        // 분열 대시 준비: 완전 고정
        if (isDashReady)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 대시 중: 물리 개입 안 함 (각 대시 루틴이 속도 직접 제어)
        if (isFissionDashing || isNormalDashing) return;

        // 내려찍기 중: SlamRoutine이 gravityScale·velocity 직접 제어
        if (isSlamming) return;

        // 던져진 분열체
        if (thrownTimer > 0f)
        {
            rb.gravityScale = 1f;
            if (rb.linearVelocity.y < 0)
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
            return;
        }

        // 비조종 분열체
        if (!isControlled)
        {
            rb.gravityScale = 1f;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.gravityScale = 1f;

        if (knockbackTimer > 0f)
            knockbackTimer -= Time.fixedDeltaTime; // 넉백 중엔 속도를 건드리지 않고 그대로 날아가게 둠
        else if (wallJumpTimer > 0f)
            wallJumpTimer -= Time.fixedDeltaTime;
        else if (!isSlamming)
            rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

        // 벽 슬라이딩
        bool isWallSliding = isOnWall && !isGrounded && !isSlamming && wallJumpTimer <= 0f && !isClone &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));
        if (isWallSliding)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(0f, -wallSlideSpeed);
            return;
        }

        if (rb.linearVelocity.y < 0)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0 && ascendMultiplier > 1f)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (ascendMultiplier - 1) * Time.fixedDeltaTime;
    }

    // 대시/섭취/분열 대시/경직 중엔 점프·내려찍기·대시 추가 입력 차단
    bool IsActionLocked()
    {
        return isDashReady || isFissionDashing || isNormalDashing || isConsuming || isStunned;
    }

    // ── 일반 대시 / 섭취 ───────────────────────────────────────────

    void TryDashOrEat()
    {
        if (normalDashCooldownTimer > 0f) return;

        Transform target = GetMouseTarget();
        if (target != null)
        {
            StartCoroutine(ConsumeRoutine(target.gameObject));
            return;
        }

        TryNormalDash();
    }

    // 카메라~월드 좌표 변환 (공용)
    Vector3 GetMouseWorld()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;
        Vector3 ms = Input.mousePosition;
        ms.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(ms);
        world.z = transform.position.z;
        return world;
    }

    // 마우스 커서 위치의 monsterMask 콜라이더 반환 (범위 밖이거나 섭취 불가면 null)
    Transform GetMouseTarget()
    {
        Collider2D hit = Physics2D.OverlapPoint(GetMouseWorld(), monsterMask);
        if (hit == null) return null;
        if (Vector2.Distance(transform.position, hit.transform.position) > consumeRange) return null;

        IConsumable consumable = hit.GetComponent<IConsumable>();
        if (consumable == null || !consumable.IsConsumable) return null;

        return hit.transform;
    }

    // 섭취로 얻는 회복량 적용 (몬스터/회복셀 등 IConsumable.OnConsumed에서 호출)
    public void RestoreFromConsume(float hpAmount, float gaugeAmount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + hpAmount);
        currentFissionGauge = Mathf.Min(maxFissionGauge, currentFissionGauge + gaugeAmount);
    }

    public void TakeDamage(float amount, Vector2 knockback = default, float stunTime = 0f)
    {
        // 사망 모션 중엔 더 이상 피격되지 않음
        if (isDead) return;

        // 대시로 몬스터에 박는 동안은 공격 행동이므로 접촉 데미지를 받지 않는다
        if (isInvincible || dashInvincibleTimer > 0f) return;

        // 분열체는 피격 시 즉시 사망 (QA (4). 추후 1회 무효화 등 추가 예정)
        // Destroy → OnDestroy에서 PlayerManager.UnregisterPlayer가 조종 전환/목록 정리까지 자동 처리
        if (isClone)
        {
            Debug.Log("분열체 피격 — 즉시 사망");
            Destroy(gameObject);
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - amount);
        Debug.Log($"피격! HP: {currentHp}/{maxHp}");
        if (knockback.sqrMagnitude > 0.01f)
        {
            rb.linearVelocity = knockback;
            knockbackTimer = knockbackDuration; // 이동 입력이 곧바로 덮어쓰지 않도록 잠금
        }
        if (stunTime > 0f)
            StartCoroutine(StunRoutine(stunTime));

        if (currentHp <= 0f)
        {
            StartCoroutine(DeathRoutine());
            return;
        }

        // 경직이 없는 피해(가시 등)에도 무적 프레임은 항상 적용
        StartCoroutine(InvincibilityRoutine(stunTime + invincibleDuration));
    }

    // 사망 → 사망 모션(2~4초) → 마지막 세이브포인트(없으면 A00 중앙)에서 부활
    // 본체만 여기 도달함 (분열체는 TakeDamage 맨 앞에서 즉시 Destroy)
    IEnumerator DeathRoutine()
    {
        isDead = true;
        isInvincible = true; // 모션 중 추가 피격 방지
        moveX = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        if (animator != null) animator.SetTrigger("Death");
        Debug.Log("플레이어 사망 — 부활 대기");

        // 분열체 전부 자동 사망 (본체만 남기고 회수)
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.RecallAllClones();

        yield return new WaitForSeconds(deathMotionDuration);

        // 부활 위치: 마지막 세이브포인트가 있으면 그곳, 없으면 기본 부활 위치(A00 중앙)
        transform.position = SavePoint.HasSave ? SavePoint.LastSavePosition : defaultRespawnPosition;

        // 상태 초기화 후 완전 회복
        currentHp = maxHp;
        currentFissionGauge = maxFissionGauge;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 1f;
        jumpsLeft = maxJumps;
        airDashLeft = maxAirDash;
        isStunned = false;
        knockbackTimer = 0f;
        isDead = false;
        isInvincible = false;
        Debug.Log("부활!");
    }

    IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    IEnumerator InvincibilityRoutine(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }

    void TryNormalDash()
    {
        Vector2 dashDir = ((Vector2)(GetMouseWorld() - transform.position)).normalized;
        if (dashDir.sqrMagnitude < 0.001f) return;

        if (!isGrounded)
        {
            if (!allowAirDash || airDashLeft <= 0) return;
            airDashLeft--;
        }

        StartCoroutine(DashRoutine(dashDir));
    }

    IEnumerator DashRoutine(Vector2 dashDir)
    {
        isNormalDashing = true;
        normalDashCooldownTimer = dashCooldown;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        Vector2 vel = dashDir * dashSpeed;
        float calcDuration = dashDistance / dashSpeed;

        HashSet<MonsterBase> hitMonsters = new HashSet<MonsterBase>();
        float timer = 0f;
        bool knockedBack = false;

        while (timer < calcDuration && !knockedBack)
        {
            rb.linearVelocity = vel;
            dashInvincibleTimer = Mathf.Max(dashInvincibleTimer, 0.05f); // 대시하는 동안은 접촉 데미지 면역

            float castRadius = col != null ? col.bounds.extents.x : 0.3f;
            float castDist = vel.magnitude * Time.deltaTime;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, castRadius, vel.normalized, castDist, monsterMask);
            Debug.Log($"[대시] castRadius={castRadius:F2} castDist={castDist:F2} monsterMask={monsterMask.value} hits={hits.Length}");
            foreach (var hit in hits)
            {
                MonsterBase monster = hit.collider.GetComponent<MonsterBase>();
                if (monster != null && !hitMonsters.Contains(monster))
                {
                    hitMonsters.Add(monster);
                    monster.TakeDamage(dashAttackDamage);

                    // 맞은 몬스터도 대시 진행 방향으로 밀어낸다 (몬스터별 knockbackResistance로 조절, 0이면 안 밀림)
                    Vector2 monsterKnockDir = ((Vector2)(monster.transform.position - transform.position)).normalized;
                    if (monsterKnockDir.sqrMagnitude < 0.001f) monsterKnockDir = dashDir;
                    monster.ApplyKnockback(monsterKnockDir * dashMonsterKnockback);

                    rb.gravityScale = originalGravity;
                    Vector2 knockDir = (-dashDir + Vector2.up * dashKnockbackUpRatio).normalized;
                    rb.linearVelocity = knockDir * dashKnockbackSpeed;
                    knockbackTimer = knockbackDuration; // 이동 입력이 포물선을 곧바로 지우지 않도록 잠금
                    dashInvincibleTimer = dashInvincibleTime; // 튕겨나오는 동안 겹쳐 있어도 피해 없음
                    knockedBack = true;
                    break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!knockedBack)
        {
            rb.gravityScale = originalGravity;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * dashExitPreserve, 0f);
        }

        isNormalDashing = false;
    }

    IEnumerator ConsumeRoutine(GameObject target)
    {
        if (isConsuming) yield break;
        isConsuming = true;

        IConsumable consumable = target.GetComponent<IConsumable>();

        // 대상 위치까지 러지 (거리에 비례한 짧은 이동)
        Vector2 startPos = transform.position;
        Vector2 targetPos = target.transform.position;
        float moveDuration = Mathf.Clamp(Vector2.Distance(startPos, targetPos) / consumeMoveSpeed, 0.05f, 0.3f);

        float t = 0f;
        while (t < moveDuration)
        {
            transform.position = Vector2.Lerp(startPos, targetPos, t / moveDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        // 커졌다 원래대로
        Vector3 originalScale = transform.localScale;
        Vector3 bigScale = originalScale * 1.4f;

        t = 0f;
        while (t < 0.1f)
        {
            transform.localScale = Vector3.Lerp(originalScale, bigScale, t / 0.1f);
            t += Time.deltaTime;
            yield return null;
        }

        if (target != null)
        {
            consumable?.OnConsumed(this);
            Destroy(target);
        }

        t = 0f;
        while (t < 0.1f)
        {
            transform.localScale = Vector3.Lerp(bigScale, originalScale, t / 0.1f);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localScale = originalScale;

        isConsuming = false;
        Debug.Log("섭취!");
    }

    // ── 분열 ──────────────────────────────────────────────────────

    void Fission()
    {
        if (!FissionUnlocked) return;
        if (playerPrefab == null) return;
        if (currentFissionGauge < 30f) return;

        currentFissionGauge -= 30f;
        float facing = (spr != null && spr.flipX) ? -1f : 1f;
        Vector2 spawnPos = (Vector2)transform.position + Vector2.right * (-facing) * 0.5f;
        GameObject clone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        clone.transform.localScale *= 0.75f;
        PlayerController cloneCtrl = clone.GetComponent<PlayerController>();
        if (cloneCtrl != null) cloneCtrl.isClone = true;
        Debug.Log("분열체 생성됨! (조작하려면 숫자키를 누르세요)");
    }

    void FissionDash()
    {
        if (!FissionUnlocked) return;
        if (currentFissionGauge < 100f) return;

        Vector2 dashDir = ((Vector2)(GetMouseWorld() - transform.position)).normalized;
        if (dashDir.sqrMagnitude < 0.001f) return;

        currentFissionGauge -= 100f;

        // 분열체를 원래 위치(대시 반대방향 약간 오프셋)에 남기고 본체가 마우스 방향으로 대시
        if (playerPrefab != null)
        {
            Vector2 spawnPos = (Vector2)transform.position - dashDir * 0.6f;
            GameObject clone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            clone.transform.localScale *= 0.75f;
            PlayerController cloneCtrl = clone.GetComponent<PlayerController>();
            if (cloneCtrl != null) cloneCtrl.isClone = true;
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = dashDir * fissionDashSpeed;
        isFissionDashing = true;
        fissionDashTimer = fissionDashDuration;

        Debug.Log("분열 대시!");
    }

    // ── 내려찍기 ──────────────────────────────────────────────────

    void Slam()
    {
        StartCoroutine(SlamRoutine());
    }

    IEnumerator SlamRoutine()
    {
        isSlamming = true;
        Debug.Log("[내려찍기] 시작 - 공중 정지");

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(slamPreDelay);

        Debug.Log("[내려찍기] 하강 시작");
        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(0f, -slamForce);

        // 착지 대기: isGrounded 또는 하강 속도가 거의 0이 되면 착지로 판정 (최대 3초 타임아웃)
        float timeout = 3f;
        while (!isGrounded && rb.linearVelocity.y < -0.5f && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        Debug.Log($"[내려찍기] 착지 감지 - 경직 시작 (isGrounded={isGrounded}, vy={rb.linearVelocity.y:F2}, 타임아웃 잔여={timeout:F2}s)");

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(slamPostDelay);

        rb.gravityScale = 1f;
        isSlamming = false;
        Debug.Log("[내려찍기] 완료 - 조작 해제");
    }

    // ── 충돌 ──────────────────────────────────────────────────────

    // ── 벽 감지 ───────────────────────────────────────────────────
    // 맵이 wall/ground 레이어로 나뉘어 있지 않아 wallMask로는 판정할 수 없다.
    // 대신 "닿은 면이 수직면인가"로 벽을 정의한다 — 법선이 거의 수평이면 벽, 천장/바닥이면 아님.
    // 높이로 거르지 않으므로 플레이어보다 낮은 1칸 공중 블록의 옆면도 정상적으로 벽점프가 된다.
    // (머리로 벽을 기어다니던 현상은 천장·돌출부 아랫면이 법선 검사에서 걸러지므로 생기지 않는다)
    bool CheckWallSide(int dir)
    {
        if (col == null) return false;

        Bounds b = col.bounds;
        float x = dir > 0 ? b.max.x : b.min.x;

        // 몸 전체 높이에 걸쳐 검사 (어느 높이의 블록이든 잡히도록)
        float[] ys =
        {
            b.max.y - b.size.y * 0.15f,
            b.center.y,
            b.min.y + b.size.y * 0.2f,
        };

        foreach (float y in ys)
            if (WallRay(new Vector2(x, y), dir)) return true;

        return false;
    }

    bool WallRay(Vector2 origin, int dir)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * dir, wallCheckDistance);
        foreach (var h in hits)
        {
            Collider2D c = h.collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.GetComponent<MonsterBase>() != null) continue;      // 몬스터는 벽이 아님
            if (c.GetComponent<PlayerController>() != null) continue; // 다른 분열체도 벽이 아님

            // 수직면만 벽으로 인정 (천장·경사면·모서리에 매달리는 것 방지)
            if (Mathf.Abs(h.normal.x) < 0.7f) continue;

            return true;
        }
        return false;
    }

    // 레이어/태그로 지면을 구분하지 않는다 — 씬마다 바닥이 Default/ground/wall 어디에 있을지 제각각이라 계속 어긋났음.
    // 대신 "발밑에서 나를 받치고 있는가"라는 형태로 판정하므로, 벽 블록 위에 올라서도 지면으로 인정된다.
    bool IsGroundCandidate(GameObject obj)
    {
        return obj.GetComponent<MonsterBase>() == null; // 몬스터 위는 지면으로 치지 않음
    }

    bool CheckGrounded(Vector3 checkPos)
    {
        float feetY = col != null ? col.bounds.min.y : transform.position.y;

        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius);
        foreach (var h in hits)
        {
            if (h == null || h.isTrigger) continue;
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (!IsGroundCandidate(h.gameObject)) continue;

            // 옆에 서 있는 벽을 지면으로 오인하지 않도록, 발밑보다 위로 솟아있는 면은 제외
            if (h.bounds.max.y > feetY + groundCheckRadius) continue;

            return true;
        }
        return false;
    }

    // 발밑에서 받치는 접촉(법선이 위를 향함)이 하나라도 있는지
    bool HasUpwardContact(Collision2D collision)
    {
        foreach (var c in collision.contacts)
            if (c.normal.y > 0.5f) return true;
        return false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        OneWayPlatformTile owp = collision.gameObject.GetComponent<OneWayPlatformTile>();
        if (owp != null) currentOneWayPlatform = owp;

        if (IsGroundCandidate(collision.gameObject) && HasUpwardContact(collision))
        {
            isGrounded = true;
            jumpsLeft = maxJumps;
            airDashLeft = maxAirDash;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // 착지 순간 접촉점이 아직 안 잡히는 경우가 있어 유지 중에도 갱신 (벽 위에 올라선 경우 포함)
        if (IsGroundCandidate(collision.gameObject) && HasUpwardContact(collision))
        {
            jumpsLeft = maxJumps;
            airDashLeft = maxAirDash;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (currentOneWayPlatform != null && collision.gameObject.GetComponent<OneWayPlatformTile>() == currentOneWayPlatform)
            currentOneWayPlatform = null;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
