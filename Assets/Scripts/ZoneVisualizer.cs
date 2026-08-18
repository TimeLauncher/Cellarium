using UnityEngine;

// 눈에 안 보이는 '구역'을 게임 화면에서 보이게 해주는 공용 표시기.
//
// 회복 영역·이벤트 트리거·음향 범위는 전부 콜라이더나 반경일 뿐이라 Scene 뷰 기즈모로만 보이고
// 실제 플레이 화면(Game 뷰)에서는 아무것도 안 보인다. 이펙트 에셋이 나오기 전까지
// 런타임에 사각형/원을 만들어 덮어준다 (HitboxVisualizer·ExplosionRangeIndicator와 같은 방식).
//
// 직접 붙이지 말 것 — 각 구역 스크립트가 Show Area를 켜면 알아서 붙인다.
[DisallowMultipleComponent]
public class ZoneVisualizer : MonoBehaviour
{
    SpriteRenderer fill;
    TextMesh label;

    static Sprite sharedSquare;
    static Sprite sharedCircle;

    // 콜라이더 모양대로 사각형을 덮는다
    public static ZoneVisualizer ShowBox(GameObject host, Collider2D area, Color color, string text, int sortingOrder)
    {
        if (host == null || area == null) return null;

        ZoneVisualizer v = Attach(host);
        if (sharedSquare == null) sharedSquare = MakeSquareSprite();

        if (!TryGetLocalShape(area, out Vector2 center, out Vector2 size)) return v;

        v.EnsureFill(sharedSquare, sortingOrder);
        v.fill.color = color;
        v.fill.transform.localPosition = center;
        v.fill.transform.localScale = new Vector3(size.x, size.y, 1f);

        v.EnsureLabel(text, center.y + size.y * 0.5f + 0.4f, sortingOrder);
        return v;
    }

    // 반경(월드 단위)만큼 원을 덮는다
    public static ZoneVisualizer ShowCircle(GameObject host, float worldRadius, Color color, string text, int sortingOrder)
    {
        if (host == null) return null;

        ZoneVisualizer v = Attach(host);
        if (sharedCircle == null) sharedCircle = MakeCircleSprite(96);

        v.EnsureFill(sharedCircle, sortingOrder);
        v.fill.color = color;
        v.fill.transform.localPosition = Vector3.zero;

        // 스프라이트는 지름 1유닛 — 부모 스케일이 1이 아닐 수 있으니 보정한다
        Vector3 lossy = host.transform.lossyScale;
        float sx = Mathf.Approximately(lossy.x, 0f) ? 1f : worldRadius * 2f / lossy.x;
        float sy = Mathf.Approximately(lossy.y, 0f) ? 1f : worldRadius * 2f / lossy.y;
        v.fill.transform.localScale = new Vector3(sx, sy, 1f);

        v.EnsureLabel(text, worldRadius + 0.4f, sortingOrder);
        return v;
    }

    // 표시를 끈다 (이벤트 트리거가 한 번 발동한 뒤 등). 이 컴포넌트는 구역 스크립트와 같은
    // 오브젝트에 붙으므로 gameObject.SetActive(false)를 하면 구역까지 죽는다 — 자식 표시만 끈다.
    public void Hide()
    {
        if (fill != null) fill.enabled = false;
        if (label != null) label.gameObject.SetActive(false);
    }

    static ZoneVisualizer Attach(GameObject host)
    {
        ZoneVisualizer v = host.GetComponent<ZoneVisualizer>();
        if (v == null) v = host.AddComponent<ZoneVisualizer>();
        return v;
    }

    void EnsureFill(Sprite sprite, int sortingOrder)
    {
        if (fill == null)
        {
            GameObject go = new GameObject("ZoneFill");
            go.transform.SetParent(transform, false);
            fill = go.AddComponent<SpriteRenderer>();
        }
        fill.sprite = sprite;
        fill.sortingOrder = sortingOrder;
    }

    // 무엇을 하는 구역인지 글씨로 띄운다 (기획자 시연용 — 텍스트를 비우면 안 만든다).
    // Canvas가 아니라 TextMesh라서 해상도·CanvasScaler 설정과 무관하게 월드에 그대로 뜬다.
    void EnsureLabel(string text, float localY, int sortingOrder)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (label != null) label.gameObject.SetActive(false);
            return;
        }

        if (label == null)
        {
            GameObject go = new GameObject("ZoneLabel");
            go.transform.SetParent(transform, false);

            label = go.AddComponent<TextMesh>();
            label.font = GetDefaultFont();
            label.fontSize = 64;
            label.characterSize = 0.06f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;

            // TextMesh는 폰트의 머티리얼을 그대로 써야 글자가 보인다
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && label.font != null)
            {
                mr.sharedMaterial = label.font.material;
                mr.sortingOrder = sortingOrder + 1;
            }
        }

        label.gameObject.SetActive(true);
        label.text = text;

        // 부모 스케일이 커도 글씨 크기는 그대로 유지
        Vector3 lossy = transform.lossyScale;
        float inv = Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x;
        label.transform.localScale = new Vector3(inv, inv, 1f);
        label.transform.localPosition = new Vector3(0f, localY, 0f);
    }

    // 콜라이더가 꺼져 있어도 되도록 로컬 모양을 직접 읽는다 (HitboxVisualizer와 같은 이유)
    static bool TryGetLocalShape(Collider2D c, out Vector2 center, out Vector2 size)
    {
        if (c is BoxCollider2D box)
        {
            center = box.offset;
            size = box.size;
            return true;
        }

        if (c is CircleCollider2D circle)
        {
            center = circle.offset;
            size = Vector2.one * circle.radius * 2f;
            return true;
        }

        if (c is CapsuleCollider2D capsule)
        {
            center = capsule.offset;
            size = capsule.size;
            return true;
        }

        center = default;
        size = default;
        return false;
    }

    static Sprite MakeSquareSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    static Sprite MakeCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        Vector2 center = new Vector2(r, r);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                tex.SetPixel(x, y, d <= r ? Color.white : Color.clear);
            }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Font GetDefaultFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }
}
