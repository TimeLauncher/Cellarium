using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 심장 결절 OBJ_HT_LeftHeartNode / OBJ_HT_RightHeartNode (기획서 A12~A13 (4)).
//   ① 대시로 때려 체력을 0으로 만들면 코어(Core)가 드러난다
//   ② 드러난 코어를 '섭취'하면 결절이 활성화된다 → 코어는 사라지고, 결절은 활성화된 이미지로 바뀐 채 남는다
//   ③ 활성화는 영구 — 씬을 바꿔도, 죽어도 되돌아가지 않는다
//   ④ A09의 보스 게이트(BossGate)가 다른 씬에서 활성화된 결절 수를 보고 열린다
//
// ★ 다른 씬에서 상태를 읽어야 하므로 기록 키는 씬 이름이 붙지 않는 Node Key 그대로 쓴다.
//   왼쪽 결절은 "LeftHeartNode", 오른쪽은 "RightHeartNode"처럼 결절마다 다르게, 보스 게이트의 Required Nodes와 똑같이 적을 것.
//
// 배치법
//   결절 본체: 5x5 스프라이트 + Collider2D(대시로 맞을 크기) + 이 컴포넌트
//   코어: 자식 오브젝트(2x2 정도 스프라이트) — Core 칸에 연결. 처음엔 숨겨지고, 체력 0일 때 나타난다.
//         Collider2D가 없으면 자동으로 원형을 붙이고, 섭취 대상이 되도록 Monster 레이어로 옮긴다.
//   활성화 연출(추후 이벤트 기능으로 추가): On Activated 칸에 연결해 두면 섭취 순간 호출된다.
public class HeartNode : DestructibleObject
{
    [Header("심장 결절")]
    [Tooltip("다른 씬(보스 게이트 등)이 이 결절을 찾는 이름. 결절마다 다르게 (예: LeftHeartNode / RightHeartNode)")]
    public string nodeKey = "LeftHeartNode";
    [Tooltip("체력 0이 되면 드러나는 코어 (자식 오브젝트)")]
    public GameObject core;
    [Tooltip("체력을 다 깎았지만 아직 코어를 안 먹은 상태의 이미지 (비우면 그대로)")]
    public Sprite brokenSprite;
    [Tooltip("활성화된 뒤 계속 보여줄 이미지")]
    public Sprite activatedSprite;
    [Tooltip("코어를 섭취했을 때 회복되는 체력 / 분열 게이지 (0이면 회복 없음)")]
    public float coreHealHp = 0f;
    public float coreHealGauge = 0f;

    [Header("연출 연결 (추후)")]
    [Tooltip("코어를 섭취해 활성화되는 순간 호출 (결절 활성화 연출, A09 배경 연출 등)")]
    public UnityEvent onActivated;

    public bool IsActivated { get; private set; }

    const string KeyPrefix = "heart:";

    // ── 다른 오브젝트가 쓰는 조회 ───────────────────────────────

    public static bool IsNodeActivated(string key)
        => !string.IsNullOrEmpty(key) && WorldState.Has(WorldCategory.Event, KeyPrefix + key);

    public static int CountActivated(IEnumerable<string> keys)
    {
        int n = 0;
        if (keys == null) return 0;
        foreach (var k in keys) if (IsNodeActivated(k)) n++;
        return n;
    }

    // ── 동작 ───────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        if (core != null)
        {
            var link = core.GetComponent<HeartNodeCore>();
            if (link == null) link = core.AddComponent<HeartNodeCore>();
            link.owner = this;

            if (core.GetComponent<Collider2D>() == null) core.AddComponent<CircleCollider2D>();
            foreach (var c in core.GetComponents<Collider2D>()) c.isTrigger = true;
            // PC는 섭취 대상을 Monster 레이어에서 찾는다 (PlayerController.monsterMask)
            int monster = LayerMask.NameToLayer("Monster");
            if (monster >= 0) core.layer = monster;

            core.SetActive(false);
        }
    }

    protected override void Start()
    {
        // 활성화 기록이 먼저 — 활성화된 결절은 체력·코어 없이 활성 이미지로 시작
        if (IsNodeActivated(nodeKey))
        {
            ApplyActivatedLook();
            return;
        }
        base.Start(); // 체력만 깎아 둔 상태면 OnAlreadyDestroyed → 코어가 드러난 채로
    }

    // 체력 0 — 사라지지 않고 코어가 드러난다
    protected override void OnDestroyed() => RevealCore();
    protected override void OnAlreadyDestroyed() => RevealCore();

    void RevealCore()
    {
        if (spr != null && brokenSprite != null) spr.sprite = brokenSprite;
        if (core != null) core.SetActive(true);
    }

    // 코어를 섭취했을 때 (HeartNodeCore가 호출)
    internal void OnCoreConsumed(PlayerController consumer)
    {
        if (IsActivated) return;
        if (consumer != null && (coreHealHp > 0f || coreHealGauge > 0f))
            consumer.RestoreFromConsume(coreHealHp, coreHealGauge);

        WorldState.Record(WorldCategory.Event, KeyPrefix + nodeKey);
        ApplyActivatedLook();
        Debug.Log($"[HeartNode] {nodeKey} 활성화");
        onActivated?.Invoke();
    }

    void ApplyActivatedLook()
    {
        IsActivated = true;
        IsActive = true; // 같은 씬의 장치를 직접 켤 수도 있다 (WorldActivator)
        if (spr != null && activatedSprite != null) spr.sprite = activatedSprite;
        IsDestroyed = true; // 대시가 더 이상 맞지(튕겨나오지) 않는다
        if (core != null) core.SetActive(false);
    }

    public override void TakeDamage(float amount, Vector2 hitDirection = default)
    {
        if (IsActivated) return;
        base.TakeDamage(amount, hitDirection);
    }

    public override void SetActive(bool active) { }
}
