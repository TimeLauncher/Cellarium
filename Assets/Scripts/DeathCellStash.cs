using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 화이트셀이 죽을 때 들고 있던 셀을 떨군 '사망 셀 덩어리' 기록 (기타 메모 '화이트셀 사망 시 셀 덩어리 드랍').
//
//   - 죽은 자리에 덩어리가 남는다. 주우면 그만큼 셀을 되찾는다.
//   - 어떤 상황에서도 사라지지 않는다 — 안 줍고 또 죽어도 이전 덩어리는 그대로 있고, 새 덩어리가 하나 더 생긴다.
//   - 덩어리는 '부활 시점'에 생긴다. 죽은 직후(사망 모션 중)엔 보이지 않는다.
//     부활은 씬 리로드라서, 씬이 로드될 때 그 씬에 남아 있는 덩어리를 전부 다시 만들어주면 된다.
//     다른 씬에서 죽은 덩어리는 그 씬에 다시 들어갈 때 만들어진다.
//
// 씬에 배치할 필요 없음. 떨구는 양(%)은 PlayerController의 '사망/부활' 항목에서 조절한다.
public static class DeathCellStash
{
    class Entry
    {
        public string scene;
        public Vector3 position;
        public int amount;
        public GameObject prefab;
    }

    static readonly List<Entry> entries = new List<Entry>();

    const float PickupDelayAfterRespawn = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        entries.Clear(); // 도메인 리로드를 끈 에디터에서도 이전 플레이 기록이 남지 않게
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 사망 시 호출. prefab을 비우면 기본 셀 덩어리(Resources/Effects/CellDrop) 모습으로 나온다.
    public static void Add(string scene, Vector3 position, int amount, GameObject prefab)
    {
        if (amount <= 0 || string.IsNullOrEmpty(scene)) return;
        entries.Add(new Entry { scene = scene, position = position, amount = amount, prefab = prefab });
    }

    // 새 게임 / 타이틀 진입 시 (GameSession)
    public static void Clear()
    {
        entries.Clear();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (Entry e in entries)
            if (e.scene == scene.name)
                Spawn(e);
    }

    static void Spawn(Entry e)
    {
        CellChunk chunk = CellChunk.Spawn(e.position, e.amount, e.prefab);
        if (chunk == null) return;

        // ★ 플레이어는 씬을 넘어 유지되고, 부활 위치로 옮겨지는 건 로드 다음 프레임이다(RespawnManager).
        //   그래서 덩어리가 생기는 이 순간엔 플레이어가 아직 죽은 자리 = 덩어리 위에 겹쳐 있어서
        //   생기자마자 도로 주워져 '안 나오는' 것처럼 보였다. 부활 위치로 옮겨질 때까지 줍지 못하게 한다.
        chunk.pickupDelay = Mathf.Max(chunk.pickupDelay, PickupDelayAfterRespawn);

        // 주웠을 때만 기록을 지운다 — 씬을 나가서 덩어리 오브젝트가 사라져도 기록은 남아 다음에 다시 생긴다
        chunk.onCollected = () => entries.Remove(e);
    }
}
