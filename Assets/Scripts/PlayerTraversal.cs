using System.Collections.Generic;
using UnityEngine;

// 지형 기믹이 PC의 이동/점프/대시를 일시적으로 깎는 배율 계층.
// 배율은 거미줄(TL_SpiderWeb)이 첫 사용처이고, 후속 감속 기믹도 Apply로 같은 창구를 쓴다.
// 혈류(OBJ_BloodFlow)는 배율이 아니라 속도를 통째로 정하므로 아래 TouchFlow/UpdateFlow로 따로 관리한다.
// 효과는 출처(source)별로 따로 들고 있다가, 겹치면 가장 강한(가장 작은) 배율 하나만 적용한다 —
// 거미줄 두 장이 겹친다고 속도가 0.5 × 0.5로 곱해지면 밸런싱이 불가능해진다.
public class PlayerTraversal
{
    struct Effect
    {
        public float expireAt;
        public float move, jump, dash;
    }

    readonly Dictionary<Object, Effect> effects = new Dictionary<Object, Effect>();
    readonly List<Object> expired = new List<Object>();

    public float MoveMultiplier { get; private set; } = 1f;
    public float JumpMultiplier { get; private set; } = 1f;
    public float DashMultiplier { get; private set; } = 1f;

    // 상태 '거미줄' — 애니메이션/이펙트에서 참고용
    public bool IsWebbed { get; private set; }

    // 거미줄 접촉. 닿아 있는 동안 매 FixedUpdate 갱신되므로 상태가 상시 유지되고,
    // 벗어나면 duration 뒤에 풀린다.
    public void ApplyWeb(SpiderWeb source, float duration, float move, float jump, float dash)
    {
        Apply(source, duration, move, jump, dash);
    }

    public void Apply(Object source, float duration, float move, float jump, float dash)
    {
        if (source == null) return;
        effects[source] = new Effect
        {
            expireAt = Time.time + Mathf.Max(0f, duration),
            move = Mathf.Clamp01(move),
            jump = Mathf.Clamp01(jump),
            dash = Mathf.Clamp01(dash),
        };
        Recalculate();
    }

    public void Clear()
    {
        effects.Clear();
        flowContacts.Clear();
        CurrentFlow = null;
        Recalculate();
    }

    // PlayerController.Update에서 매 프레임 호출 — 만료된 효과 정리
    public void Tick()
    {
        if (effects.Count == 0) return;
        expired.Clear();
        foreach (var pair in effects)
            if (pair.Key == null || pair.Value.expireAt <= Time.time) expired.Add(pair.Key);
        if (expired.Count == 0) return;
        foreach (var key in expired) effects.Remove(key);
        Recalculate();
    }

    // ── 혈류 ──────────────────────────────────────────────
    // 혈류 구간들이 매 FixedUpdate 접촉을 알려오고, 그중 가장 나중에 들어간 구간을 따른다.
    struct FlowContact
    {
        public float enteredAt;
        public float lastSeen;
    }

    readonly Dictionary<BloodFlow, FlowContact> flowContacts = new Dictionary<BloodFlow, FlowContact>();
    readonly List<BloodFlow> leftFlows = new List<BloodFlow>();
    float flowImmuneUntil = -1f;

    public BloodFlow CurrentFlow { get; private set; }

    public void TouchFlow(BloodFlow flow)
    {
        if (flow == null || Time.time < flowImmuneUntil) return;
        flowContacts[flow] = new FlowContact
        {
            enteredAt = flowContacts.TryGetValue(flow, out var old) ? old.enteredAt : Time.fixedTime,
            lastSeen = Time.fixedTime,
        };
    }

    // PlayerController.FixedUpdate에서 호출. 이번 스텝에 접촉이 끊긴 구간을 정리하고 현재 구간을 고른다.
    public BloodFlow UpdateFlow()
    {
        leftFlows.Clear();
        BloodFlow newest = null;
        float newestAt = float.MinValue;
        foreach (var pair in flowContacts)
        {
            // 실행 순서가 어긋나도 한 스텝은 봐준다
            bool present = pair.Key != null && pair.Key.isActiveAndEnabled &&
                           Time.fixedTime - pair.Value.lastSeen <= Time.fixedDeltaTime * 1.5f;
            if (!present) { leftFlows.Add(pair.Key); continue; }
            if (pair.Value.enteredAt >= newestAt) { newestAt = pair.Value.enteredAt; newest = pair.Key; }
        }
        foreach (var flow in leftFlows) flowContacts.Remove(flow);
        CurrentFlow = newest;
        return newest;
    }

    // 점프 이탈. immunity 동안 어떤 혈류에도 다시 붙지 않는다.
    public void ExitFlow(float immunity)
    {
        flowContacts.Clear();
        CurrentFlow = null;
        flowImmuneUntil = Time.time + Mathf.Max(0f, immunity);
    }

    void Recalculate()
    {
        float move = 1f, jump = 1f, dash = 1f;
        bool webbed = false;
        foreach (var pair in effects)
        {
            move = Mathf.Min(move, pair.Value.move);
            jump = Mathf.Min(jump, pair.Value.jump);
            dash = Mathf.Min(dash, pair.Value.dash);
            if (pair.Key is SpiderWeb) webbed = true;
        }
        MoveMultiplier = move;
        JumpMultiplier = jump;
        DashMultiplier = dash;
        IsWebbed = webbed;
    }
}
