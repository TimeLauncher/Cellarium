using System.Collections.Generic;
using UnityEngine;

// 맵에 배치된 오브젝트가 '이미 처리됐다'는 사실을 씬 리로드 너머로 들고 다닌다.
// (열린 문 / 먹은 셀 덩어리 / 섭취한 다크셀 / 활성화된 세이브포인트)
//
// ★ 왜 필요한가:
//   RespawnManager는 부활할 때 씬을 통째로 리로드한다. 몬스터·버튼을 초기 상태로 되돌리려면
//   그래야 하는데, 그 부작용으로 이미 연 문이 닫히고 이미 먹은 코인이 되살아난다.
//   이 매니저가 '무엇을 이미 했는지'만 따로 기억했다가 리로드된 씬에 다시 적용해준다.
//
// ★ 부활 시 무엇을 유지할지는 카테고리별로 고른다 (WorldStateSettings 컴포넌트로 씬에서 변경).
//   ResetOnRespawn       부활하면 초기 상태로 — 몬스터처럼 다시 풀려야 하는 것
//   RestoreToCheckpoint  마지막 세이브 시점 상태로 되돌린다 (기본값)
//   AlwaysPersist        죽어도 그대로 유지 — 한 번 하면 절대 안 되돌아감
//
// 씬에 배치할 필요 없음 — RespawnManager처럼 시작 시 자동 생성된다.
public enum WorldCategory
{
    Door,       // 버튼 퍼즐로 열리는 문
    Pickup,     // 셀 덩어리 등 주워 먹는 재화
    DarkCell,   // 다크셀 잔재 (능력 해금)
    SavePoint,  // 세이브포인트 활성화 여부
    Event,      // 이미 본 연출/이벤트 트리거 (EventTriggerZone)
}

public enum PersistMode
{
    ResetOnRespawn,
    RestoreToCheckpoint,
    AlwaysPersist,
}

public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }

    readonly Dictionary<WorldCategory, PersistMode> modes = new Dictionary<WorldCategory, PersistMode>
    {
        // 재화·능력은 기본을 RestoreToCheckpoint로 둔다. AlwaysPersist로 바꾸면
        // RespawnManager가 재화를 세이브 시점으로 되돌리는 것과 어긋나 손해가 난다 (아래 SetMode 주석 참고).
        { WorldCategory.Door,      PersistMode.RestoreToCheckpoint },
        { WorldCategory.Pickup,    PersistMode.RestoreToCheckpoint },
        { WorldCategory.DarkCell,  PersistMode.RestoreToCheckpoint },
        { WorldCategory.SavePoint, PersistMode.AlwaysPersist },

        // 강제 연출은 한 번 봤으면 다시 안 보는 편이 낫다.
        // 세이브 전으로 되돌리고 싶으면 WorldStateSettings에서 RestoreToCheckpoint로 바꿀 것.
        { WorldCategory.Event,     PersistMode.AlwaysPersist },
    };

    readonly Dictionary<WorldCategory, HashSet<string>> live = NewTable();       // 지금 진행 중인 상태
    readonly Dictionary<WorldCategory, HashSet<string>> checkpoint = NewTable(); // 마지막 세이브 시점 스냅샷

    static Dictionary<WorldCategory, HashSet<string>> NewTable()
    {
        var table = new Dictionary<WorldCategory, HashSet<string>>();
        foreach (WorldCategory c in System.Enum.GetValues(typeof(WorldCategory)))
            table[c] = new HashSet<string>();
        return table;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("WorldState").AddComponent<WorldState>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 정책 ────────────────────────────────────────────────────────────────

    public PersistMode GetMode(WorldCategory category) => modes[category];

    public void SetMode(WorldCategory category, PersistMode mode)
    {
        modes[category] = mode;
    }

    // ── 기록 / 조회 ─────────────────────────────────────────────────────────

    // 정적 래퍼 — 호출부에서 매번 null 검사를 하지 않아도 되게 한다
    public static void Record(WorldCategory category, string id)
    {
        if (Instance != null && !string.IsNullOrEmpty(id)) Instance.live[category].Add(id);
    }

    public static bool Has(WorldCategory category, string id)
    {
        return Instance != null && !string.IsNullOrEmpty(id) && Instance.live[category].Contains(id);
    }

    // 다크셀을 하나라도 먹은 적이 있는가.
    // RespawnManager가 분열 해금을 되돌릴 때 소프트락을 막는 데 쓴다.
    public static bool AnyDarkCellConsumed()
    {
        return Instance != null && Instance.live[WorldCategory.DarkCell].Count > 0;
    }

    // ── 체크포인트 / 부활 ───────────────────────────────────────────────────

    // 세이브포인트 활성화 시 호출 — 지금 상태를 스냅샷으로 떠둔다
    public void CaptureCheckpoint()
    {
        foreach (WorldCategory c in System.Enum.GetValues(typeof(WorldCategory)))
        {
            checkpoint[c].Clear();
            foreach (string id in live[c]) checkpoint[c].Add(id);
        }
    }

    // 부활 직전 호출 — 카테고리별 정책대로 live를 정리한다.
    // ★ 반드시 SceneManager.LoadScene 이전에 불러야 한다.
    //   Unity는 새 씬 오브젝트의 Awake를 sceneLoaded 콜백보다 먼저 돌리므로,
    //   로드 후에 정리하면 문·코인이 이미 낡은 상태를 읽어버린다.
    public void ApplyRespawnPolicy()
    {
        foreach (WorldCategory c in System.Enum.GetValues(typeof(WorldCategory)))
        {
            switch (modes[c])
            {
                case PersistMode.ResetOnRespawn:
                    live[c].Clear();
                    break;

                case PersistMode.RestoreToCheckpoint:
                    live[c].Clear();
                    foreach (string id in checkpoint[c]) live[c].Add(id);
                    break;

                case PersistMode.AlwaysPersist:
                    break; // 손대지 않는다
            }
        }
    }

    // 타이틀로 나가거나 새 게임을 시작할 때
    public void ClearAll()
    {
        foreach (WorldCategory c in System.Enum.GetValues(typeof(WorldCategory)))
        {
            live[c].Clear();
            checkpoint[c].Clear();
        }
    }

    // ── 식별자 ──────────────────────────────────────────────────────────────

    // 씬을 리로드해도 같은 오브젝트가 같은 문자열을 받아야 한다.
    // explicitId를 비워두면 계층 경로 + 형제 인덱스로 자동 생성한다 —
    // 이름이 같은 코인이 여러 개여도 인덱스 덕분에 서로 구분된다.
    // 단 에디터에서 오브젝트를 옮기거나 순서를 바꾸면 id가 달라지므로,
    // 그게 걱정되면 컴포넌트의 Persistent Id 칸에 직접 이름을 적어두면 된다.
    public static string MakeId(Component target, string explicitId)
    {
        if (target == null) return null;

        string scene = target.gameObject.scene.name;
        if (!string.IsNullOrEmpty(explicitId)) return scene + "/" + explicitId;

        var sb = new System.Text.StringBuilder();
        BuildPath(target.transform, sb);
        return scene + "/" + sb;
    }

    static void BuildPath(Transform t, System.Text.StringBuilder sb)
    {
        if (t.parent != null)
        {
            BuildPath(t.parent, sb);
            sb.Append('/');
        }
        sb.Append(t.name).Append('[').Append(t.GetSiblingIndex()).Append(']');
    }
}
