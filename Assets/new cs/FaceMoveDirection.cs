using UnityEngine;

public class FaceMoveDirection : MonoBehaviour
{
    private SpriteRenderer spr;
    private Vector3 lastPosition;

    [Tooltip("이 정도 이상 움직였을 때만 방향을 바꿉니다.")]
    public float moveThreshold = 0.001f;

    void Awake()
    {
        spr = GetComponent<SpriteRenderer>();
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        float moveX = transform.position.x - lastPosition.x;

        if (Mathf.Abs(moveX) > moveThreshold)
        {
            // 오른쪽 이동 = 기본 방향
            // 왼쪽 이동 = 좌우 반전
            spr.flipX = moveX > 0f;
        }

        lastPosition = transform.position;
    }
}