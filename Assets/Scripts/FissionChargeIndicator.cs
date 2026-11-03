using UnityEngine;
using UnityEngine.UI;

// Q홀드 진행도를 캐릭터 위에 조그맣게 표시
// Canvas가 어디에 있든 상관없이 Inspector에서 player와 fillImage를 직접 연결
public class FissionChargeIndicator : MonoBehaviour
{
    public PlayerController player;
    public Image fillImage;
    public Vector3 offset = new Vector3(0f, 1.2f, 0f);

    // A10~A11 Fix: 분열 차지바는 인게임에서 뺀다 (분열 후 한 번 더 도는 문제도 같이 사라짐).
    // 디버그로 다시 보고 싶을 때만 켠다.
    [Tooltip("켜면 Q 홀드 진행도 바를 표시한다. 인게임에서는 끈다")]
    public bool showInGame = false;

    void Update()
    {
        if (player == null || fillImage == null) return;
        if (!showInGame)
        {
            if (fillImage.enabled) fillImage.enabled = false;
            return;
        }

        float progress = player.FissionHoldProgress;
        fillImage.enabled = progress > 0f;

        if (progress > 0f)
        {
            transform.position = player.transform.position + offset;
            fillImage.fillAmount = progress;
        }
    }
}
