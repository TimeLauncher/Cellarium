using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 사망 시 체크포인트 씬을 리로드해 몬스터·버튼·문·셀덩어리 등 배치 오브젝트를 전부 '초기 상태'로 되돌린다.
// 플레이어의 진행상황(위치/재화/분열 해금·횟수)은 마지막 세이브포인트에서 찍어둔 스냅샷으로 복원한다.
// 씬을 넘나들어도 유지되도록 DontDestroyOnLoad 싱글턴으로, 시작 시 자동 생성된다(씬에 배치 불필요).
public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    // 체크포인트 스냅샷 (씬 리로드해도 이 매니저가 살아있어 유지됨)
    bool hasCheckpoint;
    string checkpointScene;
    Vector3 checkpointPos;
    int savedCell, savedDarkCell, savedMaxFission;
    bool savedFissionUnlocked;

    // 세이브포인트를 한 번도 안 찍었을 때 돌아갈 지점 (DefaultRespawnPoint 마커가 등록해준다)
    bool hasDefaultRespawn;
    string defaultScene;
    Vector3 defaultPos;

    bool pendingRespawn;
    bool respawnUsedDefault; // 이번 부활이 '기본 지점' 부활인지 (체크포인트 부활과 위치 적용이 다르다)

    // 부활로 인한 씬 리로드가 진행 중인지. GameProgress가 이때는 진행상황을 덮어쓰지 않도록 참조한다
    // (부활은 '체크포인트 시점으로 되돌리기'라서, 죽기 직전 상태를 이어붙이면 안 된다)
    public bool RespawnInProgress { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("RespawnManager").AddComponent<RespawnManager>();
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

    // 세이브포인트 활성화 시 호출 — 현재 진행상황을 체크포인트로 저장
    public void SaveCheckpoint(Vector3 pos)
    {
        hasCheckpoint = true;
        checkpointScene = SceneManager.GetActiveScene().name;
        checkpointPos = pos;

        // 열린 문·먹은 코인 등 맵 상태도 같은 시점으로 스냅샷을 떠둔다
        if (WorldState.Instance != null)
            WorldState.Instance.CaptureCheckpoint();

        PlayerManager m = PlayerManager.Instance;
        if (m != null)
        {
            savedCell = m.cellCurrency;
            savedDarkCell = m.darkCellCurrency;
            savedMaxFission = m.maxFissionCount;
            savedFissionUnlocked = m.fissionUnlocked;
        }
    }

    // 타이틀로 나가거나 새 게임을 시작할 때 호출 (GameSession) — 찍어둔 체크포인트를 버린다.
    // 이걸 안 하면 새 게임을 시작해도 이전 판의 세이브 지점으로 부활한다.
    public void ClearCheckpoint()
    {
        hasCheckpoint = false;
        checkpointScene = null;
        checkpointPos = Vector3.zero;
        savedCell = savedDarkCell = savedMaxFission = 0;
        savedFissionUnlocked = false;

        // 기본 부활 지점도 함께 버린다 — A00의 DefaultRespawnPoint가 씬 로드 때 다시 등록해준다.
        // (남겨두면 이전 판에서 등록된 다른 씬 좌표를 물고 갈 수 있다)
        hasDefaultRespawn = false;
        defaultScene = null;
        defaultPos = Vector3.zero;

        pendingRespawn = false;
        respawnUsedDefault = false;
        RespawnInProgress = false;
    }

    // DefaultRespawnPoint 마커가 Awake에서 호출 — 세이브 없을 때 돌아갈 지점 등록
    public void SetDefaultRespawn(string scene, Vector3 pos)
    {
        hasDefaultRespawn = true;
        defaultScene = scene;
        defaultPos = pos;
    }

    // 사망 시 호출 — 체크포인트(없으면 기본 지점, 그것도 없으면 현재) 씬을 리로드.
    // 리로드로 모든 배치 오브젝트가 초기 상태로 돌아간다.
    public void Respawn()
    {
        pendingRespawn = true;
        RespawnInProgress = true;

        // 문으로 이동하다 죽은 경우 등, 남아 있는 도착 지점 예약이 부활 위치를 덮지 않도록 지운다
        SceneEntryPoint.ClearEntry();

        // ★ 반드시 LoadScene 이전. 새 씬 오브젝트의 Awake가 sceneLoaded 콜백보다 먼저 돌기 때문에,
        //   로드 후에 정리하면 문·코인이 낡은 상태를 읽고 시작한다.
        if (WorldState.Instance != null)
            WorldState.Instance.ApplyRespawnPolicy();

        string scene;
        if (hasCheckpoint)
        {
            respawnUsedDefault = false;
            scene = checkpointScene;
        }
        else if (hasDefaultRespawn)
        {
            // 세이브포인트를 한 번도 안 찍었으면 게임 시작 지점(보통 A00)으로 되돌린다.
            // 죽은 그 자리에서 다시 시작하면 사실상 페널티가 없다.
            respawnUsedDefault = true;
            scene = defaultScene;
        }
        else
        {
            respawnUsedDefault = false;
            scene = SceneManager.GetActiveScene().name;
            Debug.LogWarning("[RespawnManager] 세이브포인트도 DefaultRespawnPoint도 없어 현재 씬 그 자리에서 부활합니다. " +
                             "A00에 DefaultRespawnPoint를 배치하세요.");
        }

        SceneManager.LoadScene(scene);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingRespawn) return;
        pendingRespawn = false;

        // 재화/해금은 즉시 복원 (PlayerManager.Awake는 sceneLoaded 이전에 끝나 있음).
        // 여기서 먼저 넣어야 HUD 첫 프레임에 '획득 팝업'이 잘못 뜨지 않는다.
        PlayerManager m = PlayerManager.Instance;
        if (m != null && hasCheckpoint)
        {
            m.cellCurrency = savedCell;
            m.darkCellCurrency = savedDarkCell;
            m.maxFissionCount = savedMaxFission;

            // 다크셀 잔재가 '먹은 것'으로 남아 있는데 해금만 되돌리면 맵에 잔재가 없어
            // 능력을 영영 못 얻는 소프트락이 된다. 잔재가 사라진 상태면 해금도 유지한다.
            m.fissionUnlocked = savedFissionUnlocked || WorldState.AnyDarkCellConsumed();
        }

        // 플레이어 위치는 등록(Start)이 끝난 뒤 한 프레임 후 적용
        StartCoroutine(ApplyPositionAfterLoad());
    }

    IEnumerator ApplyPositionAfterLoad()
    {
        yield return null; // 플레이어 등록(RegisterPlayer)까지 대기

        PlayerManager m = PlayerManager.Instance;
        if (m == null || m.allPlayers.Count == 0) yield break;

        PlayerController main = m.allPlayers[0];
        if (hasCheckpoint) main.transform.position = checkpointPos;
        else if (respawnUsedDefault) main.transform.position = defaultPos;
        main.RestoreFromConsume(main.maxHp, main.maxFissionGauge); // 체력/분열 게이지 완전 회복

        // 부활 복원이 끝난 뒤에야 진행상황 저장을 재개시킨다
        RespawnInProgress = false;
        if (GameProgress.Instance != null) GameProgress.Instance.CaptureNow();
    }
}
