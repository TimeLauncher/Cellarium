using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 한 판(run)의 끝을 감지해서, 씬 밖에서 살아남는 진행상황을 전부 비운다.
//
// ★ 왜 필요한가:
//   WorldState / GameProgress / RespawnManager / SavePoint(static)는 전부 씬을 바꿔도 죽지 않는다.
//   부활(씬 리로드)과 씬 이동에서 진행상황을 유지하려면 그래야 하는데, 그 부작용으로
//   타이틀로 나갔다가 새 게임을 시작해도 이전 판이 그대로 이어졌다 —
//   시작 위치만 A00이고 이미 연 문 / 먹은 다크셀·셀덩어리 / 찍어둔 세이브포인트는 살아 있는 상태.
//   → 타이틀·세이브선택 화면에 들어오는 순간을 '판이 끝난 시점'으로 보고 여기서 전부 지운다.
//
// 슬롯 세이브(SaveSystem / GameDataManager / PlayerPrefs)는 파일 기반의 별개 시스템이라 건드리지 않는다.
// 나중에 '이어하기'를 붙일 땐 ResetRun()으로 비운 뒤 파일에서 읽은 값을 얹으면 된다.
//
// 씬에 배치할 필요 없음 — 다른 매니저들과 같이 시작 시 자동 생성된다.
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    // 이 씬들에 들어오면 진행 중이던 판을 버린다. 씬 이름이 바뀌면 여기만 고치면 된다.
    // LoadingScene은 세이브선택 → 게임 씬 사이를 거쳐 가는 통로라 일부러 넣지 않는다.
    static readonly List<string> menuScenes = new List<string>
    {
        "maintitle",
        "SaveSelectScene",
    };

    // 메뉴 씬을 더 만들면 여기에 추가 (씬 로드 전에 한 번만 부르면 된다)
    public static void AddMenuScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName) && !menuScenes.Contains(sceneName))
            menuScenes.Add(sceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("GameSession").AddComponent<GameSession>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (!IsMenuScene(scene.name)) return;

        ResetRun();
    }

    static bool IsMenuScene(string sceneName)
    {
        foreach (string s in menuScenes)
            if (string.Equals(s, sceneName, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // 판 하나를 통째로 초기화한다. 타이틀 진입 시 자동 호출되며,
    // '새 게임' 버튼 쪽에서 직접 부르고 싶으면 GameSession.ResetRun()으로 부르면 된다(중복 호출해도 안전).
    public static void ResetRun()
    {
        // 열린 문 / 먹은 셀덩어리 / 섭취한 다크셀 / 활성화된 세이브포인트 기록
        if (WorldState.Instance != null) WorldState.Instance.ClearAll();

        // 씬을 넘어 들고 다니던 체력·게이지·재화·분열 해금
        if (GameProgress.Instance != null) GameProgress.Instance.ResetProgress();

        // 체크포인트 스냅샷 + 기본 부활 지점
        if (RespawnManager.Instance != null) RespawnManager.Instance.ClearCheckpoint();

        // 세이브포인트 위치 (static이라 씬 로드로는 안 지워진다)
        SavePoint.ClearSave();

        // 포탈로 이동하다 메뉴로 나간 경우 남아 있을 수 있는 도착 지점 예약
        SceneEntryPoint.ClearEntry();

        Debug.Log("[GameSession] 진행 중이던 판을 초기화했습니다 (메뉴 진입)");
    }
}
