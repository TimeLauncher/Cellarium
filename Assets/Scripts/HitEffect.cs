using UnityEngine;

// 피격 순간에 터지는 이펙트를 재생하는 공용 창구.
//
// 재생할 것을 이 순서로 고른다:
//   ① 인스펙터의 Prefab 칸에 꽂은 것
//   ② Assets/Resources/Effects/HitEffect_Hit.prefab (팀원이 만든 HitSpark+HitFlash를 프리팹으로 뽑아둔 것)
//   ③ 둘 다 없으면 런타임에 만드는 임시 스파크
// ②가 있어서 몬스터마다·씬마다 인스펙터를 연결하지 않아도 이펙트가 나온다.
// (씬마다 몬스터가 개별 배치된 프로젝트라 인스펙터 연결을 강요하면 반드시 어딘가 빠진다)
//
// 이 컴포넌트 자체는 임시 이펙트의 수명을 굴리는 역할만 한다. 프리팹 경로에서는 쓰이지 않는다.
[DisallowMultipleComponent]
public class HitEffect : MonoBehaviour
{
    // 인스펙터에 한 덩어리로 노출하기 위한 설정 묶음.
    // MonsterBase / PlayerController가 각각 하나씩 들고 있다.
    [System.Serializable]
    public class Settings
    {
        [Tooltip("맞은 자리에 생성할 이펙트 프리팹. 비우면 Resources의 기본 이펙트를 쓰고, " +
                 "그것도 없으면 임시 스파크를 런타임에 만든다")]
        public GameObject prefab;

        [Tooltip("이펙트를 맞은 방향으로 회전시킬지. 방사형 이펙트는 켜도 차이가 없고, " +
                 "한쪽으로 뻗는 이펙트만 켜면 된다")]
        public bool rotateToHitDirection = false;

        [Tooltip("이펙트 크기 배율")]
        public float scale = 1f;

        [Tooltip("최소 표시 시간. 프리팹을 쓸 땐 이 시간이 지난 뒤 파티클이 다 꺼지면 사라진다")]
        public float lifetime = 0.35f;

        [Tooltip("프리팹이 없을 때 쓰는 임시 이펙트의 색")]
        public Color fallbackColor = new Color(1f, 0.85f, 0.3f, 1f);

        // ★ 이 프로젝트는 Sorting Layer가 Default / New Layer 1 / New Layer 2 / camera로 갈려 있고,
        //   Sorting Layer가 다르면 Order in Layer는 완전히 무시된다. 이펙트 프리팹에 박힌 정렬값을
        //   그대로 쓰면 어떤 몬스터에선 맵 뒤에 가려 안 보인다. 맞은 대상 기준으로 맞춰주는 게 안전하다.
        [Tooltip("맞은 대상의 Sorting Layer를 이펙트에 물려준다. 끄면 프리팹에 설정된 값을 그대로 쓴다")]
        public bool inheritSortingFromTarget = true;

        [Tooltip("맞은 대상보다 몇 칸 앞에 그릴지")]
        public int sortingOrderOffset = 5;

        [Tooltip("끄면 이펙트를 아예 재생하지 않는다")]
        public bool enabled = true;
    }

    const int ShardCount = 6;
    const float ShardSpeedMin = 3.5f;
    const float ShardSpeedMax = 7f;
    const float ShardSpreadDegrees = 55f; // 맞은 방향 기준 좌우로 퍼지는 각도

