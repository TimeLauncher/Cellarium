using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씬을 넘어가도 플레이어 진행상황(체력/분열 게이지/재화/해금)을 유지시킨다.
//
// ★ 필요한 이유:
//   PlayerController는 프리팹이 아니라 각 씬에 개별 배치돼 있고, Start()에서 currentHp = maxHp로 초기화한다.
//   그래서 ScenePortal로 다음 씬에 넘어가면 체력이 꽉 찬 채로, 얻은 능력도 없는 채로 시작한다.
//   이 매니저가 씬 밖에서 살아남아 마지막 상태를 들고 있다가 새 씬의 플레이어에 다시 넣어준다.
//
// 씬에 배치할 필요 없음 — RespawnManager와 같이 시작 시 자동 생성된다.
public class GameProgress : MonoBehaviour
{
    public static GameProgress Instance { get; private set; }

    // 들고 다니는 값들
    bool hasData;

    // 씬 로드 직후 복원이 끝날 때까지 캡처를 멈추는 잠금.
    // ★ 이게 없으면: 씬 로드 첫 프레임에 PlayerController.Start()가 currentHp = maxHp로 초기화하고,
    //   같은 프레임 LateUpdate의 CaptureNow()가 그 '풀피'를 저장해버린다. 복원 코루틴은 다음 프레임에
    //   깨어나므로, 이미 풀피로 덮인 값을 그대로 되넣게 되어 체력이 유지되지 않았다.
    bool restoring;
    float hp, maxHp;
    float gauge, maxGauge;
    int cell, darkCell, maxFissionCount;
    bool fissionUnlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("GameProgress").AddComponent<GameProgress>();
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

    // 매 프레임 최신 상태를 들고 있는다. 씬이 언제 어떻게 바뀌든(포탈/부활/타이틀 복귀)
    // 마지막 값이 항상 남아 있어야 하므로, 이동 직전에 따로 호출할 필요가 없게 이렇게 둔다.
    void LateUpdate()
    {
        CaptureNow();
    }

    public void CaptureNow()
    {
        // 씬 로드 후 복원이 끝나기 전엔 새 씬의 초기값(풀피)이 들어오므로 저장하지 않는다
        if (restoring) return;

        // 부활 중엔 죽기 직전 상태(체력 0 등)를 덮어쓰면 안 된다 — RespawnManager가 체크포인트로 복원 중이다
        if (RespawnManager.Instance != null && RespawnManager.Instance.RespawnInProgress) return;

        PlayerManager m = PlayerManager.Instance;
        if (m == null || m.allPlayers.Count == 0) return;

        PlayerController main = m.allPlayers[0]; // 본체 기준 (분열체는 임시 존재라 제외)
        if (main == null) return;

        hp = main.CurrentHp;
        maxHp = main.maxHp;
        gauge = main.CurrentFissionGauge;
        maxGauge = main.maxFissionGauge;

        cell = m.cellCurrency;
        darkCell = m.darkCellCurrency;
        maxFissionCount = m.maxFissionCount;
        fissionUnlocked = m.fissionUnlocked;

        hasData = true;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasData) return;

        // 부활은 '체크포인트 시점으로 되돌리기'라 RespawnManager가 따로 복원한다. 여기선 손대지 않는다.
        if (RespawnManager.Instance != null && RespawnManager.Instance.RespawnInProgress) return;

        // 재화/해금은 즉시 (PlayerManager.Awake는 sceneLoaded 이전에 끝나 있음).
        // 여기서 먼저 넣어야 HUD 첫 프레임에 값이 0으로 보였다 튀지 않는다.
        PlayerManager m = PlayerManager.Instance;
        if (m != null)
        {
            m.cellCurrency = cell;
            m.darkCellCurrency = darkCell;
            m.maxFissionCount = maxFissionCount;
            m.fissionUnlocked = fissionUnlocked;
        }

        restoring = true; // 복원이 끝날 때까지 LateUpdate의 캡처를 막는다
        StartCoroutine(ApplyToPlayerAfterLoad());
    }

    IEnumerator ApplyToPlayerAfterLoad()
    {
        // PlayerController.Start()가 currentHp = maxHp로 초기화하고 RegisterPlayer까지 끝낸 뒤에 덮어써야 한다
        yield return null;

        PlayerManager m = PlayerManager.Instance;
        PlayerController main = (m != null && m.allPlayers.Count > 0) ? m.allPlayers[0] : null;

        if (main != null)
        {
            // 최대치가 씬마다 다르게 배치돼 있을 수 있다.
            // 성장으로 올라간 최대치(보스 처치 등)는 유지해야 하므로 더 큰 쪽을 택한다.
            main.maxHp = Mathf.Max(main.maxHp, maxHp);
            main.maxFissionGauge = Mathf.Max(main.maxFissionGauge, maxGauge);

            main.SetHp(hp);
            main.SetFissionGauge(gauge);
        }
        else
        {
            // 플레이어가 없는 씬(타이틀/세이브선택/로딩)이면 들고 있던 값을 그대로 유지한다
            Debug.Log("[GameProgress] 이 씬엔 플레이어가 없어 복원을 건너뜀");
        }

        restoring = false; // 이제부터 다시 캡처 시작
    }

    // 새 게임 시작(타이틀 → 첫 씬)에서 호출. 안 부르면 이전 플레이의 체력을 그대로 물고 간다.
    public void ResetProgress()
    {
        hasData = false;
        Debug.Log("[GameProgress] 진행상황 초기화");
    }
}
