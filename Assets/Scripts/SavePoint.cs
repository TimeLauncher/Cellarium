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

    private bool playerInRange;
    private SpriteRenderer spr;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        spr = GetComponentInChildren<SpriteRenderer>();
        if (spr != null && inactiveSprite != null)
            spr.sprite = inactiveSprite; // 기본 스프라이트 지정 시 초기화
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
            IsActivated = true;
            if (spr != null)
            {
                if (activatedSprite != null) spr.sprite = activatedSprite;
                else spr.color = activatedColor;
            }
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) playerInRange = false;
    }
}
