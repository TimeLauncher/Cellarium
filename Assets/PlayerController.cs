using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("설정값")]
    public float moveSpeed = 4.5f; 
    public float jumpPower = 10f;
    public bool isControlled = false;

    [Header("분열 시스템")]
    public GameObject playerPrefab;

    private Rigidbody2D rb;
    private SpriteRenderer spr;
    private bool isGround;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spr = GetComponent<SpriteRenderer>();

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.RegisterPlayer(this);
        }
    }

    void OnDestroy()
    {
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.UnregisterPlayer(this);
        }
    }

    void Update()
    {
        if (isControlled)
        {
            Move();
            Jump();
            Fission();
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    void Move()
    {
        float xInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(xInput * moveSpeed, rb.linearVelocity.y);
        if (xInput != 0) spr.flipX = (xInput < 0);
    }

    void Jump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGround)
        {
            rb.AddForce(Vector2.up * jumpPower, ForceMode2D.Impulse);
            isGround = false;
        }
    }

    void Fission()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (playerPrefab == null) return;

            // 1. 생성
            GameObject clone = Instantiate(playerPrefab, transform.position, Quaternion.identity);

            // 2. 시각적 구분
            clone.GetComponent<SpriteRenderer>().color = Color.green;

            UnityEngine.Debug.Log("분열체 생성됨! (조작하려면 숫자키를 누르세요)");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground")) isGround = true;
    }
}