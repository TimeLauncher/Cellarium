using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 침식 거미균 (준보스) — 기획서 Cellarium_A10~A11 (1), A12~A13 (4).
// 거미균(SpiderGerm)의 벽·천장 기어 다니기·베기·덮치기는 그대로 쓰고, 공격은 기획서의 '몬스터 행동 선택 규칙'으로 고른다.
//   ① 쿨타임이 끝났고 조건이 맞는 '우선' 패턴이 있으면 그것부터 (우선 순위 숫자가 작을수록 먼저)
//   ② 없으면 지금 거리 조건을 만족하는 '일반' 패턴 중 가중치 확률로 1개
//   '연계' 패턴은 벽면 이동 뒤에 자동으로 이어진다 (벽면 이동 → 다크셀 분출 → 덮치기)
// 쿨타임은 패턴이 끝난 직후부터 돈다 (기획서 몬스터 공통 사항).
//
// 기본 행동 없음: 플레이어가 engageRange 안에 처음 들어오기 전까진 제자리에 붙어 있다.
// 전투에 들어가면 거리와 상관없이 끝까지 쫓는다 (추적 제한 없음).
//
// 애니메이션이 아직 없어서 공격 범위를 반투명 판으로 보여준다 (준비 = 보라, 판정 = 빨강).
// 애니메이터를 붙이면 패턴 시작 때 Slash / Pounce / Tentacle / DarkBurst / WallMove 트리거를 건다 (파라미터가 있을 때만).
public class ErosionSpiderGerm : SpiderGerm
{
    [Header("침식 거미균 - 전투 돌입")]
    [Tooltip("플레이어가 이 거리 안에 처음 들어오면 전투 시작. 이후엔 거리와 상관없이 끝까지 쫓는다")]
    public float engageRange = 10f;
    [Tooltip("켜면 처치 기록이 세이브포인트의 몬스터 리젠에도 지워지지 않는다 (보스는 한 번 잡으면 끝)")]
    public bool stayDefeated = true;
    [Tooltip("켜면 전투 시작 시 우선 패턴(촉수 뻗기·벽면 이동)이 쿨타임을 한 번 다 채운 뒤부터 나온다. " +
             "끄면 전투 시작하자마자 벽면 이동부터 한다")]
    public bool priorityStartsOnCooldown = true;

    [Header("베기 (일반) — 범위·준비시간·후딜레이·피해는 위 '베기 (근접)' 값 사용")]
    public float slashWeight = 1f;

    [Header("덮치기 (일반) — 준비시간·체공시간·높이·피해·후딜레이는 위 '덮치기' 값 사용")]
    public float pounceWeight = 1f;

    [Header("다크셀 분출 (일반)")]
    public float darkBurstWeight = 1f;
    public float darkBurstWindup = 1f;
    public float darkBurstRecovery = 1f;
    [Tooltip("기획서 본문 '쿨타임 5초' (표에는 '-')")]
    public float darkBurstCooldown = 5f;
    [Tooltip("시전 순간 주변에 즉시 피해를 주는 반경")]
    public float darkBurstRadius = 2.5f;
    public float darkBurstDamage = 50f;
    [Tooltip("투척하는 투사체 개수 (최소~최대 랜덤). 기획서: 5~6개")]
    public Vector2Int darkBurstProjectileCount = new Vector2Int(5, 6);
    [Tooltip("투사체가 떨어지는 전방 거리. 기획서: 12타일 이내 랜덤")]
    public float darkBurstRange = 12f;
    public float darkBurstProjectileDamage = 50f;
    [Tooltip("투사체 체공 시간 (아래 '포물선 높이 랜덤'을 끈 경우에만 사용. 클수록 높이 날아간다)")]
    public float darkBurstFlightTime = 1.1f;
    [Tooltip("비우면 보라색 동그라미로 임시 생성")]
    public GameObject darkBurstProjectilePrefab;

