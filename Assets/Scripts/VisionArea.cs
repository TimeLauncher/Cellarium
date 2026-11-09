using System.Collections.Generic;
using UnityEngine;

// 시야 제한 영역(EV_VisionArea) — 이 오브젝트에 붙은 Collider2D 범위가 영역이다.
// 기획서 기능 세 가지를 각각 켜고 끌 수 있다:
//   ① hideUntilEntered      PC가 들어오기 전까지 영역을 까맣게 가린다
//   ② revealOnFirstEntry    한 번 들어오면 ①을 영구 해제한다 (끄면 나갈 때마다 다시 가려진다)
//   ③ darkenOutsideInside   PC가 영역 안에 있는 동안 영역 바깥 전체를 어둡게 한다
// 벽 파괴 등 외부 조건으로 풀 때는 Reveal()을 부른다 (CoagulatedWall이 사용).
//
// ★ A07~A09 Fix (2) 반영:
//   - "Box Collider의 형태와 딱 맞도록 형태가 고정되어 있음" →
//     이제 사각형만이 아니라 **아무 Collider2D 모양이나** 그대로 따라간다.
//     컴포넌트를 처음 붙이면 BoxCollider2D가 생기고(기본은 사각형), 자유 형태로 만들고 싶으면
//     BoxCollider2D를 지우고 Polygon Collider 2D를 붙이면 된다. 원/캡슐/Composite도 된다.
//   - "외곽선이 반듯함 → 블러, 에어브러시와 같은 음영의 형태를 띄도록" →
//     edgeFeather 만큼 바깥으로 알파가 서서히 빠지는 띠를 붙여 경계선을 흐린다.
//
// 암막은 런타임에 만든 메시라 아트가 따로 필요 없다.
public class VisionArea : MonoBehaviour
{
    [Header("기능 On/Off")]
    [Tooltip("① PC가 영역에 닿기 전까지 영역을 암시야로 표시")]
    public bool hideUntilEntered = true;
    [Tooltip("② 최초 입장 이후 ①을 영구 해제. 끄면 PC가 나갈 때마다 다시 가려진다")]
    public bool revealOnFirstEntry = true;
    [Tooltip("③ PC가 영역 안에 있을 때 영역 바깥을 암시야로 표시")]
    public bool darkenOutsideInside = false;
    [Tooltip("켜면 PC가 닿아도 ①이 풀리지 않는다 — 벽 파괴(Reveal) 같은 외부 조건으로만 풀 때 사용")]
    public bool revealOnlyByTrigger = false;

    [Header("연출")]
    public Color darkColor = Color.black;
    [Tooltip("③ 바깥 암시야의 진하기 (0~1)")]
    [Range(0f, 1f)] public float outsideDarkness = 0.85f;
    [Tooltip("경계선이 흐려지는 폭(타일). 0이면 예전처럼 칼같이 잘린 선이 된다. 1~2 권장")]
    [Min(0f)] public float edgeFeather = 1.2f;
    [Tooltip("암막이 켜지고 꺼지는 시간(초)")]
    [Min(0.01f)] public float fadeDuration = 0.4f;
    [Tooltip("비우면 가장 위쪽 Sorting Layer를 쓴다")]
    public string sortingLayerName = "";
    public int sortingOrder = 500;
    [Tooltip("③ 바깥 암막이 영역 밖으로 뻗는 거리. 화면보다 넉넉하면 된다")]
    public float outsideExtent = 60f;

    [Tooltip("해제 기록용 식별자. 비우면 계층 경로로 자동 생성")]
    public string persistentId = "";

    public bool Revealed { get; private set; }
    public bool PlayerInside { get; private set; }

    Collider2D area;
    MeshRenderer cover;   // ① 영역 안쪽 암막 (모양 그대로 + 바깥으로 흐려지는 띠)
    MeshRenderer outside; // ③ 영역 바깥 암막 (모양 바깥 전체)
    MaterialPropertyBlock block;
    float coverAlpha, outsideAlpha;
    string id;
    static Material sharedMat;

    void Reset()
    {
        // 처음 붙였을 때 기본은 사각형 — 자유 형태가 필요하면 이걸 지우고 Polygon Collider 2D를 붙이면 된다
        if (GetComponent<Collider2D>() == null)
            gameObject.AddComponent<BoxCollider2D>().size = new Vector2(6f, 4f);
    }

