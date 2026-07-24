using UnityEngine;

// 자폭형(부유균/부유산포균)의 폭발 예정 범위를 화면에 원으로 표시한다.
// (이펙트 에셋이 나오기 전까지의 임시 표시 — HitboxVisualizer와 같은 "런타임 생성" 방식)
// FloaterGerm이 자폭 준비를 시작할 때 Begin()으로 붙여주며, 몬스터가 파괴되면 자식으로 같이 사라진다.
//   준비가 진행될수록 점점 진하게 + 깜빡여서 곧 터진다는 걸 예고한다.
[DisallowMultipleComponent]
public class ExplosionRangeIndicator : MonoBehaviour
{
    private SpriteRenderer view;
    private float radius = 1.5f;
    private float duration = 1.5f;
    private float timer;
    private readonly Color warnColor = new Color(1f, 0.35f, 0.1f, 1f);

    private static Sprite sharedCircle;

    // explosionRadius: OverlapCircle 반경과 동일하게, windup: 자폭까지 걸리는 시간
    public void Begin(float explosionRadius, float windup, int sortingOrder = 40)
    {
        radius = Mathf.Max(0.01f, explosionRadius);
        duration = Mathf.Max(0.01f, windup);
        timer = 0f;
        EnsureView(sortingOrder);
        Apply();
    }

    void EnsureView(int sortingOrder)
    {
        if (sharedCircle == null) sharedCircle = MakeCircleSprite(64);

        if (view == null)
        {
            GameObject go = new GameObject("ExplosionRangeView");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            view = go.AddComponent<SpriteRenderer>();
            view.sprite = sharedCircle;
            view.sortingOrder = sortingOrder;
        }

        // 스프라이트는 지름 1유닛 기준으로 만들었으므로 지름(= 반지름 × 2)으로 스케일한다.
        // (부모 몬스터 스케일이 1이 아닐 수 있어 localScale 대신 lossyScale 보정)
        Vector3 parentLossy = transform.lossyScale;
        float sx = Mathf.Approximately(parentLossy.x, 0f) ? 1f : radius * 2f / parentLossy.x;
        float sy = Mathf.Approximately(parentLossy.y, 0f) ? 1f : radius * 2f / parentLossy.y;
        view.transform.localScale = new Vector3(sx, sy, 1f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        Apply();
    }

    void Apply()
    {
        if (view == null) return;
        float t = Mathf.Clamp01(timer / duration);          // 진행도 (0 → 1)
        float blink = 0.5f + 0.5f * Mathf.Sin(timer * 20f);  // 깜빡임
        Color c = warnColor;
        c.a = Mathf.Lerp(0.2f, 0.55f, t) * Mathf.Lerp(0.6f, 1f, blink);
        view.color = c;
    }

    // 속이 채워진 원 스프라이트를 런타임에 생성 (pixelsPerUnit = size → 지름 1유닛)
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
}
