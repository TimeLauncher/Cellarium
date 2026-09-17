using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // =========================
    // 세이브 슬롯 기본 정보
    // =========================

    // 플레이 시간 (초 단위)
    public float playTime;

    // 마지막으로 저장한 날짜/시간
    public string lastSaveTime;


    // =========================
    // 현재 진행 위치
    // =========================

    // 저장 당시 씬 이름
    // 예: "Heart A00", "Heart A01"
    public string sceneName;

    // 마지막 세이브 포인트의 고유 ID
    public string savePointID;


    // =========================
    // 플레이어 데이터
    // =========================

    public int currentHP;
    public int maxHP;


    // =========================
    // 게임 진행도
    // =========================

    // 필요하면 나중에 구체적인 진행 데이터로 변경
    public int progress;


    // =========================
    // 월드 상태
    // =========================

    // 완료된 이벤트 ID
    public List<string> completedEvents = new List<string>();

    // 획득한 아이템 ID
    public List<string> collectedItems = new List<string>();

    // 처치된 보스 ID
    public List<string> defeatedBosses = new List<string>();


    // =========================
    // 생성자
    // =========================

    public SaveData()
    {
        playTime = 0f;
        lastSaveTime = "";

        // 새 게임 시작 위치
        sceneName = "Heart A00";
        savePointID = "";

        currentHP = 0;
        maxHP = 0;

        progress = 0;
    }
}