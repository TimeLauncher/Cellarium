using System.Collections;
using UnityEngine;

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

    [Header("설정")]
    [Range(0f, 1f)] public float volume = 0.5f;
    [Tooltip("곡이 바뀔 때 서서히 갈아타는 시간 (0이면 즉시 전환)")]
    public float crossfadeDuration = 1f;
    public bool loop = true;

    private AudioSource source;
    private AudioClip currentClip;
    private Coroutine fadeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = volume;
        source.spatialBlend = 0f; // 2D — 카메라 위치와 무관하게 항상 같은 크기로 들린다

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        PlayForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance == this)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        PlayForScene(scene.name);
    }

    void PlayForScene(string sceneName)
    {
        if (sceneTracks == null) return;

        foreach (SceneTrack t in sceneTracks)
            if (t != null && t.sceneName == sceneName) { Play(t.clip); return; }

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
            source.volume = volume;
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
        if (fadeRoutine == null) source.volume = volume;
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
            source.volume = Mathf.Lerp(0f, volume, t / half);
            yield return null;
        }

        source.volume = volume;
        fadeRoutine = null;
    }
}
