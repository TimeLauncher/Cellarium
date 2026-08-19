using System.Collections.Generic;
using UnityEngine;

// 조직 그물망: 분열체만 통과 가능한 반투명 장막, 본체는 통과 불가.
// 새 Unity 레이어를 만들지 않고 기존 분열체-분열체 충돌 무시 패턴(PlayerManager)과 동일하게
// 콜라이더 쌍 단위로 Physics2D.IgnoreCollision을 사용
[RequireComponent(typeof(Collider2D))]
public class TissueMesh : MonoBehaviour
{
    private static readonly List<TissueMesh> active = new List<TissueMesh>();

    private Collider2D[] cols;

    void Awake()
    {
        cols = GetComponents<Collider2D>();
        active.Add(this);

        if (PlayerManager.Instance != null)
        {
            foreach (var p in PlayerManager.Instance.allPlayers)
                if (p != null && p.isClone) IgnoreWith(p);
        }
    }

    void OnDestroy()
    {
        active.Remove(this);
    }

    void IgnoreWith(PlayerController clone)
    {
        if (clone == null || cols == null) return;

        // ★ GetComponent<Collider2D>() 하나만 잡으면 안 된다.
        //   플레이어에는 피격용 트리거 BoxCollider2D와 몸통 CircleCollider2D가 함께 붙어 있고,
        //   컴포넌트 순서상 트리거 쪽이 먼저 잡힌다. 그물망을 실제로 막는 건 몸통 콜라이더라서
        //   트리거에만 IgnoreCollision을 걸면 분열체가 그대로 튕긴다.
        //   (PlayerController.Awake가 bodyColliders를 따로 걸러내는 것과 같은 이유)
        foreach (Collider2D cloneCol in clone.GetComponents<Collider2D>())
        {
            if (cloneCol == null) continue;
            foreach (Collider2D meshCol in cols)
                if (meshCol != null) Physics2D.IgnoreCollision(cloneCol, meshCol, true);
        }
    }

    // 새 분열체가 등록될 때 PlayerManager가 호출 — 현재 존재하는 모든 그물망과 충돌 무시 설정
    public static void RegisterClone(PlayerController clone)
    {
        foreach (var mesh in active)
            mesh.IgnoreWith(clone);
    }
}
