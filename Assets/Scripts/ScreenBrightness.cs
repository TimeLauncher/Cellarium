using UnityEngine;
using UnityEngine.UI;

// 화면 밝기 조절 (기획서 (7) 설정 UI의 '화면 밝기').
//
// URP 후처리(ColorAdjustments)가 갖춰진 씬에서는 GameSettings가 그쪽으로 밝기를 걸고,
// 그렇지 않은 씬에서만 이 방식으로 넘어온다 — 화면 전체를 덮는 검은 판의 투명도로 어둡게 한다.
// 후처리와 달리 '원본보다 밝게'는 만들 수 없어서 0.5 위쪽은 원본 그대로 둔다.
//   밝기 0.5 이상 = 판이 완전히 투명 (원본 그대로)
//   밝기 0.25     = 판이 반쯤 검게 덮임
//   밝기 0        = 가장 어두움 (그래도 완전히 검게는 안 만든다 — 아래 maxDarkness 참고)
//
// 씬에 배치할 필요 없음 — GameSettings.Apply()가 처음 부를 때 자동 생성되고
// DontDestroyOnLoad라 씬을 넘어가도 유지된다.
public class ScreenBrightness : MonoBehaviour
{
    static ScreenBrightness instance;
    static Image overlay;

    // 검은 판은 설정 UI보다도 위에 그려진다(sortingOrder 32000). 완전히 불투명해지면
    // 밝기를 도로 올릴 설정창조차 안 보여서 빠져나올 방법이 없어진다.
    const float maxDarkness = 0.85f;

    public static void Apply(float brightness)
    {
        // 원본 밝기면 굳이 오브젝트를 만들지 않는다 (기본 상태에서 쓸데없는 캔버스가 안 생기게)
        if (instance == null && brightness >= 0.5f) return;

        EnsureInstance();
        if (overlay == null) return;

        // 0.5(원본) → 0, 0(가장 어두움) → maxDarkness
        float dark = Mathf.Clamp01((0.5f - brightness) * 2f) * maxDarkness;
        overlay.color = new Color(0f, 0f, 0f, dark);
        overlay.enabled = dark > 0.001f;
    }

    static void EnsureInstance()
    {
        if (instance != null) return;

        GameObject go = new GameObject("ScreenBrightness");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScreenBrightness>();

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // 무엇보다도 위 — 설정 UI·대화창까지 같이 어두워져야 한다

        GameObject overlayGo = new GameObject("Overlay");
        overlayGo.transform.SetParent(go.transform, false);

        overlay = overlayGo.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);
        overlay.raycastTarget = false; // 버튼 클릭을 막으면 안 된다

        RectTransform rt = overlayGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
