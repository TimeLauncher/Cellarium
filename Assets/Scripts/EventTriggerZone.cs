using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 이벤트 트리거 (기획서 (3) '기능 추가: 이벤트 트리거').
//
// 기획서: "PC가 특정 범위 내 진입 시 강제로 연출 씬 재생 혹은 특정 '대화 이벤트'로 진입"
// 사용처: A03 진입 이후 노란 구역에 들어가면 백혈구 병사가 접근해 대화 이벤트가 강제 시작,
//         이후 A06 방향으로 끌려가는 연출 → A06 강제 씬 전환 및 T세포 대화 이벤트로 이어짐.
//
// 연출은 '단계(Step) 목록'을 위에서부터 차례로 실행하는 방식이다.
// A03 예시를 그대로 만들면 이렇게 된다:
//   1) MoveActor  : 백혈구 병사를 플레이어 앞까지 걸어오게 함
//   2) Dialogue   : 대화 이벤트 (끝날 때까지 다음 단계로 안 넘어감)
//   3) MovePlayer : A06 쪽 지점까지 플레이어가 끌려감
//   4) LoadScene  : A06으로 강제 씬 전환 (도착 지점은 SceneEntryPoint의 Entry Id로 지정)
//
// CameraFocus 단계를 쓰면 멀리 있는 것(열린 문, 무너진 벽 등)을 잠깐 비춰준 뒤 돌아올 수 있다.
//
// ★ 동시에 여러 개를 움직이려면 Run With Next 를 켠다.
//   단계는 원래 위에서부터 하나씩 순서대로 도는데, Run With Next 를 켜면
//   그 단계를 시작만 하고 기다리지 않은 채 다음 단계도 같이 시작한다.
//   켜진 단계들 + 바로 뒤의 한 단계가 '한 묶음'이 되어 동시에 돌고, 다 끝나야 다음으로 넘어간다.
//
//   백혈구 병사 둘이 PC를 데려가는 연출(A03)이면 이렇게 된다:
//     1) Dialogue   "거기 누구냐!"
//     2) MoveActor  병사A -> 플레이어      [Run With Next 켬]
//     3) MoveActor  병사B -> 플레이어              <- 둘이 같이 다가온다
//     4) Dialogue   "따라와, 우리가 안내해주지"
//     5) MovePlayer 플레이어 -> A06 통로 앞  [Run With Next 켬]
//     6) MoveActor  병사A, Follow Player 켬 / Offset +1.3  [Run With Next 켬]
//     7) MoveActor  병사B, Follow Player 켬 / Offset -1.3  <- 양옆에 붙어 같이 이동
//     8) LoadScene  Heart A06
//
//   Follow Player 를 켠 MoveActor 는 '도착'이 없으므로 스스로 끝나지 않는다.
//   같은 묶음의 다른 단계(위 예에서는 5번 MovePlayer)가 끝나면 같이 멈춘다.
//
// 배치법
//   빈 게임오브젝트 + Collider2D(Is Trigger ✓) 에 붙이고 노란 구역 크기로 키운다.
[RequireComponent(typeof(Collider2D))]
public class EventTriggerZone : MonoBehaviour
{
    public enum StepType
    {
        Wait,          // 그냥 기다린다 (연출 사이 뜸 들이기)
        Dialogue,      // 대화 이벤트 — 끝날 때까지 대기
        MoveActor,     // 지정한 오브젝트를 목표 지점까지 이동 (NPC 접근 연출)
        MovePlayer,    // 플레이어를 목표 지점까지 강제 이동 (끌려가는 연출)
        SetActive,     // 오브젝트 켜기/끄기 (벽 막이, 연출용 오브젝트 등)
        UnlockFission, // 분열 능력 해금
        LoadScene,     // 씬 강제 전환

