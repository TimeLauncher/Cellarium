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
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("M키 입력됨");

            if (!IsPaused && !isSettingsOpen && !isQuitConfirmOpen)
            {
                ToggleMap();
            }

            return;
        }
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        // 종료 확인창 → ESC → 옵션 메뉴
        if (isQuitConfirmOpen)
        {
            CloseQuitConfirm();
            return;
        }

        // 설정창 → ESC → 옵션 메뉴
        if (isSettingsOpen)
        {
            CloseSettings();
            return;
        }

        // 일반 게임 ↔ 옵션 메뉴
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
        isSettingsOpen = false;
        isQuitConfirmOpen = false;

        optionPanel.SetActive(false);
        settingsPanel.SetActive(false);
        quitConfirmPanel.SetActive(false);

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
        if (IsPaused)
            return;

        IsMapOpen = true;

        if (mapPanel != null)
        {
            mapPanel.SetActive(true);
        }

        Time.timeScale = 0f;

        StartCoroutine(CenterMapNextFrame());
    }

    public void CloseMap()
    {
        IsMapOpen = false;

        mapPanel.SetActive(false);

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
}