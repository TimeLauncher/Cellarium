using UnityEngine;

// 파괴형 작동장치 (기획서 A12~A13 (1)) — 신경 다발 OBJ_NerveBundle / 신경 조직 OBJ_NerveTissue.
// 대시로 때려 체력을 0으로 만들면 부서지면서 '켜진 상태'가 되어, 버튼(ButtonSwitch)과 똑같이 연결된 장치를 작동시킨다.
//   문(Door)의 Required Buttons, 이동 발판(MovingPlatform)의 Activators, 혈류(BloodFlow)의 Reverse Activators,
//   낙하 혈전(FallingClot)의 Activators 칸에 이 오브젝트를 넣으면 된다.
// 한 번 부서지면 다시 생기지 않는다 (WorldCategory.Destructible, 씬을 다시 들어와도·죽어도 유지).
//
// 배치법
//   신경 다발: 벽과 벽, 바닥과 천장 사이를 잇는 긴 직사각형(1x4 등). BoxCollider2D를 그 크기로. Blocks Player 켬(길을 막는다)
//   신경 조직: 배경에 놓는 2x2 등. Blocks Player 끔 → 콜라이더가 트리거가 되어 지나다닐 수 있고 대시로는 맞는다
//   레이어는 아무거나 상관없다 (대시는 레이어와 상관없이 이 컴포넌트를 찾는다). 길을 막아야 하면 지형 레이어(Ground 등)에.
[RequireComponent(typeof(Collider2D))]
public class DestructibleActivator : DestructibleObject
{
    [Header("작동장치")]
    [Tooltip("켜면 PC가 통과하지 못하는 몸체(신경 다발). 끄면 콜라이더를 트리거로 바꿔 배경처럼 지나다닌다(신경 조직)")]
    public bool blocksPlayer = true;
    [Tooltip("부서진 뒤 보여줄 이미지. 비우면 부서지면 아예 사라진다")]
    public Sprite destroyedSprite;

    protected override void Awake()
    {
        base.Awake();
        if (!blocksPlayer)
            foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
    }

    // 버튼처럼 한 번 켜지면 계속 켜져 있어야 하므로 오브젝트를 지우지 않고 겉모습·충돌만 끈다
    protected override void OnDestroyed()
    {
        IsActive = true;
        ApplyBrokenLook();
    }

    protected override void OnAlreadyDestroyed()
    {
        IsActive = true;
        ApplyBrokenLook();
    }

    void ApplyBrokenLook()
    {
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;

        if (spr != null && destroyedSprite != null)
        {
            spr.sprite = destroyedSprite;
            return;
        }
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    // 연결 장치는 isActiveAndEnabled도 보므로 컴포넌트는 켜둔 채로 둔다
    public override void SetActive(bool active) { }
}
