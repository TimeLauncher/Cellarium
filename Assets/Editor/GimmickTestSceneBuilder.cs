using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// A07~A09 기믹 테스트 씬 생성기.
// Heart A00을 TEST_Gimmick.unity로 복사한 뒤, 맵과 멀리 떨어진 곳(x=1000)에 기믹을 한 줄로 늘어놓고
// 플레이어·카메라·기본 부활 지점을 그리로 옮긴다. A00 원본은 건드리지 않는다.
// 메뉴: Cellarium > 기믹 테스트 씬 만들기
// Library/GimmickTestScene.request 파일이 있으면 스크립트 로드 직후 한 번 자동 실행된다.
[InitializeOnLoad]
public static class GimmickTestSceneBuilder
{
    const string SourcePath = "Assets/Scenes/Heart A00.unity";
    const string TargetPath = "Assets/Scenes/TEST_Gimmick.unity";
    const string RequestFile = "Library/GimmickTestScene.request";
    const string SquarePath = "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png";
    const string FlyingGermPath = "Assets/Sprites/monster/Flyinggerm (2).prefab";

    static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);

    static GameObject root;
    static Sprite square;

    static GimmickTestSceneBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestFile) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestFile);
            Build();
        };
    }

    [MenuItem("Cellarium/기믹 테스트 씬 만들기")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(TargetPath)) AssetDatabase.DeleteAsset(TargetPath);
        if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
        {
            Debug.LogError($"[GimmickTest] {SourcePath} 복사 실패");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
        square = LoadSquare();

        root = new GameObject("== GIMMICK TEST AREA ==");
        root.transform.position = Origin;

        BuildTerrain();
        BuildSpiderWeb(7f);
        BuildPulseFloor(15f);
        BuildMovingPlatform(25f);
        BuildBloodFlow(40f);
        BuildVisionAreas(64f);
        BuildWallAndNest(87f);
        BuildEntryPoints();
        MovePlayerAndCamera(scene, new Vector2(0f, 1.5f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log($"[GimmickTest] {TargetPath} 생성 완료. 플레이를 누르면 테스트 구역에서 시작합니다.");
    }

    // ── 구역 ──────────────────────────────────────────────

    static void BuildTerrain()
    {
        Color ground = new Color(0.35f, 0.3f, 0.3f);
        Block("Floor", new Vector2(49.5f, -0.5f), new Vector2(117f, 1f), ground, "Ground");
        Block("Wall_Left", new Vector2(-8.5f, 7f), new Vector2(1f, 15f), ground, "Ground");
        Block("Wall_Right", new Vector2(107.5f, 7f), new Vector2(1f, 15f), ground, "Ground");
        Label("START  (0)", new Vector2(0f, 3.5f));
    }

    static void BuildSpiderWeb(float x)
    {
        var go = Block("TL_SpiderWeb", new Vector2(x, 0.5f), new Vector2(5f, 1f), new Color(0.85f, 0.85f, 1f, 0.45f), null, true);
        go.AddComponent<SpiderWeb>();
        Label("1 SpiderWeb", new Vector2(x, 2.5f));
    }

    static void BuildPulseFloor(float x)
    {
        Color c = new Color(1f, 0.55f, 0.3f);
        Block("TL_PulseFloor1", new Vector2(x, 2.2f), new Vector2(2.5f, 0.4f), c, "Ground").AddComponent<PulseFloor>();
        Block("TL_PulseFloor2", new Vector2(x + 4f, 3.6f), new Vector2(2.5f, 0.4f), c, "Ground").AddComponent<PulseFloor>();
        Label("2 PulseFloor", new Vector2(x + 2f, 6f));
    }

    static void BuildMovingPlatform(float x)
    {
        var buttonA = Button("OBJ_Button_A", new Vector2(x, 0.15f), false);
        var buttonB = Button("OBJ_Button_B", new Vector2(x + 10f, 0.15f), false);

        var platform = Block("TL_MovingPlatform", new Vector2(x + 5f, 0.3f), new Vector2(3f, 0.4f), new Color(0.3f, 0.9f, 1f), "Ground");
        var body = platform.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var mp = platform.AddComponent<MovingPlatform>();
        mp.activators = new WorldActivator[] { buttonA, buttonB };
        mp.destinationOffset = new Vector2(0f, 5f);

        Block("Ledge", new Vector2(x + 8.5f, 5.5f), new Vector2(2f, 0.4f), new Color(0.35f, 0.3f, 0.3f), "Ground");
        Label("3 Button A / B -> MovingPlatform (hold)", new Vector2(x + 5f, 8f));
    }

    static void BuildBloodFlow(float x)
    {
        // 오른쪽 → 위 → 왼쪽 → 아래 한 바퀴. 아래 구간은 오른쪽 구간과 겹치지 않게 떨어뜨려 무한 루프를 막는다.
        var right = Flow("OBJ_BloodFlow_Right", new Vector2(x + 6.5f, 3f), new Vector2(11f, 1.6f), BloodFlow.Direction.Right);
        Flow("OBJ_BloodFlow_Up", new Vector2(x + 11.8f, 7.2f), new Vector2(1.6f, 10f), BloodFlow.Direction.Up);
        Flow("OBJ_BloodFlow_Left", new Vector2(x + 5.9f, 12f), new Vector2(12.8f, 1.6f), BloodFlow.Direction.Left);
        Flow("OBJ_BloodFlow_Down", new Vector2(x - 0.8f, 8.75f), new Vector2(1.6f, 7.5f), BloodFlow.Direction.Down);

        var reverse = Button("OBJ_Button_ReverseFlow", new Vector2(x + 17f, 0.15f), true);
        right.reverseActivators = new WorldActivator[] { reverse };

        Label("4 BloodFlow (R->U->L->D)", new Vector2(x + 6f, 15f));
        Label("reverse (latch)", new Vector2(x + 17f, 1.5f));
    }

    static void BuildVisionAreas(float x)
    {
        var a = Vision("EV_VisionArea_12", new Vector2(x, 3f), new Vector2(6f, 6f));
        a.hideUntilEntered = true; a.revealOnFirstEntry = true; a.darkenOutsideInside = false;
        Marker(new Vector2(x, 0.4f));
        Label("5 Vision 1+2", new Vector2(x, 7f));

        var b = Vision("EV_VisionArea_1only", new Vector2(x + 8f, 3f), new Vector2(6f, 6f));
        b.hideUntilEntered = true; b.revealOnFirstEntry = false; b.darkenOutsideInside = false;
        Marker(new Vector2(x + 8f, 0.4f));
        Label("6 Vision 1 (re-hide)", new Vector2(x + 8f, 7f));

        var c = Vision("EV_VisionArea_3", new Vector2(x + 16f, 3f), new Vector2(6f, 6f));
        c.hideUntilEntered = false; c.revealOnFirstEntry = false; c.darkenOutsideInside = true;
        Label("7 Vision 3", new Vector2(x + 16f, 7f));
    }

    static void BuildWallAndNest(float x)
    {
        var wallGo = Block("OBJ_CoagulatedWall", new Vector2(x, 4f), new Vector2(1f, 8f), new Color(0.6f, 0.1f, 0.15f), "Ground");
        var wall = wallGo.AddComponent<CoagulatedWall>();
        wall.cellDropTotal = 20;
        wall.cellDropCount = 5;

        var behind = Vision("EV_VisionArea_BehindWall", new Vector2(x + 8f, 4.5f), new Vector2(14f, 9f));
        behind.hideUntilEntered = true;
        behind.revealOnFirstEntry = false;
        behind.revealOnlyByTrigger = true;
        wall.revealOnBreak = new[] { behind };
        Label("8 CoagulatedWall (dash)", new Vector2(x, 9.5f));

        var nestGo = Block("OBJ_FlyingGermNest", new Vector2(x + 13f, 4f), new Vector2(1.5f, 1.5f), new Color(0.5f, 0.35f, 0.15f), null, true);
        var nest = nestGo.AddComponent<MonsterNest>();
        nest.maxHp = 200f;
        nest.cellDropTotal = 50;
        nest.cellDropCount = 10;
        nest.spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingGermPath);
        if (nest.spawnPrefab == null) Debug.LogWarning($"[GimmickTest] 비행균 프리팹을 못 찾음: {FlyingGermPath} — 둥지 Spawn Prefab을 직접 연결하세요");
        Label("9 FlyingGermNest", new Vector2(x + 13f, 6f));
    }

    static void BuildEntryPoints()
    {
        // JumpUp: 발판 위에 착지. 시작 위치는 발판 왼쪽 아래 빈 공간
        Block("EntryTest_Platform", new Vector2(-3f, 4f), new Vector2(3f, 0.4f), new Color(0.35f, 0.3f, 0.3f), "Ground");
        var jump = Entry("EntryTest_JumpUp", new Vector2(-3f, 5.2f), SceneEntryPoint.ArrivalStyle.JumpUp);
        jump.jumpInDepth = 2f;
        jump.jumpInSideOffset = 2.5f;

        var fall = Entry("EntryTest_FallDown", new Vector2(3f, 1f), SceneEntryPoint.ArrivalStyle.FallDown);
        fall.fallInHeight = 6f;

        Label("10 Entry test: Play -> select EntryTest_* -> component menu", new Vector2(0f, 9f));
    }

    static void MovePlayerAndCamera(Scene scene, Vector2 local)
    {
        Vector3 pos = Origin + (Vector3)local;

        foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (pc.gameObject.scene == scene) pc.transform.position = pos;

        foreach (var rp in Object.FindObjectsByType<DefaultRespawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rp.gameObject.scene == scene) rp.transform.position = pos;

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam.gameObject.scene == scene) cam.transform.position = new Vector3(pos.x, pos.y, cam.transform.position.z);

        // 시네머신: 가상 카메라 위치를 옮기고, A00 맵 경계(Confiner)는 테스트 구역 밖이라 끈다
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.gameObject.scene != scene) continue;
            string type = mb.GetType().Name;
            if (type == "CinemachineCamera")
                mb.transform.position = new Vector3(pos.x, pos.y, mb.transform.position.z);
            else if (type.StartsWith("CinemachineConfiner"))
                mb.enabled = false;
        }
    }

    // ── 도우미 ─────────────────────────────────────────────

    static GameObject Block(string name, Vector2 local, Vector2 size, Color color, string layer, bool trigger = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = local;
        if (!string.IsNullOrEmpty(layer)) go.layer = LayerMask.NameToLayer(layer);

        Vector2 spriteSize = square != null ? (Vector2)square.bounds.size : Vector2.one;
        go.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;

        var box = go.AddComponent<BoxCollider2D>();
        box.size = spriteSize;
        box.offset = square != null ? (Vector2)square.bounds.center : Vector2.zero;
        box.isTrigger = trigger;
        return go;
    }

    static GameObject Zone(string name, Vector2 local, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = local;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;
        box.isTrigger = true;
        return go;
    }

    static ButtonSwitch Button(string name, Vector2 local, bool latch)
    {
        var go = Block(name, local, new Vector2(1.2f, 0.3f), new Color(1f, 0.3f, 0.3f), "Ground");
        var b = go.AddComponent<ButtonSwitch>();
        b.latch = latch;
        return b;
    }

    static BloodFlow Flow(string name, Vector2 local, Vector2 size, BloodFlow.Direction dir)
    {
        var go = Block(name, local, size, new Color(0.9f, 0.15f, 0.15f, 0.35f), null, true);
        var f = go.AddComponent<BloodFlow>();
        f.flowDirection = dir;
        return f;
    }

    static VisionArea Vision(string name, Vector2 local, Vector2 size)
    {
        return Zone(name, local, size).AddComponent<VisionArea>();
    }

    static SceneEntryPoint Entry(string name, Vector2 local, SceneEntryPoint.ArrivalStyle style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = local;
        var e = go.AddComponent<SceneEntryPoint>();
        e.entryId = name;
        e.arrivalStyle = style;
        e.walkInDirection = SceneEntryPoint.WalkDirection.Right;
        return e;
    }

    // 암시야 확인용 표식 (가려져 있으면 안 보여야 한다)
    static void Marker(Vector2 local)
    {
        var go = new GameObject("VisionMarker");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = local;
        go.transform.localScale = Vector3.one * 0.6f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = Color.yellow;
    }

    static void Label(string text, Vector2 local)
    {
        var go = new GameObject("Label " + text);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = local;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = 0.08f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.white;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        go.GetComponent<MeshRenderer>().sortingOrder = 600;
    }

    static Sprite LoadSquare()
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (s == null) s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (s == null) Debug.LogWarning("[GimmickTest] 사각형 스프라이트를 못 찾음 — 오브젝트가 안 보일 수 있음");
        return s;
    }
}
