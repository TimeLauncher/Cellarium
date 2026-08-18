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
    public int maxJumps =1;

    // ★ 예전엔 "착지하면 jumpsLeft를 채우고, jumpsLeft가 남아 있으면 점프" 였다.
    //   그래서 점프를 안 쓰고 발판에서 걸어 나가거나, 대시로 공중에 뜨거나, 그냥 떨어지는 중에도
    //   jumpsLeft가 1로 남아 있어 공중에서 한 번 더 뛸 수 있었다.
    //   이제 발판을 떠나면 coyoteTime 안에서만 점프를 받아준다.
    [Tooltip("켜면 공중에서도 maxJumps만큼 점프할 수 있다(이단점프). 끄면 지면/코요테 시간 안에서만 점프된다")]
    public bool allowAirJump = false;
    [Tooltip("발판을 떠난 직후 이 시간까지는 점프 입력을 받아준다. 0으로 두면 완전히 지면에서만 점프된다")]
    public float coyoteTime = 0.1f;
    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 2f;
    public float ascendMultiplier = 1f;

    // 튜토리얼 구간에서는 아직 안 배운 동작을 잠가둔다. 개발 중엔 인스펙터에서 켜서 그대로 테스트 가능.
    // (씬마다 플레이어가 개별 배치돼 있으므로 씬별로 따로 설정해야 한다)
    [Header("기능 On/Off (튜토리얼 제한용)")]
    public bool allowSlam = true;        // 내려찍기 (S+Space, 공중)
    public bool allowFissionDash = true; // 분열 대시 (우클릭 홀드→뗌)
    // 벽타기 on/off는 아래 "벽타기" 섹션의 allowWallSlide / allowWallJump

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

    // 기획서 기타 메모 '대시 시전 방향으로 모션 재생'.
    // 좌우 반전만으로는 위/아래 대각선 대시가 표현되지 않는데, 방향별 대시 클립은 아직 없다.
    // 클립이 나오기 전까지는 스프라이트를 대시 방향으로 기울여서 방향을 보여준다.
    [Tooltip("대시하는 동안 스프라이트를 대시 방향으로 기울인다. 방향별 대시 클립이 생기면 꺼도 된다")]
    public bool rotateOnDash = true;
    [Range(0f, 90f)]
    [Tooltip("기울일 수 있는 최대 각도. 90이면 마우스 방향을 그대로 본다")]
    public float dashRotationMaxAngle = 70f;
    [Tooltip("스프라이트가 든 자식 오브젝트를 넣으면 그것만 회전시킨다(콜라이더에 영향 0). " +
             "비워두면 몸통 콜라이더가 원형일 때만 본체를 회전시킨다 — 사각 콜라이더를 돌리면 " +
             "지면/벽 판정이 어긋나고 지형에 끼는 사고가 나기 때문")]
    public Transform dashVisualRoot;
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
    [Tooltip("Q를 누른 뒤 이 시간 동안 제자리 고정 + 다른 조작 차단 (분열 모션 길이에 맞출 것). " +
             "분열체가 나오는 시점 자체는 split2 클립의 SpawnFissionClone 애니메이션 이벤트가 정한다")]
    public float fissionMotionLock = 0.4f;
    [Tooltip("사용 안 함 — 예전 '홀드해서 차징' 방식의 잔재. 지금은 Q를 누르는 즉시 발동한다")]
    public float fissionHoldDuration = 0.5f;
    [Tooltip("분열체 크기 배율 (본체 대비)")]
    public float cloneScaleRatio = 0.75f;
    [Tooltip("분열체를 몸 밖에 생성할 때 두는 여유 간격. 0이면 콜라이더가 딱 맞닿은 채로 생성된다")]
    public float fissionSpawnMargin = 0.15f;
    [SerializeField]
