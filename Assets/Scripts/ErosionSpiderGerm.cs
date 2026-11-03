using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 침식 거미균 (준보스) — 기획서 Cellarium_A10~A11 (1).
// 거미균(SpiderGerm)의 벽·천장 기어 다니기는 그대로 쓰고, 공격은 기획서의 '몬스터 행동 선택 규칙'으로 고른다.
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
    [Tooltip("공격 범위를 반투명 판으로 표시 (애니메이션·이펙트가 나오기 전 임시)")]
    public bool showAttackAreas = true;

    [Header("베기 (일반) — 사거리·준비시간·피해는 위 '베기 (근접)' 값 사용")]
    public float slashWeight = 1f;
    [Tooltip("후딜레이")]
    public float slashRecovery = 1f;
    [Tooltip("공격 범위 (가로, 세로). 기획서: 전방 3x3")]
    public Vector2 slashBoxSize = new Vector2(3f, 3f);
    [Tooltip("범위 중심 위치 (앞쪽 거리, 위쪽 높이). 앞쪽은 바라보는 방향으로 자동 반전")]
    public Vector2 slashBoxOffset = new Vector2(2f, 1f);

    [Header("덮치기 (일반) — 최소 거리·준비시간·체공시간·피해·착지 반경은 위 '덮치기' 값 사용")]
    public float pounceWeight = 1f;
    [Tooltip("점프 높이 최대값. 최소값은 위 Pounce Height. 기획서: 6~8타일")]
    public float pounceHeightMax = 8f;
    [Tooltip("후딜레이")]
    public float pounceRecovery = 0.4f;

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
    [Tooltip("투사체 체공 시간 (클수록 높이 날아간다)")]
    public float darkBurstFlightTime = 1.1f;
    [Tooltip("비우면 보라색 동그라미로 임시 생성")]
    public GameObject darkBurstProjectilePrefab;

    [Header("촉수 뻗기 (우선)")]
    public int tentaclePriority = 2;
    [Tooltip("이 거리 안에 플레이어가 있어야 시전. 기획서: 5타일")]
    public float tentacleTriggerRange = 5f;
    public float tentacleWindup = 1.5f;
    [Tooltip("판정 유지 시간. 기획서: 0.7~1초")]
    public float tentacleActiveTime = 0.85f;
    public float tentacleRecovery = 1f;
    public float tentacleCooldown = 8f;
    [Tooltip("몸 중심에서 앞뒤로 뻗는 거리. 기획서: 전후방 6타일")]
    public float tentacleReach = 6f;
    public float tentacleHeight = 2f;
    public float tentacleDamage = 100f;

    [Header("벽면 이동 (우선) → 다크셀 분출 → 덮치기 (연계)")]
    public int wallMovePriority = 1;
    public float wallMoveCooldown = 15f;
    [Tooltip("이동할 벽면 지점들. 빈 오브젝트를 벽에 붙여 놓고 초록 화살표(Y축)가 벽 바깥을 보게 돌려둘 것. 비우면 이 패턴은 안 나온다")]
    public Transform[] wallPoints;
    public float wallMoveSpeed = 14f;
    public float chainBurstWindup = 0.5f;
    public float chainBurstRecovery = 0f;
    public float chainPounceWindup = 0.5f;
    public float chainPounceRecovery = 1f;

    [Header("처치 보상 - 다크셀 조각")]
    [Tooltip("섭취하거나 시체가 사라질 때 셀과 함께 떨구는 다크셀 양. 0이면 안 떨군다")]
    public int darkCellDropAmount = 1;
    [Tooltip("다크셀 조각 모습. 비우면 기본 셀 덩어리를 보라색으로 물들여 쓴다")]
    public GameObject darkCellChunkPrefab;

    enum MoveMode { Crawl, Airborne }

    bool engaged;
    MoveMode moveMode = MoveMode.Crawl;
    Coroutine patternRoutine;
    Vector2 lastTargetPos;

    float slashTimer;
    float pounceTimerCd;
    float darkBurstTimer;
    float tentacleTimer;
    float wallMoveTimer;

    SpriteRenderer telegraphArea;
    SpriteRenderer secondaryArea;

    // ★ 노랑은 쓰지 않는다 — 죽은 몬스터(섭취 가능)가 노랗게 변해서, 예고 판이 몸에 겹치면 '먹을 수 있는 상태'로 보인다
    static readonly Color TelegraphColor = new Color(0.75f, 0.3f, 1f, 0.2f);
    static readonly Color ActiveColor = new Color(1f, 0.25f, 0.15f, 0.55f);
    static readonly Color DarkCellTint = new Color(0.55f, 0.25f, 0.85f, 1f);

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
    }

    // 컴포넌트를 처음 붙였을 때 기획서 수치로 채운다 (이미 배치된 값은 건드리지 않음)
    void Reset()
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

        pounceMinRange = 2f;
        pounceMaxRange = 999f;
        pounceWindup = 0.5f;
        pounceAirTime = 0.8f;
        pounceCooldown = 0f;
        pounceDamage = 50f;
        pounceLandRadius = 1.5f;
        pounceHeight = 6f;

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
        if (slashTimer > 0f) slashTimer -= dt;
        if (pounceTimerCd > 0f) pounceTimerCd -= dt;
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
        if (pick != null) patternRoutine = StartCoroutine(RunPattern(pick()));
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

    IEnumerator RunPattern(IEnumerator body)
    {
        isAttacking = true;
        yield return body;
        isAttacking = false;
        patternRoutine = null;
    }

    // ── 베기: 전방 3x3 ────────────────────────────────────────────

    IEnumerator SlashPattern()
    {
        FaceTarget();
        Trigger("Slash");

        Vector2 center = LocalToWorld(new Vector2(slashBoxOffset.x * facingDir, slashBoxOffset.y));
        float angle = transform.eulerAngles.z;
        ShowBox(ref telegraphArea, center, slashBoxSize, angle, TelegraphColor);
        yield return new WaitForSeconds(slashWindup);

        ShowBox(ref telegraphArea, center, slashBoxSize, angle, ActiveColor);
        float t = 0f;
        bool hit = false;
        while (t < Mathf.Max(slashActiveTime, 0.05f))
        {
            if (!hit) hit = HitBox(center, slashBoxSize, angle, slashDamage);
            t += Time.deltaTime;
            yield return null;
        }
        Hide(telegraphArea);

        slashTimer = slashCooldown;
        yield return new WaitForSeconds(slashRecovery);
    }

    // ── 덮치기: 준비 → 높이 점프 → 시전 순간 PC가 있던 자리로 내려찍기 ─────────

    IEnumerator PouncePattern(float windup, float recovery, bool useCooldown)
    {
        FaceTarget();
        Trigger("Pounce");

        // 시전 순간의 PC 위치 (공중이면 그 아래 바닥)
        Vector2 land = GroundBelow(TargetPos()) + Vector2.up * surfaceOffset;
        ShowCircle(ref telegraphArea, land, pounceLandRadius, TelegraphColor);
        yield return new WaitForSeconds(windup);

        // 공중에선 몸을 똑바로 세운다
        surfaceNormal = Vector2.up;
        AlignToSurface();
        moveMode = MoveMode.Airborne;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; }

        Vector2 start = transform.position;
        float height = Random.Range(pounceHeight, Mathf.Max(pounceHeight, pounceHeightMax));
        float airTime = Mathf.Max(0.05f, pounceAirTime);
        float t = 0f;
        while (t < airTime)
        {
            t += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(t / airTime);
            transform.position = Vector2.Lerp(start, land, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * height);
            yield return new WaitForFixedUpdate();
        }
        transform.position = land;
        moveMode = MoveMode.Crawl;

        ShowCircle(ref telegraphArea, land, pounceLandRadius, ActiveColor);
        HitCircle(land, pounceLandRadius, pounceDamage);
        yield return new WaitForSeconds(0.12f);
        Hide(telegraphArea);

        if (useCooldown) pounceTimerCd = pounceCooldown;
        yield return new WaitForSeconds(recovery);
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
        for (int i = 0; i < count; i++)
        {
            float x = center.x + dir * Random.Range(1f, Mathf.Max(1f, darkBurstRange));
            Vector2 landAt = GroundBelow(new Vector2(x, center.y));
            LaunchProjectile(center, landAt);
        }

        yield return new WaitForSeconds(0.12f);
        Hide(telegraphArea);

        if (useCooldown) darkBurstTimer = darkBurstCooldown;
        yield return new WaitForSeconds(recovery);
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

        // 정해진 시간 뒤 목표 지점에 떨어지는 포물선 속도: v = (Δ - ½gT²) / T
        float T = Mathf.Max(0.2f, darkBurstFlightTime);
        Vector2 g = Physics2D.gravity;
        Vector2 v = ((to - from) - 0.5f * g * T * T) / T;

        proj.Init(v, v.magnitude, darkBurstProjectileDamage);
        body.gravityScale = 1f; // AcidProjectile은 직선 탄이라 중력을 꺼두므로 다시 켠다
        body.linearVelocity = v;
    }

    // ── 촉수 뻗기: 준비 후 전후방 6타일을 일정 시간 공격 ─────────────────

    IEnumerator TentaclePattern()
    {
        Trigger("Tentacle");

        Vector2 center = BodyCenter();
        Vector2 size = new Vector2(tentacleReach * 2f, tentacleHeight);
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

    // ── 벽면 이동 → 다크셀 분출 → 덮치기 ────────────────────────────

    IEnumerator WallMovePattern()
    {
        Transform point = PickWallPoint();
        if (point == null) yield break;

        Trigger("WallMove");
        moveMode = MoveMode.Airborne;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; }

        while (Vector2.Distance(transform.position, point.position) > 0.05f)
        {
            transform.position = Vector2.MoveTowards(transform.position, point.position, wallMoveSpeed * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        // 지점의 Y축(초록 화살표)을 벽 바깥 방향으로 본다
        surfaceNormal = point.up;
        AlignToSurface();
        moveMode = MoveMode.Crawl;

        yield return DarkBurstPattern(chainBurstWindup, chainBurstRecovery, false);
        yield return PouncePattern(chainPounceWindup, chainPounceRecovery, false);

        wallMoveTimer = wallMoveCooldown;
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

    // ── 이동 ────────────────────────────────────────────────────

    protected override void UpdateMovement()
    {
        // 점프·벽면 이동 중엔 패턴 코루틴이 위치를 직접 옮긴다
        if (moveMode == MoveMode.Airborne)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }
        base.UpdateMovement();
    }

    // ── 사망 / 보상 ──────────────────────────────────────────────

    protected override void OnDeath()
    {
        base.OnDeath();
        if (patternRoutine != null) StopCoroutine(patternRoutine);
        patternRoutine = null;
        isAttacking = false;
        moveMode = MoveMode.Crawl;
        Hide(telegraphArea);
        Hide(secondaryArea);

        // 벽·공중에서 죽으면 바닥으로 떨어뜨린다 (그래야 섭취할 수 있다)
        surfaceNormal = Vector2.up;
        AlignToSurface();
        if (rb != null) rb.gravityScale = 1f;

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

    void OnDestroy()
    {
        if (telegraphArea != null) Destroy(telegraphArea.gameObject);
        if (secondaryArea != null) Destroy(secondaryArea.gameObject);
    }

    // ── 공용 ────────────────────────────────────────────────────

    void FaceTarget() => FaceDirection(TargetPos().x - transform.position.x);

    Vector2 TargetPos() => target != null ? (Vector2)target.position : lastTargetPos;

    Vector2 BodyCenter() => bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;

    // 몸 기준 좌표(앞=x, 위=y) → 월드. 벽·천장에 붙어 회전해 있어도 몸 방향을 따른다 (스케일은 무시)
    Vector2 LocalToWorld(Vector2 local)
        => (Vector2)transform.position + (Vector2)transform.right * local.x + (Vector2)transform.up * local.y;

    Vector2 GroundBelow(Vector2 from)
    {
        if (CastSurface(from + Vector2.up * 0.5f, Vector2.down, 40f, out RaycastHit2D hit))
            return hit.point;
        return from;
    }

    bool HitBox(Vector2 center, Vector2 size, float angle, float damage)
    {
        foreach (var h in Physics2D.OverlapBoxAll(center, size, angle, playerMask))
            if (TryDamage(h, damage)) return true;
        return false;
    }

    bool HitCircle(Vector2 center, float radius, float damage)
    {
        foreach (var h in Physics2D.OverlapCircleAll(center, radius, playerMask))
            if (TryDamage(h, damage)) return true;
        return false;
    }

    bool TryDamage(Collider2D h, float damage)
    {
        PlayerController pc = h.GetComponent<PlayerController>();
        if (pc == null) return false;
        // 공격 판정 — 대시 접촉 무적으로는 못 막는다
        pc.TakeDamage(damage, KnockbackVector(pc.transform.position), stunDuration, DamageSource.Attack);
        return true;
    }

    void Trigger(string name)
    {
        if (!HasAnimatorController) return;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(name);
                return;
            }
    }

    // ── 범위 표시 (임시) ─────────────────────────────────────────

    void ShowBox(ref SpriteRenderer area, Vector2 center, Vector2 size, float angle, Color color)
    {
        if (!showAttackAreas) return;
        area = EnsureArea(area);
        area.sprite = SquareSprite();
        area.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
        area.transform.localScale = new Vector3(size.x, size.y, 1f);
        area.color = color;
        area.gameObject.SetActive(true);
    }

    void ShowCircle(ref SpriteRenderer area, Vector2 center, float radius, Color color)
    {
        if (!showAttackAreas) return;
        area = EnsureArea(area);
        area.sprite = CircleSprite();
        area.transform.SetPositionAndRotation(center, Quaternion.identity);
        area.transform.localScale = Vector3.one * (radius * 2f);
        area.color = color;
        area.gameObject.SetActive(true);
    }

    static void Hide(SpriteRenderer area)
    {
        if (area != null) area.gameObject.SetActive(false);
    }

    SpriteRenderer EnsureArea(SpriteRenderer area)
    {
        if (area != null) return area;
        var go = new GameObject(name + "_AttackArea");
        var sr = go.AddComponent<SpriteRenderer>();
        // 몸 뒤에 깐다 — 앞에 그리면 거미 몸이 판 색으로 물든다
        if (spr != null) { sr.sortingLayerID = spr.sortingLayerID; sr.sortingOrder = spr.sortingOrder - 1; }
        else sr.sortingOrder = 50;
        return sr;
    }

    static Sprite squareSprite, circleSprite;

    static Sprite SquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 1x1 유닛
        return squareSprite;
    }

    static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int n = 64;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n); // 지름 1 유닛
        return circleSprite;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, engageRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, tentacleTriggerRange);
        Gizmos.DrawWireCube(transform.position, new Vector3(tentacleReach * 2f, tentacleHeight, 0f));

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
