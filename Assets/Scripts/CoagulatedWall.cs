using UnityEngine;

// 응고 조직 벽(OBJ_CoagulatedWall) — PC의 공격으로 부서지는 벽. 부서지면 재생성되지 않는다.
// 벽 뒤 공간은 VisionArea로 가려 두고 revealOnBreak에 연결하면, 벽이 부서질 때 암시야가 풀린다.
// ★ 벽이 PC를 막아야 하므로 콜라이더는 Is Trigger 끔, 레이어는 지형(Ground/Wall)에 둘 것.
[RequireComponent(typeof(Collider2D))]
public class CoagulatedWall : DestructibleObject
{
    [Header("벽 뒤 암시야")]
    [Tooltip("벽이 부서지면 해제할 시야 제한 영역. 그 영역은 보통 'Reveal Only By Trigger'를 켜 둔다")]
    public VisionArea[] revealOnBreak = new VisionArea[0];

    [Header("파괴 연출")]
    [Tooltip("파괴 시 추가로 재생할 이펙트 (비워도 됨)")]
    public GameObject breakEffectPrefab;

    protected override void Awake()
    {
        base.Awake();
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = false;
    }

    protected override void OnDestroyed()
    {
        RevealLinked();
        if (breakEffectPrefab != null)
            Instantiate(breakEffectPrefab, HitBounds.center, Quaternion.identity);
        base.OnDestroyed();
    }

    protected override void OnAlreadyDestroyed()
    {
        RevealLinked(); // VisionArea 쪽도 기록이 남아 있지만, 설정을 바꿔도 어긋나지 않게 한 번 더 푼다
        base.OnAlreadyDestroyed();
    }

    void RevealLinked()
    {
        foreach (var v in revealOnBreak)
            if (v != null) v.Reveal();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 1f);
        foreach (var v in revealOnBreak)
            if (v != null) Gizmos.DrawLine(transform.position, v.transform.position);
    }
}
