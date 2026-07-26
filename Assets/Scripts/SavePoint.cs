using UnityEngine;

// 세이브 포인트: 범위 내 E키 상호작용 시 체력/분열 게이지 최대치 회복 + 분열체 즉시 회수.
// 위치는 static으로 저장해둠 - 사망 시 재시작 연결은 사망 처리 시스템이 아직 없어서 추후 구현
[RequireComponent(typeof(Collider2D))]
public class SavePoint : MonoBehaviour
{
    public static Vector3 LastSavePosition { get; private set; }
    public static bool HasSave { get; private set; }

    [Header("활성화 이미지 교체")]
    [Tooltip("활성화되면 이 스프라이트로 바뀐다 (비우면 아래 색으로 대체)")]
    public Sprite activatedSprite;
    [Tooltip("기본(미활성) 스프라이트. 비우면 시작 시의 스프라이트를 그대로 사용")]
    public Sprite inactiveSprite;
    public Color activatedColor = Color.white; // activatedSprite가 없을 때 대체로 입힐 색
    public bool IsActivated { get; private set; }

    [Header("사용법 안내 이미지")]
    [Tooltip("가까이 가면 위에 뜨는 작은 안내 이미지 (E키 안내 등). 비우면 안내를 안 만든다")]
    public Sprite usageHintSprite;
    [Tooltip("이 거리 안에 들어오면 안내가 페이드 인 된다")]
    public float hintShowRange = 3f;
    public Vector3 hintOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("안내 이미지 크기 (1이면 원본 크기)")]
    public float hintScale = 0.5f;
    [Tooltip("세이브를 찍은 뒤에는 안내를 감춘다")]
    public bool hideHintAfterActivate = true;

    [Tooltip("부활 후에도 '이미 활성화됨'을 기억할 때 쓰는 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    private bool playerInRange;
    private SpriteRenderer spr;
    private WorldTooltip hint;
    private string id;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null && inactiveSprite != null)
            spr.sprite = inactiveSprite; // 기본 스프라이트 지정 시 초기화

        BuildHint();

        // 이미 찍어둔 세이브포인트면 활성 이미지로 시작한다
        id = WorldState.MakeId(this, persistentId);
        if (WorldState.Has(WorldCategory.SavePoint, id))
            ApplyActivatedLook();
    }

    // 안내 이미지를 자식으로 자동 생성한다 (Door의 표시등 자동 생성과 같은 방식).
    // 세이브포인트마다 손으로 오브젝트를 만들지 않아도 되게.
    void BuildHint()
    {
        if (usageHintSprite == null) return;

        GameObject go = new GameObject("UsageHint");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * hintScale;

        SpriteRenderer hintSpr = go.AddComponent<SpriteRenderer>();
        hintSpr.sprite = usageHintSprite;

        hint = go.AddComponent<WorldTooltip>();
        hint.showMode = WorldTooltip.ShowMode.PlayerNear;
        hint.showRange = hintShowRange;
        hint.followTarget = transform; // 세이브포인트를 기준으로 위치를 잡는다
        hint.offset = hintOffset;
    }

    void Update()
    {
        if (!playerInRange || !Input.GetKeyDown(KeyCode.E)) return;
        Activate();
    }

    void Activate()
    {
        LastSavePosition = transform.position;
        HasSave = true;

        // 체크포인트 저장 — 사망 시 이 지점/이 시점의 진행상황으로 되돌아온다 (몬스터/버튼은 씬 리로드로 초기화)
        if (RespawnManager.Instance != null)
            RespawnManager.Instance.SaveCheckpoint(transform.position);

        // 활성화되면 이미지 교체 (한 번만, 이후 계속 활성 이미지 유지)
        if (!IsActivated)
        {
            ApplyActivatedLook();
            WorldState.Record(WorldCategory.SavePoint, id);
        }

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.RecallAllClones();
            if (PlayerManager.Instance.allPlayers.Count > 0)
            {
                PlayerController main = PlayerManager.Instance.allPlayers[0];
                main.RestoreFromConsume(main.maxHp, main.maxFissionGauge);
            }
        }

        Debug.Log("세이브 포인트 활성화!");
    }

    void ApplyActivatedLook()
    {
        IsActivated = true;

        if (spr != null)
        {
            if (activatedSprite != null) spr.sprite = activatedSprite;
            else spr.color = activatedColor;
        }

        if (hideHintAfterActivate && hint != null)
            hint.gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) playerInRange = false;
    }
}
