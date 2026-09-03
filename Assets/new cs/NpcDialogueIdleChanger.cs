using UnityEngine;

public class NpcDialogueIdleChanger : MonoBehaviour
{
    [Header("애니메이터")]
    [SerializeField] private Animator animator;

    [Header("변경 조건")]
    [Tooltip("몇 번째 대사부터 Idle을 변경할지. 1 = 첫 번째, 2 = 두 번째")]
    [Min(1)]
    [SerializeField] private int changeAtLine = 2;

    [Header("어떤 대화를 볼지")]
    // 줄 번호만 보면 같은 씬의 다른 대화가 그 줄 수를 넘길 때도 Idle이 바뀐다.
    [Tooltip("이 대화가 재생될 때만 발동합니다.\n" +
             "비우면 같은 오브젝트의 NpcInteractable 대사를 자동으로 씁니다.")]
    [SerializeField] private DialogueData targetDialogue;

    [Header("Animator 설정")]
    [Tooltip("변경된 Idle로 전환할 Bool 파라미터 이름")]
    [SerializeField] private string changedIdleBool = "ChangedIdle";

    private bool changed = false;

    private NpcInteractable ownerNpc;
    private bool warnedNoTarget;

    void Awake()
    {
        ownerNpc = GetComponent<NpcInteractable>();
    }

    void Update()
    {
        // 이미 변경했다면 이후에는 아무것도 하지 않음
        if (changed)
            return;

        // 현재 대화 중이 아니면 확인하지 않음
        if (!DialogueManager.IsPlaying)
            return;

        // 내가 기다리던 그 대화가 아니면 무시
        if (!IsWatchedDialogue())
            return;

        // DialogueManager는 0부터 시작하므로
        // Inspector의 2번째 대사 = Index 1
        int targetIndex = changeAtLine - 1;

        if (DialogueManager.CurrentLineIndex >= targetIndex)
        {
            ChangeIdle();
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
            Debug.LogWarning("[" + name + "] NpcDialogueIdleChanger에 Target Dialogue가 비어 있고 NpcInteractable도 없습니다. " +
                             "아무 대화에나 반응하니 인스펙터에서 대화를 지정하세요.", this);
        }

        return true;    // 지정이 없으면 예전 동작(아무 대화나) 유지
    }

    void ChangeIdle()
    {
        changed = true;

        if (animator != null)
            animator.SetBool(changedIdleBool, true);
    }
}
