using UnityEngine;

public class SaveTest : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SaveGame();
        }
    }

    private void SaveGame()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogError("GameDataManager가 없습니다.");
            return;
        }

        SaveData data = GameDataManager.Instance.CurrentSaveData;

        if (data == null)
        {
            Debug.LogError("현재 SaveData가 없습니다.");
            return;
        }

        data.currentSceneName = UnityEngine.SceneManagement
            .SceneManager.GetActiveScene().name;

        data.playerPositionX = transform.position.x;
        data.playerPositionY = transform.position.y;

        data.saveDate =
            System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        GameDataManager.Instance.SaveCurrentData();

        Debug.Log("F5 저장 완료");
    }
}