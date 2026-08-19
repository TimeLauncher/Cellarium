using UnityEngine;

// 일반균: MonsterBase 기본 행동을 사용하고,
// 일반균 전용 애니메이션과 스프라이트 방향만 별도로 처리
public class GeneralGerm : MonsterBase
{
    protected override void Update()
    {
        // 감지, 추적, 공격, 쿨다운 등 부모 로직 실행
        base.Update();

        UpdateGeneralGermAnimator();
    }

    private void UpdateGeneralGermAnimator()
    {
        if (animator == null)
            return;

        bool isMoving =
            !IsDead &&
            !isAttacking &&
            rb != null &&
            Mathf.Abs(rb.linearVelocity.x) > 0.05f;

        animator.SetBool("move", isMoving);
        animator.SetBool("IsDead", IsDead);
    }

    protected override void FaceDirection(float dirX)
    {
        if (Mathf.Abs(dirX) <= 0.01f)
            return;

        // 부모에서 facingDir 및 공격 히트박스 방향 처리
        base.FaceDirection(dirX);

        // 일반균 원본 스프라이트가 왼쪽을 바라보므로 반대로 뒤집기
        if (spr != null)
        {
            spr.flipX = dirX > 0f;
        }
    }
}