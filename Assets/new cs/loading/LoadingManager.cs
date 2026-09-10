using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    private void Start()
    {
        Time.timeScale = 1f;

        // 메인 타이틀로 돌아가는 경우에만
        // 인게임 영속 오브젝트 정리
        if (SceneLoader.nextSceneName == "maintitle")
        {
            CleanupGameObjects();
        }

        StartCoroutine(LoadAsync());
    }

    private void CleanupGameObjects()
    {
        // 플레이어 관련 영속 오브젝트 제거
        PersistentPlayerRoot playerRoot =
            FindFirstObjectByType<PersistentPlayerRoot>();

        if (playerRoot != null)
        {
            Destroy(playerRoot.gameObject);
        }

        // 인게임 Pause / Option / Map UI 제거
        PauseMenu pauseMenu =
            FindFirstObjectByType<PauseMenu>();

        if (pauseMenu != null)
        {
            Destroy(pauseMenu.gameObject);
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