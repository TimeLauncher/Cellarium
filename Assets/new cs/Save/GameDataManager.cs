using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    public SaveData CurrentSaveData { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetCurrentSaveData(SaveData data)
    {
        CurrentSaveData = data;
    }

    public void SaveCurrentData()
    {
        if (CurrentSaveData == null)
        {
            Debug.LogWarning("현재 저장 데이터가 없습니다.");
            return;
        }

        SaveSystem.Save(
            CurrentSaveData.slotIndex,
            CurrentSaveData
        );
    }
}