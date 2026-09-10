using UnityEngine;
using UnityEngine.SceneManagement;

public class MainPauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;

    private bool isSettingsOpen;

    private void Start()
    {
        isSettingsOpen = false;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void Update()
    {
        // 설정창이 열려 있을 때 ESC로 닫기
        if (isSettingsOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseSettings();
        }
    }

    public void OpenSettings()
    {
        isSettingsOpen = true;

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        isSettingsOpen = false;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
}