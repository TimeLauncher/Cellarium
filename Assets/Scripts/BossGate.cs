using UnityEngine;
using UnityEngine.Events;

// 보스 게이트 OBJ_BossGate (기획서 A12~A13 (2)).
// A09에 놓이는, 보스 방(A14)으로 가는 문. 다른 씬(A11·A13)의 심장 결절(HeartNode)이 전부 활성화되면 사라진다.
// 버튼 문(Door)과 비슷하지만, 같은 씬의 장치가 아니라 결절의 '활성화 기록'(WorldState)을 읽는다.
//
// 기획서대로 열리는 연출은 없다 — 조건이 차면 그냥 사라진다(Open Mode = Hide).
// 나중에 '혈류가 역류해 게이트가 부서지는' 이벤트 연출을 붙일 때는 Open Mode를 Event Only로 바꾸고
// On Ready에 연출을 연결하면 된다 (게이트를 없애는 건 그 연출이 맡는다).
//
// 배경 변화(결절 1개 활성 시 A09 좌측 배경에 혈류 등)는 HeartNodeStateObject로 따로 건다.
public class BossGate : MonoBehaviour
{
    public enum OpenMode { Hide, EventOnly }

    [Header("열리는 조건")]
    [Tooltip("이 이름의 결절이 전부 활성화되면 열린다. 결절의 Node Key와 똑같이 적을 것")]
    public string[] requiredNodes = { "LeftHeartNode", "RightHeartNode" };

    [Header("열림")]
    [Tooltip("Hide: 조건이 차면 게이트 오브젝트를 끈다. Event Only: 끄지 않고 On Ready만 호출한다 (연출이 직접 처리)")]
    public OpenMode openMode = OpenMode.Hide;
    [Tooltip("조건이 처음 찼을 때 한 번 호출 (씬에 들어왔을 때 이미 차 있었으면 Invoke On Load가 켜져 있을 때만)")]
    public UnityEvent onReady;
    [Tooltip("씬에 들어왔을 때 이미 조건이 차 있으면 On Ready도 호출할지. 끄면 조용히 열린 상태로만 시작한다")]
    public bool invokeOnLoad = false;

    bool done;

    public bool IsReady => requiredNodes != null && requiredNodes.Length > 0
                           && HeartNode.CountActivated(requiredNodes) >= requiredNodes.Length;

    void Start()
    {
        if (!IsReady) return;
        done = true;
        if (invokeOnLoad) onReady?.Invoke();
        if (openMode == OpenMode.Hide) gameObject.SetActive(false);
    }

    // 같은 씬 안에서 조건이 찰 수도 있으니(테스트 등) 계속 본다. 기록 조회라 가볍다.
    void Update()
    {
        if (done || !IsReady) return;
        done = true;
        Debug.Log($"[BossGate] {name} 개방 조건 충족");
        onReady?.Invoke();
        if (openMode == OpenMode.Hide) gameObject.SetActive(false);
    }
}
