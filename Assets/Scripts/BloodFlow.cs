using UnityEngine;

// 혈류(OBJ_BloodFlow) — 한 오브젝트가 한 직선 구간이다.
// BoxCollider2D 범위에 닿은 PC는 구간 중심선으로 끌려 들어가 flowDirection 방향으로 흘러간다.
// 꺾이는 경로는 구간을 여러 개 이어 붙여 만든다 — 겹친 곳에서는 나중에 들어간 구간이 이긴다.
// 혈류 안에서는 좌우 이동이 막히고, 방향키는 바라보는 방향(=이탈 방향)만 정한다. 점프로 이탈.
[DefaultExecutionOrder(-50)] // PlayerController.FixedUpdate보다 먼저 접촉을 알려야 같은 스텝에 반영된다
[RequireComponent(typeof(BoxCollider2D))]
public class BloodFlow : MonoBehaviour
{
    public enum Direction { Right, Left, Up, Down }

    [Header("흐름")]
    public Direction flowDirection = Direction.Right;
    [Min(0.1f)] public float flowSpeed = 8f;
    [Tooltip("구간 중심선으로 끌어당기는 속도. 클수록 빨려 들어가듯 붙는다")]
    [Min(0f)] public float centerPull = 10f;

    [Header("이탈 (점프)")]
    [Min(0f)] public float exitSpeed = 10f;
    [Tooltip("이탈 직후 혈류에 다시 붙지 않는 시간(초)")]
    [Min(0f)] public float exitImmunity = 0.35f;

    [Header("방향 반전 (선택)")]
    [Tooltip("이 중 하나라도 켜져 있으면 흐름 방향이 반대가 된다. 비워두면 반전 없음")]
    public WorldActivator[] reverseActivators = new WorldActivator[0];

    BoxCollider2D area;

    public Vector2 FlowDirection
    {
        get
        {
            Vector2 dir = ToVector(flowDirection);
            return WorldActivator.AnyActive(reverseActivators) ? -dir : dir;
        }
    }

    void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
    }

    void FixedUpdate()
    {
        foreach (var player in GimmickPhysics.PlayersIn(area))
            player.Traversal.TouchFlow(this);
    }

    // 흐름 속도 + 중심선 쪽으로 당기는 속도
    public Vector2 VelocityAt(Vector2 position)
    {
        Vector2 dir = FlowDirection;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float offset = Vector2.Dot((Vector2)area.bounds.center - position, perp);
        float pull = Mathf.Clamp(offset / Time.fixedDeltaTime, -centerPull, centerPull);
        return dir * flowSpeed + perp * pull;
    }

    // 역류 금지: 흐름 반대 성분을 지운 방향. 남는 게 없으면 zero.
    public Vector2 ClampAgainstFlow(Vector2 wanted)
    {
        Vector2 dir = FlowDirection;
        float against = Vector2.Dot(wanted, dir);
        if (against < 0f) wanted -= dir * against;
        return wanted.sqrMagnitude < 0.01f ? Vector2.zero : wanted.normalized;
    }

    static Vector2 ToVector(Direction d)
    {
        switch (d)
        {
            case Direction.Left: return Vector2.left;
            case Direction.Up: return Vector2.up;
            case Direction.Down: return Vector2.down;
            default: return Vector2.right;
        }
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Vector3 c = box.bounds.center;
        Vector3 dir = Application.isPlaying ? (Vector3)FlowDirection : (Vector3)ToVector(flowDirection);
        float len = Mathf.Abs(Vector2.Dot(box.bounds.size, dir)) * 0.5f;
        Gizmos.color = new Color(0.9f, 0.1f, 0.1f);
        Gizmos.DrawWireCube(c, box.bounds.size);
        Gizmos.DrawLine(c - dir * len, c + dir * len);
        Vector3 side = new Vector3(-dir.y, dir.x) * 0.3f;
        Gizmos.DrawLine(c + dir * len, c + dir * (len - 0.4f) + side);
        Gizmos.DrawLine(c + dir * len, c + dir * (len - 0.4f) - side);
    }
}