        // ★ 새 항목은 반드시 맨 아래에 추가할 것.
        //   중간에 끼워 넣으면 뒤쪽 값이 한 칸씩 밀려서, 이미 씬에 저장된 단계가
        //   전혀 다른 동작으로 바뀐다 (LoadScene이 UnlockFission이 되는 식).
        CameraFocus,   // 지정 지점을 잠깐 비춰준 뒤 다시 조종 중인 캐릭터로 돌아온다
    }

    [System.Serializable]
    public class Step
    {
        public StepType type = StepType.Dialogue;

        [Tooltip("켜면 이 단계를 시작만 하고 기다리지 않은 채 다음 단계도 같이 시작한다.\n" +
                 "병사 둘이 동시에 다가오게 하거나, 끌려가는 플레이어 옆에 NPC를 붙일 때 쓴다")]
        public bool runWithNext = false;

        [Tooltip("Wait: 기다릴 시간(초). MoveActor/MovePlayer: 이 시간이 지나면 도착 못 해도 다음으로 넘어간다(0이면 10초)")]
        public float duration = 1f;

        [Header("Dialogue")]
        public DialogueData dialogue;

        [Header("MoveActor / MovePlayer")]
        [Tooltip("MoveActor에서 움직일 오브젝트 (NPC 등)")]
        public Transform actor;
        [Tooltip("도착 지점. 빈 오브젝트를 놓고 연결하면 된다")]
        public Transform moveTarget;
        [Tooltip("초당 이동 거리. Follow Player로 끌려가는 PC를 따라갈 때는\n" +
                 "PlayerController.moveSpeed(기본 8)보다 크게 줘야 안 뒤처진다")]
        public float moveSpeed = 3f;
        [Tooltip("이 거리 안에 들어오면 도착으로 친다")]
        public float arriveDistance = 0.3f;

        [Tooltip("MoveActor에서 켜면 Move Target 대신 '조종 중인 캐릭터'를 계속 따라다닌다.\n" +
                 "끌려가는 PC 양옆에 호위를 붙이는 연출용. 도착이 없으므로 같은 묶음의\n" +
                 "다른 단계가 끝날 때 같이 멈춘다 (Run With Next 와 같이 쓸 것)")]
        public bool followPlayer = false;
        [Tooltip("따라다닐 때 플레이어로부터 좌우로 얼마나 떨어져 설지. +면 오른쪽, -면 왼쪽.\n" +
                 "높이는 건드리지 않는다(발이 뜨지 않게)")]
        public float followOffsetX = 1.3f;

        [Header("CameraFocus")]
        [Tooltip("잠깐 비출 지점. 머무는 시간은 위의 Duration을 쓴다")]
        public Transform cameraTarget;
        [Tooltip("카메라가 그 지점까지 날아가는 시간")]
        public float cameraTravelTime = 0.8f;
        [Tooltip("다시 조종 중인 캐릭터로 돌아오는 시간")]
        public float cameraReturnTime = 0.8f;

        [Header("SetActive")]
        public GameObject target;
        public bool active = true;

        [Header("LoadScene")]
        [Tooltip("Build Settings에 등록된 씬 이름과 정확히 같아야 한다")]
        public string sceneName;
        [Tooltip("도착 씬의 SceneEntryPoint와 맞출 id. 비우면 그 씬의 원래 자리에서 시작")]
        public string entryId = "";
    }

    [Header("연출 단계 (위에서부터 차례로 실행)")]
    public Step[] steps;

    [Header("발동 조건")]
    [Tooltip("끄면 범위에 들어와도 발동하지 않는다 (다른 이벤트가 켜주는 식으로 순서를 만들 때)")]
    public bool armed = true;

    [Tooltip("한 번 발동하면 다시는 발동하지 않는다. 씬을 다시 로드해도 기억한다")]
    public bool onceOnly = true;

    [Tooltip("비우면 계층 경로로 자동 생성된다. 오브젝트를 옮길 예정이면 직접 이름을 적을 것")]
    public string persistentId = "";

    [Tooltip("분열체가 밟아도 발동할지. 끄면 조종 중인 캐릭터만 발동시킨다")]
    public bool clonesCanTrigger = false;

