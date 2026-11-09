using UnityEngine;

public class NpcDialogueDisappear : MonoBehaviour
{
    [Header("사라지는 조건")]
    [Tooltip("이 대사가 지나간 뒤 오브젝트가 사라집니다. 예: 6 = 6번째 대사 이후")]
    [Min(1)]
    [SerializeField] private int disappearAfterLine = 6;

    [Header("어떤 대화를 볼지")]
    // 이 필드가 없으면 같은 씬의 '아무 대화나' 줄 번호만 보고 발동한다.
    // 실제로 A06에서 병사 퇴장 대사(2줄)가 아니라 T세포 대사(5줄) 도중에 병사가 사라졌다.
    [Tooltip("이 대화가 재생될 때만 발동합니다.\n" +
             "비우면 같은 오브젝트의 NpcInteractable 대사를 자동으로 씁니다.\n" +
             "이벤트(EventTriggerZone)로 재생되는 대사는 NpcInteractable이 없으므로 여기에 직접 넣어야 합니다.")]
    [SerializeField] private DialogueData targetDialogue;

    [Header("저장")]
    [Tooltip("이 NPC를 구분하는 고유 ID. NPC마다 다르게 지정하세요.")]
    [SerializeField] private string persistentId = "NPC_Disappear_01";

    bool disappeared;
    bool watching;      // 감시 대상 대화가 재생 중인가
    bool passedLine;    // 기준 대사까지 표시됐는가

    NpcInteractable ownerNpc;
    bool warnedNoTarget;

    string SaveId => WorldState.MakeId(this, persistentId);

    void Awake()
    {
        ownerNpc = GetComponent<NpcInteractable>();

        // 이전에 이미 사라진 NPC라면
        // 씬에 다시 들어와도 바로 비활성화
        if (WorldState.Has(WorldCategory.Event, SaveId))
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (disappeared)
            return;

        if (DialogueManager.IsPlaying && IsWatchedDialogue())
        {
            watching = true;

            // DialogueManager는 0부터 시작.
            // disappearAfterLine = 6이면 6번째 대사(index 5)가 뜬 시점에 passedLine이 서고,
            // 7번째 대사(index 6)로 넘어가는 순간 사라진다.
            int index = DialogueManager.CurrentLineIndex;

            if (index >= disappearAfterLine)
            {
                Disappear();
                return;
            }

            if (index >= disappearAfterLine - 1)
                passedLine = true;

            return;
        }

        // 감시하던 대화가 '끝난' 경우도 사라져야 한다.
        // 기준 대사가 마지막 줄이면(예: 2줄짜리 대사에 disappearAfterLine = 2)
        // index가 2까지 올라갈 일이 없어서 위 조건만으로는 영영 발동하지 않는다.
        if (watching)
        {
            watching = false;
            if (passedLine)
                Disappear();
        }
    }

    // 지금 재생 중인 대화가 이 NPC가 기다리던 그 대화인가
    bool IsWatchedDialogue()
    {
        DialogueData now = DialogueManager.Current;
        if (now == null)
            return false;

        if (targetDialogue != null)
            return now == targetDialogue;

        // 인스펙터를 비워 뒀으면 같은 오브젝트에 붙은 NPC의 대사를 쓴다
        if (ownerNpc != null)
            return now == ownerNpc.dialogue || now == ownerNpc.repeatDialogue;

        if (!warnedNoTarget)
        {
            warnedNoTarget = true;
            Debug.LogWarning("[" + name + "] NpcDialogueDisappear에 Target Dialogue가 비어 있고 NpcInteractable도 없습니다. " +
                             "아무 대화에나 반응하니 인스펙터에서 대화를 지정하세요.", this);
        }

        return true;    // 지정이 없으면 예전 동작(아무 대화나) 유지
    }

    void Disappear()
    {
        disappeared = true;

        // 사라졌다는 사실 저장
        WorldState.Record(WorldCategory.Event, SaveId);

        gameObject.SetActive(false);
    }
}
