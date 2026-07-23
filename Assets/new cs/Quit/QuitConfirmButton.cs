using UnityEngine;
using UnityEngine.EventSystems;

public class QuitConfirmButton : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private QuitConfirmSelector selector;
    [SerializeField] private int index;

    public void OnPointerEnter(PointerEventData eventData)
    {
        selector.HoverButton(index);
    }
}