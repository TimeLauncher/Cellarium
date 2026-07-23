using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveSelectManager : MonoBehaviour
{
    [SerializeField] private string firstStageSceneName = "Heart A00";
    [SerializeField] private string titleSceneName = "MainTitle";

    public void SelectSlot(int slotIndex)
    {
        PlayerPrefs.SetInt("SelectedSaveSlot", slotIndex);
        PlayerPrefs.Save();

        SaveData saveData;

        if (SaveSystem.HasSave(slotIndex))
        {
            saveData = SaveSystem.Load(slotIndex);

            Debug.Log($"기존 세이브 불러오기: 슬롯 {slotIndex}");
        }
        else
        {
            SaveSystem.CreateNewSave(slotIndex);
            saveData = SaveSystem.Load(slotIndex);

            Debug.Log($"새 게임 생성: 슬롯 {slotIndex}");
        }

        if (saveData == null)
        {
            Debug.LogError("세이브 데이터를 준비하지 못했습니다.");
            return;
        }

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetCurrentSaveData(saveData);
        }
        else
        {
            Debug.LogError("GameDataManager가 씬에 없습니다.");
            return;
        }

        string sceneToLoad = string.IsNullOrEmpty(saveData.currentSceneName)
            ? firstStageSceneName
            : saveData.currentSceneName;

        SceneLoader.LoadScene(sceneToLoad);
    }

    public void BackToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }
}