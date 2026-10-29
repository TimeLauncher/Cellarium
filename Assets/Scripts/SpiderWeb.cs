using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class SpiderWeb : MonoBehaviour
{
    // 기믹 테스트 피드백(9/22): "범위에서 탈출 후 슬로우 효과 바로 적용 안되게 하기".
    // 0이면 거미줄 밖으로 나가는 즉시 배율이 풀린다 (접촉이 끊긴 다음 프레임).
    // 늘리면 예전처럼 나간 뒤에도 그 시간만큼 끈적임이 남는다.
    [Tooltip("거미줄에서 나온 뒤 슬로우가 남는 시간(초). 0이면 나가는 즉시 풀린다")]
    [Min(0f)] public float lingerDuration = 0f;
    [Range(0.05f, 1f)] public float moveMultiplier = 0.5f;
    [Range(0.05f, 1f)] public float jumpMultiplier = 0.7f;
    [Range(0.05f, 1f)] public float dashMultiplier = 0.5f;

    // A10~A11 Fix (2): 거미줄에 닿아 있는 동안 낙하 속도가 크게 줄어든다.
    // 두 값 중 더 강한 쪽이 체감된다 — 중력 배율은 '얼마나 빨리 가속하나', 최대 낙하 속도는 '얼마나 빨라질 수 있나'.
    // 빠르게 떨어지다 거미줄에 들어오면 그 즉시 최대 낙하 속도로 꺾인다.
    [Header("낙하 감속")]
    [Tooltip("거미줄 안에서 내려갈 때 걸리는 중력 배율. 1이면 평소와 같다")]
    [Range(0.05f, 1f)] public float fallGravityMultiplier = 0.3f;
    [Tooltip("거미줄 안에서의 최대 낙하 속도(유닛/초). 0이면 제한 없음")]
    [Min(0f)] public float maxFallSpeed = 2f;
    Collider2D area;
    void Awake() { area = GetComponent<Collider2D>(); area.isTrigger = true; }
    void FixedUpdate()
    {
        foreach (var player in GimmickPhysics.PlayersIn(area))
            player.Traversal.ApplyWeb(this, Mathf.Max(lingerDuration, Time.fixedDeltaTime * 2f),
                moveMultiplier, jumpMultiplier, dashMultiplier,
                fallGravityMultiplier, maxFallSpeed > 0f ? maxFallSpeed : float.PositiveInfinity);
    }
}
