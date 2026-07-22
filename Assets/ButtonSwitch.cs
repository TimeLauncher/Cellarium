using UnityEngine;

// 버튼: PC가 위에 올라가 접촉해 있는 동안 활성화 (밟고 서는 방식이라 솔리드 콜라이더 + 일반 충돌 사용)
public class ButtonSwitch : MonoBehaviour
{
    public bool IsActive { get; private set; }

    private int contactCount;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponent<PlayerController>() == null) return;
        contactCount++;
        IsActive = contactCount > 0;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponent<PlayerController>() == null) return;
        contactCount = Mathf.Max(0, contactCount - 1);
        IsActive = contactCount > 0;
    }
}
