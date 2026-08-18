using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// 배경음악 재생 담당. DontDestroyOnLoad 싱글턴이라 씬을 넘어가도 살아남는다.
//
// ★ 씬에 AudioSource를 그냥 놓으면 안 되는 이유:
//   사망 시 RespawnManager가 씬을 통째로 리로드하므로, 씬에 있는 AudioSource는 파괴됐다 다시 생성돼
//   죽을 때마다 BGM이 처음부터 다시 시작된다. 이 매니저는 씬 밖에서 살아남아 같은 곡이면 이어서 재생한다.
//
// 사용법
//   1) 빈 게임오브젝트 하나에 이 스크립트를 붙이고 sceneTracks에 "씬 이름 → 클립"을 채워둔다.
//      (첫 씬에만 놓으면 된다. 이후 씬으로 넘어가도 따라다닌다)
//   2) 코드에서 직접 바꾸고 싶으면 MusicManager.Instance.Play(clip) 호출.
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [System.Serializable]
    public class SceneTrack
    {
        public string sceneName;  // Build Settings에 등록된 씬 이름과 정확히 같아야 함
        public AudioClip clip;
    }

    [Header("씬별 BGM")]
    [Tooltip("씬 이름이 목록에 없으면 재생 중인 곡을 그대로 유지한다")]
    public SceneTrack[] sceneTracks;

    [Header("BGM을 끄는 씬")]
    // '목록에 없으면 유지'는 사망 리로드에서 음악이 끊기지 않게 하려는 규칙인데,
    // 그것 때문에 타이틀로 나가도 게임 BGM이 계속 흘렀다. 확실히 꺼야 하는 씬은 여기 적는다.
    // (새 필드라 씬에 저장된 값이 없어서, 씬을 안 고쳐도 아래 기본값이 그대로 적용된다)
    [Tooltip("이 목록에 있는 씬으로 들어가면 재생 중인 BGM을 멈춘다. sceneTracks보다 먼저 검사한다")]
    public string[] stopMusicScenes = { "maintitle", "SaveSelectScene" };



    [Header("설정")]
    [Range(0f, 1f)] public float volume = 0.5f;
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [Tooltip("곡이 바뀔 때 서서히 갈아타는 시간 (0이면 즉시 전환)")]
    public float crossfadeDuration = 1f;
    public bool loop = true;

    private AudioSource source;
    private AudioClip currentClip;
    private Coroutine fadeRoutine;

    // 실제로 스피커에 나가는 크기 = 이 씬에 정해둔 곡 크기 × 설정의 '배경음' 볼륨.
    // (설정의 '전체 볼륨'은 AudioListener에 따로 걸리므로 여기서 또 곱하지 않는다)
    private float TargetVolume => volume * GameSettings.BgmVolume;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 씬마다 MusicManager를 하나씩 배치해둔 구성이라, 중복이라고 그냥 버리면
            // 그 씬에 적어둔 곡 정보까지 같이 사라져 BGM이 안 바뀐다. 목록만 넘기고 사라진다.
            // (씬 오브젝트의 Awake는 sceneLoaded 콜백보다 먼저 돌기 때문에,
            //  합쳐둔 목록이 이번 씬 전환에 곧바로 반영된다)
            Instance.MergeTracks(sceneTracks);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = outputMixerGroup;
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = TargetVolume;
        source.spatialBlend = 0f; // 2D — 카메라 위치와 무관하게 항상 같은 크기로 들린다

        GameSettings.Changed += OnSettingsChanged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        PlayForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            GameSettings.Changed -= OnSettingsChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // 설정에서 배경음 볼륨을 바꾸면 곡을 끊지 않고 크기만 바로 반영한다.
    // 페이드 중이면 손대지 않는다 — 코루틴이 매 프레임 볼륨을 덮어쓰고 있어서
    // 여기서 끼어들면 페이드가 튄다 (끝날 때 TargetVolume으로 정리된다).
    void OnSettingsChanged()
    {
        if (source != null && fadeRoutine == null) source.volume = TargetVolume;
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        PlayForScene(scene.name);
    }

    // 중복 인스턴스가 들고 있던 씬별 곡 정보를 살아남는 쪽에 흡수한다. 같은 씬 이름은 먼저 등록된 쪽 우선.
    void MergeTracks(SceneTrack[] incoming)
    {
        if (incoming == null || incoming.Length == 0) return;

        List<SceneTrack> merged = sceneTracks != null
            ? new List<SceneTrack>(sceneTracks)
            : new List<SceneTrack>();

        foreach (SceneTrack t in incoming)
        {
            if (t == null || string.IsNullOrEmpty(t.sceneName)) continue;
            if (merged.Exists(m => m != null && m.sceneName == t.sceneName)) continue;
            merged.Add(t);
        }

        sceneTracks = merged.ToArray();
    }

    // 씬 이름 비교는 대소문자를 무시한다.
    // 실제 씬 파일은 'maintitle'인데 코드/인스펙터에는 'MainTitle'로 적혀 있는 곳이 섞여 있어서,
    // 문자열 == 로 비교하면 조용히 안 맞는다. (GameScene.TITLE, SaveSelectManager.titleSceneName)
    static bool SameScene(string a, string b)
    {
        return !string.IsNullOrEmpty(a) && string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    void PlayForScene(string sceneName)
    {
        // 음악이 없어야 하는 씬(타이틀 등)을 먼저 본다 — 여기 걸리면 무조건 정지
        if (stopMusicScenes != null)
            foreach (string s in stopMusicScenes)
                if (SameScene(s, sceneName)) { Stop(); return; }

        if (sceneTracks == null) return;

        foreach (SceneTrack t in sceneTracks)
            if (t != null && SameScene(t.sceneName, sceneName)) { Play(t.clip); return; }

        // 목록에 없는 씬이면 아무것도 하지 않는다 — 재생 중인 곡이 그대로 이어진다
    }

    // 같은 곡이면 아무것도 하지 않는다. 죽어서 씬이 리로드돼도 BGM이 끊기지 않는 핵심.
    public void Play(AudioClip clip)
    {
        if (clip == null) { Stop(); return; }
        if (clip == currentClip && source.isPlaying) return;

        currentClip = clip;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (crossfadeDuration <= 0f)
        {
            source.clip = clip;
            source.volume = TargetVolume;
            source.Play();
            return;
        }

        fadeRoutine = StartCoroutine(CrossfadeTo(clip));
    }

    public void Stop()
    {
        currentClip = null;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        source.Stop();
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (fadeRoutine == null) source.volume = TargetVolume;
    }

    IEnumerator CrossfadeTo(AudioClip clip)
    {
        float half = crossfadeDuration * 0.5f;

        // 현재 곡 페이드 아웃
        if (source.isPlaying)
        {
            float from = source.volume;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(from, 0f, t / half);
                yield return null;
            }
        }

        source.clip = clip;
        source.loop = loop;
        source.volume = 0f;
        source.Play();

        // 새 곡 페이드 인
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(0f, TargetVolume, t / half);
            yield return null;
        }

        source.volume = TargetVolume;
        fadeRoutine = null;
    }
}
