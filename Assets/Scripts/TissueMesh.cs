using System.Collections.Generic;
using UnityEngine;

// 조직 그물망: 분열체만 통과 가능한 반투명 장막, 본체는 통과 불가.
// 새 Unity 레이어를 만들지 않고 기존 분열체-분열체 충돌 무시 패턴(PlayerManager)과 동일하게
// 콜라이더 쌍 단위로 Physics2D.IgnoreCollision을 사용
[RequireComponent(typeof(Collider2D))]
public class TissueMesh : MonoBehaviour
{
    private static readonly List<TissueMesh> active = new List<TissueMesh>();

    private Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        active.Add(this);

        if (PlayerManager.Instance != null)
        {
            foreach (var p in PlayerManager.Instance.allPlayers)
                if (p.isClone) IgnoreWith(p);
        }
    }

    void OnDestroy()
    {
        active.Remove(this);
    }

    void IgnoreWith(PlayerController clone)
    {
        Collider2D cloneCol = clone.GetComponent<Collider2D>();
        if (cloneCol != null && col != null)
            Physics2D.IgnoreCollision(cloneCol, col, true);
    }

    // 새 분열체가 등록될 때 PlayerManager가 호출 — 현재 존재하는 모든 그물망과 충돌 무시 설정
    public static void RegisterClone(PlayerController clone)
    {
        foreach (var mesh in active)
            mesh.IgnoreWith(clone);
    }
}
