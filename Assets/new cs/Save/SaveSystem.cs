using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string GetSavePath(int slotIndex)
    {
        return Path.Combine(
            Application.persistentDataPath,
            $"save_slot_{slotIndex}.json"
        );
    }

    public static bool HasSave(int slotIndex)
    {
        return File.Exists(GetSavePath(slotIndex));
    }

    public static void CreateNewSave(int slotIndex)
    {
        SaveData newData = new SaveData(slotIndex);
        Save(slotIndex, newData);

        Debug.Log($"새 세이브 생성 완료: 슬롯 {slotIndex}");
    }

    public static void Save(int slotIndex, SaveData data)
    {
        if (data == null)
        {
            Debug.LogError("저장할 SaveData가 없습니다.");
            return;
        }

        data.slotIndex = slotIndex;

        string json = JsonUtility.ToJson(data, true);
        string path = GetSavePath(slotIndex);

        File.WriteAllText(path, json);

        Debug.Log($"저장 완료: {path}");
    }

    public static SaveData Load(int slotIndex)
    {
        string path = GetSavePath(slotIndex);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"슬롯 {slotIndex}에 세이브 파일이 없습니다.");
            return null;
        }

        string json = File.ReadAllText(path);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        Debug.Log($"불러오기 완료: 슬롯 {slotIndex}");

        return data;
    }

    public static void Delete(int slotIndex)
    {
        string path = GetSavePath(slotIndex);

        if (!File.Exists(path))
            return;

        File.Delete(path);

        Debug.Log($"세이브 삭제 완료: 슬롯 {slotIndex}");
    }

    public static string GetDebugSavePath(int slotIndex)
    {
        return GetSavePath(slotIndex);
    }
}