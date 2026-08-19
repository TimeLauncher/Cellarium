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
    [Header("종료 확인 선택")]
    [SerializeField] private QuitConfirmSelector quitConfirmSelector;
    [SerializeField] private GameObject settingsPanel;

    [Header("지도")]
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private MapController mapController;
    private GameObject gameplayUI;

    private bool isMapOpen;
    public static bool IsMapOpen { get; private set; }
    private static PauseMenu instance;

    private bool isQuitConfirmOpen;
    private bool isSettingsOpen;
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        IsPaused = false;
        IsMapOpen = false;
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

        FindGameplayUI();
    }

    private void Update()
    {
        // =========================
        // M : 지도
        // =========================
        if (Input.GetKeyDown(KeyCode.M))
        {
            // 설정창 / 종료창 / Pause 메뉴가 열려있으면
            // M 입력 무시
            if (IsPaused || isSettingsOpen || isQuitConfirmOpen)
                return;

            ToggleMap();
            return;
        }

        // =========================
        // ESC
        // =========================
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        // 1순위 : 지도가 열려 있으면
        // ESC는 지도만 닫는다.
        if (IsMapOpen)
        {
            CloseMap();
            return;
        }

        // 2순위 : 종료 확인창
        if (isQuitConfirmOpen)
        {
            CloseQuitConfirm();
            return;
        }

        // 3순위 : 설정창
        if (isSettingsOpen)
        {
            CloseSettings();
            return;
        }

        // 4순위 : 일반 게임 ↔ Pause 메뉴
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

        if (gameplayUI != null)
            gameplayUI.SetActive(false);

        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(false);

        isQuitConfirmOpen = false;

        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        IsPaused = false;
        isSettingsOpen = false;
        isQuitConfirmOpen = false;

        optionPanel.SetActive(false);
        settingsPanel.SetActive(false);
        quitConfirmPanel.SetActive(false);

        if (gameplayUI != null)
            gameplayUI.SetActive(true);

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
        Time.timeScale = 1f;

        IsPaused = false;
        IsMapOpen = false;
    }
    public void OpenSettings()
    {
        isSettingsOpen = true;

        optionPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        isSettingsOpen = false;

        settingsPanel.SetActive(false);
        optionPanel.SetActive(true);
    }
    public void ToggleMap()
    {
        if (IsMapOpen)
            CloseMap();
        else
            OpenMap();
    }

    public void OpenMap()
    {
        if (IsPaused || isSettingsOpen || isQuitConfirmOpen)
            return;

        IsMapOpen = true;

        if (mapPanel != null)
            mapPanel.SetActive(true);

        if (gameplayUI != null)
            gameplayUI.SetActive(false);

        Time.timeScale = 0f;

        StartCoroutine(CenterMapNextFrame());
    }

    public void CloseMap()
    {
        IsMapOpen = false;

        if (mapPanel != null)
            mapPanel.SetActive(false);

        if (gameplayUI != null)
            gameplayUI.SetActive(true);

        Time.timeScale = 1f;
    }
    private System.Collections.IEnumerator CenterMapNextFrame()
    {
        yield return null;

        if (mapController != null)
        {
            mapController.CenterOnPlayer();
        }
    }
    private void FindGameplayUI()
    {
        GameplayHUD hud =
            FindFirstObjectByType<GameplayHUD>();

        if (hud != null)
        {
            gameplayUI = hud.gameObject;
        }
        else
        {
            gameplayUI = null;

            Debug.LogWarning(
                "[UI] 현재 Scene에서 GameplayHUD를 찾을 수 없습니다."
            );
        }
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode
    )
    {
        FindGameplayUI();
    }
}