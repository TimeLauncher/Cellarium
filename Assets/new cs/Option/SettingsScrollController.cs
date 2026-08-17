using UnityEngine;
using UnityEngine.UI;

public class SettingsScrollController : MonoBehaviour
{
    [Header("Scroll")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Sections")]
    [SerializeField] private RectTransform generalSection;
    [SerializeField] private RectTransform audioSection;
    [SerializeField] private RectTransform controlsSection;

    public void GoToGeneral()
    {
        ScrollTo(generalSection);
    }

    public void GoToAudio()
    {
        ScrollTo(audioSection);
    }

    public void GoToControls()
    {
        ScrollTo(controlsSection);
    }

    private void ScrollTo(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport;

        Vector2 targetLocalPosition =
            (Vector2)viewport.InverseTransformPoint(content.position)
            - (Vector2)viewport.InverseTransformPoint(target.position);

        content.anchoredPosition =
            new Vector2(content.anchoredPosition.x, targetLocalPosition.y);
    }
}