    // 기획서 A12~A13 (4): 투사체마다 포물선 높이와 떨어지는 속도가 다르게, 투사체끼리는 최소 간격을 두고 떨어지게
    [Header("다크셀 분출 - 투사체 궤적")]
    [Tooltip("켜면 투사체마다 아래 범위에서 포물선 높이·떨어지는 속도를 랜덤으로 뽑는다. 끄면 Flight Time 하나로 모두 같은 궤적")]
    public bool darkBurstRandomArc = true;
    [Tooltip("포물선 꼭대기 높이 (던지는 위치 기준, 최소~최대 타일)")]
    public Vector2 darkBurstArcHeight = new Vector2(2f, 6f);
    [Tooltip("떨어지는 속도 배율 = 중력 배율 (최소~최대). 클수록 빨리 떨어진다")]
    public Vector2 darkBurstFallSpeed = new Vector2(0.7f, 1.4f);
    [Tooltip("투사체가 떨어지는 지점끼리의 최소 거리(타일). PC가 피할 틈을 만든다. 0이면 제한 없음")]
    [Min(0f)] public float darkBurstMinSpacing = 1.5f;

    [Header("촉수 뻗기 (우선)")]
    public int tentaclePriority = 2;
    [Tooltip("이 거리 안에 플레이어가 있어야 시전. 기획서: 5타일")]
    public float tentacleTriggerRange = 5f;
    public float tentacleWindup = 1.5f;
    [Tooltip("판정 유지 시간. 기획서: 0.7~1초")]
    public float tentacleActiveTime = 0.85f;
    public float tentacleRecovery = 1f;
    public float tentacleCooldown = 8f;
    [Tooltip("몸 중심에서 '앞쪽'으로 뻗는 거리. 기획서: 전후방 6타일")]
    public float tentacleReach = 6f;
    [Tooltip("몸 중심에서 '뒤쪽'으로 뻗는 거리")]
    public float tentacleBackReach = 6f;
    [Tooltip("범위 세로 크기")]
    public float tentacleHeight = 2f;
    [Tooltip("범위를 옮기는 값 (앞쪽, 위쪽). 아래로 내리려면 Y를 음수로. 앞쪽은 바라보는 방향으로 자동 반전")]
    public Vector2 tentacleBoxOffset = Vector2.zero;
    public float tentacleDamage = 100f;

    [Header("벽면 이동 (우선) → 다크셀 분출 → 덮치기 (연계)")]
    public int wallMovePriority = 1;
    public float wallMoveCooldown = 15f;
    [Tooltip("이동할 벽면 지점들. 빈 오브젝트를 벽에 붙여 놓고 초록 화살표(Y축)가 벽 바깥을 보게 돌려둘 것. 비우면 이 패턴은 안 나온다")]
    public Transform[] wallPoints;
    [Tooltip("벽을 타고 올라가는 속도")]
    public float wallMoveSpeed = 14f;
    // 기획서 A12~A13 (4): 지점까지 직선으로 날아가지 않고 바닥 → 벽 순서로 걸어간다
    [Tooltip("벽면 이동 때 바닥을 걸어서 벽 밑까지 가는 속도")]
    public float wallMoveWalkSpeed = 9f;
    public float chainBurstWindup = 0.5f;
    public float chainBurstRecovery = 0f;
    public float chainPounceWindup = 0.5f;
    public float chainPounceRecovery = 1f;

    // 기획서 A12~A13 (4): 체력이 일정량 깎일 때마다 주변 바닥에 회복 오브젝트를 떨군다
    [Header("회복 오브젝트 드랍")]
    [Tooltip("체력이 이만큼 깎일 때마다 회복 오브젝트를 떨군다 (기획서: 400~500). 0이면 안 떨군다")]
    [Min(0f)] public float healDropEveryHpLost = 450f;
    [Tooltip("한 번에 떨구는 개수")]
    [Min(1)] public int healDropCount = 1;
    [Tooltip("몸 중심에서 좌우로 이 거리 안의 바닥에 무작위로 떨어진다")]
    [Min(0f)] public float healDropRadius = 6f;
    [Tooltip("이 높이(타일)에서 떨어지기 시작한다")]
    [Min(0f)] public float healDropFallHeight = 5f;
    [Tooltip("섭취하면 회복되는 양 (임시 오브젝트일 때만. 프리팹이면 프리팹의 HealCell 값)")]
    public float healDropAmount = 100f;
    [Tooltip("회복 오브젝트 프리팹 (HealCell 컴포넌트 필요, Monster 레이어). 비우면 초록 동그라미로 임시 생성")]
    public GameObject healDropPrefab;

