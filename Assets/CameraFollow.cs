using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public float smoothSpeed = 0.125f; // 따라가는 속도 (부드럽게)
    public Vector3 offset = new Vector3(0, 0, -10); // 카메라 거리 유지

    void LateUpdate()
    {
        // 매니저가 없거나 조종 중인 캐릭터가 없으면 아무것도 안 함
        if (PlayerManager.Instance == null || PlayerManager.Instance.currentPlayer == null)
            return;

        // 목표 지점 (현재 조종 중인 캐릭터 위치 + 오프셋)
        Transform target = PlayerManager.Instance.currentPlayer.transform;
        Vector3 desiredPosition = target.position + offset;

        // 부드럽게 이동 (Lerp 사용)
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }
}