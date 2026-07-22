using System.Collections;
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
        StartCoroutine(DropRoutine(playerCol));
    }

    private IEnumerator DropRoutine(Collider2D playerCol)
    {
        Physics2D.IgnoreCollision(playerCol, platformCol, true);
        yield return new WaitForSeconds(dropIgnoreDuration);
        if (playerCol != null && platformCol != null)
            Physics2D.IgnoreCollision(playerCol, platformCol, false);
    }
}
