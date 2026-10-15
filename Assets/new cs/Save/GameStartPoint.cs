
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStartPoint : MonoBehaviour
{
    [SerializeField] private string firstSceneName = "Heart A00";

    // 게임 실행 중 최초 시작 위치 적용 여부
    private static bool hasAppliedStartPosition;

    private IEnumerator Start()
    {
        if (SceneManager.GetActiveScene().name != firstSceneName)
            yield break;

        if (hasAppliedStartPosition)
            yield break;

        if (SaveManager.Instance == null ||
            SaveManager.Instance.CurrentData == null)
            yield break;

        if (!string.IsNullOrEmpty(
            SaveManager.Instance.CurrentData.savePointID))
            yield break;

        if (!string.IsNullOrEmpty(SceneEntryPoint.PendingEntryId))
            yield break;

        if (RespawnManager.Instance != null &&
            RespawnManager.Instance.RespawnInProgress)
            yield break;

        yield return null;

        PlayerManager manager = PlayerManager.Instance;

        if (manager == null || manager.allPlayers.Count == 0)
        {
            Debug.LogWarning("[GameStartPoint] 플레이어를 찾지 못했습니다.");
            yield break;
        }

        PlayerController player = manager.allPlayers[0];

        if (player == null)
            yield break;

        player.transform.position = transform.position;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = transform.position;
            rb.linearVelocity = Vector2.zero;
        }

        // 최초 시작 위치 적용 완료
        hasAppliedStartPosition = true;

        CameraSnap.SnapNow();

        Debug.Log("[GameStartPoint] 최초 새 게임 시작 위치 적용 완료");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.up * 1.5f
        );
    }

    // 새로운 게임을 시작할 때 호출
    public static void ResetStartPosition()
    {
        hasAppliedStartPosition = false;
    }
}
