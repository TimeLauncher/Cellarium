using UnityEngine;
using UnityEngine.UI;

// 문: 연결된 버튼이 전부 동시에 활성화되면 영구 개방 (이후 버튼이 꺼져도 유지)
public class Door : MonoBehaviour
{
    public ButtonSwitch[] requiredButtons;
    public Text requiredCountText; // "활성화 수/필요 수" 표시 (디자인 미정 - 임시 텍스트)
    public bool isOpen;

    private Collider2D col;
    private SpriteRenderer spr;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (isOpen) return;

        int activeCount = 0;
        foreach (var b in requiredButtons)
            if (b != null && b.IsActive) activeCount++;

        if (requiredCountText != null)
            requiredCountText.text = $"{activeCount}/{requiredButtons.Length}";

        if (requiredButtons.Length > 0 && activeCount >= requiredButtons.Length)
            Open();
    }

    void Open()
    {
        isOpen = true;
        if (col != null) col.enabled = false;
        if (spr != null) spr.enabled = false; // 디자인 미정 - 임시로 숨김 처리
        Debug.Log("문 개방!");
    }
}