    [Header("처치 보상 - 다크셀 조각")]
    [Tooltip("섭취하거나 시체가 사라질 때 셀과 함께 떨구는 다크셀 양. 0이면 안 떨군다")]
    public int darkCellDropAmount = 1;
    [Tooltip("다크셀 조각 모습. 비우면 기본 셀 덩어리를 보라색으로 물들여 쓴다")]
    public GameObject darkCellChunkPrefab;

    bool engaged;
    float nextHealDropAt;

    float darkBurstTimer;
    float tentacleTimer;
    float wallMoveTimer;

    static readonly Color DarkCellTint = new Color(0.55f, 0.25f, 0.85f, 1f);
    static readonly Color HealTint = new Color(0.35f, 1f, 0.45f, 1f);

    string BossKey => "boss:" + WorldState.MakeId(this, persistentId);

    // ★ 기존 거미균 오브젝트에 이 컴포넌트를 '추가'만 하고 원래 SpiderGerm을 안 지우면 몬스터가 둘이 된다.
    //   대시 피해·섭취 표시(ConsumeIndicator)는 첫 번째 몬스터 컴포넌트(옛 SpiderGerm)를 보는데 공격은 이쪽이 해서,
    //   옛 컴포넌트만 죽고 → 살아서 공격하는 보스 위에 섭취 원이 뜬다. 다른 몬스터 컴포넌트와 그 표시를 치운다.
    protected override void Awake()
    {
        bool removed = false;
        foreach (var other in GetComponents<MonsterBase>())
        {
            if (other == this) continue;
            Debug.LogWarning($"[ErosionSpiderGerm] '{name}'에 다른 몬스터 컴포넌트({other.GetType().Name})가 같이 붙어 있어 제거합니다. " +
                             "인스펙터에서도 지워 주세요.", this);
            DestroyImmediate(other);
            removed = true;
        }
        if (removed)
        {
            var oldIndicator = GetComponent<ConsumeIndicator>();
            if (oldIndicator != null) DestroyImmediate(oldIndicator); // 옛 컴포넌트를 주인으로 잡고 있으므로 새로 만든다
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == "ConsumeCollider" || c.name == "ConsumeRangeView" || c.name == "ConsumeHoverView")
                    DestroyImmediate(c.gameObject);
            }
        }

        base.Awake();
        nextHealDropAt = healDropEveryHpLost;
    }

    // 컴포넌트를 처음 붙였을 때 기획서 수치로 채운다 (이미 배치된 값은 건드리지 않음)
    protected override void Reset()
    {
        maxHp = 700f;
        moveSpeed = 5f;
        attackDamage = 50f;          // 기본(접촉) 피해
        knockbackResistance = 0f;    // 준보스 — 대시에 밀리지 않는다
        maxLeashDistance = 0f;
        detectionRange = engageRange;
        cellDropTotal = 100;
        cellDropCount = 10;

        slashRange = 3.5f;
        slashWindup = 0.7f;
        slashActiveTime = 0.2f;
        slashDamage = 50f;
        slashCooldown = 0f;
        slashRecovery = 1f;

        pounceMinRange = 2f;
        pounceMaxRange = 999f;
        pounceWindup = 0.5f;
        pounceAirTime = 0.8f;
        pounceCooldown = 0f;
        pounceDamage = 50f;
        pounceLandRadius = 1.5f;
        pounceHeight = 6f;
        pounceHeightMax = 8f;
        pounceRecovery = 0.4f;

        int player = LayerMask.NameToLayer("Player");
        if (player >= 0) playerMask = 1 << player;
        int monster = LayerMask.NameToLayer("Monster");
        if (monster >= 0) gameObject.layer = monster;
    }

    protected override void Start()
    {
        base.Start();
        if (stayDefeated && WorldState.Has(WorldCategory.Event, BossKey))
            Destroy(gameObject);
    }

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        if (darkBurstTimer > 0f) darkBurstTimer -= dt;
        if (tentacleTimer > 0f) tentacleTimer -= dt;
        if (wallMoveTimer > 0f) wallMoveTimer -= dt;
    }

    protected override bool HoldPosition => !engaged;

    // ── 감지: 한 번 조우하면 끝까지 쫓는다 ──────────────────────────

    protected override void UpdateDetection()
    {
        if (!engaged)
        {
            Collider2D hit = Physics2D.OverlapCircle(transform.position, engageRange, playerMask);
            if (hit == null || hit.GetComponent<PlayerController>() == null) { target = null; return; }

            engaged = true;
            if (priorityStartsOnCooldown)
            {
                tentacleTimer = tentacleCooldown;
                wallMoveTimer = wallMoveCooldown;
            }
            target = hit.transform;
        }

        // 분열체로 조종을 바꾸면 그쪽을 쫓는다
        PlayerManager m = PlayerManager.Instance;
        if (m != null && m.currentPlayer != null) target = m.currentPlayer.transform;
        hasDetectedPlayer = target != null;
        if (target != null) lastTargetPos = target.position;
    }

    // ── 행동 선택 ────────────────────────────────────────────────

    struct Choice
    {
        public int priority;   // 우선 패턴만 사용
        public float weight;
        public System.Func<IEnumerator> run;
    }

    protected override void UpdateBehavior()
    {
        if (!engaged || isAttacking || patternRoutine != null || target == null) return;

        System.Func<IEnumerator> pick = ChoosePattern();
        if (pick != null) StartPattern(pick());
    }

    System.Func<IEnumerator> ChoosePattern()
    {
        float dist = Vector2.Distance(transform.position, target.position);

        // ① 우선 패턴 — 쿨타임이 끝났고 조건이 맞는 것 중 우선 순위가 가장 높은 것 (같으면 확률)
        var priority = new List<Choice>();
        if (wallMoveTimer <= 0f && HasWallPoint())
            priority.Add(new Choice { priority = wallMovePriority, weight = 1f, run = WallMovePattern });
        if (tentacleTimer <= 0f && dist <= tentacleTriggerRange)
            priority.Add(new Choice { priority = tentaclePriority, weight = 1f, run = TentaclePattern });

        if (priority.Count > 0)
        {
            int best = int.MaxValue;
            foreach (var c in priority) best = Mathf.Min(best, c.priority);
            priority.RemoveAll(c => c.priority != best);
            return WeightedPick(priority);
        }

        // ② 일반 패턴 — 거리 조건을 만족하는 것 중 가중치 확률
        var normal = new List<Choice>();
        if (slashTimer <= 0f && dist <= slashRange)
            normal.Add(new Choice { weight = slashWeight, run = SlashPattern });
        if (pounceTimerCd <= 0f && dist >= pounceMinRange && dist <= pounceMaxRange)
            normal.Add(new Choice { weight = pounceWeight, run = () => PouncePattern(pounceWindup, pounceRecovery, true) });
        if (darkBurstTimer <= 0f)
            normal.Add(new Choice { weight = darkBurstWeight, run = () => DarkBurstPattern(darkBurstWindup, darkBurstRecovery, true) });

        return WeightedPick(normal);
    }

    static System.Func<IEnumerator> WeightedPick(List<Choice> list)
    {
        float total = 0f;
        foreach (var c in list) total += Mathf.Max(0f, c.weight);
        if (total <= 0f) return null;

        float r = Random.value * total;
        foreach (var c in list)
        {
            r -= Mathf.Max(0f, c.weight);
            if (r <= 0f) return c.run;
        }
        return list[list.Count - 1].run;
    }

    bool HasWallPoint()
    {
        if (wallPoints == null) return false;
        foreach (var p in wallPoints) if (p != null) return true;
        return false;
    }

    // ── 다크셀 분출: 주변 즉시 피해 + 전방에 투사체 포물선 투척 ──────────────

    IEnumerator DarkBurstPattern(float windup, float recovery, bool useCooldown)
    {
        FaceTarget();
        Trigger("DarkBurst");

        Vector2 center = BodyCenter();
        ShowCircle(ref telegraphArea, center, darkBurstRadius, TelegraphColor);
        yield return new WaitForSeconds(windup);

        ShowCircle(ref telegraphArea, center, darkBurstRadius, ActiveColor);
        HitCircle(center, darkBurstRadius, darkBurstDamage);

        // '전방' = 플레이어 쪽. 벽에 붙어 있어도 좌우 기준으로 던진다
        float dir = Mathf.Sign(TargetPos().x - center.x);
        if (Mathf.Approximately(dir, 0f)) dir = facingDir;

        int min = Mathf.Max(0, darkBurstProjectileCount.x);
        int count = Random.Range(min, Mathf.Max(min, darkBurstProjectileCount.y) + 1);
        foreach (float x in PickLandingXs(center.x, dir, count))
        {
            Vector2 landAt = GroundBelow(new Vector2(x, center.y));
            LaunchProjectile(center, landAt);
        }

        yield return new WaitForSeconds(0.12f);
        Hide(telegraphArea);

        if (useCooldown) darkBurstTimer = darkBurstCooldown;
        yield return new WaitForSeconds(recovery);
    }

    // 떨어질 x 좌표들. 서로 darkBurstMinSpacing 이상 떨어지게 뽑고, 자리가 모자라면 간격을 포기하고 그냥 뽑는다
    List<float> PickLandingXs(float fromX, float dir, int count)
    {
        var xs = new List<float>();
        float range = Mathf.Max(1f, darkBurstRange);
        for (int i = 0; i < count; i++)
        {
            float x = fromX + dir * Random.Range(1f, range);
            for (int tries = 0; tries < 30 && darkBurstMinSpacing > 0f && TooClose(xs, x); tries++)
                x = fromX + dir * Random.Range(1f, range);
            xs.Add(x);
        }
        return xs;
    }

    bool TooClose(List<float> xs, float x)
    {
        foreach (float o in xs)
            if (Mathf.Abs(o - x) < darkBurstMinSpacing) return true;
        return false;
    }

    void LaunchProjectile(Vector2 from, Vector2 to)
    {
        GameObject go;
        if (darkBurstProjectilePrefab != null)
        {
            go = Instantiate(darkBurstProjectilePrefab, from, Quaternion.identity);
        }
        else
        {
            go = new GameObject("DarkBurstProjectile");
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSprite();
            sr.color = DarkCellTint;
            if (spr != null) { sr.sortingLayerID = spr.sortingLayerID; sr.sortingOrder = spr.sortingOrder + 2; }
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            go.AddComponent<Rigidbody2D>();
        }

        var proj = go.GetComponent<AcidProjectile>();
        if (proj == null) proj = go.AddComponent<AcidProjectile>();
        var body = go.GetComponent<Rigidbody2D>();

        float gravityScale = 1f;
        Vector2 v;
        if (darkBurstRandomArc)
        {
            // 꼭대기 높이 h와 중력 배율을 뽑고, 그 포물선으로 목표 지점에 떨어지는 속도를 거꾸로 구한다
            gravityScale = Mathf.Max(0.05f, Random.Range(darkBurstFallSpeed.x, Mathf.Max(darkBurstFallSpeed.x, darkBurstFallSpeed.y)));
            float g = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
            float apex = Mathf.Max(from.y, to.y) + Mathf.Max(0.1f, Random.Range(darkBurstArcHeight.x, Mathf.Max(darkBurstArcHeight.x, darkBurstArcHeight.y)));
            float vy = Mathf.Sqrt(2f * g * (apex - from.y));
            float T = vy / g + Mathf.Sqrt(2f * (apex - to.y) / g); // 올라가는 시간 + 내려오는 시간
            v = new Vector2((to.x - from.x) / T, vy);
        }
        else
        {
            // 정해진 시간 뒤 목표 지점에 떨어지는 포물선 속도: v = (Δ - ½gT²) / T
            float T = Mathf.Max(0.2f, darkBurstFlightTime);
            Vector2 g = Physics2D.gravity;
            v = ((to - from) - 0.5f * g * T * T) / T;
        }

        proj.Init(v, v.magnitude, darkBurstProjectileDamage);
        body.gravityScale = gravityScale; // AcidProjectile은 직선 탄이라 중력을 꺼두므로 다시 켠다
        body.linearVelocity = v;
    }

    // ── 촉수 뻗기: 준비 후 전후방을 일정 시간 공격 ─────────────────

    IEnumerator TentaclePattern()
    {
        Trigger("Tentacle");

        Vector2 size;
        Vector2 center = TentacleBox(out size);
        float angle = transform.eulerAngles.z;
        ShowBox(ref telegraphArea, center, size, angle, TelegraphColor);
        yield return new WaitForSeconds(tentacleWindup);

        ShowBox(ref telegraphArea, center, size, angle, ActiveColor);
        float t = 0f;
        bool hit = false;
        while (t < tentacleActiveTime)
        {
            if (!hit) hit = HitBox(center, size, angle, tentacleDamage);
            t += Time.deltaTime;
            yield return null;
        }
        Hide(telegraphArea);

        tentacleTimer = tentacleCooldown;
        yield return new WaitForSeconds(tentacleRecovery);
    }

    // 촉수 범위 박스 (몸 기준: 앞 tentacleReach, 뒤 tentacleBackReach, 오프셋만큼 이동). 벽·천장에선 몸 방향을 따른다
    Vector2 TentacleBox(out Vector2 size)
    {
        float front = Mathf.Max(0f, tentacleReach), back = Mathf.Max(0f, tentacleBackReach);
        size = new Vector2(front + back, tentacleHeight);
        Vector2 bodyLocal = (Vector2)transform.InverseTransformPoint(BodyCenter());
        bodyLocal.Scale(transform.lossyScale); // InverseTransformPoint는 스케일로 나누므로 월드 거리로 되돌린다
        float localX = bodyLocal.x + ((front - back) * 0.5f + tentacleBoxOffset.x) * facingDir;
        float localY = bodyLocal.y + tentacleBoxOffset.y;
        return LocalToWorld(new Vector2(localX, localY));
    }

    // ── 벽면 이동 → 다크셀 분출 → 덮치기 ────────────────────────────
    // 바닥 → 벽 순서로 걷는다: ① 벽·천장에 붙어 있으면 바닥으로 내려옴 ② 바닥을 걸어 벽 밑까지 ③ 벽을 타고 지점까지

    IEnumerator WallMovePattern()
    {
        Transform point = PickWallPoint();
        if (point == null) yield break;

        Trigger("WallMove");
        moveMode = MoveMode.Airborne;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; }

        // ① 바닥이 아니면 바로 아래 바닥으로 내려온다
        if (surfaceNormal.y < 0.7f)
        {
            Vector2 floor = GroundBelow(BodyCenter()) + Vector2.up * SurfaceGap;
            surfaceNormal = Vector2.up;
            AlignToSurface();
            yield return MoveTo(floor, wallMoveSpeed);
        }

        // ② 벽 밑(지점 아래 바닥)까지 걸어간다
        Vector2 wallOut = point.up;
        Vector2 baseSpot = GroundBelow((Vector2)point.position + wallOut * SurfaceGap) + Vector2.up * SurfaceGap;
        Vector2 walkTo = new Vector2(baseSpot.x, transform.position.y);
        FaceDirection(walkTo.x - transform.position.x);
        yield return MoveTo(walkTo, wallMoveWalkSpeed);

        // ③ 지점의 Y축(초록 화살표)을 벽 바깥 방향으로 보고 벽을 타고 올라간다
        surfaceNormal = wallOut;
        AlignToSurface();
        yield return MoveTo(point.position, wallMoveSpeed);
        moveMode = MoveMode.Crawl;

        yield return DarkBurstPattern(chainBurstWindup, chainBurstRecovery, false);
        yield return PouncePattern(chainPounceWindup, chainPounceRecovery, false);

        wallMoveTimer = wallMoveCooldown;
    }

    IEnumerator MoveTo(Vector2 dest, float speed)
    {
        while (Vector2.Distance(transform.position, dest) > 0.05f)
        {
            transform.position = Vector2.MoveTowards(transform.position, dest, Mathf.Max(0.1f, speed) * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }
        transform.position = dest;
    }

    // 지금 자리와 가장 가까운 지점은 빼고 무작위 (같은 벽에 다시 붙는 것 방지)
    Transform PickWallPoint()
    {
        var list = new List<Transform>();
        foreach (var p in wallPoints) if (p != null) list.Add(p);
        if (list.Count == 0) return null;
        if (list.Count > 1)
        {
            Transform nearest = list[0];
            foreach (var p in list)
                if (Vector2.Distance(p.position, transform.position) < Vector2.Distance(nearest.position, transform.position))
                    nearest = p;
            list.Remove(nearest);
        }
        return list[Random.Range(0, list.Count)];
    }

    // ── 회복 오브젝트 드랍 ───────────────────────────────────────

    public override void TakeDamage(float amount, Vector2 hitDirection = default)
    {
        base.TakeDamage(amount, hitDirection);
        if (IsDead || healDropEveryHpLost <= 0f) return;

        while (maxHp - currentHp >= nextHealDropAt)
        {
            nextHealDropAt += healDropEveryHpLost;
            for (int i = 0; i < healDropCount; i++) DropHealObject();
        }
    }

    void DropHealObject()
    {
        Vector2 c = BodyCenter();
        Vector2 ground = GroundBelow(new Vector2(c.x + Random.Range(-healDropRadius, healDropRadius), c.y + 1f));
        Vector2 landAt = ground + Vector2.up * 0.5f;
        Vector2 startAt = landAt + Vector2.up * healDropFallHeight;

        GameObject go;
        if (healDropPrefab != null)
        {
            go = Instantiate(healDropPrefab, startAt, Quaternion.identity);
        }
        else
        {
            go = new GameObject("HealDrop");
            go.transform.position = startAt;
            go.transform.localScale = Vector3.one * 0.6f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSprite();
            sr.color = HealTint;
            if (spr != null) { sr.sortingLayerID = spr.sortingLayerID; sr.sortingOrder = spr.sortingOrder + 1; }
            go.AddComponent<CircleCollider2D>().radius = 0.5f;
            // 섭취 대상은 Monster 레이어에서 찾는다 (PlayerController.monsterMask)
            int monster = LayerMask.NameToLayer("Monster");
            if (monster >= 0) go.layer = monster;
            go.AddComponent<HealCell>().healAmount = healDropAmount;
        }

        var heal = go.GetComponent<HealCell>();
        if (heal != null) heal.DropTo(landAt);
    }

    // ── 사망 / 보상 ──────────────────────────────────────────────

    protected override void OnDeath()
    {
        base.OnDeath();
        if (stayDefeated) WorldState.Record(WorldCategory.Event, BossKey);
    }

    protected override void DropCells(Transform absorbTarget = null)
    {
        base.DropCells(absorbTarget);
        if (darkCellDropAmount <= 0) return;

        CellChunk chunk = CellChunk.Spawn(BodyCenter(), darkCellDropAmount, darkCellChunkPrefab, spr);
        if (chunk == null) return;
        chunk.isDarkCell = true;
        if (darkCellChunkPrefab == null)
            foreach (var sr in (chunk.popRoot != null ? chunk.popRoot : chunk.transform).GetComponentsInChildren<SpriteRenderer>(true))
                sr.color = DarkCellTint;

        chunk.pickupDelay = cellPickupDelay;
        chunk.Launch(new Vector2(Random.Range(-cellPopSideSpeed, cellPopSideSpeed), cellPopUpSpeed));
        if (absorbTarget != null) chunk.AttractTo(absorbTarget, cellAbsorbDelay);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, engageRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, tentacleTriggerRange);

        // 촉수 범위 (오른쪽을 볼 때 기준)
        float front = Mathf.Max(0f, tentacleReach), back = Mathf.Max(0f, tentacleBackReach);
        Vector3 c = transform.position + transform.right * ((front - back) * 0.5f + tentacleBoxOffset.x) + transform.up * tentacleBoxOffset.y;
        Gizmos.matrix = Matrix4x4.TRS(c, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(front + back, tentacleHeight, 0f));
        Gizmos.matrix = Matrix4x4.identity;

        if (wallPoints == null) return;
        Gizmos.color = Color.magenta;
        foreach (var p in wallPoints)
        {
            if (p == null) continue;
            Gizmos.DrawWireSphere(p.position, 0.4f);
            Gizmos.DrawLine(p.position, p.position + p.up);
        }
    }
}
