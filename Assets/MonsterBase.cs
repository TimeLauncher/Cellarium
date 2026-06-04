using UnityEngine;

public class MonsterBase : MonoBehaviour
{
    [Header("스탯")]
    public float maxHp = 100f;
    public float moveSpeed = 3f;
    [Range(0f, 1f)]
    public float consumeThreshold = 0.25f; // HP 몇 % 이하면 섭취 가능

    [Header("감지")]
    public float detectionRange = 6f;
    public LayerMask playerMask;

    [Header("공격")]
    public float attackRange = 1.5f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;

    private float currentHp;
    private float attackCooldownTimer;
    private Rigidbody2D rb;
    private SpriteRenderer spr;
    private Transform target;

    public bool IsConsumable => currentHp <= 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spr = GetComponent<SpriteRenderer>();
        currentHp = maxHp;
    }

    void Update()
    {
        if (spr != null)
            spr.color = IsConsumable ? Color.yellow : Color.white;

        if (IsConsumable) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerMask);
        target = hit != null ? hit.transform : null;

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (target != null && attackCooldownTimer <= 0f &&
            Vector2.Distance(transform.position, target.position) <= attackRange)
        {
            Attack(target);
            attackCooldownTimer = attackCooldown;
        }
    }

    void FixedUpdate()
    {
        if (target == null || IsConsumable) return;

        float dir = target.position.x - transform.position.x;
        rb.linearVelocity = new Vector2(Mathf.Sign(dir) * moveSpeed, rb.linearVelocity.y);

        if (spr != null)
            spr.flipX = dir < 0;
    }

    void Attack(Transform playerTransform)
    {
        PlayerController pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null)
            pc.TakeDamage(attackDamage);
    }

    public void TakeDamage(float amount)
    {
        if (IsConsumable) return;
        currentHp = Mathf.Max(0f, currentHp - amount);
        if (IsConsumable)
            rb.linearVelocity = Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
