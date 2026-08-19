using UnityEngine;
using UnityEngine.UI;

// 화면 밝기 조절 (기획서 (7) 설정 UI의 '화면 밝기').
//
// 이 프로젝트엔 Post Processing 패키지가 없어서 카메라 후처리로 밝기를 못 만진다.
// 대신 화면 전체를 덮는 검은 판을 하나 띄우고 그 투명도로 어둡게 한다.
//   밝기 1   = 판이 완전히 투명 (원본 그대로)
//   밝기 0.5 = 판이 반쯤 검게 덮임
//
// 씬에 배치할 필요 없음 — GameSettings.Apply()가 처음 부를 때 자동 생성되고
// DontDestroyOnLoad라 씬을 넘어가도 유지된다.
public class ScreenBrightness : MonoBehaviour
{
    static ScreenBrightness instance;
    static Image overlay;

    public static void Apply(float brightness)
    {
        // 원본 밝기면 굳이 오브젝트를 만들지 않는다 (기본 상태에서 쓸데없는 캔버스가 안 생기게)
        if (instance == null && brightness >= 0.999f) return;

        EnsureInstance();
        if (overlay == null) return;

        float dark = Mathf.Clamp01(1f - brightness);
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
