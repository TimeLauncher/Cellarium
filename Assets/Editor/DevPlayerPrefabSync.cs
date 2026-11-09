using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// A00에만 있는 PersistentPlayerRoot(플레이어)와 TotalOptioncontroller(HUD·일시정지 UI)를
// 에디터 전용 프리팹으로 복사해 둔다. A01 등 다른 씬에서 바로 Play할 때 DevPlayerBootstrap이 이걸 꺼내 쓴다.
// A00 원본은 건드리지 않는다. Play 직전에 A00이 프리팹보다 새로우면 자동으로 다시 복사한다.
// 메뉴: Cellarium > 개발용 플레이어 프리팹 갱신
[InitializeOnLoad]
public static class DevPlayerPrefabSync
{
    const string SourceScene = "Assets/Scenes/Heart A00.unity";
    public const string Folder = "Assets/Editor/DevBootstrap";
    public static readonly string[] RootNames = { "PersistentPlayerRoot", "TotalOptioncontroller" };

    static DevPlayerPrefabSync()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode && IsStale()) Sync();
        };
    }

    public static string PrefabPath(string rootName) => $"{Folder}/{rootName}.prefab";

    static bool IsStale()
    {
        var sceneTime = File.GetLastWriteTimeUtc(SourceScene);
        foreach (string name in RootNames)
        {
            string path = PrefabPath(name);
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < sceneTime) return true;
        }
        return false;
    }

    [MenuItem("Cellarium/개발용 플레이어 프리팹 갱신")]
    public static void Sync()
    {
        if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

        Scene preview = EditorSceneManager.OpenPreviewScene(SourceScene);
        try
        {
            foreach (string name in RootNames)
            {
                GameObject root = null;
                foreach (var go in preview.GetRootGameObjects())
                    if (go.name == name) { root = go; break; }

                if (root == null)
                {
                    Debug.LogWarning($"[DevPlayer] {SourceScene}에서 '{name}'을 찾지 못함 — 이름이 바뀌었으면 DevPlayerPrefabSync.RootNames를 고치세요");
                    continue;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
        Debug.Log($"[DevPlayer] {SourceScene}에서 플레이어·HUD 프리팹 갱신 완료 ({Folder})");
    }
}
