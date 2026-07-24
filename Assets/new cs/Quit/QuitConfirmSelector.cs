using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class QuitConfirmSelector : MonoBehaviour
{
    [Header("예/아니요 버튼")]
    [SerializeField] private Button[] buttons;

    [Header("선택 화살표")]
    [SerializeField] private RectTransform arrow;

    [Header("화살표 위치 보정")]
    [SerializeField] private Vector2 arrowOffset = new Vector2(-40f, 0f);

    private int currentIndex;
    private bool isActive;

    private void Update()
    {
        if (!isActive)
            return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.A))
        {
            SelectButton(currentIndex - 1);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) ||
            Input.GetKeyDown(KeyCode.D))
        {
            SelectButton(currentIndex + 1);
        }

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            buttons[currentIndex].onClick.Invoke();
        }
    }

    public void OpenSelector()
    {
        isActive = true;

        arrow.gameObject.SetActive(true);

        // 처음 선택을 "아니요"로 하고 싶다면 1
        SelectButton(1);
    }

    public void CloseSelector()
    {
        isActive = false;

        arrow.gameObject.SetActive(false);

        EventSystem.current.SetSelectedGameObject(null);
    }

    public void HoverButton(int index)
    {
        if (!isActive)
            return;

        SelectButton(index);
    }

    private void SelectButton(int index)
    {
        if (index < 0)
            index = buttons.Length - 1;

        if (index >= buttons.Length)
            index = 0;

        currentIndex = index;

        RectTransform target =
            buttons[currentIndex].GetComponent<RectTransform>();

        arrow.position =
            target.position +
            new Vector3(arrowOffset.x, arrowOffset.y, 0f);

        EventSystem.current.SetSelectedGameObject(
            buttons[currentIndex].gameObject
        );
    }
}