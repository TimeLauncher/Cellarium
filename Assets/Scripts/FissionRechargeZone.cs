using System.Collections.Generic;
using UnityEngine;

// 분열게이지 회복 영역 (기획서 '그 외 필요 사항 메모': 영역 내에 있을 경우 분열 게이지가 빠르게 회복).
//
// 배치법
//   빈 게임오브젝트 + Collider2D(Is Trigger ✓) 에 붙이고 영역 크기로 키운다.
//
// ★ OnTriggerStay2D를 안 쓰는 이유
//   Stay는 물리 프레임에서만 호출된다. 그래서 "영역이 매 프레임 플레이어에게 배수를 알려주고
//   플레이어가 그걸 소비하는" 방식으로 만들면, 물리 프레임이 없는 화면 프레임에서 배수가
//   1로 돌아가 회복 속도가 들쭉날쭉해진다. 대신 켜져 있는 영역들을 목록으로 들고 있다가
//   PlayerController가 자기 위치로 직접 물어보게 한다 (호출 순서에 영향을 받지 않는다).
[RequireComponent(typeof(Collider2D))]
public class FissionRechargeZone : MonoBehaviour
{
    [Tooltip("이 영역 안에서 분열 게이지 회복 속도에 곱해지는 배수")]
    public float recoverMultiplier = 4f;

    [Tooltip("씬 뷰에서 영역을 표시할 색")]
    public Color gizmoColor = new Color(0.4f, 1f, 0.8f, 0.8f);

    [Header("게임 화면 표시")]
    // 기즈모는 Scene 뷰에서만 보인다. 플레이어가 "여기 들어오면 게이지가 빨리 찬다"를 알아야 하는
    // 정보이므로 실제 플레이 화면에도 보이게 기본으로 켜둔다. 진짜 이펙트가 나오면 끄면 된다.
    [Tooltip("게임 화면에도 영역을 반투명하게 표시한다 (이펙트 에셋 나오기 전 임시)")]
    public bool showArea = true;
    public Color areaColor = new Color(0.35f, 1f, 0.75f, 0.18f);
    [Tooltip("영역 위에 띄울 설명. 비우면 글씨를 안 만든다")]
    public string areaLabel = "분열게이지 회복 영역";
    public int areaSortingOrder = 30;

    static readonly List<FissionRechargeZone> activeZones = new List<FissionRechargeZone>();

    Collider2D area;

    void Awake()
    {
        area = GetComponent<Collider2D>();
        if (area != null && !area.isTrigger)
        {
            area.isTrigger = true;
            Debug.LogWarning($"[{name}] Collider2D를 Is Trigger로 바꿨습니다. 회복 영역은 트리거여야 합니다.", this);
        }

        if (showArea)
            ZoneVisualizer.ShowBox(gameObject, area, areaColor, areaLabel, areaSortingOrder);
    }

    void OnEnable()
    {
        if (!activeZones.Contains(this)) activeZones.Add(this);
    }

    void OnDisable()
    {
        activeZones.Remove(this);
    }

    // 해당 위치에 걸린 회복 영역 중 가장 센 배수를 돌려준다 (영역이 겹쳐 있어도 안전).
    // 어디에도 안 걸리면 1 = 평소 회복 속도.
    public static float MultiplierAt(Vector2 position)
    {
        float best = 1f;

        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            FissionRechargeZone z = activeZones[i];
            if (z == null) { activeZones.RemoveAt(i); continue; }
            if (z.area == null || !z.area.enabled) continue;

            if (z.area.OverlapPoint(position) && z.recoverMultiplier > best)
                best = z.recoverMultiplier;
        }

        return best;
    }

    void OnDrawGizmos()
    {
        Collider2D c = area != null ? area : GetComponent<Collider2D>();
        if (c == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
