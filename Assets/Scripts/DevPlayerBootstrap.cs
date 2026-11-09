#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 에디터 전용: A00이 아닌 게임 씬(A01~A09 등)에서 바로 Play를 누르면 플레이어가 없는 문제를 막는다.
// 씬에 플레이어가 없을 때만 A00에서 복사해 둔 플레이어·HUD 프리팹(DevPlayerPrefabSync)을 꺼내 배치한다.
// 빌드에는 포함되지 않는다. 실제 게임은 지금처럼 A00 → 문 이동으로 플레이어를 데려간다.
// 배치 위치 우선순위: DefaultRespawnPoint → 첫 SceneEntryPoint → 메인 카메라 위치
public static class DevPlayerBootstrap
{
    const string Folder = "Assets/Editor/DevBootstrap";
    static readonly string[] RootNames = { "PersistentPlayerRoot", "TotalOptioncontroller" };
    static readonly string[] SkipScenes = { "LoadingScene" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (GameSession.IsMenuScene(scene.name)) return;
        foreach (string s in SkipScenes) if (scene.name == s) return;

        if (Object.FindFirstObjectByType<PersistentPlayerRoot>(FindObjectsInactive.Include) != null) return;
        if (Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include) != null) return;

        foreach (string name in RootNames)
        {
            if (name == "TotalOptioncontroller" && Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include) != null) continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[DevPlayer] {Folder}/{name}.prefab 없음 — 메뉴 Cellarium > 개발용 플레이어 프리팹 갱신을 실행하세요");
                continue;
            }
            var go = Object.Instantiate(prefab);
            go.name = name;
        }

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null) return;

        Vector3 spawn = FindSpawn(pc.transform.position.z);
        var root = pc.GetComponentInParent<PersistentPlayerRoot>();
        Transform mover = root != null ? root.transform : pc.transform;
        mover.position += spawn - pc.transform.position;

        var cam = Object.FindFirstObjectByType<CinemachineCamera>();
        if (cam != null) cam.Follow = pc.transform;

        Debug.Log($"[DevPlayer] '{scene.name}'에 플레이어가 없어 A00 플레이어를 {(Vector2)spawn}에 배치함 (에디터 테스트 전용)");
    }

    static Vector3 FindSpawn(float z)
    {
        Vector3 p;
        var respawn = Object.FindFirstObjectByType<DefaultRespawnPoint>();
        var entry = Object.FindFirstObjectByType<SceneEntryPoint>();
        if (respawn != null) p = respawn.transform.position;
        else if (entry != null) p = entry.transform.position;
        else if (Camera.main != null) p = Camera.main.transform.position;
        else p = Vector3.zero;
        p.z = z;
        return p;
    }
}
#endif
