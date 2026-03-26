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

    private float currentHp;
    private Rigidbody2D rb;
    private SpriteRenderer spr;
    private Transform target;

    public bool IsConsumable => currentHp <= maxHp * consumeThreshold;

    void Start()
    {
        currentHp = maxHp;
        rb = GetComponent<Rigidbody2D>();
        spr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerMask);
        target = hit != null ? hit.transform : null;
    }

    void FixedUpdate()
    {
        if (target == null) return;

        float dir = target.position.x - transform.position.x;
        rb.linearVelocity = new Vector2(Mathf.Sign(dir) * moveSpeed, rb.linearVelocity.y);

        if (spr != null)
            spr.flipX = dir < 0;
    }

    public void TakeDamage(float amount)
    {
        currentHp -= amount;
        if (currentHp <= 0f)
        {
            currentHp = 0f;
            Destroy(gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
