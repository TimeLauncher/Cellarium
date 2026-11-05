
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
        if (GameSessionRoot.Instance != null)
            return;

        if (gameSessionPrefab == null)
        {
            Debug.LogError(
                "[LoadingManager] GameSessionRoot Prefab이 연결되지 않았습니다."
            );
            return;
        }

        Instantiate(gameSessionPrefab);

        Debug.Log("[LoadingManager] GameSessionRoot 생성 완료");
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