    struct Shard
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 velocity;
        public float startScale;
    }

    Transform flash;
    SpriteRenderer flashRenderer;
    Shard[] shards;
    float life = 0.35f;
    float elapsed;
    float flashStartScale;
    Color tint = Color.white;

    // ── 공개 진입점 ───────────────────────────────────────────────

    // position: 이펙트가 터질 자리 / direction: 맞은 방향(공격이 날아온 반대쪽. 0이면 방향 없음)
    // sortingRef: 맞은 대상의 SpriteRenderer — 정렬 레이어를 물려받아 맵/배경 뒤에 숨지 않게 한다
    public static void Play(Settings settings, Vector3 position, Vector2 direction, SpriteRenderer sortingRef = null)
    {
        if (settings == null || !settings.enabled) return;

        // ① 인스펙터에 꽂은 프리팹 → ② Resources의 기본 이펙트 → ③ 런타임 임시 스파크
        GameObject prefab = settings.prefab != null ? settings.prefab : DefaultPrefab;

        if (prefab != null)
            SpawnPrefab(settings, prefab, position, direction, sortingRef);
        else
            SpawnFallback(settings, position, direction, sortingRef);
    }

    // ── 프리팹 경로 ───────────────────────────────────────────────

    // 인스펙터에 아무것도 안 꽂았을 때 쓰는 기본 이펙트.
    // Resources 아래에 있어서 씬/몬스터마다 일일이 연결하지 않아도 바로 나온다.
    // (이 프로젝트는 몬스터가 씬마다 개별 배치돼 있어 인스펙터 연결을 강요하면 반드시 빠뜨린다)
    public const string DefaultPrefabPath = "Effects/HitEffect_Hit";

    static GameObject defaultPrefab;
    static bool defaultPrefabLoaded;

    static GameObject DefaultPrefab
    {
        get
        {
            if (!defaultPrefabLoaded)
            {
                defaultPrefabLoaded = true;
                defaultPrefab = Resources.Load<GameObject>(DefaultPrefabPath);
            }
            return defaultPrefab;
        }
    }

    static void SpawnPrefab(Settings settings, GameObject prefab, Vector3 position, Vector2 direction,
                            SpriteRenderer sortingRef)
    {
        Quaternion rot = Quaternion.identity;
        if (settings.rotateToHitDirection && direction.sqrMagnitude > 0.0001f)
            rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        // ★ 부모를 붙이지 않는다 — 몬스터는 섭취되면 Destroy되는데, 자식으로 달아두면
        //   이펙트가 재생 도중 같이 사라진다.
        GameObject go = Object.Instantiate(prefab, position, rot);

        if (!Mathf.Approximately(settings.scale, 1f))
            go.transform.localScale *= Mathf.Max(0.01f, settings.scale);

        ApplySorting(settings, go, sortingRef);
        PlayAll(go);

        // 파티클이 다 꺼질 때까지 살려둔다. 길이를 미리 계산하지 않는 이유는
        // Particle System의 duration(=방출 구간)과 실제로 보이는 시간이 다르기 때문이다
        // (HitSpark은 duration이 5초지만 t=0에 한 번 터지고 입자 수명은 0.7초다).
        AutoDestroy killer = go.AddComponent<AutoDestroy>();
        killer.minLifetime = Mathf.Max(0.05f, settings.lifetime);
    }

    // 파티클/스프라이트 렌더러를 전부 맞은 대상과 같은 Sorting Layer로 올린다.
    // ParticleSystemRenderer도 SpriteRenderer도 Renderer라서 한 번에 처리된다.
    static void ApplySorting(Settings settings, GameObject go, SpriteRenderer sortingRef)
    {
        if (!settings.inheritSortingFromTarget || sortingRef == null) return;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerID = sortingRef.sortingLayerID;
            r.sortingOrder = sortingRef.sortingOrder + settings.sortingOrderOffset;
        }
    }

    // 생성만으로 재생되지 않는 프리팹(playOnAwake가 꺼져 있거나 애니메이터가 이미 끝난 상태)도
    // 확실히 처음부터 재생되게 한다. 팀원이 만든 이펙트는 테스트 씬에서 키를 눌러 재생하는
    // 구조라 playOnAwake가 꺼져 있는 경우가 있다.
    static void PlayAll(GameObject go)
    {
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }

        foreach (Animator anim in go.GetComponentsInChildren<Animator>(true))
        {
            if (anim.runtimeAnimatorController == null) continue;
            anim.Play(0, 0, 0f); // 현재 스테이트를 처음부터 다시
        }
    }

    // 파티클이 전부 소멸하면 스스로 지운다.
    class AutoDestroy : MonoBehaviour
    {
        public float minLifetime = 0.3f;
        public float maxLifetime = 10f; // 설정 실수로 영원히 안 사라지는 것 방지

        ParticleSystem[] systems;
        float elapsed;

        void Awake() => systems = GetComponentsInChildren<ParticleSystem>(true);

        void Update()
        {
            elapsed += Time.deltaTime;

            if (elapsed < minLifetime) return;
            if (elapsed >= maxLifetime) { Destroy(gameObject); return; }

            if (systems != null)
                foreach (ParticleSystem ps in systems)
                    if (ps != null && ps.IsAlive(true)) return;

            Destroy(gameObject);
        }
    }

    // ── 임시 이펙트 경로 ──────────────────────────────────────────

    static void SpawnFallback(Settings settings, Vector3 position, Vector2 direction, SpriteRenderer sortingRef)
    {
        GameObject root = new GameObject("HitEffect(임시)");
        root.transform.position = position;

        HitEffect fx = root.AddComponent<HitEffect>();
        fx.Build(settings, direction, sortingRef);
    }

    void Build(Settings settings, Vector2 direction, SpriteRenderer sortingRef)
    {
        life = Mathf.Max(0.05f, settings.lifetime);
        tint = settings.fallbackColor;

        float size = Mathf.Max(0.01f, settings.scale);

        int sortingLayerId = sortingRef != null ? sortingRef.sortingLayerID : 0;
        int sortingOrder = sortingRef != null ? sortingRef.sortingOrder + 5 : 100;

        // 가운데 번쩍임
        flashStartScale = 0.5f * size;
        flash = MakePiece("Flash", sortingLayerId, sortingOrder, out flashRenderer);
        flash.localScale = Vector3.one * flashStartScale;

        // 사방으로 튀는 파편. 맞은 방향이 있으면 그쪽으로 치우쳐 퍼진다.
        float baseAngle = direction.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
            : Random.Range(0f, 360f);
        float spread = direction.sqrMagnitude > 0.0001f ? ShardSpreadDegrees : 180f;

        shards = new Shard[ShardCount];
        for (int i = 0; i < ShardCount; i++)
        {
            Transform tr = MakePiece($"Shard{i}", sortingLayerId, sortingOrder, out SpriteRenderer sr);

            float angle = (baseAngle + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
            float speed = Random.Range(ShardSpeedMin, ShardSpeedMax) * size;
            float scale = Random.Range(0.12f, 0.22f) * size;

            tr.localScale = Vector3.one * scale;

            shards[i] = new Shard
            {
                tr = tr,
                sr = sr,
                velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                startScale = scale,
            };
        }
    }

    Transform MakePiece(string pieceName, int sortingLayerId, int sortingOrder, out SpriteRenderer sr)
    {
        GameObject go = new GameObject(pieceName);
        go.transform.SetParent(transform, false);

        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSoftCircle();
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = sortingOrder;
        sr.color = tint;

        return go.transform;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / life);

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float fade = 1f - t;

        if (flash != null)
        {
            flash.localScale = Vector3.one * Mathf.Lerp(flashStartScale, flashStartScale * 2.6f, t);
            Color c = tint;
            c.a = tint.a * fade * fade; // 번쩍임은 빨리 사라지게
            flashRenderer.color = c;
        }

        if (shards == null) return;

        for (int i = 0; i < shards.Length; i++)
        {
            Shard s = shards[i];
            if (s.tr == null) continue;

            s.tr.localPosition += (Vector3)(s.velocity * Time.deltaTime);
            s.velocity *= 0.88f; // 공기저항처럼 감속
            shards[i] = s;

            s.tr.localScale = Vector3.one * (s.startScale * fade);

            Color c = tint;
            c.a = tint.a * fade;
            s.sr.color = c;
        }
    }

    // 부드러운 원 스프라이트 (프로젝트 다른 런타임 표시와 같은 방식)
    static Sprite softCircle;

    static Sprite GetSoftCircle()
    {
        if (softCircle != null) return softCircle;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        Vector2 center = new Vector2(r, r);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01((r - d) / (r * 0.55f)); // 가운데는 꽉 차고 가장자리로 갈수록 옅어짐
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        softCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return softCircle;
    }
}
