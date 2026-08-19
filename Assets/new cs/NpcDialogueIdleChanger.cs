using UnityEngine;

public class NpcDialogueIdleChanger : MonoBehaviour
{
    [Header("애니메이터")]
    [SerializeField] private Animator animator;

    [Header("변경 조건")]
    [Tooltip("몇 번째 대사부터 Idle을 변경할지. 1 = 첫 번째, 2 = 두 번째")]
    [Min(1)]
    [SerializeField] private int changeAtLine = 2;

    [Header("Animator 설정")]
    [Tooltip("변경된 Idle로 전환할 Bool 파라미터 이름")]
    [SerializeField] private string changedIdleBool = "ChangedIdle";

    private bool changed = false;

    void Update()
    {
        // 이미 변경했다면 이후에는 아무것도 하지 않음
        if (changed)
            return;

        // 현재 대화 중이 아니면 확인하지 않음
        if (!DialogueManager.IsPlaying)
            return;

        // DialogueManager는 0부터 시작하므로
        // Inspector의 2번째 대사 = Index 1
        int targetIndex = changeAtLine - 1;

        if (DialogueManager.CurrentLineIndex >= targetIndex)
        {
            ChangeIdle();
        }
    }

    void ChangeIdle()
    {
        changed = true;

        if (animator != null)
            animator.SetBool(changedIdleBool, true);
    }
}