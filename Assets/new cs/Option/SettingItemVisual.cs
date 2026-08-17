using UnityEngine;
using UnityEngine.EventSystems;

public class SettingItemVisual : MonoBehaviour,
    ISelectHandler,
    IDeselectHandler
{
    [Header("선택 표시")]
    [SerializeField] private GameObject underline;

    [Header("좌우 선택형 옵션")]
    [SerializeField] private GameObject leftArrow;
    [SerializeField] private GameObject rightArrow;

    private void Awake()
    {
        SetSelected(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetSelected(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetSelected(false);
    }

    private void SetSelected(bool selected)
    {
        if (underline != null)
            underline.SetActive(selected);

        if (leftArrow != null)
            leftArrow.SetActive(selected);

        if (rightArrow != null)
            rightArrow.SetActive(selected);
    }
}