using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static string nextSceneName;

    public static void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;

        nextSceneName = sceneName;
        SceneManager.LoadScene("LoadingScene");
    }
}