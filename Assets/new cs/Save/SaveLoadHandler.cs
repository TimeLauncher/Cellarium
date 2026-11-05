
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveLoadHandler : MonoBehaviour
{
    private static bool pendingLoad;

    // 이어하기를 선택했을 때 호출
    public static void RequestLoad()
    {
        pendingLoad = true;
    }

    // 새 게임을 선택했을 때 호출
    public static void CancelLoad()
    {
        pendingLoad = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingLoad)
            return;

        if (SaveManager.Instance == null ||
            SaveManager.Instance.CurrentData == null)
            return;

        SaveData data = SaveManager.Instance.CurrentData;

        if (scene.name != data.sceneName)
            return;

        // 중복 복원 방지
        pendingLoad = false;

        StartCoroutine(RestoreSave(data));
    }

    private IEnumerator RestoreSave(SaveData data)
    {
        // 플레이어 등록 및 씬 초기화 대기
        yield return null;

        if (string.IsNullOrEmpty(data.savePointID))
            yield break;

        SavePoint[] savePoints =
            FindObjectsByType<SavePoint>(FindObjectsSortMode.None);

        SavePoint target = null;

        foreach (SavePoint point in savePoints)
        {
            if (point.SavePointId == data.savePointID)
            {
                target = point;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning(
                $"[SaveLoadHandler] SavePoint를 찾지 못했습니다: {data.savePointID}"
            );
            yield break;
        }

        if (PlayerManager.Instance == null ||
            PlayerManager.Instance.allPlayers.Count == 0)
        {
            Debug.LogWarning("[SaveLoadHandler] 플레이어를 찾지 못했습니다.");
            yield break;
        }

        PlayerController main =
            PlayerManager.Instance.allPlayers[0];

        if (main == null)
            yield break;

        Vector3 spawnPosition = target.transform.position;

        // 체크포인트 복원
        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.LoadCheckpointFromSave(
                data.sceneName,
                spawnPosition,
                data
            );
        }

        // 플레이어 진행 데이터 복원
        PlayerManager.Instance.cellCurrency = data.cellCurrency;
        PlayerManager.Instance.darkCellCurrency = data.darkCellCurrency;
        PlayerManager.Instance.maxFissionCount = data.maxFissionCount;
        PlayerManager.Instance.fissionUnlocked = data.fissionUnlocked;

        // 플레이어 위치 복원
        main.transform.position = spawnPosition;

        Rigidbody2D rb = main.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = spawnPosition;
            rb.linearVelocity = Vector2.zero;
        }

        main.RestoreAfterRespawn();

        CameraSnap.SnapNow();

        Debug.Log(
            $"[SaveLoadHandler] 이어하기 복원 완료 - {data.savePointID}"
        );
    }
}
