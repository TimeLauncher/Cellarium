using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // 현재 선택된 세이브 슬롯
    public int SelectedSlot { get; private set; } = -1;

    // 현재 플레이 중인 세이브 데이터
    public SaveData CurrentData { get; private set; }


    private void Awake()
    {
        // SaveManager 중복 생성 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    // ========================================
    // 세이브 파일 경로
    // ========================================

    private string GetSavePath(int slotIndex)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"save_{slotIndex}.json"
        );
    }


    // ========================================
    // 새 게임 생성
    // ========================================

    public void CreateNewGame(int slotIndex)
    {
        SelectedSlot = slotIndex;

        CurrentData = new SaveData();

        SaveGame();

        Debug.Log($"[SaveManager] 새 게임 생성 - Slot {slotIndex}");
    }


    // ========================================
    // 게임 저장
    // ========================================

    public void SaveGame()
    {
        if (SelectedSlot < 0)
        {
            Debug.LogWarning("[SaveManager] 선택된 세이브 슬롯이 없습니다.");
            return;
        }

        if (CurrentData == null)
        {
            Debug.LogWarning("[SaveManager] 저장할 SaveData가 없습니다.");
            return;
        }

        CurrentData.lastSaveTime =
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string json =
            JsonUtility.ToJson(CurrentData, true);

        string path =
            GetSavePath(SelectedSlot);

        File.WriteAllText(path, json);

        Debug.Log($"[SaveManager] 저장 완료 : {path}");
    }


    // ========================================
    // 게임 불러오기
    // ========================================

    public bool LoadGame(int slotIndex)
    {
        string path = GetSavePath(slotIndex);

        if (!File.Exists(path))
        {
            Debug.LogWarning(
                $"[SaveManager] Slot {slotIndex} 세이브 파일이 없습니다."
            );

            return false;
        }

        string json = File.ReadAllText(path);

        CurrentData =
            JsonUtility.FromJson<SaveData>(json);

        SelectedSlot = slotIndex;

        Debug.Log($"[SaveManager] 불러오기 완료 - Slot {slotIndex}");

        return true;
    }


    // ========================================
    // 세이브 파일 존재 확인
    // ========================================

    public bool HasSave(int slotIndex)
    {
        return File.Exists(GetSavePath(slotIndex));
    }


    // ========================================
    // 특정 슬롯 데이터 확인
    // SaveSelect 화면에서 사용 예정
    // ========================================

    public SaveData GetSaveData(int slotIndex)
    {
        string path = GetSavePath(slotIndex);

        if (!File.Exists(path))
            return null;

        string json = File.ReadAllText(path);

        return JsonUtility.FromJson<SaveData>(json);
    }


    // ========================================
    // 세이브 삭제
    // ========================================

    public void DeleteSave(int slotIndex)
    {
        string path = GetSavePath(slotIndex);

        if (!File.Exists(path))
            return;

        File.Delete(path);

        // 현재 플레이 중인 슬롯을 삭제한 경우
        if (SelectedSlot == slotIndex)
        {
            SelectedSlot = -1;
            CurrentData = null;
        }

        Debug.Log($"[SaveManager] 세이브 삭제 - Slot {slotIndex}");
    }
}