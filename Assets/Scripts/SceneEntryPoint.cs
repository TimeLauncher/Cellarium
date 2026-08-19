using System.Collections;
using UnityEngine;

// 다른 씬에서 넘어왔을 때 플레이어가 서 있을 지점.
// 빈 게임오브젝트에 붙이고 Entry Id에 이름을 정해두면, 그 id를 지정한 AreaPortal로 들어올 때 여기에 놓인다.
//
// 예) A01 오른쪽 끝 문 → A02로 이동, A02의 왼쪽 입구에 entryId="from_A01" 을 둔다.
//     A02 왼쪽 문 → A01로 이동, A01의 오른쪽 입구에 entryId="from_A02" 를 둔다.
//     이렇게 짝을 맞춰야 오갈 때 문 앞에서 시작한다.
public class SceneEntryPoint : MonoBehaviour
{
    [Tooltip("AreaPortal의 Target Entry Id와 똑같이 맞출 것. 씬 안에서 겹치지 않게")]
    public string entryId = "";

    // 어느 입구로 들어오는 중인지. 씬을 넘어가도 유지돼야 해서 static으로 둔다.
    public static string PendingEntryId { get; private set; }

    public static void RequestEntry(string id) => PendingEntryId = id;
    public static void ClearEntry() => PendingEntryId = null;

    void Start()
    {
        if (string.IsNullOrEmpty(PendingEntryId)) return;
        if (PendingEntryId != entryId) return;

        ClearEntry(); // 한 번만 쓰고 지운다 (다음 씬 로드까지 남아 있으면 엉뚱한 곳으로 간다)
        StartCoroutine(PlacePlayer());
    }

    // 플레이어 등록(PlayerController.Start → RegisterPlayer)이 끝난 뒤에 옮겨야 한다
    IEnumerator PlacePlayer()
    {
        yield return null;

        PlayerManager m = PlayerManager.Instance;
        if (m == null || m.allPlayers.Count == 0)
        {
            Debug.LogWarning($"[SceneEntryPoint] '{entryId}': 씬에서 플레이어를 찾지 못했다.", this);
            yield break;
        }

        PlayerController main = m.allPlayers[0];
        if (main == null) yield break;

        main.transform.position = transform.position;

        // 이전 씬에서 이동하던 속도가 남아 튀어나가지 않게 정지시킨다
        Rigidbody2D rb = main.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1.6f, 0f));
    }
}
