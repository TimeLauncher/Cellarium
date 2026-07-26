using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainTitleManager : MonoBehaviour
{
    [Header("씬")]
    [SerializeField] private string saveSelectSceneName = "SaveSelectScene";

    [Header("종료 확인창")]
    [SerializeField] private GameObject quitConfirmPanel;
    [SerializeField] private GameObject yesButton;
    [SerializeField] private GameObject quitButton;
    
    [SerializeField] private QuitConfirmSelector quitConfirmSelector;
    [SerializeField] private TitleMenuSelector titleMenuSelector;

    private bool isQuitPanelOpen;
    private void Start()
    {
        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(saveSelectSceneName);
    }

    public void OpenQuitConfirm()
    {
        titleMenuSelector.SetInputEnabled(false);

        quitConfirmPanel.SetActive(true);
        isQuitPanelOpen = true;

        quitConfirmSelector.OpenSelector();
    }

    public void CloseQuitConfirm()
    {
        quitConfirmSelector.CloseSelector();

        quitConfirmPanel.SetActive(false);
        isQuitPanelOpen = false;

        titleMenuSelector.SetInputEnabled(true);
    }

    public void ConfirmQuit()
    {
        Debug.Log("게임 종료");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void Update()
    {
        if (isQuitPanelOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseQuitConfirm();
        }
    }
}