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

    [Header("분열 대시 (우클릭 홀드→뗌)")]
    public float fissionDashSpeed = 20f;
    public float fissionDashDuration = 0.2f;

    [Header("지면 감지")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
    public LayerMask groundMask;

    [Header("분열 시스템")]
    public GameObject playerPrefab;
    public float fissionHoldDuration = 0.5f;

    [Header("섭취")]
    public float consumeRange = 2f;
    public LayerMask monsterMask;

    [Header("벽타기")]
    public float wallCheckDistance = 0.3f;
    public float wallSlideSpeed = 1.5f;
    public float wallJumpX = 6f;
    public float wallJumpY = 10f;
    public LayerMask wallMask;

    [Header("체력")]
    public float maxHp = 100f;

    [Header("제어")]
    public bool isControlled = false;
    public bool isClone = false;

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
    private float fissionHoldTimer;

    // 섭취
    private bool isConsuming;

    // 체력
    private float currentHp;

    public float CurrentHp => currentHp;
    public float FissionHoldProgress => fissionHoldTimer > 0f ? fissionHoldTimer / fissionHoldDuration : 0f;
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
        jumpsLeft = maxJumps;
        airDashLeft = maxAirDash;
        currentHp = maxHp;

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
        if (thrownTimer > 0f)
            thrownTimer -= Time.deltaTime;

        // 지면 감지
        Vector3 checkPos = groundCheck != null ? groundCheck.position : transform.position;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundMask);
        if (!wasGrounded && isGrounded)
        {
            jumpsLeft = maxJumps;
            lastWallJumpDir = 0;
            airDashLeft = maxAirDash;
        }
        wasGrounded = isGrounded;

        // 벽 감지
        float halfW = col != null ? col.bounds.extents.x : 0f;
        Vector2 rightOrigin = (Vector2)transform.position + Vector2.right * halfW;
        Vector2 leftOrigin  = (Vector2)transform.position + Vector2.left  * halfW;
        bool hitRight = Physics2D.Raycast(rightOrigin, Vector2.right, wallCheckDistance, wallMask);
        bool hitLeft  = Physics2D.Raycast(leftOrigin,  Vector2.left,  wallCheckDistance, wallMask);
        if (hitRight)       { isOnWall = true; wallDir =  1; }
        else if (hitLeft)   { isOnWall = true; wallDir = -1; }
        else                { isOnWall = false; wallDir = 0; lastWallJumpDir = 0; }

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

        if (!isControlled) return;

        moveX = Input.GetAxisRaw("Horizontal");

        bool isWallSliding = isOnWall && !isGrounded && wallJumpTimer <= 0f &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));

        if (normalDashCooldownTimer > 0f)
            normalDashCooldownTimer -= Time.deltaTime;

        // 점프 버퍼
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBuffer;

        // 내려찍기
        if (Input.GetKey(KeyCode.S) && Input.GetButtonDown("Jump") && !isGrounded && !isSlamming && !IsActionLocked())
        {
            jumpBufferTimer = 0f;
            Slam();
        }

        // 분열 (홀드 0.5초, 분열체/분열대시 중 불가)
        if (!isClone && !isFissionDashing)
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

        // 분열 대시 준비: 우클릭 누르면 (일반 대시 중엔 불가)
        if (Input.GetMouseButtonDown(1) && !isNormalDashing && !isClone)
        {
            isFissionDashing = false;
            isDashReady = true;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        // 분열 대시 시전: 우클릭 떼면
        if (Input.GetMouseButtonUp(1) && isDashReady && !isClone)
        {
            isDashReady = false;
            FissionDash();
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

    void FixedUpdate()
    {
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

        if (wallJumpTimer > 0f)
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

    // 대시/섭취/분열 대시 중엔 점프·내려찍기·대시 추가 입력 차단
    bool IsActionLocked()
    {
        return isDashReady || isFissionDashing || isNormalDashing || isConsuming;
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

        MonsterBase monster = hit.GetComponent<MonsterBase>();
        if (monster == null || !monster.IsConsumable) return null;

        return hit.transform;
    }

    public void TakeDamage(float amount)
    {
        currentHp = Mathf.Max(0f, currentHp - amount);
        Debug.Log($"피격! HP: {currentHp}/{maxHp}");
        if (currentHp <= 0f)
            Debug.Log("플레이어 사망!");  // TODO: 사망 처리
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
                    Debug.Log($"[대시 히트] {monster.name} 데미지={dashAttackDamage}");
                    rb.gravityScale = originalGravity;
                    rb.linearVelocity = -dashDir * dashKnockbackSpeed;
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

        // 몬스터 방향으로 살짝 이동
        Vector2 dir = ((Vector2)(target.transform.position - transform.position)).normalized;
        Vector2 startPos = transform.position;
        Vector2 targetPos = startPos + dir * 0.4f;

        float t = 0f;
        while (t < 0.1f)
        {
            transform.position = Vector2.Lerp(startPos, targetPos, t / 0.1f);
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

        if (target != null) Destroy(target);

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
        if (playerPrefab == null) return;
        float facing = (spr != null && spr.flipX) ? -1f : 1f;
        Vector2 spawnPos = (Vector2)transform.position + Vector2.right * (-facing) * 0.5f;
        GameObject clone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        clone.transform.localScale *= 0.75f;
        PlayerController cloneCtrl = clone.GetComponent<PlayerController>();
        if (cloneCtrl != null) cloneCtrl.isClone = true;
        clone.GetComponent<SpriteRenderer>().color = Color.green;
        Debug.Log("분열체 생성됨! (조작하려면 숫자키를 누르세요)");
    }

    void FissionDash()
    {
        Vector2 dashDir = ((Vector2)(GetMouseWorld() - transform.position)).normalized;
        if (dashDir.sqrMagnitude < 0.001f) return;

        // 분열체를 원래 위치(대시 반대방향 약간 오프셋)에 남기고 본체가 마우스 방향으로 대시
        if (playerPrefab != null)
        {
            Vector2 spawnPos = (Vector2)transform.position - dashDir * 0.6f;
            GameObject clone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            clone.transform.localScale *= 0.75f;
            PlayerController cloneCtrl = clone.GetComponent<PlayerController>();
            if (cloneCtrl != null) cloneCtrl.isClone = true;
            clone.GetComponent<SpriteRenderer>().color = Color.green;
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

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            jumpsLeft = maxJumps;
            airDashLeft = maxAirDash;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
