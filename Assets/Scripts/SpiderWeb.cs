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
    Collider2D area;
    void Awake() { area = GetComponent<Collider2D>(); area.isTrigger = true; }
    void FixedUpdate()
    {
        foreach (var player in GimmickPhysics.PlayersIn(area))
            player.Traversal.ApplyWeb(this, Mathf.Max(lingerDuration, Time.fixedDeltaTime * 2f),
                moveMultiplier, jumpMultiplier, dashMultiplier);
    }
}
