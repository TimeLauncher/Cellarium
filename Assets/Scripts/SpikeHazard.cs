using UnityEngine;

// 가시: PC 접촉 시 체력 감소 + 접촉 반대 방향으로 넉백. 솔리드 콜라이더라 위에 서있을 수 있음
public class SpikeHazard : MonoBehaviour
{
    public float damage = 50f;
    public float knockbackForce = 10f;
    [Tooltip("넉백에 섞는 위쪽 성분 비율. 0이면 옆으로만 밀리고, 클수록 위로 띄운다")]
    public float knockbackUpRatio = 0.5f;
    [Tooltip("켜면 가시보다 아래에 있어도 위로 띄운다 (천장 가시는 끄는 게 자연스럽다)")]
    public bool knockbackAlwaysUp = true;

    void OnCollisionEnter2D(Collision2D collision) => Hit(collision.gameObject);

    // QA (5): 가시에 접촉 상태를 유지해도 계속 피격. TakeDamage가 무적 중엔 무시하므로
    // 무적시간이 재피격 간격을 알아서 rate-limit 해준다.
    void OnCollisionStay2D(Collision2D collision) => Hit(collision.gameObject);

    void Hit(GameObject other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        // 좌우는 가시→플레이어 기준, 거기에 위쪽 성분을 섞어 띄운다
        // (그대로 normalize하면 서로 비슷한 높이일 때 x축으로만 밀린다)
        float dx = pc.transform.position.x - transform.position.x;
        float dy = pc.transform.position.y - transform.position.y;

        Vector2 dir = new Vector2(Mathf.Abs(dx) < 0.001f ? 0f : Mathf.Sign(dx), 0f);
        float up = knockbackAlwaysUp ? 1f : (Mathf.Abs(dy) < 0.001f ? 1f : Mathf.Sign(dy));
        dir += Vector2.up * (up * knockbackUpRatio);
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;

        pc.TakeDamage(damage, dir.normalized * knockbackForce, 0f);
    }
}
