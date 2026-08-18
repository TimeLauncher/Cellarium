using UnityEngine;

// 대화 한 줄. 기획서 (1) ②: "직업, 이름, 대화를 각각 수정할 수 있도록 구성"
[System.Serializable]
public class DialogueLine
{
    [Tooltip("이 줄을 말하는 사람의 직업. 비우면 DialogueData의 기본 직업을 쓴다")]
    public string job = "";

    [Tooltip("이 줄을 말하는 사람의 이름. 비우면 DialogueData의 기본 이름을 쓴다")]
    public string speaker = "";

    [TextArea(2, 6)]
    [Tooltip("한 글자씩 순차적으로 표시될 본문")]
    public string text = "";

    [Tooltip("이 줄만 타이핑 속도를 다르게 하고 싶을 때. 0이면 DialogueManager의 기본 속도를 쓴다")]
    public float charIntervalOverride = 0f;
}

// NPC 하나가 가진 대화 뭉치.
//
// 만드는 법: Project 창 우클릭 → Create → Cellarium → Dialogue Data
// (에셋으로 빼두면 기획자가 유니티에서 직접 대사를 고칠 수 있고, 여러 NPC가 같은 대사를 공유할 수도 있다)
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Cellarium/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("기본 화자 정보")]
    [Tooltip("줄마다 따로 지정하지 않았을 때 쓰는 직업 (예: 백혈구 병사)")]
    public string defaultJob = "";

    [Tooltip("줄마다 따로 지정하지 않았을 때 쓰는 이름")]
    public string defaultSpeaker = "";

    [Header("대사")]
    public DialogueLine[] lines;

    // 기획서 ④ "다음 대화는 추가로 표시할 대화가 존재할 경우에만 진입"
    public int LineCount => lines != null ? lines.Length : 0;

    public bool IsEmpty => LineCount == 0;

    // 줄에 값이 비어 있으면 기본값으로 메워서 돌려준다
    public string JobOf(int index)
    {
        if (lines == null || index < 0 || index >= lines.Length) return defaultJob;
        return string.IsNullOrEmpty(lines[index].job) ? defaultJob : lines[index].job;
    }

    public string SpeakerOf(int index)
    {
        if (lines == null || index < 0 || index >= lines.Length) return defaultSpeaker;
        return string.IsNullOrEmpty(lines[index].speaker) ? defaultSpeaker : lines[index].speaker;
    }
}
