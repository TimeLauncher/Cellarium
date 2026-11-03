using UnityEngine;

// 대시 이펙트 (A10~A11 Fix '대쉬 시 이펙트 추가').
// ① 잔상: 대시하는 동안 PC 스프라이트를 일정 간격으로 복사해 남기고 서서히 지운다 — 에셋 없이 바로 보인다.
// ② 시작 이펙트(선택): prefab을 꽂으면 대시 시작 자리에서 대시 방향으로 돌려 재생한다 (HitEffect와 같은 경로).
// 이 컴포넌트 자체는 잔상 한 장이다. PlayerController는 아래 static 진입점만 쓴다.
public class DashEffect : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("끄면 대시 이펙트를 아예 재생하지 않는다")]
        public bool enabled = true;

        [Header("잔상")]
        public bool afterimages = true;
        [Tooltip("잔상을 남기는 간격(초). 작을수록 촘촘하다")]
        [Min(0.01f)] public float afterimageInterval = 0.03f;
        [Tooltip("잔상 하나가 사라지기까지 걸리는 시간(초)")]
        [Min(0.01f)] public float afterimageLifetime = 0.2f;
        [Tooltip("잔상 색. 알파가 시작 투명도다")]
        public Color afterimageColor = new Color(0.6f, 0.9f, 1f, 0.55f);

        [Header("시작 이펙트 (선택)")]
        [Tooltip("대시 시작 자리에 생성할 프리팹. 비워두면 잔상만 나온다")]
        public GameObject prefab;
        public bool rotateToDashDirection = true;
        public float scale = 1f;
        [Tooltip("최소 표시 시간. 이 시간이 지난 뒤 파티클이 다 꺼지면 사라진다")]
        public float lifetime = 0.3f;
    }

    SpriteRenderer sr;
    Color startColor;
    float life;
    float elapsed;

    // 대시 시작
    public static void PlayStart(Settings settings, Vector3 position, Vector2 direction, SpriteRenderer source)
    {
        if (settings == null || !settings.enabled || settings.prefab == null) return;
        HitEffect.Play(new HitEffect.Settings
        {
            prefab = settings.prefab,
            rotateToHitDirection = settings.rotateToDashDirection,
            scale = settings.scale,
            lifetime = settings.lifetime,
            inheritSortingFromTarget = true,
            sortingOrderOffset = -1, // PC 뒤
        }, position, direction, source);
    }

    // 대시 중 잔상 한 장
    public static void SpawnAfterimage(Settings settings, SpriteRenderer source)
    {
        if (settings == null || !settings.enabled || !settings.afterimages) return;
        if (source == null || source.sprite == null) return;

        var go = new GameObject("DashAfterimage");
        Transform from = source.transform;
        go.transform.SetPositionAndRotation(from.position, from.rotation);
        go.transform.localScale = from.lossyScale;

        var copy = go.AddComponent<SpriteRenderer>();
        copy.sprite = source.sprite;
        copy.flipX = source.flipX;
        copy.flipY = source.flipY;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = source.sortingOrder - 1;
        copy.color = settings.afterimageColor;

        var fx = go.AddComponent<DashEffect>();
        fx.sr = copy;
        fx.startColor = settings.afterimageColor;
        fx.life = settings.afterimageLifetime;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = life > 0f ? elapsed / life : 1f;
        if (t >= 1f || sr == null) { Destroy(gameObject); return; }
        Color c = startColor;
        c.a *= 1f - t;
        sr.color = c;
    }
}
