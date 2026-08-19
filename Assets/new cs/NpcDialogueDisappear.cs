using UnityEngine;

public class NpcDialogueDisappear : MonoBehaviour
{
    [Header("사라지는 조건")]
    [Tooltip("이 대사가 지나간 뒤 오브젝트가 사라집니다. 예: 6 = 6번째 대사 이후")]
    [Min(1)]
    [SerializeField] private int disappearAfterLine = 6;

    [Header("저장")]
    [Tooltip("이 NPC를 구분하는 고유 ID. NPC마다 다르게 지정하세요.")]
    [SerializeField] private string persistentId = "NPC_Disappear_01";

    bool disappeared;

    string SaveId => WorldState.MakeId(this, persistentId);

    void Awake()
    {
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

        if (!DialogueManager.IsPlaying)
            return;

        // DialogueManager는 0부터 시작
        // disappearAfterLine = 6이면
        // 6번째 대사(index 5)를 넘기고
        // 7번째 대사(index 6)가 되는 순간 실행
        if (DialogueManager.CurrentLineIndex >= disappearAfterLine)
        {
            Disappear();
        }
    }

    void Disappear()
    {
        disappeared = true;

        // 사라졌다는 사실 저장
        WorldState.Record(WorldCategory.Event, SaveId);

        gameObject.SetActive(false);
    }
}