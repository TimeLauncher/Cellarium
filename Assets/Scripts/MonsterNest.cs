using System.Collections.Generic;
using UnityEngine;

// 몬스터 둥지(OBJ_FlyingGermNest 등) — 일정 시간마다 지정한 몬스터를 소환한다.
// 체력 0이면 즉시 파괴되고 재생성되지 않으며, 셀을 대량으로 떨군다(DestructibleObject의 셀 드랍 값).
// 비행균 둥지는 spawnPrefab에 비행균 프리팹을 넣은 것이다 — 다른 몬스터 둥지도 프리팹·아트만 바꾸면 된다.
[RequireComponent(typeof(Collider2D))]
public class MonsterNest : DestructibleObject
{
    [Header("소환")]
    [Tooltip("소환할 몬스터 프리팹 (MS_Germ_Flying 등)")]
    public GameObject spawnPrefab;
    [Tooltip("소환 주기(초)")]
    [Min(0.1f)] public float spawnInterval = 5f;
    [Tooltip("주기마다 소환하는 수")]
    [Min(1)] public int spawnPerWave = 1;
    [Tooltip("동시에 살아 있을 수 있는 최대 수. 0이면 제한 없음 (무한 증식 방지용으로 두는 걸 권장)")]
    [Min(0)] public int maxAlive = 3;
    [Tooltip("둥지 중심에서 이 반경 안에 무작위로 소환")]
    [Min(0f)] public float spawnRadius = 0.8f;
    [Tooltip("PC가 이 거리 안에 있을 때만 소환. 0이면 거리 상관없이 항상")]
    [Min(0f)] public float activationRange = 12f;
    [Tooltip("첫 소환까지 대기(초)")]
    [Min(0f)] public float firstSpawnDelay = 1f;

    // A07~A09 Fix (3): "소환된 몬스터가 소환 이후 즉시 공격 행동을 취함 / 셀을 드랍함"
    [Tooltip("소환된 몬스터가 '행동'을 시작하기까지의 시간(초). 이동/추적은 이 시간에도 한다")]
    [Min(0f)] public float spawnActionDelay = 1f;
    [Tooltip("소환된 몬스터가 셀을 떨구게 할지. 끄면(기본) 둥지로 셀을 무한히 뽑을 수 없다")]
    public bool spawnedMonstersDropCells = false;

    readonly List<GameObject> alive = new List<GameObject>();
    float timer;

    protected override void Awake()
    {
        base.Awake();
        timer = firstSpawnDelay;
    }

    // 컴포넌트를 처음 붙일 때의 기본값 — 기획: "파괴 직후 셀을 대량으로 드랍"
    void Reset()
    {
        maxHp = 200f;
        cellDropTotal = 50;
        cellDropCount = 10;
    }

    protected override void Update()
    {
        base.Update();
        if (IsDestroyed || spawnPrefab == null) return;

        alive.RemoveAll(m => m == null || IsCorpse(m));
        if (!PlayerNearby()) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = spawnInterval;

        for (int i = 0; i < spawnPerWave; i++)
        {
            if (maxAlive > 0 && alive.Count >= maxAlive) break;
            Vector2 pos = (Vector2)HitBounds.center + Random.insideUnitCircle * spawnRadius;
            GameObject spawned = Instantiate(spawnPrefab, pos, Quaternion.identity);

            // 소환 직후 행동 지연 + 셀 드랍 여부를 여기서 넣어준다 (프리팹 원본은 건드리지 않는다)
            MonsterBase mb = spawned.GetComponent<MonsterBase>();
            if (mb != null) mb.OnSpawnedFromNest(spawnActionDelay, spawnedMonstersDropCells);

            alive.Add(spawned);
        }
    }

    // 체력 0인 시체(섭취 대기)는 살아 있는 수에서 뺀다 — 안 그러면 시체가 남아 있는 동안 소환이 멈춘다
    static bool IsCorpse(GameObject m)
    {
        var mb = m.GetComponent<MonsterBase>();
        return mb != null && mb.IsConsumable; // 섭취 가능 = 체력 0 (자폭형처럼 섭취 불가인 타입은 죽으면 스스로 사라진다)
    }

    bool PlayerNearby()
    {
        if (activationRange <= 0f) return true;
        var pm = PlayerManager.Instance;
        if (pm == null) return false;
        Vector2 c = HitBounds.center;
        foreach (var p in pm.allPlayers)
            if (p != null && Vector2.Distance(c, p.transform.position) <= activationRange) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 c = transform.position;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(c, spawnRadius);
        if (activationRange > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(c, activationRange);
        }
    }
}
