using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenFadeManager : MonoBehaviour
{
    public static ScreenFadeManager Instance { get; private set; }

    [Header("페이드 설정")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeOutDuration = 0.7f;
    [SerializeField] private float fadeInDuration = 0.7f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        // 씬 재로드 시 중복 생성 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Canvas 전체가 씬이 바뀌어도 유지됨
        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 새로운 씬이 완전히 표시된 다음 밝아지게 함
        StartCoroutine(FadeInAfterSceneLoad());
    }

    private IEnumerator FadeInAfterSceneLoad()
    {
        // 씬 오브젝트가 한 프레임 동안 초기화될 시간을 줌
        yield return null;

        yield return FadeIn();
    }

    public IEnumerator FadeOut()
    {
        StopCurrentFade();

        canvasGroup.blocksRaycasts = true;

        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeOutDuration)
        {
            timer += Time.unscaledDeltaTime;

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                1f,
                timer / fadeOutDuration
            );

            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    public IEnumerator FadeIn()
    {
        StopCurrentFade();

        canvasGroup.blocksRaycasts = true;

        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                0f,
                timer / fadeInDuration
            );

            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    private void StopCurrentFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }
}