using UnityEngine;

// 거리별 음향 자동 조정 (기획서 (2) '거리별 음향 자동 조정').
//
// 기획서: "특정 구역 내에서만 소리가 들리며, 소리의 핵심에 가까울수록 해당 소리가 더 커지는 기능"
// 사용처: A02 우측 상단 적혈구 주민 구조 이벤트 — NPC가 근처에 있으면 울음소리가 들리고,
//         가까워질수록 소리가 커져 플레이어의 동선을 유도한다.
//
// ★ Unity 내장 3D 사운드(spatialBlend=1)를 안 쓰는 이유
//   3D 사운드는 AudioListener(=카메라) 기준으로 계산된다. 이 게임은 카메라가 플레이어를
//   따라가긴 하지만 분열체로 조종을 옮기면 카메라도 같이 옮겨가고, 시네머신 보정 때문에
//   실제 플레이어 위치와 어긋난다. 기획 의도는 "PC가 가까워지면 커진다"이므로
//   2D 사운드로 두고 조종 중인 캐릭터와의 거리로 직접 볼륨을 계산한다.
//
// 배치법
//   1) 소리의 '핵심' 위치(= 구조 대상 NPC)에 빈 오브젝트를 두고 이 스크립트를 붙인다
//   2) Clip에 울음소리 등을 넣는다 (Loop 권장)
//   3) Outer Radius = 소리가 들리기 시작하는 범위, Inner Radius = 최대 볼륨이 되는 범위
//      → 씬 뷰에서 노란 원 두 개로 확인할 수 있다 (기획서 그림의 노란 원)
[DisallowMultipleComponent]
public class ProximitySound : MonoBehaviour
{
    [Header("소리")]
    public AudioClip clip;
    public bool loop = true;
    [Range(0f, 1f)] public float maxVolume = 1f;

    [Header("범위")]
    [Tooltip("이 거리 안으로 들어오면 최대 볼륨")]
    public float innerRadius = 3f;
    [Tooltip("이 거리 밖에서는 아무 소리도 안 들린다")]
    public float outerRadius = 15f;

    [Tooltip("바깥(0) → 안쪽(1)으로 갈 때 볼륨이 커지는 곡선. 기본은 부드러운 곡선")]
    public AnimationCurve falloff = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("동작")]
    [Tooltip("끄면 범위 밖에서도 클립을 계속 돌린다(볼륨만 0). 여러 소리가 박자를 맞춰야 할 때만 켤 것")]
    public bool stopWhenOutOfRange = true;
    [Tooltip("설정의 '효과음' 볼륨을 곱한다")]
    public bool useSfxVolume = true;

    [Header("게임 화면 표시 (확인용)")]
    // 소리가 나는 범위는 원래 눈에 안 보이는 게 맞다(기획 의도가 '소리로 동선 유도'이므로).
    // 시연·테스트 씬에서 범위를 확인할 때만 켠다.
    [Tooltip("게임 화면에 들리는 범위를 원으로 표시한다. 실제 게임에선 꺼둘 것")]
    public bool showArea = false;
    public Color areaColor = new Color(1f, 0.85f, 0.2f, 0.12f);
    [Tooltip("범위 위에 띄울 설명. 비우면 글씨를 안 만든다")]
    public string areaLabel = "거리별 음향";
    public int areaSortingOrder = 25;

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D — 거리 감쇠는 아래에서 직접 계산한다
        source.volume = 0f;

        if (showArea)
            ZoneVisualizer.ShowCircle(gameObject, outerRadius, areaColor, areaLabel, areaSortingOrder);
    }

    void Update()
    {
        if (clip == null || source == null) return;

        Transform listener = GetListener();
        if (listener == null) return;

        float distance = Vector2.Distance(transform.position, listener.position);

        if (distance > outerRadius)
        {
            if (stopWhenOutOfRange)
            {
                if (source.isPlaying) source.Stop();
            }
            else
            {
                source.volume = 0f;
                if (!source.isPlaying) source.Play();
            }
            return;
        }

        // 바깥(0) → 안쪽(1)
        float span = Mathf.Max(0.01f, outerRadius - innerRadius);
        float t = Mathf.Clamp01((outerRadius - distance) / span);

        float volume = maxVolume * falloff.Evaluate(t);
        if (useSfxVolume) volume *= GameSettings.SfxVolume;

        source.volume = volume;
        if (!source.isPlaying) source.Play();
    }

    // 조종 중인 캐릭터를 기준으로 한다. 분열체로 조종을 옮기면 그 분열체가 기준이 된다.
    Transform GetListener()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.currentPlayer != null)
            return PlayerManager.Instance.currentPlayer.transform;

        // 플레이어가 아직 없는 씬(타이틀 등)에서는 카메라 기준으로 대체
        return Camera.main != null ? Camera.main.transform : null;
    }

    // 기획서 그림처럼 씬 뷰에서 범위를 눈으로 확인할 수 있게 한다
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.92f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, outerRadius);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Min(innerRadius, outerRadius));
    }
}
