using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveSelectManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string firstStageSceneName = "Heart A00";
    [SerializeField] private string titleSceneName = "MainTitle";


    // ========================================
    // 세이브 슬롯 선택
    // ========================================

    public void SelectSlot(int slotIndex)
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SaveSelectManager] SaveManager가 없습니다.");
            return;
        }

        // 기존 세이브 파일이 있는 경우
        if (SaveManager.Instance.HasSave(slotIndex))
        {
            ContinueGame(slotIndex);
        }
        // 빈 슬롯인 경우
        else
        {
            StartNewGame(slotIndex);
        }
    }


    // ========================================
    // 새 게임
    // ========================================

    private void StartNewGame(int slotIndex)
    {
        SaveManager.Instance.CreateNewGame(slotIndex);

        Debug.Log(
            $"[SaveSelectManager] 새 게임 시작 - Slot {slotIndex}"
        );

        SceneManager.LoadScene(firstStageSceneName);
    }


    // ========================================
    // 이어하기
    // ========================================

    private void ContinueGame(int slotIndex)
    {
        bool success =
            SaveManager.Instance.LoadGame(slotIndex);

        if (!success)
        {
            Debug.LogError(
                $"[SaveSelectManager] Slot {slotIndex} 불러오기 실패"
            );

            return;
        }

        string sceneName =
            SaveManager.Instance.CurrentData.sceneName;

        Debug.Log(
            $"[SaveSelectManager] 이어하기 - {sceneName}"
        );

        SceneManager.LoadScene(sceneName);
    }


    // ========================================
    // 타이틀로 돌아가기
    // ========================================

    public void BackToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }
}