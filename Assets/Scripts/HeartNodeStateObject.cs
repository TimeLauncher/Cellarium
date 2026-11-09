using UnityEngine;

// 심장 결절 활성화 상태에 따라 켜지고 꺼지는 배경 오브젝트 (기획서 A12~A13 (2)·(4) 참고 메모).
// 예) A09 좌측 배경의 '혈류가 쏟아지는' 환경 오브젝트 → Watch Nodes에 LeftHeartNode, Required Count 1,
//     Show When Met에 그 오브젝트들. 결절이 활성화된 뒤 A09에 들어오면 켜진 채로 보인다.
// 연출(혈류가 흐르기 시작하는 장면)은 이벤트 기능으로 따로 만들고, 이건 '연출이 끝난 뒤의 상태'를 맞춰 두는 용도.
//
// 이 컴포넌트는 꺼지지 않는 오브젝트(빈 부모 등)에 붙이고, 켜고 끌 대상은 아래 목록에 넣을 것.
public class HeartNodeStateObject : MonoBehaviour
{
    [Tooltip("볼 결절 이름들 (결절의 Node Key)")]
    public string[] watchNodes = { "LeftHeartNode" };
    [Tooltip("위 결절 중 몇 개 이상 활성화돼야 '충족'인지. 0이면 전부")]
    [Min(0)] public int requiredCount = 0;

    [Tooltip("충족되면 켜고, 아니면 끈다")]
    public GameObject[] showWhenMet;
    [Tooltip("충족되면 끄고, 아니면 켠다")]
    public GameObject[] hideWhenMet;

    bool? last;

    public bool IsMet
    {
        get
        {
            if (watchNodes == null || watchNodes.Length == 0) return false;
            int need = requiredCount > 0 ? Mathf.Min(requiredCount, watchNodes.Length) : watchNodes.Length;
            return HeartNode.CountActivated(watchNodes) >= need;
        }
    }

    void Start() => Apply(true);
    void Update() => Apply(false);

    void Apply(bool force)
    {
        bool met = IsMet;
        if (!force && last == met) return;
        last = met;
        if (showWhenMet != null) foreach (var g in showWhenMet) if (g != null && g != gameObject) g.SetActive(met);
        if (hideWhenMet != null) foreach (var g in hideWhenMet) if (g != null && g != gameObject) g.SetActive(!met);
    }
}
