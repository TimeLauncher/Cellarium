using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleMenuSelector : MonoBehaviour
{
    [Header("메뉴 버튼들")]
    [SerializeField] private Button[] buttons;

    [Header("선택 표시 화살표")]
    [SerializeField] private RectTransform arrow;

    [Header("화살표 위치 보정")]
    [SerializeField] private Vector2 arrowOffset = new Vector2(-80f, 0f);

    private bool menuActivated = false;
    private int currentIndex = 0;

    private void Start()
    {
        arrow.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 아직 메뉴 선택을 시작하지 않은 상태
        if (!menuActivated)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.S) ||
                Input.GetKeyDown(KeyCode.W))
            {
                menuActivated = true;

                arrow.gameObject.SetActive(true);

                // 아래를 눌렀으면 첫 번째 메뉴
                SelectMenu(0);
            }

            return;
        }

        // 메뉴 선택 시작 후
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            SelectMenu(currentIndex + 1);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            SelectMenu(currentIndex - 1);
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            buttons[currentIndex].onClick.Invoke();
        }
    }

    private void SelectMenu(int index)
    {
        if (index < 0)
            index = buttons.Length - 1;

        if (index >= buttons.Length)
            index = 0;

        currentIndex = index;

        RectTransform target = buttons[currentIndex].GetComponent<RectTransform>();

        arrow.position = target.position + new Vector3(arrowOffset.x, arrowOffset.y, 0f);

        EventSystem.current.SetSelectedGameObject(buttons[currentIndex].gameObject);
    }
    public void HoverMenu(int index)
    {
        menuActivated = true;

        arrow.gameObject.SetActive(true);

        SelectMenu(index);
    }
}