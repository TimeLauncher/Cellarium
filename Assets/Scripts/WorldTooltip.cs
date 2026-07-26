using UnityEngine;

// 월드에 떠 있는 작은 안내 이미지 (조작 설명, E키 상호작용 표시 등).
//
// Canvas를 안 쓰고 SpriteRenderer로 그린다 — Canvas Scaler 설정이나 해상도에 영향을 받지 않고,
// 맵 위 정해진 위치에 그대로 붙어 있어야 하는 안내에는 이쪽이 다루기 쉽다.
//
// 사용법: 빈 게임오브젝트 + SpriteRenderer + 이 스크립트.
//   - showMode = Always      : 항상 표시 (튜토리얼 구간 벽면 안내 등)
//   - showMode = PlayerNear  : 플레이어가 showRange 안에 들어오면 표시 (세이브포인트 E키 안내 등)
[RequireComponent(typeof(SpriteRenderer))]
public class WorldTooltip : MonoBehaviour
{
    public enum ShowMode { Always, PlayerNear }

    [Header("표시 조건")]
    public ShowMode showMode = ShowMode.PlayerNear;
    [Tooltip("PlayerNear일 때 이 거리 안에 플레이어가 들어오면 표시")]
    public float showRange = 3f;

    [Header("배치")]
    [Tooltip("따라다닐 대상. 비우면 이 오브젝트 자리에 고정된다 (맵에 붙는 안내는 비워두면 됨)")]
    public Transform followTarget;
    public Vector3 offset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("캐릭터·타일보다 앞에 그려지도록 충분히 큰 값")]
    public int sortingOrder = 200;

    [Header("연출")]
    public float fadeSpeed = 8f;
    [Tooltip("위아래로 살짝 떠다니는 폭 (0이면 안 움직임)")]
    public float bobAmount = 0.12f;
    public float bobSpeed = 2f;

    private SpriteRenderer spr;
    private Color baseColor;
    private float alpha;
    private Vector3 basePos;

    void Awake()
    {
        spr = GetComponent<SpriteRenderer>();
        spr.sortingOrder = sortingOrder;
        baseColor = spr.color;
        basePos = transform.position;

        // 처음엔 숨긴 상태에서 페이드 인 (Always여도 툭 튀어나오지 않게)
        alpha = 0f;
        SetAlpha(0f);
    }

    void LateUpdate()
    {
        bool shouldShow = ShouldShow();

        alpha = Mathf.MoveTowards(alpha, shouldShow ? 1f : 0f, fadeSpeed * Time.deltaTime);
        SetAlpha(alpha);

        // 완전히 투명하면 그리지 않는다 (드로우콜 아끼기)
        spr.enabled = alpha > 0.001f;
        if (!spr.enabled) return;

        Vector3 anchor = followTarget != null ? followTarget.position : basePos;
        float bob = bobAmount > 0f ? Mathf.Sin(Time.time * bobSpeed) * bobAmount : 0f;
        transform.position = anchor + offset + new Vector3(0f, bob, 0f);
    }

    bool ShouldShow()
    {
        if (showMode == ShowMode.Always) return true;

        PlayerController p = NearestPlayer();
        if (p == null) return false;

        Vector3 from = followTarget != null ? followTarget.position : basePos;
        return Vector2.Distance(p.transform.position, from) <= showRange;
    }

    // 조종 중인 개체 기준. 매니저가 없으면(테스트 씬 등) 씬에서 아무 플레이어나 찾는다.
    PlayerController NearestPlayer()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.currentPlayer != null)
            return PlayerManager.Instance.currentPlayer;

        return FindFirstObjectByType<PlayerController>();
    }

    void SetAlpha(float a)
    {
        Color c = baseColor;
        c.a = baseColor.a * a;
        spr.color = c;
    }

    void OnDrawGizmosSelected()
    {
        if (showMode != ShowMode.PlayerNear) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(followTarget != null ? followTarget.position : transform.position, showRange);
    }
}
