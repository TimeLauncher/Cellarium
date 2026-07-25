using UnityEngine;
using UnityEngine.SceneManagement;

// 구역(씬) 사이를 잇는 문/통로.
// 기존 ScenePortal과 달리 '도착 지점'을 지정할 수 있다 —
// 그냥 씬만 로드하면 플레이어는 그 씬에 배치된 원래 자리에서 시작하므로,
// A01↔A02처럼 양방향으로 오가는 문에는 도착 지점 지정이 반드시 필요하다.
//
// 사용법
//   1) 빈 게임오브젝트 + Collider2D(Is Trigger ✓)에 이 스크립트를 붙인다
//   2) Target Scene에 이동할 씬 이름 (Build Settings에 등록된 이름과 정확히 일치)
//   3) Target Entry Id에 도착할 SceneEntryPoint의 Entry Id
//   4) 도착 씬에 SceneEntryPoint를 만들고 같은 Entry Id를 넣는다
[RequireComponent(typeof(Collider2D))]
public class AreaPortal : MonoBehaviour
{
    [Header("이동할 곳")]
    [Tooltip("Build Settings에 등록된 씬 이름과 정확히 같아야 한다")]
    public string targetScene;
    [Tooltip("도착 씬의 SceneEntryPoint와 맞출 id. 비우면 그 씬에 배치된 원래 자리에서 시작한다")]
    public string targetEntryId = "";

    [Header("발동 방식")]
    [Tooltip("켜면 범위 안에서 E키를 눌러야 이동. 끄면 닿는 즉시 이동")]
    public bool requireKeyPress = false;
    public KeyCode interactKey = KeyCode.E;

    private bool triggered;
    private bool playerInRange;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[{name}] Collider2D를 Is Trigger로 바꿨습니다. 통로는 트리거여야 합니다.", this);
        }
    }

    void Update()
    {
        if (!requireKeyPress || !playerInRange || triggered) return;
        if (Input.GetKeyDown(interactKey)) Go();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInRange = true;
        if (!requireKeyPress) Go();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) playerInRange = false;
    }

    void Go()
    {
        if (triggered) return;

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError($"[{name}] Target Scene이 비어 있습니다.", this);
            return;
        }

        triggered = true;

        // 도착 지점 예약. 도착 씬의 SceneEntryPoint가 이 id를 보고 플레이어를 옮긴다.
        if (!string.IsNullOrEmpty(targetEntryId))
            SceneEntryPoint.RequestEntry(targetEntryId);
        else
            SceneEntryPoint.ClearEntry(); // 이전 예약이 남아 엉뚱한 곳으로 가지 않게

        Debug.Log($"[{name}] 씬 이동: {targetScene} (도착 지점: {(string.IsNullOrEmpty(targetEntryId) ? "지정 없음" : targetEntryId)})");
        SceneManager.LoadScene(targetScene);
    }

    void OnDrawGizmos()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
