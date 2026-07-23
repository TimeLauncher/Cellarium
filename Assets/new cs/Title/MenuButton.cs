using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButton : MonoBehaviour, IPointerEnterHandler
{
    public int index;
    public TitleMenuSelector selector;

    public void OnPointerEnter(PointerEventData eventData)
    {
        selector.HoverMenu(index);
    }
}