    void Awake()
    {
        area = GetComponent<Collider2D>();
        if (area == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(6f, 4f);
            area = box;
        }
        area.isTrigger = true;
    }

    void Start()
    {
        id = WorldState.MakeId(this, persistentId);
        Revealed = WorldState.Has(WorldCategory.Vision, id);

        block = new MaterialPropertyBlock();
        BuildMeshes();

        coverAlpha = CoverTarget() ? 1f : 0f; // 시작부터 가려져 있어야 한다 (페이드 인 금지)
        Apply();
    }

    void Update()
    {
        PlayerInside = ControlledPlayerInside();

        if (PlayerInside && hideUntilEntered && revealOnFirstEntry && !revealOnlyByTrigger && !Revealed)
            Reveal();

        float step = Time.deltaTime / fadeDuration;
        coverAlpha = Mathf.MoveTowards(coverAlpha, CoverTarget() ? 1f : 0f, step);
        outsideAlpha = Mathf.MoveTowards(outsideAlpha, darkenOutsideInside && PlayerInside ? outsideDarkness : 0f, step);
        Apply();
    }

    // 외부 조건(벽 파괴 등)으로 ①을 영구 해제
    public void Reveal()
    {
        if (Revealed) return;
        Revealed = true;
        WorldState.Record(WorldCategory.Vision, id ?? WorldState.MakeId(this, persistentId));
    }

    bool CoverTarget()
    {
        if (!hideUntilEntered || Revealed) return false;
        return revealOnlyByTrigger || !PlayerInside;
    }

    bool ControlledPlayerInside()
    {
        var m = PlayerManager.Instance;
        var pc = m != null ? m.currentPlayer : null;
        if (pc == null || area == null) return false;
        return GimmickPhysics.PlayersIn(area).Contains(pc);
    }

    void OnDisable()
    {
        SetAlpha(cover, 0f);
        SetAlpha(outside, 0f);
    }

    void OnEnable()
    {
        if (cover != null) Apply();
    }

    void OnDestroy()
    {
        if (cover != null) Destroy(cover.gameObject);
        if (outside != null) Destroy(outside.gameObject);
    }

    void Apply()
    {
        SetAlpha(cover, coverAlpha);
        SetAlpha(outside, outsideAlpha);
    }

    void SetAlpha(MeshRenderer r, float a)
    {
        if (r == null || block == null) return;
        r.enabled = a > 0.001f;
        if (!r.enabled) return;
        r.GetPropertyBlock(block);
        block.SetColor("_Color", new Color(darkColor.r, darkColor.g, darkColor.b, a));
        r.SetPropertyBlock(block);
    }

    // ── 메시 생성 ────────────────────────────────────────────────────────────

    void BuildMeshes()
    {
        List<Vector2[]> paths = ColliderPaths(area);
        if (paths.Count == 0) return;

        cover = MakeRenderer("VisionCover", BuildCoverMesh(paths));
        outside = MakeRenderer("VisionOutside", BuildOutsideMesh(paths));
    }

    // ① 영역 안쪽을 채우고, 경계에서 바깥으로 edgeFeather만큼 알파가 빠지는 띠를 덧댄다
    Mesh BuildCoverMesh(List<Vector2[]> paths)
    {
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();

        foreach (Vector2[] path in paths)
        {
            Triangulate(path, verts, colors, tris, Color.white);
            if (edgeFeather > 0f)
                AddRing(path, verts, colors, tris, 0f, edgeFeather, Color.white, Clear(Color.white));
        }
        return MakeMesh(verts, colors, tris);
    }

    // ③ 영역 바깥 전체. 경계에서 edgeFeather만큼은 알파가 0→1로 차오르고, 그 밖은 끝까지 채운다
    Mesh BuildOutsideMesh(List<Vector2[]> paths)
    {
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();

        foreach (Vector2[] path in paths)
        {
            if (edgeFeather > 0f)
                AddRing(path, verts, colors, tris, 0f, edgeFeather, Clear(Color.white), Color.white);
            AddRing(path, verts, colors, tris, edgeFeather, Mathf.Max(edgeFeather + 0.01f, outsideExtent),
                Color.white, Color.white);
        }
        return MakeMesh(verts, colors, tris);
    }

