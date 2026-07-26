using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 관통 타일: 아래→위 통과는 PlatformEffector2D(인스펙터에서 컴포넌트 추가, useOneWay 체크)로 처리.
// S+Space로 아래로 내려가는 것만 이 스크립트가 담당 - 짧은 시간 해당 플레이어와의 충돌만 무시
[RequireComponent(typeof(Collider2D))]
public class OneWayPlatformTile : MonoBehaviour
{
    public float dropIgnoreDuration = 0.3f;

    private Collider2D platformCol;

    void Awake()
    {
        platformCol = GetComponent<Collider2D>();
    }

    public void DropThrough(Collider2D playerCol)
    {
        DropThrough(new List<Collider2D> { playerCol });
    }

    // 플레이어에 몸통 콜라이더가 여러 개일 수 있어(씬마다 Box+Circle 조합이 다름) 전부 무시해야 실제로 내려간다.
    // 하나라도 남아 있으면 그 콜라이더가 발판에 걸려 하강이 씹힌다.
    public void DropThrough(List<Collider2D> playerCols)
    {
        if (playerCols == null || playerCols.Count == 0) return;
        StartCoroutine(DropRoutine(playerCols));
    }

    private IEnumerator DropRoutine(List<Collider2D> playerCols)
    {
        foreach (Collider2D pc in playerCols)
            if (pc != null && platformCol != null) Physics2D.IgnoreCollision(pc, platformCol, true);

        yield return new WaitForSeconds(dropIgnoreDuration);

        foreach (Collider2D pc in playerCols)
            if (pc != null && platformCol != null) Physics2D.IgnoreCollision(pc, platformCol, false);
    }
}