    [Header("연출 중")]
    [Tooltip("연출이 끝날 때까지 PC 조작을 막는다")]
    public bool lockInput = true;

    [Header("게임 화면 표시 (확인용)")]
    // 실제 게임에서 이벤트 트리거는 '보이면 안 되는' 숨은 구역이므로 기본은 꺼둔다.
    // 시연·테스트 씬에서만 켜서 어디에 깔려 있는지 눈으로 확인한다.
    [Tooltip("게임 화면에 구역을 반투명하게 표시한다. 실제 게임에선 꺼둘 것")]
    public bool showArea = false;
    public Color areaColor = new Color(1f, 0.9f, 0.2f, 0.16f);
    [Tooltip("구역 위에 띄울 설명. 비우면 글씨를 안 만든다")]
    public string areaLabel = "이벤트 트리거";
    public int areaSortingOrder = 30;

    bool running;
    bool consumed;
    bool lockAcquired;
    ZoneVisualizer areaView;

    string Id => WorldState.MakeId(this, persistentId);

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[{name}] Collider2D를 Is Trigger로 바꿨습니다. 이벤트 범위는 트리거여야 합니다.", this);
        }

        // 이미 본 연출이면 아예 꺼둔다 (부활로 씬이 리로드돼도 다시 안 나오게)
        if (onceOnly && WorldState.Has(WorldCategory.Event, Id))
            consumed = true;

        if (showArea && !consumed && col != null)
            areaView = ZoneVisualizer.ShowBox(gameObject, col, areaColor, areaLabel, areaSortingOrder);
    }

    void OnDestroy()
    {
        // 연출 도중 씬이 바뀌면(사망·씬 전환) 잠금이 남아 조작 불가가 된다
        ReleaseLock();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!armed || consumed || running) return;

        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        if (!clonesCanTrigger && pc.isClone) return;

        StartEvent();
    }

    // 다른 스크립트에서 직접 발동시킬 수도 있게 열어둔다
    public void StartEvent()
    {
        if (running || consumed) return;
        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning($"[{name}] 실행할 연출 단계가 비어 있습니다.", this);
            return;
        }

        running = true;

        // 발동했으면 구역 표시는 지운다 (연출 중에 노란 판이 화면에 남아 있으면 방해된다)
        if (areaView != null) areaView.Hide();

        if (onceOnly)
        {
            consumed = true;
            WorldState.Record(WorldCategory.Event, Id);
        }

        StartCoroutine(RunSteps());
    }

    IEnumerator RunSteps()
    {
        AcquireLock();

        int i = 0;
        while (i < steps.Length)
        {
            // Run With Next 가 켜진 단계들 + 바로 뒤의 한 단계까지를 한 묶음으로 모은다
            int last = i;
            while (last < steps.Length - 1 && steps[last] != null && steps[last].runWithNext)
                last++;

            if (last == i)
            {
                if (steps[i] != null) yield return RunStep(steps[i]);
            }
            else
            {
                yield return RunGroup(i, last);
            }

            i = last + 1;
        }

        ReleaseLock();
        running = false;
    }

    // 한 묶음을 동시에 실행한다.
    // Follow Player 단계는 스스로 끝나지 않으므로, 나머지가 다 끝나면 같이 멈춘다.
    IEnumerator RunGroup(int from, int to)
    {
        List<Step> anchors = new List<Step>();   // 스스로 끝나는 단계
        List<Step> escorts = new List<Step>();   // 따라다니기만 하는 단계

        for (int i = from; i <= to; i++)
        {
            Step s = steps[i];
            if (s == null) continue;
            if (IsEscort(s)) escorts.Add(s);
            else anchors.Add(s);
        }

        // 묶음이 통째로 따라다니기뿐이면 끊어줄 기준이 없다.
        // 그럴 땐 각자 Duration 만큼 돌게 둔다.
        if (anchors.Count == 0)
        {
            anchors.AddRange(escorts);
            escorts.Clear();
        }

        Group g = new Group { pending = anchors.Count };

        foreach (Step s in anchors) StartCoroutine(RunAndCount(s, g));
        foreach (Step s in escorts) StartCoroutine(RunStep(s, g));

        while (g.pending > 0) yield return null;

        // 따라다니던 쪽이 마지막 정리(걷기 애니메이션 끄기)를 할 한 프레임을 준다
        yield return null;
    }

    static bool IsEscort(Step s)
    {
        return s.type == StepType.MoveActor && s.followPlayer;
    }

    IEnumerator RunAndCount(Step s, Group g)
    {
        yield return RunStep(s);
        g.pending--;
    }

    // 같은 묶음이 아직 도는 중인지 알려주는 공유 표식
    class Group
    {
        public int pending;
    }

    // group: 같은 묶음으로 동시에 도는 중이면 그 표식. 단독 실행이면 null.
    IEnumerator RunStep(Step s, Group group = null)
    {
        switch (s.type)
        {
            case StepType.Wait:
                yield return new WaitForSeconds(Mathf.Max(0f, s.duration));
                break;

            case StepType.Dialogue:
                yield return PlayDialogue(s.dialogue);
                break;

            case StepType.MoveActor:
                yield return MoveTransform(s.actor, s, group);
                break;

            case StepType.MovePlayer:
                yield return DragPlayer(s);
                break;

            case StepType.CameraFocus:
                if (s.cameraTarget == null)
                {
                    Debug.LogWarning($"[{name}] CameraFocus 단계의 Camera Target이 비어 있습니다.", this);
                    break;
                }
                // 조작 잠금은 이 트리거의 Lock Input 설정을 그대로 따른다 (여기서 또 걸지 않는다)
                yield return global::CameraFocus.PlayRoutine(
                    s.cameraTarget, s.cameraTravelTime, s.duration, s.cameraReturnTime, false);
                break;

            case StepType.SetActive:
                if (s.target != null) s.target.SetActive(s.active);
                else Debug.LogWarning($"[{name}] SetActive 단계의 Target이 비어 있습니다.", this);
                break;

            case StepType.UnlockFission:
                if (PlayerManager.Instance != null) PlayerManager.Instance.UnlockFission();
                break;

            case StepType.LoadScene:
                yield return LoadScene(s);
                break;
        }
    }

    IEnumerator PlayDialogue(DialogueData data)
    {
        if (data == null || data.IsEmpty)
        {
            Debug.LogWarning($"[{name}] Dialogue 단계에 DialogueData가 없습니다.", this);
            yield break;
        }

        bool done = false;
        DialogueManager.EnsureInstance().Play(data, () => done = true);

        while (!done) yield return null;
    }

    // NPC 접근 연출 — Rigidbody가 없는 연출용 오브젝트 기준이라 위치를 직접 옮긴다.
    // Follow Player를 켜면 목표를 매 프레임 '플레이어 + 좌우 간격'으로 다시 잡아
    // 끌려가는 PC 옆에 붙어 같이 이동한다.
    IEnumerator MoveTransform(Transform mover, Step s, Group group)
    {
        Transform follow = null;
        if (s.followPlayer)
        {
            PlayerController pc = CurrentPlayer();
            if (pc != null) follow = pc.transform;
            else Debug.LogWarning($"[{name}] MoveActor 단계가 Follow Player인데 플레이어를 찾지 못했습니다.", this);
        }

        if (mover == null || (follow == null && s.moveTarget == null))
        {
            Debug.LogWarning($"[{name}] MoveActor 단계의 Actor 또는 Move Target이 비어 있습니다.", this);
            yield break;
        }

        float limit = s.duration > 0f ? s.duration : 10f;
        float timer = 0f;

        SpriteRenderer spr = mover.GetComponent<SpriteRenderer>();
        Animator anim = mover.GetComponent<Animator>();
        if (anim != null) SetBoolIfExists(anim, "move", true);

        while (timer < limit)
        {
            // 같은 묶음의 다른 단계가 다 끝났으면 호위도 여기서 멈춘다
            if (group != null && group.pending <= 0) break;

            float goalX = follow != null
                ? follow.position.x + s.followOffsetX
                : s.moveTarget.position.x;
            Vector3 goal = new Vector3(goalX, mover.position.y, mover.position.z);

            float gap = Mathf.Abs(goalX - mover.position.x);
            // 따라다니는 중엔 '도착'으로 끝내지 않는다 (목표가 계속 움직이므로)
            if (follow == null && gap <= s.arriveDistance) break;

            // 목표 위에 서 있을 땐 방향을 뒤집지 않는다 (제자리에서 좌우로 떠는 것 방지)
            if (spr != null && gap > 0.05f) spr.flipX = goalX < mover.position.x;
            mover.position = Vector3.MoveTowards(mover.position, goal, s.moveSpeed * Time.deltaTime);

            timer += Time.deltaTime;
            yield return null;
        }

        if (anim != null) SetBoolIfExists(anim, "move", false);
    }

    // 끌려가는 연출 — 플레이어는 Rigidbody로 움직이므로 위치를 직접 건드리면 안 된다.
    // PlayerController의 연출용 이동 입력(scriptedMoveX)에 방향만 넣어주면
    // 평소 걷는 것과 똑같이(중력·지형 충돌 유지) 이동한다.
    IEnumerator DragPlayer(Step s)
    {
        PlayerController pc = CurrentPlayer();
        if (pc == null || s.moveTarget == null)
        {
            Debug.LogWarning($"[{name}] MovePlayer 단계의 Move Target이 비었거나 플레이어를 찾지 못했습니다.", this);
            yield break;
        }

        float limit = s.duration > 0f ? s.duration : 10f;
        float timer = 0f;

        while (timer < limit)
        {
            float dx = s.moveTarget.position.x - pc.transform.position.x;
            if (Mathf.Abs(dx) <= s.arriveDistance) break;

            pc.SetScriptedMove(Mathf.Sign(dx));

            timer += Time.deltaTime;
            yield return null;
        }

        pc.ClearScriptedMove();
    }

    IEnumerator LoadScene(Step s)
    {
        if (string.IsNullOrEmpty(s.sceneName))
        {
            Debug.LogError($"[{name}] LoadScene 단계의 Scene Name이 비어 있습니다.", this);
            yield break;
        }

        if (!string.IsNullOrEmpty(s.entryId)) SceneEntryPoint.RequestEntry(s.entryId);
        else SceneEntryPoint.ClearEntry();

        // 씬이 바뀌면 이 코루틴도 같이 죽으므로, 잠금은 여기서 확실히 풀어둔다
        // (PlayerInputLock은 sceneLoaded에서도 0으로 초기화되지만 짝을 맞춰두는 편이 안전하다)
        ReleaseLock();

        if (ScreenFadeManager.Instance != null)
            yield return ScreenFadeManager.Instance.FadeOut();

        SceneManager.LoadScene(s.sceneName);
    }

    // ── 도우미 ────────────────────────────────────────────────────────

    static PlayerController CurrentPlayer()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.currentPlayer != null)
            return PlayerManager.Instance.currentPlayer;
        return FindFirstObjectByType<PlayerController>();
    }

    // 연출용 NPC에 아직 Animator Controller가 없거나 파라미터가 없어도 에러가 나지 않게
    static void SetBoolIfExists(Animator anim, string param, bool value)
    {
        if (anim.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
            {
                anim.SetBool(param, value);
                return;
            }
    }

    void AcquireLock()
    {
        if (!lockInput || lockAcquired) return;
        PlayerInputLock.Acquire();
        lockAcquired = true;
    }

    void ReleaseLock()
    {
        if (!lockAcquired) return;
        PlayerInputLock.Release();
        lockAcquired = false;
    }

    void OnDrawGizmos()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) return;

        // 기획서 그림의 노란 사각형 구역
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
