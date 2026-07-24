using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerControll : MonoBehaviour
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

    [Header("지면 감지")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
    public LayerMask groundMask;

    [Header("제어")]
    public bool isControlled = false;

    [HideInInspector] public float thrownTimer = 0f;

    private Rigidbody2D rb;
    private SpriteRenderer spr;
    private Animator animator;

    private PlayerFission playerFission;
    private PlayerDashEat playerDashEat;

    private float moveX;
    private bool isGrounded;
    private bool wasGrounded;
    private float jumpBufferTimer;
    private int jumpsLeft;
    private bool isSlamming;

    public Rigidbody2D RB => rb;
    public float MoveX => moveX;
    public bool IsGrounded => isGrounded;
    public float YVelocity => rb != null ? rb.linearVelocity.y : 0f;
    public bool IsMoving => Mathf.Abs(moveX) > 0.01f;
    public bool IsControlled => isControlled;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;

        spr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        playerFission = GetComponent<PlayerFission>();
        playerDashEat = GetComponent<PlayerDashEat>();

        jumpsLeft = maxJumps;

        if (PlayerControlManager.Instance != null)
            PlayerControlManager.Instance.RegisterPlayer(this);
    }

    void OnDestroy()
    {
        if (PlayerControlManager.Instance != null)
            PlayerControlManager.Instance.UnregisterPlayer(this);
    }

    void Update()
    {
        if (thrownTimer > 0f)
            thrownTimer -= Time.deltaTime;

        CheckGround();

        if (!isControlled)
        {
            moveX = 0f;
            UpdateAnimator();
            return;
        }

        moveX = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBuffer;

        if (Input.GetKey(KeyCode.S) && Input.GetButtonDown("Jump") && !isGrounded && !isSlamming && !IsActionLocked())
            Slam();

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        if (jumpBufferTimer > 0f && jumpsLeft > 0 && !IsActionLocked())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
            jumpsLeft--;
            isGrounded = false;
            jumpBufferTimer = 0f;
        }

        if (spr != null && Mathf.Abs(moveX) > 0.01f)
            spr.flipX = (moveX < 0f);

        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (IsFullyLocked())
            return;

        if (thrownTimer > 0f)
        {
            rb.gravityScale = 1f;

            if (rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
            }
            return;
        }

        if (!isControlled)
        {
            rb.gravityScale = 1f;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
            }
            return;
        }

        rb.gravityScale = 1f;
        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !Input.GetButton("Jump"))
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    void CheckGround()
    {
        Vector3 checkPos = groundCheck != null ? groundCheck.position : transform.position;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundMask);

        if (!wasGrounded && isGrounded)
        {
            jumpsLeft = maxJumps;
            isSlamming = false;

            if (playerDashEat != null)
                playerDashEat.ResetAirDash();
        }

        wasGrounded = isGrounded;
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("yVelocity", rb != null ? rb.linearVelocity.y : 0f);
        animator.SetBool("move", Mathf.Abs(moveX) > 0.01f);
    }

    bool IsActionLocked()
    {
        if (playerFission != null && (playerFission.IsDashReady || playerFission.IsFissionDashing))
            return true;

        if (playerDashEat != null && playerDashEat.IsDashing)
            return true;

        return false;
    }

    bool IsFullyLocked()
    {
        if (playerFission != null && playerFission.IsDashReady)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            return true;
        }

        if (playerFission != null && playerFission.IsFissionDashing)
            return true;

        if (playerDashEat != null && playerDashEat.IsDashing)
            return true;

        return false;
    }

    void Slam()
    {
        isSlamming = true;
        rb.linearVelocity = new Vector2(0f, -slamForce);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}