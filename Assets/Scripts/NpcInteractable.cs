using System.Collections;
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

    [Header("구조 이벤트 (A02 적혈구 주민 등)")]
    // 기획서 (2)의 '적혈구 주민 구조 이벤트' — 울음소리(ProximitySound)로 동선을 유도해
    // 찾아가서 말을 걸면 구조 완료. 감사 인사를 남기고 사라지면서 셀을 떨군다.
    [Tooltip("켜면 대화가 끝난 뒤 이 NPC가 사라진다(구조 완료). 씬을 다시 로드해도 기억한다")]
    public bool leaveAfterTalk = false;

    [Tooltip("구조 보상으로 떨어뜨릴 셀 양. 0이면 보상 없음")]
    public int rewardCell = 100;

    [Tooltip("보상 셀의 모양. 비우면 기본 셀(Resources/Effects/CellDrop)이 나온다")]
    public GameObject rewardCellPrefab;

    [Tooltip("대화가 끝나고 이만큼 뒤에 떠나기 시작한다")]
    public float leaveDelay = 0.3f;

    [Tooltip("사라지는 데 걸리는 시간 (0이면 즉시 사라진다). 아래 Blackout On Leave를 켜면 안 쓰인다")]
    public float leaveFadeDuration = 0.8f;

    [Tooltip("켜면 대화가 끝난 뒤 화면이 잠깐 암전되고, 어두운 사이에 사라진다.\n" +
             "다시 밝아지면 그 자리엔 보상 셀만 남아 있다.\n" +
             "(ScreenFadeManager가 없으면 위의 스프라이트 페이드로 대신한다)")]
    public bool blackoutOnLeave = false;

    [Tooltip("화면이 완전히 검어진 뒤 이만큼 더 기다렸다가 다시 밝아진다")]
    public float blackoutHold = 0.4f;

    [Tooltip("암전으로 사라질 때 보상 셀이 이 시간 동안은 획득되지 않는다.\n" +
             "플레이어가 바로 앞에 서 있어서, 짧게 주면 화면이 밝아지기도 전에 먹혀버린다.\n" +
             "(Blackout Hold + 화면이 밝아지는 시간)보다 넉넉하게 줄 것")]
    public float rewardPickupDelay = 1.2f;

    [Tooltip("구조 여부를 기억할 때 쓰는 식별자. 비우면 계층 경로로 자동 생성된다")]
    public string persistentId = "";

    public bool HasTalked { get; private set; }

    bool playerInRange;
    bool leaving;
    WorldTooltip hint;

    string RescueId => WorldState.MakeId(this, persistentId);

    // ★ 대화가 끝난 그 프레임에 같은 E키로 곧바로 다시 말을 거는 것을 막는다.
    //   DialogueManager.Update가 먼저 돌아 대화를 끝내면, 같은 프레임에 이 Update가
    //   GetKeyDown(E)를 그대로 다시 읽어 대화가 무한히 재시작된다.
    int lastEndedFrame = -1;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // 이미 구조한 주민이면 리로드된 씬에 다시 나타나지 않는다 (울음소리도 같이 멎는다)
        if (leaveAfterTalk && WorldState.Has(WorldCategory.Event, RescueId))
        {
            gameObject.SetActive(false);
            return;
        }

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
        if (leaving) return;
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

        // 구조 대상이면 대화가 끝나는 순간 구조 완료 — 보상을 남기고 떠난다
        if (leaveAfterTalk && !leaving)
        {
            leaving = true;
            SetHintVisible(false);
            StartCoroutine(LeaveAfterRescue());
            return;
        }

        // 다시 말을 걸 수 있으면 안내를 되살린다 (범위 안에 있을 때만 실제로 보인다)
        SetHintVisible(CanTalk);
    }

    // 구조 완료 — 보상 셀을 남기고 사라진다
    IEnumerator LeaveAfterRescue()
    {
        WorldState.Record(WorldCategory.Event, RescueId);

        if (leaveDelay > 0f) yield return new WaitForSeconds(leaveDelay);

        ScreenFadeManager fade = blackoutOnLeave ? ScreenFadeManager.Instance : null;
        if (blackoutOnLeave && fade == null)
            Debug.LogWarning($"[NPC] {name}: 암전을 쓰려면 ScreenFadeManager가 필요합니다. " +
                             "스프라이트 페이드로 대신합니다.", this);

        if (fade != null)
        {
            yield return BlackoutLeave(fade);
            yield break;
        }

        // 예전 방식 — 셀이 튀어오르는 걸 보여주고 그 자리에서 서서히 투명해진다
        DropReward(true);
        yield return FadeOut();
        gameObject.SetActive(false);
    }

    // 암전 연출 (A02 적혈구 구조):
    //   대화 종료 → 화면 암전 → 어두운 사이에 적혈구를 지우고 그 자리에 셀을 놓음 → 다시 밝아짐
    //
    // ★ 어두운 동안 gameObject.SetActive(false)를 하면 이 코루틴도 같이 죽어서
    //   화면이 검은 채로 영영 남는다. 그래서 어두울 땐 '안 보이게'만 하고,
    //   다 밝아진 뒤에 오브젝트를 끈다.
    IEnumerator BlackoutLeave(ScreenFadeManager fade)
    {
        PlayerInputLock.Acquire();

        yield return fade.FadeOut();

        // 화면이 검은 지금 자리를 바꿔치기한다
        DropReward(false);
        Hide();

        if (blackoutHold > 0f) yield return new WaitForSecondsRealtime(blackoutHold);

        yield return fade.FadeIn();

        PlayerInputLock.Release();
        gameObject.SetActive(false);
    }

    // 오브젝트를 끄지 않고 '없는 것처럼' 만든다 (코루틴은 계속 돌아야 하므로)
    void Hide()
    {
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
            if (r != null) r.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 울음소리는 매 프레임 볼륨을 다시 쓰므로 Stop만으로는 안 멎는다 — 컴포넌트를 끈다
        foreach (ProximitySound ps in GetComponentsInChildren<ProximitySound>())
            if (ps != null) ps.enabled = false;

        foreach (AudioSource a in GetComponentsInChildren<AudioSource>())
            if (a != null) a.Stop();
    }

    // pop: 셀이 튀어오르는 연출을 쓸지. 암전 중에는 어차피 안 보이므로 제자리에 놓는다.
    void DropReward(bool pop)
    {
        if (rewardCell <= 0) return;

        SpriteRenderer spr = GetComponentInChildren<SpriteRenderer>();
        CellChunk reward = CellChunk.Spawn(transform.position, rewardCell, rewardCellPrefab, spr);
        if (reward == null) return;

        if (pop)
        {
            // 플레이어가 바로 앞에 서 있어서 그냥 두면 생기자마자 흡수된다 —
            // 잠깐 튀어오르는 걸 보여준 뒤 먹히게 한다 (몬스터 셀 드랍과 같은 처리)
            reward.pickupDelay = 0.45f;
            reward.Launch(new Vector2(Random.Range(-1.2f, 1.2f), 4f));
        }
        else
        {
            // 화면이 다시 밝아지기 전에 먹히면 '셀이 남았다'는 게 화면에 안 보인다
            reward.pickupDelay = rewardPickupDelay;
        }
    }

    IEnumerator FadeOut()
    {
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();

        if (leaveFadeDuration <= 0f || sprites.Length == 0) yield break;

        float[] startAlpha = new float[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) startAlpha[i] = sprites[i].color.a;

        float t = 0f;
        while (t < leaveFadeDuration)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / leaveFadeDuration);

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color c = sprites[i].color;
                c.a = startAlpha[i] * k;
                sprites[i].color = c;
            }
            yield return null;
        }
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
