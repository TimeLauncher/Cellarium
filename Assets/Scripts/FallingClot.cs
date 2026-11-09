using System.Collections.Generic;
using UnityEngine;

// 낙하 혈전 OBJ_FallingClot (기획서 A12~A13 (3)).
// 연결된 장치(버튼·파괴형 작동장치·심장 결절 등)가 켜지면 아래로 빠르게 떨어지면서
// 지나가는 자리에 있는 몬스터·PC·부서지는 오브젝트에 피해를 주고, 바닥에 닿으면 사라진다.
// 한 번 떨어진 혈전은 다시 생기지 않는다 (씬을 다시 들어와도·죽어도 유지).
//
// 배치법: 천장에 매달린 위치에 놓고 Collider2D(크기 = 피해 범위)를 붙인 뒤 Activators에 장치를 연결.
//   콜라이더는 자동으로 트리거가 된다 (매달려 있는 동안 PC가 부딪혀 서지 않게).
[RequireComponent(typeof(Collider2D))]
public class FallingClot : MonoBehaviour
{
    [Header("작동")]
    [Tooltip("이 중 하나라도 켜지면 떨어진다")]
    public WorldActivator[] activators = new WorldActivator[0];
    [Tooltip("장치가 켜지고 떨어지기 시작할 때까지 흔들리는 시간(초). 0이면 바로 떨어진다")]
    [Min(0f)] public float fallDelay = 0.3f;
    [Tooltip("흔들림 세기")]
    [Min(0f)] public float shakeAmount = 0.06f;

    [Header("낙하")]
    [Tooltip("떨어지기 시작하는 속도")]
    [Min(0f)] public float startSpeed = 6f;
    [Tooltip("가속도 (초당 늘어나는 속도)")]
    [Min(0f)] public float acceleration = 60f;
    [Tooltip("최대 낙하 속도")]
    [Min(0.1f)] public float maxSpeed = 30f;

    [Header("피해 (한 번 떨어질 때 대상마다 한 번)")]
    [Tooltip("몬스터에게 주는 피해")]
    public float monsterDamage = 100f;
    [Tooltip("PC에게 주는 피해")]
    public float playerDamage = 50f;
    [Tooltip("부서지는 오브젝트(벽·둥지·작동장치 등)에 주는 피해")]
    public float objectDamage = 100f;
    [Tooltip("PC를 옆으로 밀어내는 힘")]
    public float playerKnockback = 6f;
    public float playerStun = 0.3f;

    [Header("기록")]
    [Tooltip("떨어진 기록용 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    enum State { Hanging, Shaking, Falling, Done }
    State state = State.Hanging;
    float timer;
    float speed;
    Vector3 hangPos;
    Collider2D area;
    string id;
    readonly HashSet<Object> alreadyHit = new HashSet<Object>();

    void Awake()
    {
        area = GetComponent<Collider2D>();
        area.isTrigger = true;
        hangPos = transform.position;
    }

    void Start()
    {
        id = "clot:" + WorldState.MakeId(this, persistentId);
        if (WorldState.Has(WorldCategory.Destructible, id)) Destroy(gameObject);
    }

    void Update()
    {
        switch (state)
        {
            case State.Hanging:
                if (!WorldActivator.AnyActive(activators)) return;
                state = State.Shaking;
                timer = fallDelay;
                break;

            case State.Shaking:
                timer -= Time.deltaTime;
                transform.position = hangPos + (Vector3)(Random.insideUnitCircle * shakeAmount);
                if (timer > 0f) return;
                transform.position = hangPos;
                speed = startSpeed;
                state = State.Falling;
                // 한 번 떨어지기 시작하면 다시 매달리지 않는다
                WorldState.Record(WorldCategory.Destructible, id);
                break;

            case State.Falling:
                Fall(Time.deltaTime);
                break;
        }
    }

    void Fall(float dt)
    {
        speed = Mathf.Min(maxSpeed, speed + acceleration * dt);
        float step = speed * dt;
        Bounds b = area.bounds;

        // 이번 프레임에 내려갈 거리 안에 바닥이 있으면 거기까지만 내려가고 사라진다
        bool landed = FindGround(b, step, out float groundDist);
        float move = landed ? groundDist : step;

        // 지나가는 구간 전체를 판정해서 빠르게 떨어져도 사이에 낀 대상을 놓치지 않는다
        Vector2 sweepCenter = (Vector2)b.center + Vector2.down * (move * 0.5f);
        Vector2 sweepSize = new Vector2(b.size.x, b.size.y + move);
        DamageInside(sweepCenter, sweepSize);

        transform.position += Vector3.down * move;
        if (landed)
        {
            state = State.Done;
            Destroy(gameObject);
        }
    }

    // 몸 아래로 dist 안에 닿는 지형(트리거·몬스터·PC·부서지는 오브젝트 제외)이 있는가
    bool FindGround(Bounds b, float dist, out float hitDist)
    {
        hitDist = dist;
        bool found = false;
        Vector2 size = new Vector2(b.size.x * 0.9f, 0.05f);
        Vector2 origin = new Vector2(b.center.x, b.min.y + 0.03f);
        foreach (var h in Physics2D.BoxCastAll(origin, size, 0f, Vector2.down, dist))
        {
            Collider2D c = h.collider;
            if (c == null || c.isTrigger || c == area) continue;
            if (c.GetComponentInParent<MonsterBase>() != null) continue;
            if (c.GetComponentInParent<PlayerController>() != null) continue;
            if (c.GetComponentInParent<DestructibleObject>() != null) continue;
            if (h.distance < hitDist) { hitDist = h.distance; found = true; }
        }
        return found;
    }

    void DamageInside(Vector2 center, Vector2 size)
    {
        foreach (var c in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (c == null || c == area) continue;

            var pc = c.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                if (!alreadyHit.Add(pc)) continue;
                float side = Mathf.Sign(pc.transform.position.x - transform.position.x);
                if (side == 0f) side = 1f;
                pc.TakeDamage(playerDamage, new Vector2(side, 0.3f).normalized * playerKnockback, playerStun, DamageSource.Attack);
                continue;
            }

            var mb = c.GetComponentInParent<MonsterBase>();
            if (mb != null)
            {
                if (!alreadyHit.Add(mb)) continue;
                mb.TakeDamage(monsterDamage, Vector2.down);
                continue;
            }

            var obj = c.GetComponentInParent<DestructibleObject>();
            if (obj != null && !obj.IsDestroyed)
            {
                if (!alreadyHit.Add(obj)) continue;
                obj.TakeDamage(objectDamage, Vector2.down);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        // 떨어질 경로 (바닥까지)
        Bounds b = c.bounds;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(b.center.x, b.min.y - 0.05f), Vector2.down, 50f);
        float len = hit.collider != null ? hit.distance : 10f;
        Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.5f);
        Gizmos.DrawWireCube(b.center + Vector3.down * (len * 0.5f), new Vector3(b.size.x, b.size.y + len, 0f));
    }
}
