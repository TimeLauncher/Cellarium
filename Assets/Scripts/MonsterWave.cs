using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 몬스터 웨이브 (기획서 A12~A13 (5)).
//   PC가 이 영역에 들어오면 시작 → 연결된 출입구가 닫힌다(Gates 오브젝트가 켜짐)
//   → 웨이브마다 정해 둔 몬스터를 정해 둔 위치에 생성 → 그 웨이브 몬스터를 전부 잡으면 대기 후 다음 웨이브
//   → 마지막 웨이브까지 끝나면 출입구가 열리고(Gates가 꺼짐) 완료 상태가 저장된다. 완료된 웨이브는 다시 나오지 않는다.
//   웨이브에서 생성된 몬스터는 셀을 떨구지 않는다.
//   싸우는 중에 죽으면 씬이 다시 불러와지므로 웨이브도 처음부터 다시 한다(완료 전이라 기록이 없다).
//
// 배치법
//   빈 오브젝트 + BoxCollider2D(시작 영역 크기, 자동으로 트리거가 됨) + 이 컴포넌트.
//   Gates: 닫힐 때 길을 막을 벽 오브젝트들 (처음엔 꺼 둬도 되고 켜 둬도 된다 — 시작 전엔 자동으로 꺼진다)
//   Waves: 웨이브 수만큼 칸을 늘리고, 칸마다 Monsters에 (몬스터 프리팹, 생성 위치) 를 원하는 만큼 넣는다.
//          생성 위치는 빈 오브젝트를 놓고 연결. 비우면 이 오브젝트 위치에 생성된다.
[RequireComponent(typeof(Collider2D))]
public class MonsterWave : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        [Tooltip("생성할 몬스터 프리팹")]
        public GameObject monster;
        [Tooltip("생성 위치. 비우면 웨이브 오브젝트 위치")]
        public Transform spawnPoint;
    }

    [System.Serializable]
    public class Wave
    {
        [Tooltip("이 웨이브에 나오는 몬스터들 (여러 마리 가능)")]
        public SpawnEntry[] monsters = new SpawnEntry[0];
        [Tooltip("생성 연출 시간 — 생성된 뒤 행동(공격)을 시작하기까지의 시간(초). 몬스터 둥지와 같은 방식")]
        [Min(0f)] public float spawnActionDelay = 1f;
        [Tooltip("이 웨이브를 다 잡고 다음 웨이브가 시작될 때까지 대기(초)")]
        [Min(0f)] public float delayBeforeNext = 2f;
    }

    [Header("웨이브")]
    public Wave[] waves = new Wave[1];
    [Tooltip("영역에 들어오고 첫 웨이브가 나오기까지 대기(초)")]
    [Min(0f)] public float startDelay = 0.5f;

    [Header("출입구")]
    [Tooltip("웨이브가 시작되면 켜져서 길을 막고, 모두 끝나면 꺼지는 오브젝트들")]
    public GameObject[] gates = new GameObject[0];

    [Header("연출 연결 (선택)")]
    public UnityEvent onStarted;
    public UnityEvent onCompleted;

    [Header("기록")]
    [Tooltip("완료 기록용 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    enum State { Idle, Running, Completed }
    State state = State.Idle;
    Collider2D area;
    string id;
    readonly List<MonsterBase> alive = new List<MonsterBase>();

    public int CurrentWave { get; private set; } = -1;

    void Awake()
    {
        area = GetComponent<Collider2D>();
        area.isTrigger = true;
    }

    void Start()
    {
        id = "wave:" + WorldState.MakeId(this, persistentId);
        if (WorldState.Has(WorldCategory.Event, id))
        {
            state = State.Completed;
            SetGates(false);
            return;
        }
        SetGates(false);
    }

    void Update()
    {
        if (state != State.Idle) return;
        if (!PlayerInside()) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        state = State.Running;
        SetGates(true);
        onStarted?.Invoke();
        Debug.Log($"[MonsterWave] {name} 시작 ({waves.Length}웨이브)");

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < waves.Length; i++)
        {
            CurrentWave = i;
            Wave w = waves[i];
            if (w == null) continue;

            SpawnWave(w);
            // 전부 잡을 때까지 (시체 = 섭취 대기 상태도 잡은 것으로 친다)
            while (true)
            {
                alive.RemoveAll(m => m == null || m.IsConsumable);
                if (alive.Count == 0) break;
                yield return null;
            }

            if (i < waves.Length - 1 && w.delayBeforeNext > 0f)
                yield return new WaitForSeconds(w.delayBeforeNext);
        }

        state = State.Completed;
        WorldState.Record(WorldCategory.Event, id);
        SetGates(false);
        onCompleted?.Invoke();
        Debug.Log($"[MonsterWave] {name} 완료");
    }

    void SpawnWave(Wave w)
    {
        if (w.monsters == null) return;
        foreach (var e in w.monsters)
        {
            if (e == null || e.monster == null) continue;
            Vector3 pos = e.spawnPoint != null ? e.spawnPoint.position : transform.position;
            GameObject go = Instantiate(e.monster, pos, Quaternion.identity);

            // 둥지와 같은 방식 — 생성 직후 행동 지연, 셀 드랍 없음, 사망 기록 안 함
            MonsterBase mb = go.GetComponent<MonsterBase>();
            if (mb == null) continue;
            mb.OnSpawnedFromNest(w.spawnActionDelay, false);
            alive.Add(mb);
        }
    }

    void SetGates(bool closed)
    {
        if (gates == null) return;
        foreach (var g in gates) if (g != null) g.SetActive(closed);
    }

    bool PlayerInside()
    {
        Bounds b = area.bounds;
        foreach (var h in Physics2D.OverlapBoxAll(b.center, b.size, 0f))
            if (h != null && h.GetComponentInParent<PlayerController>() != null) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (waves == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        foreach (var w in waves)
        {
            if (w?.monsters == null) continue;
            foreach (var e in w.monsters)
                if (e?.spawnPoint != null) Gizmos.DrawWireSphere(e.spawnPoint.position, 0.5f);
        }
    }
}
