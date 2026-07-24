using UnityEngine;

// A02 등에 배치하는 분열 능력 획득 지점.
// 플레이어가 범위에 들어오면(자동) 또는 E키 상호작용 시 PlayerManager.UnlockFission() 호출.
// 한 번 해금되면 스스로 비활성화된다.
[RequireComponent(typeof(Collider2D))]
public class FissionUnlockZone : MonoBehaviour
{
    [Tooltip("체크하면 범위 진입 즉시 해금, 해제하면 범위 안에서 E키를 눌러야 해금")]
    public bool unlockOnEnter = true;

    private bool playerInRange;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        if (unlockOnEnter) return;
        if (!playerInRange || !Input.GetKeyDown(KeyCode.E)) return;
        Unlock();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        playerInRange = true;
        if (unlockOnEnter) Unlock();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) playerInRange = false;
    }

    void Unlock()
    {
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.UnlockFission();
        gameObject.SetActive(false); // 한 번만 해금
    }
}
