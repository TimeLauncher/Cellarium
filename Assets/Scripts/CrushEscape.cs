using System.Collections.Generic;
using UnityEngine;

// 이동하는 지형(이동 발판, 이동 타일, 앞으로 추가될 이동형 오브젝트)과 다른 지형 사이에
// 화이트셀이 끼었을 때 가까운 바깥쪽으로 밀어내 빼준다.
//
// ★ A07~A09 Fix (1): "이동 발판이 지면 혹은 천장 등 벽에 맞닿아있는 경우 그 사이에 PC가 존재할 시
//   벽에 끼이는 상태가 됨 → 이동형 오브젝트/타일에 끼일 경우 가까운 바깥 쪽으로 자연스럽게 밀려나 탈출"
//
// 발판 쪽(MovingPlatform)이 아니라 PC 쪽에서 처리하는 이유:
//   끼이는 상황은 발판 말고도 이동 타일·닫히는 문 등 어떤 것이든 만들 수 있다.
//   PlayerController.FixedUpdate가 매 프레임 이걸 부르므로, 새 기믹을 만들어도 자동으로 적용된다.
//
// 동작: 몸통 콜라이더가 '서로 반대 방향으로 밀어내는' 두 지형에 동시에 겹쳐 있으면(=끼임) 그때만 개입한다.
//   끼임 축과 직각인 두 방향 중 ① 막혀 있지 않고 ② 더 짧게 빠져나갈 수 있는 쪽으로 밀어낸다.
//   한쪽만 겹친 경우는 물리 엔진이 알아서 밀어내므로 건드리지 않는다 (개입하면 오히려 덜덜 떨린다).
public static class CrushEscape
{
    static readonly Collider2D[] overlaps = new Collider2D[16];
    static readonly RaycastHit2D[] casts = new RaycastHit2D[16];
    static readonly List<Vector2> escapeDirs = new List<Vector2>();
    static ContactFilter2D solidFilter = new ContactFilter2D { useTriggers = false };

    public static void Resolve(Rigidbody2D body, List<Collider2D> bodyColliders, float escapeSpeed)
    {
        if (body == null || bodyColliders == null || escapeSpeed <= 0f) return;

        escapeDirs.Clear();
        Collider2D squeezer = null;
        float deepest = 0f;

        foreach (Collider2D self in bodyColliders)
        {
            if (self == null || !self.enabled) continue;

            int count = Physics2D.OverlapCollider(self, solidFilter, overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = overlaps[i];
                if (!IsTerrain(other, body)) continue;
                if (Physics2D.GetIgnoreCollision(self, other)) continue; // 관통 발판 하강 중 등

                ColliderDistance2D d = self.Distance(other);
                if (!d.isOverlapped) continue;

                // normal * distance = 겹침에서 빠져나가는 방향(거리가 음수라 부호가 뒤집힌다)
                Vector2 out2 = (d.normal * d.distance).normalized;
                if (out2.sqrMagnitude > 0.0001f) escapeDirs.Add(out2);

                float depth = -d.distance;
                if (depth > deepest) { deepest = depth; squeezer = other; }
            }
        }

        if (squeezer == null || escapeDirs.Count < 2) return;

        // 끼임 판정: 빠져나가야 할 방향이 서로 반대면 어느 쪽으로도 못 나간다
        Vector2 crushAxis = Vector2.zero;
        for (int i = 0; i < escapeDirs.Count && crushAxis == Vector2.zero; i++)
            for (int j = i + 1; j < escapeDirs.Count; j++)
                if (Vector2.Dot(escapeDirs[i], escapeDirs[j]) < -0.5f)
                {
                    crushAxis = (escapeDirs[i] - escapeDirs[j]).normalized;
                    break;
                }

        if (crushAxis == Vector2.zero) return;

        Vector2 side = new Vector2(-crushAxis.y, crushAxis.x); // 끼임 축과 직각 = 빠져나갈 수 있는 축
        float step = escapeSpeed * Time.fixedDeltaTime;

        float distA = ClearDistance(body, squeezer, side);
        float distB = ClearDistance(body, squeezer, -side);
        bool blockedA = Blocked(body, squeezer, side, step);
        bool blockedB = Blocked(body, squeezer, -side, step);

        Vector2 dir;
        if (blockedA && blockedB) return;            // 양쪽 다 막혔으면 할 수 있는 게 없다
        else if (blockedA) dir = -side;
        else if (blockedB) dir = side;
        else dir = distA <= distB ? side : -side;    // 둘 다 열려 있으면 가까운 바깥쪽

        body.position += dir * step;
    }

    // 지형으로 칠 것: 트리거·자기 몸통·다른 화이트셀·몬스터는 제외
    static bool IsTerrain(Collider2D other, Rigidbody2D body)
    {
        if (other == null || other.isTrigger) return false;
        if (other.attachedRigidbody == body) return false;
        if (other.GetComponentInParent<PlayerController>() != null) return false;
        if (other.GetComponentInParent<MonsterBase>() != null) return false;
        return true;
    }

    // dir 방향으로 squeezer 밖까지 나가는 데 필요한 거리 (짧을수록 '가까운 바깥쪽')
    static float ClearDistance(Rigidbody2D body, Collider2D squeezer, Vector2 dir)
    {
        Bounds b = squeezer.bounds;
        Vector2 c = body.position;
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            return dir.x > 0f ? b.max.x - c.x : c.x - b.min.x;
        return dir.y > 0f ? b.max.y - c.y : c.y - b.min.y;
    }

    // 그 방향에 또 다른 지형이 바로 붙어 있으면 그쪽으로는 못 나간다
    static bool Blocked(Rigidbody2D body, Collider2D squeezer, Vector2 dir, float step)
    {
        int count = body.Cast(dir, solidFilter, casts, Mathf.Max(step, 0.05f));
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = casts[i].collider;
            if (hit == squeezer) continue;
            if (IsTerrain(hit, body)) return true;
        }
        return false;
    }
}
