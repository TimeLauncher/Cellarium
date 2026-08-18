using UnityEngine;

// (주석이 깨진 인코딩으로 저장돼 있어 이번에 UTF-8 한글로 다시 적었다. 동작은 그대로다)
public class CameraFollow : MonoBehaviour
{
    public float smoothSpeed = 0.125f;              // 따라가는 속도 (작을수록 부드럽게)
    public Vector3 offset = new Vector3(0, 0, -10); // 카메라 거리 조정

    void LateUpdate()
    {
        if (!TryGetTarget(out Transform target)) return;

        // 목표 지점 (지금 조종 중인 캐릭터 위치 + 오프셋)
        Vector3 desiredPosition = target.position + offset;

        // 부드럽게 이동 (Lerp)
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
    }

    // 보간 없이 즉시 대상 위로. 씬을 넘어온 직후 카메라가 옛 위치에서 스르륵 따라오며
    // 화면이 흔들려 보이는 것을 막는다 (CameraSnap이 호출).
    public void SnapToTarget()
    {
        if (!TryGetTarget(out Transform target)) return;
        transform.position = target.position + offset;
    }

    // 매니저가 없거나 조종 중인 캐릭터가 없으면 아무것도 안 함
    bool TryGetTarget(out Transform target)
    {
        target = null;

        if (PlayerManager.Instance == null || PlayerManager.Instance.currentPlayer == null)
            return false;

        target = PlayerManager.Instance.currentPlayer.transform;
        return true;
    }
}
