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

    bool pendingRespawn;

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

        PlayerManager m = PlayerManager.Instance;
        if (m != null)
        {
            savedCell = m.cellCurrency;
            savedDarkCell = m.darkCellCurrency;
            savedMaxFission = m.maxFissionCount;
            savedFissionUnlocked = m.fissionUnlocked;
        }
    }

    // 사망 시 호출 — 체크포인트(없으면 현재) 씬을 리로드. 리로드로 모든 배치 오브젝트가 초기 상태로 돌아간다.
    public void Respawn()
    {
        pendingRespawn = true;
        string scene = hasCheckpoint ? checkpointScene : SceneManager.GetActiveScene().name;
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
            m.fissionUnlocked = savedFissionUnlocked;
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
        main.RestoreFromConsume(main.maxHp, main.maxFissionGauge); // 체력/분열 게이지 완전 회복
    }
}
