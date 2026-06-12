using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainTitleManager : MonoBehaviour
{
#if UNITY_EDITOR
    public SceneAsset firstScene;
#endif

    private string sceneName;

    private void Awake()
    {
#if UNITY_EDITOR
        if (firstScene != null)
        {
            sceneName = firstScene.name;
        }
#endif
    }

    public void StartGame()
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}