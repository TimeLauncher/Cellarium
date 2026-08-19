using UnityEngine;

// NPC 상호작용 (기획서 (1) '기능 추가: NPC 상호작용').
//
// 기획서: "'대화' 이미지 표시 기능 및 상호작용 기능은 세이브 포인트와 동일하게 작동"
//   → SavePoint의 구조(트리거 범위 + WorldTooltip 안내 이미지 + E키)를 그대로 따른다.
//
// 배치법
//   1) NPC 오브젝트에 이 스크립트를 붙인다 (Collider2D가 자동으로 붙고 트리거로 바뀐다).
//   2) 콜라이더 크기를 'NPC 크기 주변 1타일 여유' 만큼 키운다 (기획서 상호작용 범위).
//   3) Dialogue 필드에 DialogueData 에셋을 연결한다
//      (Project 창 우클릭 → Create → Cellarium → Dialogue Data).
//   4) Talk Hint Sprite 에 '말 걸기' 이미지를 넣으면 머리 위에 자동으로 뜬다. 비워도 동작은 한다.
[RequireComponent(typeof(Collider2D))]
public class NpcInteractable : MonoBehaviour
{
    [Header("대화 내용")]
    public DialogueData dialogue;

    [Tooltip("한 번 대화를 끝낸 뒤부터 재생할 대사. 비우면 위 Dialogue를 계속 반복한다")]
    public DialogueData repeatDialogue;

    [Tooltip("끄면 대화를 한 번 끝낸 뒤로는 다시 말을 걸 수 없다")]
    public bool repeatable = true;

    [Header("'대화' 표시 이미지")]
    [Tooltip("접근하면 머리 위에 뜨는 '말 걸기' 이미지. 비우면 안내를 만들지 않는다")]
    public Sprite talkHintSprite;
    [Tooltip("이 거리 안에 들어오면 안내가 페이드 인 된다")]
    public float hintShowRange = 3f;
    public Vector3 hintOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("안내 이미지 크기 (1이면 원본 크기)")]
    public float hintScale = 0.5f;

    [Header("입력")]
    public KeyCode interactKey = KeyCode.E;

    public bool HasTalked { get; private set; }

    bool playerInRange;
    WorldTooltip hint;

    // ★ 대화가 끝난 그 프레임에 같은 E키로 곧바로 다시 말을 거는 것을 막는다.
    //   DialogueManager.Update가 먼저 돌아 대화를 끝내면, 같은 프레임에 이 Update가
    //   GetKeyDown(E)를 그대로 다시 읽어 대화가 무한히 재시작된다.
    int lastEndedFrame = -1;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        BuildHint();
    }

    // 안내 이미지를 자식으로 자동 생성 (SavePoint.BuildHint와 같은 방식 —
    // NPC마다 손으로 오브젝트를 만들지 않아도 되게)
    void BuildHint()
    {
        if (talkHintSprite == null) return;

        GameObject go = new GameObject("TalkHint");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * hintScale;

        SpriteRenderer hintSpr = go.AddComponent<SpriteRenderer>();
        hintSpr.sprite = talkHintSprite;

        hint = go.AddComponent<WorldTooltip>();
        hint.showMode = WorldTooltip.ShowMode.PlayerNear;
        hint.showRange = hintShowRange;
        hint.followTarget = transform;
        hint.offset = hintOffset;
    }

    void Update()
    {
        if (!playerInRange) return;
        if (DialogueManager.IsPlaying) return;   // 대화 중엔 재진입 금지
        if (Time.frameCount == lastEndedFrame) return; // 종료 프레임의 E키가 재시작으로 먹히는 것 방지
        if (!CanTalk) return;
        if (!Input.GetKeyDown(interactKey)) return;

        StartTalk();
    }

    bool CanTalk => repeatable || !HasTalked;

    void StartTalk()
    {
        DialogueData data = (HasTalked && repeatDialogue != null) ? repeatDialogue : dialogue;

        if (data == null || data.IsEmpty)
        {
            Debug.LogWarning($"[NPC] {name}: Dialogue 필드가 비어 있어 대화를 시작할 수 없습니다.");
            return;
        }

        // 대화 중엔 '말 걸기' 안내를 감춰서 대화창과 겹치지 않게 한다
        SetHintVisible(false);

        DialogueManager.EnsureInstance().Play(data, OnTalkFinished);
    }

    void OnTalkFinished()
    {
        HasTalked = true;
        lastEndedFrame = Time.frameCount;

        // 다시 말을 걸 수 있으면 안내를 되살린다 (범위 안에 있을 때만 실제로 보인다)
        SetHintVisible(CanTalk);
    }

    void SetHintVisible(bool visible)
    {
        if (hint != null && hint.gameObject.activeSelf != visible)
            hint.gameObject.SetActive(visible);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) playerInRange = false;
    }

    // 상호작용 범위를 씬 뷰에서 확인 (콜라이더를 '1타일 여유' 크기로 맞출 때 필요)
    void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Bounds b = col.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
