
using System.Collections.Generic;
using UnityEngine;

public class MapFogSaveManager : MonoBehaviour
{
    public static MapFogSaveManager Instance { get; private set; }

    [SerializeField] private Transform mapContent;

    private void Awake()
    {
        Instance = this;
    }

    public List<MapFogSaveEntry> ExportAllFog()
    {
        List<MapFogSaveEntry> result = new List<MapFogSaveEntry>();

        if (mapContent == null)
        {
            Debug.LogWarning("[MapFogSaveManager] Map Content가 연결되지 않았습니다.");
            return result;
        }

        MapArea[] areas = mapContent.GetComponentsInChildren<MapArea>(true);

        foreach (MapArea area in areas)
        {
            if (area.Fog == null || string.IsNullOrEmpty(area.SceneName))
                continue;

            string fogData = area.Fog.ExportFog();

            if (string.IsNullOrEmpty(fogData))
                continue;

            result.Add(new MapFogSaveEntry
            {
                sceneName = area.SceneName,
                fogData = fogData
            });
        }

        Debug.Log($"[MapFogSaveManager] 포그 {result.Count}개 저장 데이터 수집 완료");
        return result;
    }

    public void ImportAllFog(List<MapFogSaveEntry> savedData)
    {
        if (mapContent == null || savedData == null)
            return;

        MapArea[] areas = mapContent.GetComponentsInChildren<MapArea>(true);

        foreach (MapArea area in areas)
        {
            foreach (MapFogSaveEntry entry in savedData)
            {
                if (entry == null || entry.sceneName != area.SceneName)
                    continue;

                if (area.Fog != null)
                    area.Fog.ImportFog(entry.fogData);

                break;
            }
        }

        Debug.Log("[MapFogSaveManager] 지도 포그 복원 완료");
    }
}
