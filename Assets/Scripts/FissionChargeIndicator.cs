using UnityEngine;
using UnityEngine.UI;

// Q홀드 진행도를 캐릭터 위에 조그맣게 표시
// Canvas가 어디에 있든 상관없이 Inspector에서 player와 fillImage를 직접 연결
public class FissionChargeIndicator : MonoBehaviour
{
    public PlayerController player;
    public Image fillImage;
    public Vector3 offset = new Vector3(0f, 1.2f, 0f);

    void Update()
    {
        if (player == null || fillImage == null) return;

        float progress = player.FissionHoldProgress;
        fillImage.enabled = progress > 0f;

        if (progress > 0f)
        {
            transform.position = player.transform.position + offset;
            fillImage.fillAmount = progress;
        }
    }
}
