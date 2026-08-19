using UnityEngine;
using UnityEngine.UI;

// 문: 연결된 버튼이 전부 동시에 활성화되면 영구 개방 (이후 버튼이 꺼져도 유지)
public class Door : MonoBehaviour
{
    public ButtonSwitch[] requiredButtons;
    public Text requiredCountText; // "활성화 수/필요 수" 표시 (디자인 미정 - 임시 텍스트)
    public bool isOpen;

    [Header("개방 진행 표시 (스프라이트 나오기 전 임시)")]
    public Color closedColor = Color.white;
    public Color readyColor = Color.cyan; // 버튼이 눌릴수록 이 색에 가까워짐

    private Collider2D col;
    private SpriteRenderer spr;
    private int lastActiveCount = -1;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spr = GetComponent<SpriteRenderer>();

        if (requiredButtons == null || requiredButtons.Length == 0)
            Debug.LogWarning($"[{name}] Required Buttons가 비어 있습니다. 버튼을 연결해야 문이 열립니다.", this);
    }

    void Update()
    {
        if (isOpen) return;

        int activeCount = 0;
        foreach (var b in requiredButtons)
            if (b != null && b.IsActive) activeCount++;

        if (activeCount != lastActiveCount)
        {
            lastActiveCount = activeCount;

            if (requiredCountText != null)
                requiredCountText.text = $"{activeCount}/{requiredButtons.Length}";

            // 텍스트 UI를 아직 안 붙였어도 진행 상황이 보이도록 색으로도 표시
            if (spr != null && requiredButtons.Length > 0)
                spr.color = Color.Lerp(closedColor, readyColor, (float)activeCount / requiredButtons.Length);

            Debug.Log($"[{name}] 버튼 {activeCount}/{requiredButtons.Length} 활성화");
        }

        if (requiredButtons.Length > 0 && activeCount >= requiredButtons.Length)
            Open();
    }

    void Open()
    {
        isOpen = true;
        if (col != null) col.enabled = false;
        if (spr != null) spr.enabled = false; // 디자인 미정 - 임시로 숨김 처리
        Debug.Log($"[{name}] 문 개방!");
    }
}
