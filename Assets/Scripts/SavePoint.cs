using UnityEngine;

// 세이브 포인트: 범위 내 E키 상호작용 시 체력/분열 게이지 최대치 회복 + 분열체 즉시 회수.
// 위치는 static으로 저장해둠 - 사망 시 재시작 연결은 사망 처리 시스템이 아직 없어서 추후 구현
[RequireComponent(typeof(Collider2D))]
public class SavePoint : MonoBehaviour
{
    public static Vector3 LastSavePosition { get; private set; }
    public static bool HasSave { get; private set; }

    private bool playerInRange;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
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
