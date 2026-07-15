using UnityEngine;
using UnityEngine.SceneManagement;

public class MainTitleManager : MonoBehaviour
{
    public string saveSelectSceneName = "SaveSelectScene";

    public void StartGame()
    {
        SceneManager.LoadScene(saveSelectSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}