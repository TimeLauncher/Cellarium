using UnityEngine;

// 림프 게이트: 범위 내 E키 최초 입력 시 개방, 개방 후 입력 시 지도 UI 오픈(스텁).
// 게이트 간 이동은 기획서상 추후 개발 예정
[RequireComponent(typeof(Collider2D))]
public class LymphGate : MonoBehaviour
{
    public bool isOpen;

    private bool playerInRange;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        if (!playerInRange || !Input.GetKeyDown(KeyCode.E)) return;

        if (!isOpen)
        {
            isOpen = true;
            Debug.Log("림프 게이트 개방!");
        }
        else
        {
            Debug.Log("지도 UI 오픈 (스텁) - 게이트 간 이동 기능은 추후 구현");
            // TODO: 지도 UI, 개방된 다른 림프 게이트로 이동
        }
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
