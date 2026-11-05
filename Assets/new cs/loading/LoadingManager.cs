
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    [Header("Game Session")]
    [SerializeField] private GameSessionRoot gameSessionPrefab;

    private void Start()
    {
        Time.timeScale = 1f;

        if (SceneLoader.nextSceneName == "maintitle")
        {
            CleanupGameObjects();
        }
        else
        {
            PrepareGameSession();
        }

        StartCoroutine(LoadAsync());
    }


    private void PrepareGameSession()
    {
        if (GameSessionRoot.Instance == null)
        {
            if (gameSessionPrefab == null)
            {
                Debug.LogError("[LoadingManager] GameSessionRoot Prefab이 없습니다.");
                return;
            }

            Instantiate(gameSessionPrefab);
        }

        // 이어하기로 불러온 지도 탐험 기록 복원
        if (SaveManager.Instance == null ||
            SaveManager.Instance.CurrentData == null)
            return;

        // 새 게임에서는 저장된 포그 데이터가 없으므로 복원하지 않음
        var fogData = SaveManager.Instance.CurrentData.mapFogData;

        if (fogData == null || fogData.Count == 0)
            return;

        MapFogSaveManager fogManager =
            FindFirstObjectByType<MapFogSaveManager>(
                FindObjectsInactive.Include
            );

        if (fogManager != null)
        {
            fogManager.ImportAllFog(fogData);

            Debug.Log("[LoadingManager] 지도 탐험 기록 복원 완료");
        }
        else
        {
            Debug.LogWarning("[LoadingManager] MapFogSaveManager를 찾지 못했습니다.");
        }
    }


    private void CleanupGameObjects()
    {
        if (GameSessionRoot.Instance != null)
        {
            Destroy(GameSessionRoot.Instance.gameObject);

            Debug.Log("[LoadingManager] GameSessionRoot 정리 완료");
        }
    }

    private IEnumerator LoadAsync()
    {
        yield return new WaitForSeconds(0.5f);

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(SceneLoader.nextSceneName);

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        operation.allowSceneActivation = true;
    }
}
