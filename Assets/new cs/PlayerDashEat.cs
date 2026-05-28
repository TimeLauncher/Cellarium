using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerControll))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDashEat : MonoBehaviour
{
    public enum DashType
    {
        None,
        Normal,
        Eat
    }

    [Header("일반 대시")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.5f;
    public float dashExitPreserve = 0.35f;

    [Header("공중 대시")]
    public bool allowAirDash = true;
    public int maxAirDash = 1;

    [Header("섭취")]
    public float eatRange = 3f;
    public float eatDashSpeed = 25f;
    public float eatHitDistance = 0.5f;
    public float eatMaxDuration = 0.35f;
    public LayerMask monsterLayer;

    [Header("기능 제한")]
    [SerializeField] private bool canEat = true;

    private Rigidbody2D rb;
    private PlayerControll playerControll;
    private PlayerFission playerFission;
    private Collider2D[] playerColliders;

    private bool isDashing;
    private float dashCooldownTimer;
    private int airDashLeft;

    private Vector2 currentDashDirection;
    private DashType currentDashType = DashType.None;

    public bool IsDashing => isDashing;
    public Vector2 DashDirection => currentDashDirection;
    public DashType CurrentDashType => currentDashType;
    public bool CanEat => canEat;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerControll = GetComponent<PlayerControll>();
        playerFission = GetComponent<PlayerFission>();
        playerColliders = GetComponents<Collider2D>();

        airDashLeft = maxAirDash;
    }

    void Update()
    {
        if (!playerControll.IsControlled)
            return;

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (playerFission != null && (playerFission.IsDashReady || playerFission.IsFissionDashing))
            return;

        if (Input.GetMouseButtonDown(0))
            TryDashOrEat();
    }

    public void ResetAirDash()
    {
        airDashLeft = maxAirDash;
    }

    public void SetCanEat(bool value)
    {
        canEat = value;
    }

    void TryDashOrEat()
    {
        if (isDashing) return;
        if (dashCooldownTimer > 0f) return;

        if (!canEat)
        {
            TryDash();
            return;
        }

        Transform target = GetMouseTarget();

        if (target != null)
        {
            StartCoroutine(EatRoutine(target));
            return;
        }

        TryDash();
    }

    Vector3 GetMouseWorld(Camera cam)
    {
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = Mathf.Abs(cam.transform.position.z - transform.position.z);

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = transform.position.z;
        return mouseWorld;
    }

    Transform GetMouseTarget()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        Vector3 mouseWorld = GetMouseWorld(cam);

        Collider2D hit = Physics2D.OverlapPoint(mouseWorld, monsterLayer);
        if (hit == null) return null;

        float dist = Vector2.Distance(transform.position, hit.transform.position);
        if (dist > eatRange) return null;

        return hit.transform;
    }

    void TryDash()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorld = GetMouseWorld(cam);
        Vector2 dashDir = ((Vector2)(mouseWorld - transform.position)).normalized;

        if (dashDir.sqrMagnitude < 0.001f)
            return;

        bool consumeAirDash = false;

        if (!playerControll.IsGrounded)
            consumeAirDash = true;
        else if (dashDir.y > 0.1f)
            consumeAirDash = true;

        if (consumeAirDash)
        {
            if (!allowAirDash) return;
            if (airDashLeft <= 0) return;
            airDashLeft--;
        }

        currentDashDirection = dashDir;
        StartCoroutine(DashRoutine(dashDir));
    }

    IEnumerator DashRoutine(Vector2 dashDir)
    {
        isDashing = true;
        currentDashType = DashType.Normal;
        dashCooldownTimer = dashCooldown;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        Vector2 dashVelocity = dashDir * dashSpeed;
        if (dashDir.y > 0.1f)
            dashVelocity *= 0.8f;

        float timer = 0f;
        while (timer < dashDuration)
        {
            rb.linearVelocity = dashVelocity;
            timer += Time.deltaTime;
            yield return null;
        }

        rb.gravityScale = originalGravity;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * dashExitPreserve, rb.linearVelocity.y);

        currentDashDirection = Vector2.zero;
        isDashing = false;
        currentDashType = DashType.None;
    }

    IEnumerator EatRoutine(Transform target)
    {
        if (target == null) yield break;

        isDashing = true;
        currentDashType = DashType.Eat;
        dashCooldownTimer = dashCooldown;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        Collider2D[] targetColliders = target.GetComponents<Collider2D>();
        SetCollisionWithTarget(targetColliders, true);

        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            targetRb.linearVelocity = Vector2.zero;
            targetRb.angularVelocity = 0f;
        }

        float timer = 0f;
        Vector2 lastDir = Vector2.zero;

        while (target != null && timer < eatMaxDuration)
        {
            Vector2 toTarget = (Vector2)target.position - rb.position;
            float dist = toTarget.magnitude;

            if (dist <= eatHitDistance)
                break;

            Vector2 dir = toTarget.normalized;
            lastDir = dir;
            currentDashDirection = dir;
            rb.linearVelocity = dir * eatDashSpeed;

            if (targetRb != null)
                targetRb.linearVelocity = Vector2.zero;

            timer += Time.deltaTime;
            yield return null;
        }

        // 타겟에 너무 겹치거나 너무 붙은 상태로 끝나지 않게 약간 정리
        if (target != null && lastDir != Vector2.zero)
        {
            Vector2 settlePos = (Vector2)target.position - lastDir * Mathf.Max(0.05f, eatHitDistance * 0.5f);
            rb.position = settlePos;
        }

        if (target != null)
            Destroy(target.gameObject);

        SetCollisionWithTarget(targetColliders, false);
        EndEatState(originalGravity);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDashing) return;

        if (collision.gameObject.layer == LayerMask.NameToLayer("monster"))
        {
            StopDashOnHit();
        }
    }

    void StopDashOnHit()
    {
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 1f;

        currentDashDirection = Vector2.zero;
        currentDashType = DashType.None;
        isDashing = false;
    }

    void EndEatState(float gravityToRestore)
    {
        rb.gravityScale = gravityToRestore;
        rb.linearVelocity = Vector2.zero;
        currentDashDirection = Vector2.zero;
        currentDashType = DashType.None;
        isDashing = false;
    }

    void SetCollisionWithTarget(Collider2D[] targetColliders, bool ignore)
    {
        if (targetColliders == null || playerColliders == null) return;

        foreach (var playerCol in playerColliders)
        {
            if (playerCol == null) continue;

            foreach (var targetCol in targetColliders)
            {
                if (targetCol == null) continue;
                Physics2D.IgnoreCollision(playerCol, targetCol, ignore);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, eatRange);
    }
}