using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("일시정지 메뉴")]
    [SerializeField] private GameObject optionPanel;

    [Header("종료 확인창")]
    [SerializeField] private GameObject quitConfirmPanel;

    [Header("메인 메뉴 씬")]
    [SerializeField] private string mainMenuSceneName = "maintitle";
    [Header("종료 확인 선택")]
    [SerializeField] private QuitConfirmSelector quitConfirmSelector;

    private bool isQuitConfirmOpen;

    private void Start()
    {
        IsPaused = false;
        isQuitConfirmOpen = false;

        Time.timeScale = 1f;

        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }

        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        // 종료 확인창이 열려 있으면 ESC로 확인창만 닫기
        if (isQuitConfirmOpen)
        {
            CloseQuitConfirm();
            return;
        }

        // 종료 확인창이 아니면 일시정지 메뉴 열기/닫기
        TogglePauseMenu();
    }

    public void TogglePauseMenu()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        IsPaused = true;

        optionPanel.SetActive(true);

        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        isQuitConfirmOpen = false;

        // 플레이어, 몬스터, 물리, 일반 투사체 정지
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        IsPaused = false;
        isQuitConfirmOpen = false;

        optionPanel.SetActive(false);

        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        Time.timeScale = 1f;
    }

    public void OpenQuitConfirm()
    {
        isQuitConfirmOpen = true;

        optionPanel.SetActive(false);
        quitConfirmPanel.SetActive(true);

        if (quitConfirmSelector != null)
        {
            quitConfirmSelector.OpenSelector();
        }
    }

    public void CloseQuitConfirm()
    {
        if (quitConfirmSelector != null)
        {
            quitConfirmSelector.CloseSelector();
        }

        isQuitConfirmOpen = false;

        quitConfirmPanel.SetActive(false);
        optionPanel.SetActive(true);
    }

    public void ConfirmQuit()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        Debug.Log("게임 종료");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        // 일시정지 상태에서 씬이 종료될 때 timeScale이 0으로 남는 것 방지
        Time.timeScale = 1f;
        IsPaused = false;
    }
}