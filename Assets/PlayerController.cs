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

    [Header("내려찍기")]
    public float slamForce = 40f;

    [Header("분열 대시")]
    public float fissionDashSpeed = 20f;
    public float dashDuration = 0.2f;

    [Header("지면 감지")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
    public LayerMask groundMask;

    [Header("분열 시스템")]
    public GameObject playerPrefab;

    [Header("섭취")]
    public float consumeRange = 2f;
    public LayerMask monsterMask;

    [Header("벽타기")]
    public float wallCheckDistance = 0.3f;
    public float wallSlideSpeed = 1.5f;
    public float wallJumpX = 6f;
    public float wallJumpY = 10f;
    public LayerMask wallMask;

    [Header("제어")]
    public bool isControlled = false;

    // 대시로 던져진 분열체 여부 (외부에서 설정)
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

    private bool isDashReady;
    private bool isDashing;
    private float dashTimer;

    private bool isOnWall;
    private int wallDir; // 1=오른쪽벽, -1=왼쪽벽
    private float wallJumpTimer; // > 0이면 벽점프 직후, x입력 무시
    private int lastWallJumpDir; // 마지막으로 벽점프한 벽 방향 (같은 벽 연속 점프 방지)

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
        // 던져진 분열체 타이머
        if (thrownTimer > 0f)
            thrownTimer -= Time.deltaTime;

        // 지면 감지
        Vector3 checkPos = groundCheck != null ? groundCheck.position : transform.position;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundMask);
        if (!wasGrounded && isGrounded)
        {
            jumpsLeft = maxJumps;
            isSlamming = false;
            lastWallJumpDir = 0; // 착지하면 같은 벽 점프 제한 초기화
        }
        wasGrounded = isGrounded;

        // 벽 감지: 콜라이더 끝에서 레이캐스트 (중심에서 쏘면 콜라이더 반너비보다 짧아서 벽에 못 닿음)
        float halfW = col != null ? col.bounds.extents.x : 0f;
        Vector2 rightOrigin = (Vector2)transform.position + Vector2.right * halfW;
        Vector2 leftOrigin  = (Vector2)transform.position + Vector2.left  * halfW;
        bool hitRight = Physics2D.Raycast(rightOrigin, Vector2.right, wallCheckDistance, wallMask);
        bool hitLeft  = Physics2D.Raycast(leftOrigin,  Vector2.left,  wallCheckDistance, wallMask);
        if (hitRight)       { isOnWall = true; wallDir =  1; }
        else if (hitLeft)   { isOnWall = true; wallDir = -1; }
        else                { isOnWall = false; wallDir = 0; lastWallJumpDir = 0; }

        // 대시 타이머
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                rb.gravityScale = 1f; // 대시 종료 시 중력 복원
            }
        }

        if (!isControlled) return;

        moveX = Input.GetAxisRaw("Horizontal");

        // 벽 방향으로 누르고 있을 때만 벽 슬라이딩/벽점프 상태
        bool isWallSliding = isOnWall && !isGrounded && wallJumpTimer <= 0f &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));

        // 점프 버퍼
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBuffer;

        // 내려찍기: S + Space, 공중에서만
        if (Input.GetKey(KeyCode.S) && Input.GetButtonDown("Jump") && !isGrounded && !isSlamming)
        {
            jumpBufferTimer = 0f; // slam 시 점프버퍼 즉시 클리어 (점프로 덮어쓰기 방지)
            Slam();
        }

        // 분열
        if (Input.GetKeyDown(KeyCode.F))
            Fission();

        // 섭취: 좌클릭
        if (Input.GetMouseButtonDown(0))
            TryConsume();

        // 분열 대시 준비: 우클릭 누르는 동안
        if (Input.GetMouseButtonDown(1))
        {
            isDashing = false; // 이전 대시 강제 종료
            isDashReady = true;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        // 분열 대시 시전: 우클릭 떼면
        if (Input.GetMouseButtonUp(1) && isDashReady)
        {
            isDashReady = false;
            FissionDash(); // gravityScale은 FissionDash 내부에서 0으로 유지, 종료 시 복원
        }

        // 점프 버퍼 타이머 감소
        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        // 점프 실행 (벽점프 우선 - 벽 방향 키 누를 때만, 같은 벽 연속 점프 불가)
        if (jumpBufferTimer > 0f && !isDashReady)
        {
            if (isWallSliding && wallDir != lastWallJumpDir)
            {
                rb.linearVelocity = new Vector2(-wallDir * wallJumpX, wallJumpY);
                jumpsLeft = maxJumps - 1;
                lastWallJumpDir = wallDir;
                wallJumpTimer = 0.25f; // 0.25초간 x입력 무시
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

        // 스프라이트 반전
        if (spr != null && Mathf.Abs(moveX) > 0.01f)
            spr.flipX = (moveX < 0f);

        // 애니메이터
        if (animator != null)
        {
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetFloat("yVelocity", rb.linearVelocity.y);
            animator.SetBool("move", Mathf.Abs(moveX) > 0.01f);
        }
    }

    void FixedUpdate()
    {
        // 벽 슬라이딩 여부에 따라 마찰 제거/복원 (마찰이 있으면 슬라이딩이 막힘)
        bool isWallSlidingNow = isOnWall && !isGrounded && !isSlamming && wallJumpTimer <= 0f && isControlled &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));
        if (col != null)
            col.sharedMaterial = isWallSlidingNow ? noFrictionMat : originalMat;

        // 준비 중: 완전 고정
        if (isDashReady)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 대시 중: 속도 유지, 중력 없음 (gravityScale=0은 FissionDash에서 설정됨)
        if (isDashing) return;

        // 던져진 분열체: 물리에 맡기되 낙하 중력 배수 적용
        if (thrownTimer > 0f)
        {
            rb.gravityScale = 1f;
            if (rb.linearVelocity.y < 0)
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
            return;
        }

        // 비조종 분열체: 중력은 유지, x이동만 막음
        if (!isControlled)
        {
            rb.gravityScale = 1f;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 조종 중인 캐릭터
        rb.gravityScale = 1f;

        // 벽점프 직후엔 x입력 무시 (튕겨나가는 효과 유지)
        if (wallJumpTimer > 0f)
        {
            wallJumpTimer -= Time.fixedDeltaTime;
        }
        else
        {
            rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);
        }

        // 벽 슬라이딩: 공중에서 벽 방향으로 누르고 있을 때만 (내려찍기 중엔 제외)
        bool isWallSliding = isOnWall && !isGrounded && !isSlamming && wallJumpTimer <= 0f &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));
        if (isWallSliding)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(0f, -wallSlideSpeed);
            return;
        }

        // 낙하 중력 배수
        if (rb.linearVelocity.y < 0)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
    }

    void Fission()
    {
        if (playerPrefab == null) return;
        GameObject clone = Instantiate(playerPrefab, transform.position, Quaternion.identity);
        clone.transform.localScale *= 0.5f;
        clone.GetComponent<SpriteRenderer>().color = Color.green;
        UnityEngine.Debug.Log("분열체 생성됨! (조작하려면 숫자키를 누르세요)");
    }

    void FissionDash()
    {
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -Camera.main.transform.position.z; // 카메라~월드 거리
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = 0f;
        Vector2 dashDir = ((Vector2)(mouseWorld - transform.position)).normalized;

        // 분열체를 원래 위치에 남기고
        if (playerPrefab != null)
        {
            GameObject clone = Instantiate(playerPrefab, transform.position, Quaternion.identity);
            clone.transform.localScale *= 0.5f;
            clone.GetComponent<SpriteRenderer>().color = Color.green;
        }

        // 본체가 마우스 방향으로 대시
        rb.gravityScale = 0f;
        rb.linearVelocity = dashDir * fissionDashSpeed;
        isDashing = true;
        dashTimer = dashDuration;

        UnityEngine.Debug.Log("분열 대시!");
    }

    void TryConsume()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, consumeRange, monsterMask);
        if (hit == null) return;

        MonsterBase monster = hit.GetComponent<MonsterBase>();
        if (monster != null)
            StartCoroutine(ConsumeRoutine(hit.gameObject));
    }

    private bool isConsuming = false;

    System.Collections.IEnumerator ConsumeRoutine(GameObject target)
    {
        if (isConsuming) yield break;
        isConsuming = true;

        // 몬스터 방향으로 살짝 이동
        Vector2 dir = ((Vector2)(target.transform.position - transform.position)).normalized;
        Vector2 startPos = transform.position;
        Vector2 targetPos = startPos + dir * 0.4f;

        float moveTime = 0.1f;
        float t = 0f;
        while (t < moveTime)
        {
            transform.position = Vector2.Lerp(startPos, targetPos, t / moveTime);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        // 커졌다 원래대로 (0.2초)
        Vector3 originalScale = transform.localScale;
        Vector3 bigScale = originalScale * 1.4f;

        float growTime = 0.1f;
        t = 0f;
        while (t < growTime)
        {
            transform.localScale = Vector3.Lerp(originalScale, bigScale, t / growTime);
            t += Time.deltaTime;
            yield return null;
        }

        if (target != null) Destroy(target);

        float shrinkTime = 0.1f;
        t = 0f;
        while (t < shrinkTime)
        {
            transform.localScale = Vector3.Lerp(bigScale, originalScale, t / shrinkTime);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localScale = originalScale;

        isConsuming = false;
        UnityEngine.Debug.Log("섭취!");
    }

    void Slam()
    {
        isSlamming = true;
        rb.linearVelocity = new Vector2(0f, -slamForce);
        UnityEngine.Debug.Log("내려찍기!");
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            jumpsLeft = maxJumps;
            isSlamming = false;
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