    static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);

    // 경계선을 안쪽 offset에서 바깥쪽 offset까지 넓힌 띠(ring)를 만든다.
    // 각 꼭짓점의 바깥 방향은 이웃한 두 변의 법선을 평균 낸 값(=각도 이등분선)이라 모서리가 벌어지지 않는다.
    void AddRing(Vector2[] path, List<Vector3> verts, List<Color> colors, List<int> tris,
                 float innerOffset, float outerOffset, Color innerColor, Color outerColor)
    {
        int n = path.Length;
        if (n < 3) return;

        int start = verts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 outward = OutwardNormal(path, i);
            verts.Add(path[i] + outward * innerOffset);
            colors.Add(innerColor);
            verts.Add(path[i] + outward * outerOffset);
            colors.Add(outerColor);
        }

        for (int i = 0; i < n; i++)
        {
            int a = start + i * 2;          // 이번 꼭짓점 안쪽
            int b = start + i * 2 + 1;      // 이번 꼭짓점 바깥쪽
            int c = start + ((i + 1) % n) * 2;
            int d = c + 1;

            tris.Add(a); tris.Add(b); tris.Add(d);
            tris.Add(a); tris.Add(d); tris.Add(c);
        }
    }

    // path는 항상 CCW로 맞춰서 들어온다 → 변 (p→q)의 바깥 법선은 (dy, -dx)
    static Vector2 OutwardNormal(Vector2[] path, int i)
    {
        int n = path.Length;
        Vector2 prev = EdgeNormal(path[(i - 1 + n) % n], path[i]);
        Vector2 next = EdgeNormal(path[i], path[(i + 1) % n]);
        Vector2 sum = prev + next;
        if (sum.sqrMagnitude < 0.0001f) return next;

        Vector2 dir = sum.normalized;
        // 모서리에서 폭이 좁아지지 않도록 이등분선 길이를 보정한다 (1/cos)
        float scale = Mathf.Clamp(1f / Mathf.Max(0.25f, Vector2.Dot(dir, next)), 1f, 4f);
        return dir * scale;
    }

    static Vector2 EdgeNormal(Vector2 p, Vector2 q)
    {
        Vector2 e = q - p;
        return new Vector2(e.y, -e.x).normalized;
    }

    Mesh MakeMesh(List<Vector3> verts, List<Color> colors, List<int> tris)
    {
        var mesh = new Mesh { name = "VisionArea" };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    MeshRenderer MakeRenderer(string name, Mesh mesh)
    {
        // 부모 회전/스케일에 휘둘리지 않게 씬 루트에 둔다 (메시 좌표가 이미 월드 기준). 정리는 OnDestroy에서.
        var go = new GameObject(name + " (" + gameObject.name + ")");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        go.transform.position = Vector3.zero;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = SharedMaterial();
        r.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? TopSortingLayer() : sortingLayerName;
        r.sortingOrder = sortingOrder;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enabled = false;
        return r;
    }

    // 스프라이트용 기본 셰이더를 그대로 쓴다 (정점 색 × _Color를 곱해주므로 알파 그라데이션이 나온다)
    static Material SharedMaterial()
    {
        if (sharedMat != null) return sharedMat;

        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        Shader shader = Shader.Find("Sprites/Default");
        sharedMat = new Material(shader) { mainTexture = tex };
        return sharedMat;
    }

    static string TopSortingLayer()
    {
        var layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].name : "Default";
    }

    // ── 콜라이더 → 월드 좌표 외곽선 ─────────────────────────────────────────

    // 어떤 Collider2D든 외곽선(월드 좌표, CCW)으로 바꾼다.
    // Polygon/Composite는 경로를 그대로, Box는 네 모서리, 원/캡슐은 둥글게 샘플링한다.
    static List<Vector2[]> ColliderPaths(Collider2D col)
    {
        var result = new List<Vector2[]>();
        if (col == null) return result;

        Transform t = col.transform;

        if (col is PolygonCollider2D poly)
        {
            for (int p = 0; p < poly.pathCount; p++)
            {
                Vector2[] path = poly.GetPath(p);
                var world = new Vector2[path.Length];
                for (int i = 0; i < path.Length; i++)
                    world[i] = t.TransformPoint(path[i] + poly.offset);
                result.Add(EnsureCCW(world));
            }
        }
        else if (col is CompositeCollider2D comp)
        {
            for (int p = 0; p < comp.pathCount; p++)
            {
                var path = new Vector2[comp.GetPathPointCount(p)];
                comp.GetPath(p, path);
                var world = new Vector2[path.Length];
                for (int i = 0; i < path.Length; i++)
                    world[i] = t.TransformPoint(path[i]);
                result.Add(EnsureCCW(world));
            }
        }
        else if (col is BoxCollider2D box)
        {
            Vector2 h = box.size * 0.5f;
            var local = new[]
            {
                box.offset + new Vector2(-h.x, -h.y),
                box.offset + new Vector2( h.x, -h.y),
                box.offset + new Vector2( h.x,  h.y),
                box.offset + new Vector2(-h.x,  h.y),
            };
            var world = new Vector2[4];
            for (int i = 0; i < 4; i++) world[i] = t.TransformPoint(local[i]);
            result.Add(EnsureCCW(world));
        }
        else if (col is CircleCollider2D circle)
        {
            const int segments = 32;
            var world = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                world[i] = t.TransformPoint(circle.offset +
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * circle.radius);
            }
            result.Add(EnsureCCW(world));
        }
        else
        {
            // 그 외(Edge 등)는 경계 상자로 근사한다
            Bounds b = col.bounds;
            result.Add(EnsureCCW(new[]
            {
                new Vector2(b.min.x, b.min.y), new Vector2(b.max.x, b.min.y),
                new Vector2(b.max.x, b.max.y), new Vector2(b.min.x, b.max.y),
            }));
        }

        return result;
    }

    // 바깥 법선 계산이 항상 같은 방향을 보도록 감는 방향을 CCW로 통일한다
    static Vector2[] EnsureCCW(Vector2[] path)
    {
        float sum = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 a = path[i], b = path[(i + 1) % path.Length];
            sum += (b.x - a.x) * (b.y + a.y);
        }
        if (sum <= 0f) return path; // 이미 CCW

        var reversed = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++) reversed[i] = path[path.Length - 1 - i];
        return reversed;
    }

    // ── 삼각형 분할(ear clipping) ───────────────────────────────────────────
    // 오목한 다각형도 그대로 채워야 하므로 부채꼴(fan)이 아니라 귀 자르기를 쓴다.
    static void Triangulate(Vector2[] path, List<Vector3> verts, List<Color> colors, List<int> tris, Color color)
    {
        int n = path.Length;
        if (n < 3) return;

        int start = verts.Count;
        for (int i = 0; i < n; i++) { verts.Add(path[i]); colors.Add(color); }

        var indices = new List<int>();
        for (int i = 0; i < n; i++) indices.Add(i);

        int guard = n * n; // 자기교차 등 이상한 모양에서 무한루프 방지
        while (indices.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                int i1 = indices[i];
                int i2 = indices[(i + 1) % indices.Count];

                Vector2 a = path[i0], b = path[i1], c = path[i2];
                if (Cross(b - a, c - b) <= 0f) continue; // 볼록한 꼭짓점이 아니면 귀가 아니다

                bool contains = false;
                foreach (int j in indices)
                {
                    if (j == i0 || j == i1 || j == i2) continue;
                    if (InTriangle(path[j], a, b, c)) { contains = true; break; }
                }
                if (contains) continue;

                tris.Add(start + i0); tris.Add(start + i1); tris.Add(start + i2);
                indices.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break; // 더 이상 자를 귀가 없으면 포기 (남은 모양은 아래 fan으로 마감)
        }

        for (int i = 1; i + 1 < indices.Count; i++)
        {
            tris.Add(start + indices[0]);
            tris.Add(start + indices[i]);
            tris.Add(start + indices[i + 1]);
        }
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    void OnDrawGizmos()
    {
        Collider2D col = area != null ? area : GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = new Color(0.6f, 0.4f, 1f);
        foreach (Vector2[] path in ColliderPaths(col))
            for (int i = 0; i < path.Length; i++)
                Gizmos.DrawLine(path[i], path[(i + 1) % path.Length]);
    }
}
