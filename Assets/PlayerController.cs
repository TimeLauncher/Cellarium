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

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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
            jumpsLeft = maxJumps;
        wasGrounded = isGrounded;

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

        // 점프 버퍼
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBuffer;

        // 내려찍기: S + Space, 공중에서만
        if (Input.GetKey(KeyCode.S) && Input.GetButtonDown("Jump") && !isGrounded && !isSlamming)
            Slam();

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

        // 점프 실행
        if (jumpBufferTimer > 0f && jumpsLeft > 0 && !isDashReady)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
            jumpsLeft--;
            if (animator != null) animator.Play("jumpstart", 0, 0f);
            jumpBufferTimer = 0f;
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
        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

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

        // 본체 대시 (직선 이동을 위해 중력 차단)
        rb.gravityScale = 0f;
        rb.linearVelocity = dashDir * fissionDashSpeed;
        isDashing = true;
        dashTimer = dashDuration;

        // 분열체 반대 방향으로 생성 및 발사
        if (playerPrefab != null)
        {
            GameObject clone = Instantiate(playerPrefab, transform.position, Quaternion.identity);
            clone.GetComponent<SpriteRenderer>().color = Color.green;
            PlayerController cloneCtrl = clone.GetComponent<PlayerController>();
            cloneCtrl.thrownTimer = dashDuration; // 이 시간 동안 물리에 맡김
            clone.GetComponent<Rigidbody2D>().linearVelocity = -dashDir * fissionDashSpeed;
        }

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
