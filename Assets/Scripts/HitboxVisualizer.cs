using UnityEngine;

// 공격 히트박스를 화면에 보여준다 (스프라이트/이펙트 에셋이 나오기 전까지의 임시 표시).
// MonsterBase가 attackHitbox에 자동으로 붙여주므로 인스펙터 작업이 필요 없다.
//   준비 동작 중 = 옅은 예고 표시 (어디를 벨지 미리 보여줌)
//   판정 중      = 진한 표시 (실제로 맞는 순간)
[DisallowMultipleComponent]
public class HitboxVisualizer : MonoBehaviour
{
    public Color telegraphColor = new Color(1f, 0.9f, 0.2f, 0.22f);
    public Color activeColor = new Color(1f, 0.25f, 0.15f, 0.55f);
    public float minActiveTime = 0.12f; // 판정이 한 프레임만 켜져도 이만큼은 보여준다
    public int sortingOrder = 50;

    private Collider2D hitbox;
    private SpriteRenderer view;
    private bool telegraphOn;
    private float activeHold;

    private static Sprite sharedSquare;

    void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        CreateView();
    }

    void CreateView()
    {
        if (sharedSquare == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sharedSquare = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        GameObject go = new GameObject("HitboxView");
        go.transform.SetParent(transform, false);

        view = go.AddComponent<SpriteRenderer>();
        view.sprite = sharedSquare;
        view.sortingOrder = sortingOrder;
        view.enabled = false;
    }

    // 준비 동작 시작/종료 시 MonsterBase가 호출
    public void SetTelegraph(bool on) => telegraphOn = on;

    // 판정이 켜지는 순간 MonsterBase가 호출 — 즉시 꺼져도 최소 시간은 표시되게 함
    public void FlashActive() => activeHold = minActiveTime;

    void LateUpdate()
    {
        if (hitbox == null || view == null) return;

        if (activeHold > 0f) activeHold -= Time.deltaTime;

        bool active = hitbox.enabled || activeHold > 0f;
        if (!active && !telegraphOn)
        {
            view.enabled = false;
            return;
        }

        // 예고 표시 중에는 콜라이더가 꺼져 있어 bounds를 쓸 수 없으므로 모양에서 직접 계산한다.
        // view가 히트박스의 자식이므로 반드시 '로컬' 값으로 다뤄야 한다 (월드 크기를 넣으면 부모 스케일이 중복 적용됨)
        if (!TryGetLocalShape(out Vector2 localCenter, out Vector2 localSize)) return;

        view.transform.localPosition = localCenter;
        view.transform.localRotation = Quaternion.identity;
        view.transform.localScale = new Vector3(localSize.x, localSize.y, 1f);

        view.enabled = true;
        view.color = active ? activeColor : telegraphColor;
    }

    // 콜라이더가 꺼져 있어도 동작하도록 콜라이더의 로컬 모양을 그대로 사용
    bool TryGetLocalShape(out Vector2 center, out Vector2 size)
    {
        if (hitbox is BoxCollider2D box)
        {
            center = box.offset;
            size = box.size;
            return true;
        }

        if (hitbox is CircleCollider2D circle)
        {
            center = circle.offset;
            size = Vector2.one * circle.radius * 2f;
            return true;
        }

        if (hitbox is CapsuleCollider2D capsule)
        {
            center = capsule.offset;
            size = capsule.size;
            return true;
        }

        center = default;
        size = default;
        return false;
    }
}