private RuntimeAnimatorController cloneAnimatorController;

    [Header("섭취")]
    public float consumeRange = 2f;
    public LayerMask monsterMask;
    [Tooltip("마우스 커서 판정의 여유 반경. 커서가 대상에 정확히 안 올라가도 이 반경 안이면 섭취로 친다. " +
             "0이면 픽셀 단위로 정확히 눌러야 하고, 빗나가면 대시가 나간다")]
    public float consumePickRadius = 0.6f;
    public float consumeMoveSpeed = 15f; // 섭취 대상 위치로 러지하는 속도

    [Header("벽타기")]
    public float wallCheckDistance = 0.3f;
    public float wallSlideSpeed = 1.5f;
    public float wallJumpX = 6f;
    public float wallJumpY = 10f;
    public bool allowWallJump = true;  // 끄면 벽점프(공중 추가 점프)를 막음 — 이단점프 없는 데모에서 유용
    public bool allowWallSlide = true; // 끄면 벽 슬라이딩(벽에 붙어 천천히 내려가기)도 막음
    public LayerMask wallMask; // 현재 미사용 — 맵이 wall/ground로 나뉘어 있지 않아 기하학적 판정(CheckWallSide)을 씀

    [Header("체력")]
    public float maxHp = 100f;
    public float knockbackDuration = 0.25f; // 이 시간 동안은 이동 입력이 넉백 속도를 덮어쓰지 않음
    [Tooltip("넉백으로 떠 있는 동안 낙하 가속(Fall Multiplier)을 빼서 포물선이 보이게 한다. 끄면 튀어오르자마자 바로 처박힌다")]
    public bool knockbackKeepsArc = true;
    public float invincibleDuration = 0.5f; // 피격 후 무적 시간 (경직 시간에 더해짐)

    [Header("분열 게이지")]
    public float maxFissionGauge = 100f;
    public float fissionGaugeRecoverRate = 10f;
    public float fissionCost = 100f;     // Q 분열 1회에 소모하는 게이지 (기획: 게이지 100 = 분열 1회. maxFissionGauge와 같으면 '가득 차야 분열')
    public float fissionDashCost = 100f; // 우클릭 분열 대시에 소모하는 게이지

    [Header("사망/부활")]
    public float deathMotionDuration = 3f;                 // 사망 모션 길이(2~4초). 이 동안 조작 불가·무적
    public Vector3 defaultRespawnPosition = Vector3.zero;  // 세이브포인트가 없을 때 부활 위치 (A00 중앙). 인스펙터에서 설정

    [Header("분열체 회수")]
    // R 회수 / 분열체 사망 시 그냥 사라지지 않고 본체까지 날아와서 흡수된다.
    // 회수가 시작되면 Rigidbody 시뮬레이션을 꺼서 지형·몬스터·그물망 등 모든 장애물을 통과한다.
    public float returnStartSpeed = 6f;                    // 회수 시작 속도
    public float returnMaxSpeed = 22f;                     // 가속 후 최고 속도
    public float returnAcceleration = 45f;                 // 초당 속도 증가량
    public float returnArriveDistance = 0.35f;             // 이 거리 안에 들어오면 흡수 완료
    public float returnTimeout = 4f;                       // 본체가 사라지는 등 도달 실패 시 강제 종료
    [Tooltip("회수 중 재생할 Animator 트리거 이름. 컨트롤러에 해당 파라미터가 없으면 조용히 무시된다")]
    public string returnTriggerName = "ReturnReady";
    public bool shrinkWhileReturning = true;                // 본체에 가까워질수록 작아지는 흡수 연출

    // ★ 예전엔 크기가 그냥 (남은거리 / 출발거리)였다 — 도착할수록 0에 수렴해서, 특히 멀리서
    //   회수하면 날아오는 내내 점처럼 작아 회수 연출 자체가 안 보였다. 아래 두 값으로 조절한다.
    [Range(0f, 1f)]
    [Tooltip("작아지는 정도. 0이면 크기 그대로, 1이면 도착 시 Return Min Scale까지 줄어든다")]
    public float returnShrinkStrength = 1f;
    [Range(0.05f, 1f)]
    [Tooltip("아무리 줄어도 이 배율 아래로는 안 작아진다. 낮출수록 도착할 때 더 작아진다")]
    public float returnMinScale = 0.4f;

    [Header("제어")]
    public bool isControlled = false;
    public bool isClone = false;

    [Header("표시")]
    // 씬의 오브젝트(세이브포인트/문/타일 등)가 전부 Order in Layer 0이라, 같은 순서일 땐 그리는 차례가
    // 사실상 무작위여서 플레이어가 오브젝트 뒤로 가려졌다. 플레이어만 확실히 앞으로 끌어올린다.
    public bool forceSortingOrder = true;
    public int playerSortingOrder = 100;
    [Range(0f, 1f)] public float uncontrolledAlpha = 0.5f; // 조종 중이 아닌 개체의 투명도
    // ★ SpriteRenderer.color는 '곱하기' 틴트다. 흰색(1,1,1)을 넣으면 원본 그대로라 깜빡임이 안 보인다.
    //   예전 필드명(invincibleBlinkColor)은 씬 3개에 전부 흰색으로 박혀 있어서 이름을 바꿨다 —
    //   Unity가 없는 필드의 직렬화 값은 버리므로, 씬을 안 고쳐도 아래 기본값(빨강)이 적용된다.
    [Tooltip("피격 후 무적 동안 깜빡일 색. 스프라이트 색에 곱해지므로 흰색이면 아무 변화가 없다")]
    public Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public float invincibleBlinkInterval = 0.08f;

    [Header("피격 이펙트")]
    [Tooltip("맞은 자리에 터지는 이펙트. Prefab을 비워두면 임시 스파크가 런타임에 만들어진다")]
    public HitEffect.Settings hitEffect = new HitEffect.Settings
    {
        scale = 1.1f,
        lifetime = 0.32f,
        fallbackColor = new Color(1f, 0.35f, 0.35f, 1f),
    };


    [HideInInspector] public float thrownTimer = 0f;

    private Rigidbody2D rb;
    private SpriteRenderer spr;
    private Animator animator;

    private float moveX;
    private bool isGrounded;
    private bool wasGrounded;
    private float jumpBufferTimer;
    private float coyoteTimer;
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
    private float fissionMotionTimer; // 분열 모션 중 제자리 고정이 남은 시간
    private bool isFissioning;            // Q 홀드로 분열을 차징하는 동안 (제자리 고정 + 다른 조작 차단)

    // 섭취
    private bool isConsuming;
    private GameObject pendingConsumeTarget; // 애니메이션 이벤트로 섭취를 실행할 때 대상을 잠시 보관

    // 체력
    private float currentHp;
    private float currentFissionGauge;
    private bool isStunned;
    private bool isInvincible;
    private bool isDead;
    private bool isReturning;   // 본체로 회수되어 날아가는 중 — 입력·물리·피격 전부 정지
    private float knockbackTimer;
    private float dashInvincibleTimer;
    private Color baseColor = Color.white;

    // 연출로 강제 이동하는 방향(-1/0/1). EventTriggerZone의 '끌려가는 연출'이 매 프레임 넣어준다.
    // 조작이 잠긴 동안에도 이 값만은 이동 입력으로 쓴다.
    private float scriptedMoveX;

    // 분열 능력이 해금됐는지 (A02에서 획득 전까지 잠김). 매니저가 없으면 개발 편의상 허용
    bool FissionUnlocked => PlayerManager.Instance == null || PlayerManager.Instance.fissionUnlocked;

    // Animator에 실제 컨트롤러가 연결돼 있는지 — 없으면 애니메이션 이벤트 대신 즉시 실행
    // (섭취/분열이 애니메이션 클립·이벤트가 아직 없어도 정상 동작하도록. 몬스터 히트박스와 같은 패턴)
    bool HasAnimatorController => animator != null && animator.runtimeAnimatorController != null;

    public float CurrentHp => currentHp;
    public bool IsReturning => isReturning;
    public float CurrentFissionGauge => currentFissionGauge;
    public float MaxFissionGauge => maxFissionGauge;

    // 맵에 나와 있는 분열체 수 (본체 제외)
    public int CloneCount =>
        PlayerManager.Instance == null ? 0 : Mathf.Max(0, PlayerManager.Instance.allPlayers.Count - 1);

    // 지금 회복으로 도달할 수 있는 분열 게이지 상한.
    //
    // ★ 기획: "분열 가능 횟수 소모 1당 게이지 100 회복 불가" — 분열체가 살아 있는 동안 그 100은 잠긴다.
    //   (Bug Report '분열 이후 섭취 시 분열 게이지 버그' = 섭취가 이 잠금을 무시하고 꽉 채우던 문제)
    //   예전 코드는 자동회복만 '분열체가 있으면 아예 중단'으로 근사하고, 섭취는 그냥 최대치까지 채웠다.
    //   이제 자동회복·섭취가 같은 상한을 공유한다. 분열체를 회수하면 상한이 도로 올라간다.
    public float FissionGaugeCap => Mathf.Max(0f, maxFissionGauge - CloneCount * fissionCost);
    // 분열 모션 진행도 (0→1). 홀드 차징이 없어졌으므로 이제 '모션이 도는 동안'을 나타낸다.
    // FissionChargeIndicator가 이 값을 쓰는데, 즉시 발동이라 차지 게이지로서의 의미는 사라졌다.
    public float FissionHoldProgress =>
        fissionMotionTimer > 0f && fissionMotionLock > 0f ? 1f - (fissionMotionTimer / fissionMotionLock) : 0f;
    public float FissionDashHoldProgress => isDashReady ? Mathf.Clamp01(fissionDashHoldTimer / fissionDashHoldDuration) : 0f;
    public float DashCooldownProgress => normalDashCooldownTimer > 0f ? normalDashCooldownTimer / dashCooldown : 0f;

    // 벽타기
    private bool isOnWall;
    private bool wasOnWall;
    private int wallDir;
    private float wallJumpTimer;
    private int lastWallJumpDir;

    private Collider2D col;                     // 몸통(비트리거) 콜라이더 대표 — 지면/벽 판정 기준
    private List<Collider2D> bodyColliders;     // 몸통 콜라이더 전부 (관통 발판 하강 시 모두 무시해야 함)
    private PhysicsMaterial2D noFrictionMat;
    private PhysicsMaterial2D originalMat;
    private OneWayPlatformTile currentOneWayPlatform;

    private PlayerController returnBodyTarget;
    private Vector3 returnBaseScale = Vector3.one; // 회수 시작 시점(준비 애니메이션 전)의 원래 크기
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 몸통 콜라이더(트리거가 아닌 것)만 골라낸다.
        // 씬에 따라 플레이어에 피격범위용 트리거 BoxCollider2D가 함께 붙어 있는데,
        // GetComponent<Collider2D>()는 컴포넌트 순서상 그 트리거를 집어올 수 있다.
        // 트리거를 잡으면 지면/벽 판정 기준이 어긋나고, 관통 발판 하강(IgnoreCollision)이
        // 실제 물리 충돌과 상관없는 콜라이더에 걸려 아무 효과가 없다.
        bodyColliders = new List<Collider2D>();
        foreach (Collider2D c in GetComponents<Collider2D>())
            if (!c.isTrigger) bodyColliders.Add(c);

        col = bodyColliders.Count > 0 ? bodyColliders[0] : GetComponent<Collider2D>();
        originalMat = col != null ? col.sharedMaterial : null;
        noFrictionMat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
    }

    void Start()
    {
        spr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (spr != null) baseColor = spr.color; // 스프라이트 원래 색 보존 (강제로 흰색/초록으로 덮어쓰지 않기 위함)
        if (spr != null && forceSortingOrder) spr.sortingOrder = playerSortingOrder;
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
        if (PauseMenu.IsPaused)return;
        if (isDead) return; // 사망 모션 중엔 입력·물리 판정 모두 정지
        if (isReturning) return; // 회수 비행 중엔 ReturnRoutine이 위치를 직접 옮긴다

        if (thrownTimer > 0f)
            thrownTimer -= Time.deltaTime;

        if (dashInvincibleTimer > 0f)
            dashInvincibleTimer -= Time.deltaTime;

        // 지면 감지 — groundCheck가 비어있으면 콜라이더 발밑을 기준점으로 사용 (중앙에서 재면 거의 항상 false가 됨)
        Vector3 checkPos = groundCheck != null ? groundCheck.position
            : (col != null ? new Vector3(col.bounds.center.x, col.bounds.min.y, transform.position.z) : transform.position);
        bool overlapGrounded = CheckGrounded(checkPos);
        bool contactGrounded = HasGroundContact();

        isGrounded = overlapGrounded || contactGrounded;
        if (!wasGrounded && isGrounded)
        {
            jumpsLeft = maxJumps;
            lastWallJumpDir = 0;
            airDashLeft = maxAirDash;
        }
        wasGrounded = isGrounded;

        // 지면에 닿아 있는 동안은 계속 채워지고, 떠나는 순간부터 줄어든다.
        // 이 값이 0이 되면 (allowAirJump가 꺼져 있는 한) 더 이상 점프할 수 없다.
        if (isGrounded) coyoteTimer = coyoteTime;
        else if (coyoteTimer > 0f) coyoteTimer -= Time.deltaTime;

        // 벽 감지 (레이어 무관 — 맵이 wall/ground로 나뉘어 있지 않아 기하학적으로 판정)
        // 슬라이딩·벽점프가 둘 다 꺼져 있으면 벽에 붙는 판정 자체를 하지 않는다 (레이캐스트도 아낌)
        if (!allowWallSlide && !allowWallJump)
        {
            isOnWall = false; wallDir = 0; lastWallJumpDir = 0;
        }
        else if (CheckWallSide(1))  { isOnWall = true; wallDir =  1; }
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

        // 분열 게이지 자동 회복 (본체만). 분열체 1개당 100씩 잠기므로 FissionGaugeCap까지만 찬다.
        // 이미 상한을 넘겨 들고 있는 경우(분열 직후 등)엔 깎지 않고 그대로 둔다 — 회복만 막는 규칙이다.
        // 분열게이지 회복 영역(FissionRechargeZone) 안에 있으면 그 배수만큼 빨리 찬다.
        if (!isClone && currentFissionGauge < FissionGaugeCap)
        {
            float recoverRate = fissionGaugeRecoverRate * FissionRechargeZone.MultiplierAt(transform.position);
            currentFissionGauge = Mathf.Min(FissionGaugeCap,
                currentFissionGauge + recoverRate * Time.deltaTime);
        }
        // 조종 여부와 상관없이 애니메이션 상태는 계속 갱신
        if (animator != null)
        {
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetFloat("yVelocity", rb.linearVelocity.y);

            // 비조종 분열체는 이동 모션이 나오지 않도록 처리
            animator.SetBool(
                "move",
                isControlled && Mathf.Abs(moveX) > 0.01f
            );

            animator.SetBool("isDashing", isNormalDashing);
        }

        // 조종 대상이 아니거나, 대화·연출로 조작이 잠겨 있으면 입력 처리를 통째로 건너뛴다.
        // 위쪽 애니메이터 갱신은 이미 끝났고 FixedUpdate의 중력도 계속 돌기 때문에,
        // 잠긴 채로 공중에 있으면 정상적으로 착지한다 (moveX=0이라 좌우로만 멈춘다).
        if (!isControlled || PlayerInputLock.IsLocked)
        {
            // 연출로 강제 이동 중이면(EventTriggerZone) 그 방향으로 계속 걷는다.
            // 여기서 0으로 두면 FixedUpdate가 매 프레임 속도를 지워서 아예 못 움직인다.
            moveX = isControlled ? scriptedMoveX : 0f;

            if (spr != null && Mathf.Abs(moveX) > 0.01f)
                spr.flipX = moveX < 0f;

            return;
        }

        scriptedMoveX = 0f; // 조작이 풀리면 연출 이동은 남기지 않는다

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
                currentOneWayPlatform.DropThrough(bodyColliders);
            }
            else if (allowSlam && !isGrounded && !isSlamming)
            {
                jumpBufferTimer = 0f;
                Slam();
            }
        }

        // 분열 (Q를 누르는 즉시 발동, 분열체/분열대시 중 불가, 분열 능력 해금 전엔 불가)
        // - 준비동작은 split2 애니메이션 자체가 담당한다. 분열체가 나오는 타이밍은
        //   클립의 SpawnFissionClone 애니메이션 이벤트 위치로 조절한다 (코드 타이머 아님).
        // - 모션이 끝날 때까지(fissionMotionLock) 제자리 고정 + 다른 조작 차단.
        if (fissionMotionTimer > 0f) fissionMotionTimer -= Time.deltaTime;
        isFissioning = fissionMotionTimer > 0f;

        if (!isClone && !isFissionDashing && FissionUnlocked && Input.GetKeyDown(KeyCode.Q) && !isFissioning)
        {
            if (Fission())
            {
                fissionMotionTimer = fissionMotionLock;
                isFissioning = true; // 누른 프레임부터 바로 고정
            }
        }

        // 차징 중엔 이동 입력을 무시해 제자리에 고정 (점프/대시 등은 IsActionLocked로 차단됨)
        if (isFissioning)
            moveX = 0f;

        // 좌클릭: 마우스 커서 위치 몬스터 있으면 섭취, 없으면 일반 대시
        if (Input.GetMouseButtonDown(0) && !IsActionLocked())
            TryDashOrEat();

        // 분열 대시 준비: 우클릭 누르면 (일반 대시 중엔 불가, 분열 능력 해금 전엔 불가, allowFissionDash로 잠글 수 있음)
        if (allowFissionDash && Input.GetMouseButtonDown(1) && !isNormalDashing && !isClone && FissionUnlocked)
        {
            isFissionDashing = false;
            isDashReady = true;
            fissionDashHoldTimer = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 1f;
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
            if (allowWallJump && isWallSliding && wallDir != lastWallJumpDir && !isClone)
            {
                rb.linearVelocity = new Vector2(-wallDir * wallJumpX, wallJumpY);
                jumpsLeft = maxJumps - 1;
                lastWallJumpDir = wallDir;
                wallJumpTimer = 0.25f;
                coyoteTimer = 0f;
                if (animator != null) animator.Play("jumpstart", 0, 0f);
                jumpBufferTimer = 0f;
            }
            // 공중 점프 차단: 지면에 있거나 방금 떠난 직후(코요테)에만 점프가 나간다.
            // allowAirJump를 켜면 예전처럼 jumpsLeft만 보고 공중에서도 뛴다.
            else if (jumpsLeft > 0 && !isSlamming && (allowAirJump || isGrounded || coyoteTimer > 0f))
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
                jumpsLeft--;
                coyoteTimer = 0f; // 한 번 뛰면 코요테 시간은 소진 — 뜨자마자 또 뛰는 것 방지
                if (animator != null) animator.Play("jumpstart", 0, 0f);
                jumpBufferTimer = 0f;
            }
        }

        if (
    spr != null &&
    !isNormalDashing &&
    !isConsuming &&
    Mathf.Abs(moveX) > 0.01f
)
        {
            spr.flipX = moveX < 0f;
        }


    }

    // 색 처리는 한 곳에서만 — 조종 여부는 투명도로, 무적은 깜빡임으로 표현한다.
    // (예전엔 PlayerManager가 흰색/초록으로 직접 칠해서 스프라이트 원래 색이 날아갔음)
    void LateUpdate()
    {
        if (spr == null) return;

        Color c = baseColor;

        // 사망 모션 중엔 깜빡이지 않는다 — DeathRoutine이 추가 피격을 막으려고 isInvincible을 켜두는데,
        // 그것까지 깜빡이면 사망 애니메이션 내내 3초를 빨갛게 점멸한다.
        if (isInvincible && !isDead &&
            Mathf.FloorToInt(Time.time / Mathf.Max(0.01f, invincibleBlinkInterval)) % 2 == 0)
            c = hitFlashColor;

        // 회수 비행은 연출이 보여야 하므로 '조종 안 함' 반투명 처리에서 제외한다
        if (!isControlled && !isReturning)
            c.a = baseColor.a * uncontrolledAlpha;

        spr.color = c;
    }

    void FixedUpdate()
    {
        if (isDead) { rb.linearVelocity = Vector2.zero; return; } // 사망 모션 중 완전 정지
        if (isReturning) return; // 회수 중엔 rb.simulated = false 라 물리 갱신 자체가 무의미

        // 공중에 있는 동안은 마찰을 0으로 둔다.
        // 마찰이 남아 있으면 벽 슬라이딩을 꺼놔도 벽 방향 키를 누르는 것만으로 벽에 매달린다
        // (좌우 이동은 velocity로 직접 제어하므로 공중에서 마찰이 필요한 곳이 없다).
        // 지상에선 원래 머티리얼로 되돌려 경사면에 서 있는 동작을 유지한다.
        SetBodyMaterial(isGrounded ? originalMat : noFrictionMat);

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
        bool isWallSliding = allowWallSlide && isOnWall && !isGrounded && !isSlamming && wallJumpTimer <= 0f && !isClone &&
            ((wallDir == 1 && moveX > 0) || (wallDir == -1 && moveX < 0));
        if (isWallSliding)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(0f, -wallSlideSpeed);
            return;
        }

        // 넉백으로 떠 있는 동안은 낙하 가속(fallMultiplier)을 빼준다.
        // 이 값이 크면(A01은 8) 위로 띄운 넉백이 정점을 찍자마자 바닥으로 처박혀 포물선이 안 보인다.
        if (knockbackTimer > 0f && knockbackKeepsArc)
        {
            // 넉백 중엔 기본 중력만 적용 — 아무것도 더하지 않는다
        }
        else if (rb.linearVelocity.y < 0)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0 && ascendMultiplier > 1f)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (ascendMultiplier - 1) * Time.fixedDeltaTime;
    }

    // 몸통 콜라이더가 여러 개일 수 있으므로 머티리얼은 전부에 적용해야 한다
    // (하나라도 마찰이 남아 있으면 그 콜라이더가 벽에 걸려 매달린다)
    void SetBodyMaterial(PhysicsMaterial2D mat)
    {
        if (bodyColliders == null) return;
        foreach (Collider2D c in bodyColliders)
            if (c != null && c.sharedMaterial != mat) c.sharedMaterial = mat;
    }

    // 대시/섭취/분열 대시/경직 중엔 점프·내려찍기·대시 추가 입력 차단
    bool IsActionLocked()
    {
        return isDashReady || isFissionDashing || isNormalDashing || isConsuming || isStunned || isFissioning;
    }

    // ── 일반 대시 / 섭취 ───────────────────────────────────────────

    void TryDashOrEat()
    {
        // ★ 쿨다운 검사를 여기서 하면 안 된다 (기획서 (8) '대시 사용 후 대시 쿨타임 동안 섭취가
        //   사용되지 않는 문제'). 좌클릭 하나로 섭취와 대시를 겸하는 구조라, 함수 앞에서 막으면
        //   섭취 판정까지 같이 죽는다. 섭취는 대시 쿨타임의 영향을 받지 않는다.
        Transform target = GetMouseTarget();
        if (target != null)
        {
            StartConsumeAnimation(target.gameObject);
            return;
        }

        if (normalDashCooldownTimer > 0f) return;

        TryNormalDash();
    }

    // 섭취 애니메이션 시작 — 실제 섭취는 애니메이션 이벤트(ExecuteConsume)에서 실행.
    // 애니메이터 컨트롤러가 없으면(아직 애니 안 붙음) 즉시 실행해 애니 없이도 동작하게 한다.
    void StartConsumeAnimation(GameObject target)
    {
        if (isConsuming || target == null) return;

        isConsuming = true;
        pendingConsumeTarget = target;
        rb.linearVelocity = Vector2.zero;

        // 섭취 대상 방향으로 스프라이트 전환
        float targetDirectionX = target.transform.position.x - transform.position.x;

        if (spr != null && Mathf.Abs(targetDirectionX) > 0.01f)
        {
            spr.flipX = targetDirectionX < 0f;
        }

        if (animator != null)
        {
            animator.SetTrigger("Consume");
        }

        if (!HasAnimatorController)
        {
            ExecuteConsume();
        }
    }

    // 섭취 애니메이션의 Animation Event에서 호출 (애니 없으면 StartConsumeAnimation이 직접 호출)
    public void ExecuteConsume()
    {
        if (pendingConsumeTarget == null) { EndConsume(); return; }
        StartCoroutine(ConsumeRoutine(pendingConsumeTarget));
    }

    // 섭취 마무리(대상 정리) — 코루틴 종료 시 호출
    public void EndConsume()
    {
        pendingConsumeTarget = null;
        isConsuming = false;
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

    // 마우스 커서가 가리키는 섭취 대상 반환 (사정거리 밖이거나 섭취 불가면 null)
    Transform GetMouseTarget()
    {
        Vector2 mouse = GetMouseWorld();

        // ★ 예전엔 OverlapPoint(점 판정)였다 — 커서가 픽셀 단위로 정확히 대상 위에 있어야만 섭취가 되고,
        //   조금만 빗나가면 그대로 대시가 나가 시체에 몸을 처박았다. 기획서 (8) '섭취 편의'가
        //   없애려던 문제가 이것이라 여유 반경을 두고 찾는다.
        Collider2D[] hits = Physics2D.OverlapCircleAll(mouse, Mathf.Max(0.01f, consumePickRadius), monsterMask);
        if (hits == null || hits.Length == 0) return null;

        Transform best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            // ★ GetComponentInParent인 이유: 기획서 (8)의 '섭취 전용 원형 콜라이더'는
            //   본체 콜라이더 구성을 건드리지 않으려고 자식 오브젝트에 붙는다(ConsumeIndicator).
            //   자식이 잡혔을 때도 주인(몬스터/회복셀)을 찾아 올라가야 한다.
            IConsumable consumable = hit.GetComponentInParent<IConsumable>();
            if (consumable == null || !consumable.IsConsumable) continue;

            // 거리는 자식 콜라이더가 아니라 주인 위치 기준으로 잰다
            Transform ownerTransform = ((Component)consumable).transform;
            if (Vector2.Distance(transform.position, ownerTransform.position) > consumeRange) continue;

            // 커서에 가장 가까운 대상을 고른다 (여러 마리가 겹쳐 있을 때)
            float d = Vector2.Distance(mouse, ownerTransform.position);
            if (d < bestDistance) { bestDistance = d; best = ownerTransform; }
        }

        return best;
    }

    // 섭취로 얻는 회복량 적용 (몬스터/회복셀 등 IConsumable.OnConsumed에서 호출)
    public void RestoreFromConsume(float hpAmount, float gaugeAmount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + hpAmount);

        // 분열체가 나와 있으면 그 수 × 100은 잠겨 있어서 섭취로도 못 채운다 (자동회복과 같은 상한).
        // 이미 상한 위에 있으면 그대로 유지 — 섭취했다고 오히려 깎이면 안 된다.
        currentFissionGauge = Mathf.Max(currentFissionGauge,
            Mathf.Min(FissionGaugeCap, currentFissionGauge + gaugeAmount));
    }

    // 씬 이동 후 이전 씬의 상태를 그대로 이어받을 때 사용 (GameProgress).
    // RestoreFromConsume은 '더하기'라서 절대값을 넣는 용도로는 못 쓴다.
    public void SetHp(float value)
    {
        currentHp = Mathf.Clamp(value, 0f, maxHp);
    }

    public void SetFissionGauge(float value)
    {
        currentFissionGauge = Mathf.Clamp(value, 0f, maxFissionGauge);
    }

    // 연출용 강제 이동 (EventTriggerZone의 '끌려가는 연출').
    // 위치를 직접 옮기면 Rigidbody와 싸워서 지형을 뚫거나 떨리므로, 평소 이동 입력 자리에 넣는다.
    // 매 프레임 호출할 것 — 조작 잠금이 풀리는 순간 자동으로 0이 된다.
    public void SetScriptedMove(float dirX)
    {
        scriptedMoveX = Mathf.Clamp(dirX, -1f, 1f);
    }

    public void ClearScriptedMove()
    {
        scriptedMoveX = 0f;
    }

    public void TakeDamage(float amount, Vector2 knockback = default, float stunTime = 0f,
                           DamageSource source = DamageSource.Attack)
    {
        // 사망 모션 중엔 더 이상 피격되지 않음
        if (isDead) return;

        // 회수 비행 중엔 장애물·몬스터를 전부 무시하고 지나가므로 피격도 받지 않는다
        if (isReturning) return;

        // 피격 후 무적 프레임은 출처와 무관하게 모든 피해를 막는다
        if (isInvincible) return;

        // 대시로 몬스터에 박는 동안은 공격 행동이므로 '몸통 접촉' 피해만 받지 않는다.
        // 공격 히트박스·투사체·자폭은 대시 중에도 그대로 맞는다 (기획서 Bug Report 3번).
        if (source == DamageSource.Contact && dashInvincibleTimer > 0f) return;

        // 분열체는 피격 시 사망 (QA (4). 추후 1회 무효화 등 추가 예정)
        // 그 자리에서 사라지지 않고 본체까지 날아와 흡수된다 — 도착 시 Destroy,
        // OnDestroy에서 PlayerManager.UnregisterPlayer가 조종 전환/목록 정리까지 자동 처리
        if (isClone)
        {
            Debug.Log("분열체 피격 — 본체로 회수");
            ReturnToBody();
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - amount);

        // 맞은 자리에 이펙트. 넉백 방향이 곧 '공격이 밀어낸 방향'이라 그대로 쓴다.
        HitEffect.Play(hitEffect,
                       col != null ? col.bounds.center : transform.position,
                       knockback.sqrMagnitude > 0.01f ? knockback.normalized : Vector2.zero,
                       spr);

        if (currentHp > 0f)
        {
            if (animator != null)
                animator.SetTrigger("Hit");
        }
        else
        {
            StartCoroutine(DeathRoutine());
            return;
        }
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

        // 부활: 체크포인트 씬을 리로드해 몬스터/버튼/문 등 배치 오브젝트를 전부 초기 상태로 되돌린다.
        // (플레이어 위치/재화/해금은 RespawnManager가 마지막 세이브포인트 스냅샷으로 복원)
        /* if (RespawnManager.Instance != null)
         {
             RespawnManager.Instance.Respawn();
             yield break; // 씬이 리로드되므로 이 오브젝트는 사라진다
         }*/
        if (ScreenFadeManager.Instance != null)
        {
            yield return ScreenFadeManager.Instance.FadeOutAndRespawn();
            yield break;
        }
        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.Respawn();
            yield break;
        }

        // (폴백) RespawnManager가 없으면 예전처럼 제자리에서 부활 — 위치: 세이브포인트 or 기본 부활 지점
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

    // R 회수 / 분열체 사망의 공통 진입점.
    // 그 자리에서 뿅 사라지지 않고 어디에 있든 본체까지 날아와서 흡수된다.
    // 회수가 시작된 순간 PlayerManager 목록에서 빠지므로 숫자키 조종 대상과
    // 분열 횟수 계산에서 즉시 제외된다 (날아오는 중에 조종되면 안 됨).
    public void ReturnToBody()
    {
        if (isReturning)
            return;

        if (!isClone)
            return;

        PlayerController body = FindBody();

        if (body == null || body == this)
        {
            Destroy(gameObject);
            return;
        }

        isReturning = true;
        isControlled = false;
        returnBodyTarget = body;

        // ★ 준비 애니메이션(ReturnReady)이 크기를 줄이는 클립이면, 비행이 시작될 때 읽는
        //   localScale은 이미 작아진 값이다. 그걸 기준으로 또 줄이면 점처럼 사라진다.
        //   애니메이션이 돌기 전의 원래 크기를 여기서 잡아둔다.
        returnBaseScale = transform.localScale;

        // 회수 시작과 동시에 조종 목록에서 제외
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.UnregisterPlayer(this);

        // 제자리에서 준비 애니메이션을 보여주기 위해 물리 정지
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // 작아지는 준비 애니메이션 실행
        if (HasAnimatorParameter(returnTriggerName))
        {
            Debug.Log(
                $"[회수] Trigger 실행: {returnTriggerName}, " +
                $"Controller: {animator.runtimeAnimatorController.name}"
            );

            animator.ResetTrigger(returnTriggerName);
            animator.SetTrigger(returnTriggerName);
        }
        else
        {
            Debug.LogWarning(
                $"[회수] 현재 Animator에 {returnTriggerName} Trigger가 없음. " +
                $"Controller: {(animator != null && animator.runtimeAnimatorController != null? animator.runtimeAnimatorController.name: "없음")}"
            );

            StartReturnFlight();
        }
    }
    public void StartReturnFlight()
    {
        if (!isReturning)
            return;

        if (returnBodyTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(ReturnRoutine(returnBodyTarget));
    }

    // 본체 = allPlayers 안에서 isClone이 false인 개체
    PlayerController FindBody()
    {
        if (PlayerManager.Instance == null) return null;
        foreach (PlayerController p in PlayerManager.Instance.allPlayers)
            if (p != null && !p.isClone) return p;
        return null;
    }

    // 본체를 향해 직선으로 가속하며 날아간다. 본체가 움직이면 매 프레임 따라간다.
    IEnumerator ReturnRoutine(PlayerController body)
    {
        float speed = returnStartSpeed;
        float elapsed = 0f;
        float startDistance = Mathf.Max(0.01f, Vector2.Distance(transform.position, body.transform.position));

        while (elapsed < returnTimeout)
        {
            if (body == null) break; // 씬 리로드 등으로 본체가 사라지면 더 따라갈 대상이 없다

            Vector3 target = body.transform.position;
            float distance = Vector2.Distance(transform.position, target);
            if (distance <= returnArriveDistance) break;

            speed = Mathf.Min(returnMaxSpeed, speed + returnAcceleration * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (spr != null && Mathf.Abs(target.x - transform.position.x) > 0.01f)
                spr.flipX = target.x < transform.position.x; // flipX = 왼쪽을 봄 (SpawnFissionClone과 같은 규칙)

            if (shrinkWhileReturning)
            {
                // t: 1=출발 지점, 0=본체 도착. 최소 배율 아래로는 내려가지 않고,
                // Strength로 "얼마나 작아질지" 자체를 0~100%로 조절한다.
                float t = Mathf.Clamp01(distance / startDistance);
                float shrink = Mathf.Lerp(returnMinScale, 1f, t);
                transform.localScale = returnBaseScale * Mathf.Lerp(1f, shrink, returnShrinkStrength);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    // 컨트롤러에 그 이름의 파라미터가 실제로 있는지 확인.
    // 없는 트리거를 쏘면 Unity가 매번 경고를 뱉으므로, 회수 애니메이션을 아직
    // 안 붙였어도 코드가 조용히 동작하도록 막아둔다.
    bool HasAnimatorParameter(string paramName)
    {
        if (!HasAnimatorController || string.IsNullOrEmpty(paramName)) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == paramName) return true;
        return false;
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

        // 대시 방향으로 스프라이트 전환
        if (spr != null && Mathf.Abs(dashDir.x) > 0.01f)
        {
            spr.flipX = dashDir.x < 0f;
        }

        // 기획서 기타 메모 '대시 시전 방향으로 모션 재생 되도록 수정'.
        // 좌우 반전은 위에서 끝났고, 위/아래 대각선까지 다른 클립을 쓰려면 애니메이터가
        // 대시 방향을 알아야 한다. 파라미터가 없는 컨트롤러에 넣으면 경고가 나므로
        // 실제로 있는 경우에만 넣는다 (모션이 나오면 이 두 값으로 전이 조건을 걸면 됨).
        if (HasAnimatorParameter("dashDirX")) animator.SetFloat("dashDirX", dashDir.x);
        if (HasAnimatorParameter("dashDirY")) animator.SetFloat("dashDirY", dashDir.y);

        ApplyDashRotation(dashDir);

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
            foreach (var hit in hits)
            {
                MonsterBase monster = hit.collider.GetComponent<MonsterBase>();
                if (monster != null && !hitMonsters.Contains(monster))
                {
                    hitMonsters.Add(monster);
                    monster.TakeDamage(dashAttackDamage, dashDir); // 이펙트가 대시 진행 방향으로 뻗도록 방향도 넘긴다

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

        ClearDashRotation();
        isNormalDashing = false;
    }

    // 대시 방향으로 스프라이트를 기울인다.
    // 좌우는 이미 flipX가 처리했으므로 여기서는 위/아래 기울기만 만든다 —
    // 왼쪽을 볼 땐 스프라이트가 뒤집혀 있어서 같은 각도를 반대 부호로 넣어야 같은 쪽으로 기운다.
    void ApplyDashRotation(Vector2 dashDir)
    {
        Transform pivot = GetDashPivot();
        if (pivot == null) return;

        float tilt = Mathf.Atan2(dashDir.y, Mathf.Abs(dashDir.x)) * Mathf.Rad2Deg;
        tilt = Mathf.Clamp(tilt, -dashRotationMaxAngle, dashRotationMaxAngle);

        if (spr != null && spr.flipX) tilt = -tilt;

        pivot.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    void ClearDashRotation()
    {
        Transform pivot = GetDashPivot();
        if (pivot != null) pivot.localRotation = Quaternion.identity;
    }

    // 회전시켜도 안전한 대상만 돌려준다.
    // ★ 본체를 돌리면 콜라이더까지 같이 돈다. 원형 콜라이더는 돌아도 모양이 그대로라 무해하지만,
    //   사각 콜라이더는 bounds가 커지면서 지면/벽 판정이 어긋나고 지형에 끼는 사고가 난다.
    //   그래서 사각인 경우엔 dashVisualRoot를 따로 지정했을 때만 회전시킨다.
    Transform GetDashPivot()
    {
        if (!rotateOnDash) return null;
        if (dashVisualRoot != null) return dashVisualRoot;
        if (col is CircleCollider2D) return transform;
        return null;
    }

    IEnumerator ConsumeRoutine(GameObject target)
    {
        // isConsuming은 StartConsumeAnimation에서 이미 세팅됨. 대상만 유효성 검사
        if (target == null) { EndConsume(); yield break; }

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

        EndConsume();
        Debug.Log("섭취!");
    }

    // ── 분열 ──────────────────────────────────────────────────────

    // 분열 시작. 실제로 발동했으면 true (게이지 부족 등으로 막히면 false → 모션 잠금도 안 걸림)
    bool Fission()
    {
        if (!FissionUnlocked) return false;
        if (playerPrefab == null) return false;
        if (currentFissionGauge < fissionCost) return false;
        // 최대 분열 횟수 도달 시 차단(튜토리얼 등 하드 캡). 게이지를 소모하기 전에 막아 낭비 방지
        if (PlayerManager.Instance != null && !PlayerManager.Instance.CanSpawnClone()) return false;

        currentFissionGauge -= fissionCost;

        // 여기서 모션이 시작되고, 분열체는 split2 클립의 애니메이션 이벤트가 생성한다
        if (animator != null) animator.SetTrigger("Fission");
        if (!HasAnimatorController) SpawnFissionClone(); // 애니 컨트롤러가 없으면 이벤트가 안 오므로 즉시 생성

        return true;
    }

    // 분열체를 본체 콜라이더 밖에 생성하기 위한 거리.
    // ★ 전엔 0.5로 하드코딩돼 있었는데, 실제 몸 반지름(약 0.48)과 분열체 반지름(약 0.36)을 더하면
    //   0.84가 필요해서 분열체가 본체 안에 겹쳐 생성됐다. 분열체↔본체는 충돌이 살아 있으므로
    //   물리 엔진이 둘을 계속 밀어내고, 본체가 걸어가면 분열체가 같이 밀려가 '따라다니는' 것처럼 보였다.
    float CloneSpawnDistance()
    {
        float half = col != null ? col.bounds.extents.x : 0.5f;
        return half * (1f + cloneScaleRatio) + fissionSpawnMargin;
    }

    // 분열 애니메이션의 Animation Event에서 호출 (애니 없으면 Fission이 직접 호출)
    public void SpawnFissionClone()
    {
        if (playerPrefab == null) return;
        if (PlayerManager.Instance != null && !PlayerManager.Instance.CanSpawnClone()) return; // 하드 캡 도달 시 애니 이벤트 경로도 차단

        float facing = (spr != null && spr.flipX) ? -1f : 1f;
        Vector2 spawnPos = (Vector2)transform.position + Vector2.right * (-facing) * CloneSpawnDistance();
        /*GameObject clone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        clone.transform.localScale *= cloneScaleRatio;
        PlayerController cloneCtrl = clone.GetComponent<PlayerController>();
        if (cloneCtrl != null) cloneCtrl.isClone = true;*/
        GameObject clone =
    Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        clone.transform.localScale *= cloneScaleRatio;

        SetupClone(clone);
        Debug.Log("분열체 생성됨! (조작하려면 숫자키를 누르세요)");
    }

    void FissionDash()
    {
        if (!allowFissionDash) return;
        if (!FissionUnlocked) return;
        if (currentFissionGauge < fissionDashCost) return;
        if (PlayerManager.Instance != null && !PlayerManager.Instance.CanSpawnClone()) return; // 분열 대시도 분열체를 남기므로 하드 캡 적용

        Vector2 dashDir = ((Vector2)(GetMouseWorld() - transform.position)).normalized;
        if (dashDir.sqrMagnitude < 0.001f) return;

        currentFissionGauge -= fissionDashCost;

        // 분열체를 원래 위치(대시 반대방향 약간 오프셋)에 남기고 본체가 마우스 방향으로 대시
        if (playerPrefab != null)
        {
            Vector2 spawnPos = (Vector2)transform.position - dashDir * CloneSpawnDistance();
            /*GameObject clone = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            clone.transform.localScale *= cloneScaleRatio;
            PlayerController cloneCtrl = clone.GetComponent<PlayerController>();
            if (cloneCtrl != null) cloneCtrl.isClone = true;*/
            GameObject clone =
    Instantiate(playerPrefab, spawnPos, Quaternion.identity);

            clone.transform.localScale *= cloneScaleRatio;

            SetupClone(clone);
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = dashDir * fissionDashSpeed;
        isFissionDashing = true;
        fissionDashTimer = fissionDashDuration;

        Debug.Log("분열 대시!");
    }
    private void SetupClone(GameObject clone)
    {
        if (clone == null)
            return;

        PlayerController cloneCtrl =
            clone.GetComponent<PlayerController>();

        if (cloneCtrl != null)
        {
            cloneCtrl.isClone = true;
            cloneCtrl.isControlled = false;
        }

        Animator cloneAnimator =
            clone.GetComponent<Animator>();

        if (
            cloneAnimator != null &&
            cloneAnimatorController != null
        )
        {
            cloneAnimator.runtimeAnimatorController =
                cloneAnimatorController;
        }
    }

    // ── 내려찍기 ──────────────────────────────────────────────────

    void Slam()
    {
        if (!allowSlam) return;
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

    // 지면 접촉 판정 여유값. 감지점이 지면 표면에 딱 붙거나 살짝 파묻혔을 때
    // 최근접점 차이가 0 근처로 나오는데, 그걸 벽으로 오인하지 않게 하는 폭이다.
    const float GroundContactEpsilon = 0.05f;

    bool CheckGrounded(Vector3 checkPos)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius);
        foreach (var h in hits)
        {
            if (h == null || h.isTrigger) continue;
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (!IsGroundCandidate(h.gameObject)) continue;

            // 옆에 붙은 벽을 지면으로 오인하지 않도록, 실제로 닿는 지점이 발밑인지 본다.
            // ★ bounds로 거르면 안 된다(예전 검사를 주석 처리해둔 이유) — 타일맵은 콜라이더 하나가
            //   맵 전체를 덮어서 bounds.max.y가 항상 발보다 위다. 그러면 진짜 지면까지 전부 걸러진다.
            //   ClosestPoint는 합성 콜라이더에서도 실제 표면의 최근접점을 주므로 타일맵에서도 맞다.
            Vector2 delta = h.ClosestPoint(checkPos) - (Vector2)checkPos;
            if (delta.y > GroundContactEpsilon) continue;                              // 발보다 위 → 천장/벽
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) + GroundContactEpsilon) continue; // 옆쪽 → 벽

            return true;
        }
        return false;
    }
    bool HasGroundContact()
    {
        if (bodyColliders == null)
            return false;

        ContactPoint2D[] contacts = new ContactPoint2D[16];

        foreach (Collider2D bodyCollider in bodyColliders)
        {
            if (bodyCollider == null || !bodyCollider.enabled)
                continue;

            int count = bodyCollider.GetContacts(contacts);

            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = contacts[i];

                if (contact.collider == null)
                    continue;

                if (contact.collider.isTrigger)
                    continue;

                if (!IsGroundCandidate(contact.collider.gameObject))
                    continue;

                // 플레이어를 아래에서 받치는 면
                if (contact.normal.y > 0.5f)
                    return true;
            }
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

        // 상승 중(방금 점프)이면 리셋하지 않는다 — 이륙 프레임엔 아직 바닥 접촉이 남아있어
        // 점프로 소모한 jumpsLeft가 곧바로 되돌아가 이단점프가 되던 문제 방지
        if (rb.linearVelocity.y <= 0.1f && IsGroundCandidate(collision.gameObject) && HasUpwardContact(collision))
        {
            isGrounded = true;
            jumpsLeft = maxJumps;
            airDashLeft = maxAirDash;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Enter를 놓치는 경우(무시 해제 직후 등)를 대비해 유지 중에도 갱신
        if (currentOneWayPlatform == null)
        {
            OneWayPlatformTile owp = collision.gameObject.GetComponent<OneWayPlatformTile>();
            if (owp != null) currentOneWayPlatform = owp;
        }

        // 착지 순간 접촉점이 아직 안 잡히는 경우가 있어 유지 중에도 갱신 (벽 위에 올라선 경우 포함)
        // 단, 상승 중(방금 점프)이면 리셋하지 않는다 — 이륙 프레임엔 아직 바닥 접촉이 남아있어
        // 매 프레임 jumpsLeft가 되돌아가 maxJumps=1이어도 이단점프가 되던 문제 방지 (OnCollisionEnter2D와 동일한 가드)
        if (rb.linearVelocity.y <= 0.1f && IsGroundCandidate(collision.gameObject) && HasUpwardContact(collision))
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
