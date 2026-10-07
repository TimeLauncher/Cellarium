using System.Collections;
using UnityEngine;

public class SaveLoadHandler : MonoBehaviour
{
    private IEnumerator Start()
    {
        // 다른 오브젝트들의 Awake / Start가 끝날 때까지 대기
        yield return null;

        if (SaveManager.Instance == null)
            yield break;

        SaveData data = SaveManager.Instance.CurrentData;

        if (data == null)
            yield break;

        if (string.IsNullOrEmpty(data.savePointID))
            yield break;


        // 현재 씬의 SavePoint 검색
        SavePoint[] savePoints =
            FindObjectsByType<SavePoint>(FindObjectsSortMode.None);

        SavePoint target = null;

        foreach (SavePoint point in savePoints)
        {
            if (point.SavePointId == data.savePointID)
            {
                target = point;
                break;
            }
        }


        if (target == null)
        {
            Debug.LogWarning(
                $"[SaveLoadHandler] SavePoint를 찾지 못했습니다: {data.savePointID}"
            );

            yield break;
        }


        Vector3 spawnPosition = target.transform.position;


        // RespawnManager에도 체크포인트 복원
        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.LoadCheckpointFromSave(
                data.sceneName,
                spawnPosition,
                data
            );
        }


        // PlayerManager 등록 대기
        if (PlayerManager.Instance == null)
            yield break;

        if (PlayerManager.Instance.allPlayers.Count == 0)
            yield break;


        PlayerController main =
            PlayerManager.Instance.allPlayers[0];


        // 플레이어 진행 데이터 복원
        PlayerManager.Instance.cellCurrency =
            data.cellCurrency;

        PlayerManager.Instance.darkCellCurrency =
            data.darkCellCurrency;

        PlayerManager.Instance.maxFissionCount =
            data.maxFissionCount;

        PlayerManager.Instance.fissionUnlocked =
            data.fissionUnlocked;


        // 저장 위치로 이동
        main.transform.position = spawnPosition;

        // 체력 / 분열 게이지 회복
        main.RestoreAfterRespawn();

        // 카메라 즉시 이동
        CameraSnap.SnapNow();


        Debug.Log(
            $"[SaveLoadHandler] 로드 완료 - {data.savePointID}"
        );
    }
}