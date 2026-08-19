using System.Collections;
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

        [Tooltip("Wait: 기다릴 시간(초). MoveActor/MovePlayer: 이 시간이 지나면 도착 못 해도 다음으로 넘어간다(0이면 10초)")]
        public float duration = 1f;

        [Header("Dialogue")]
        public DialogueData dialogue;

        [Header("MoveActor / MovePlayer")]
        [Tooltip("MoveActor에서 움직일 오브젝트 (NPC 등)")]
        public Transform actor;
        [Tooltip("도착 지점. 빈 오브젝트를 놓고 연결하면 된다")]
        public Transform moveTarget;
        public float moveSpeed = 3f;
        [Tooltip("이 거리 안에 들어오면 도착으로 친다")]
        public float arriveDistance = 0.3f;

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

        foreach (Step s in steps)
        {
            if (s == null) continue;
            yield return RunStep(s);
        }

        ReleaseLock();
        running = false;
    }

    IEnumerator RunStep(Step s)
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
                yield return MoveTransform(s.actor, s);
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

    // NPC 접근 연출 — Rigidbody가 없는 연출용 오브젝트 기준이라 위치를 직접 옮긴다
    IEnumerator MoveTransform(Transform mover, Step s)
    {
        if (mover == null || s.moveTarget == null)
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
            Vector3 goal = new Vector3(s.moveTarget.position.x, mover.position.y, mover.position.z);
            if (Vector3.Distance(mover.position, goal) <= s.arriveDistance) break;

            if (spr != null) spr.flipX = goal.x < mover.position.x;
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
