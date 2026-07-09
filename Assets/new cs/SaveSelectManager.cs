using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveSelectManager : MonoBehaviour
{
    [SerializeField] private string firstStageSceneName = "Stage1";
    [SerializeField] private string titleSceneName = "MainTitle";

    public void SelectSlot(int slotIndex)
    {
        PlayerPrefs.SetInt("SelectedSaveSlot", slotIndex);

        Debug.Log("선택한 세이브 슬롯: " + slotIndex);

        SceneManager.LoadScene(firstStageSceneName);
    }

    public void BackToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }
}