using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("설정값")]
    public float moveSpeed = 5f;
    public float jumpPower = 10f;

    // 현재 이 캐릭터를 조종 중인지 여부
    public bool isControlled = true;

    [Header("분열 시스템")]
    public GameObject playerPrefab; // 여기에 플레이어 프리팹을 꼭 넣어주세요!

    private Rigidbody2D rb;
    private SpriteRenderer spr;
    private bool isGround;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 조종 권한이 있을 때만 움직임 처리
        if (isControlled)
        {
            Move();
            Jump();
            Fission();
        }
        else
        {
            // 조종 권한이 없을 때(분열된 본체 등)는 멈추게 하려면 속도를 0으로 설정
            // (선택 사항: 관성으로 미끄러지는 게 싫으면 주석 해제)
            // rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    void Move()
    {
        float xInput = Input.GetAxisRaw("Horizontal");

        // Unity 6 (6000.0+) 최신 API: linearVelocity 사용
        // 만약 빨간줄이 뜨면 rb.velocity = ... 로 변경하세요.
        rb.linearVelocity = new Vector2(xInput * moveSpeed, rb.linearVelocity.y);

        if (xInput != 0)
        {
            spr.flipX = (xInput < 0);
        }
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
            if (playerPrefab == null)
            {
                Debug.LogError("Player Prefab이 할당되지 않았습니다! 인스펙터를 확인해주세요.");
                return;
            }

            // 1. 분열체 생성 (현재 위치에서 생성)
            GameObject clone = Instantiate(playerPrefab, transform.position, Quaternion.identity);

            // 2. 스크립트 가져오기
            PlayerController cloneScript = clone.GetComponent<PlayerController>();

            if (cloneScript != null)
            {
                // 3. 제어권 넘기기 (본체 -> 끄기, 분열체 -> 켜기)
                this.isControlled = false;      // 나는 멈춤
                cloneScript.isControlled = true; // 분열체가 움직임

                // 4. 시각적 구분 (분열체 초록색)
                clone.GetComponent<SpriteRenderer>().color = Color.green;
                UnityEngine.Debug.Log("분열 성공!");
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 바닥 태그가 "Ground"인지 확인 필요
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGround = true;
        }
    }
}