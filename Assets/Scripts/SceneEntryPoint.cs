using System.Collections;
using UnityEngine;

// 다른 씬에서 넘어왔을 때 플레이어가 서 있을 지점.
// 빈 게임오브젝트에 붙이고 Entry Id에 이름을 정해두면, 그 id를 지정한 AreaPortal로 들어올 때 여기에 놓인다.
//
// 예) A01 오른쪽 끝 문 → A02로 이동, A02의 왼쪽 입구에 entryId="from_A01" 을 둔다.
//     A02 왼쪽 문 → A01로 이동, A01의 오른쪽 입구에 entryId="from_A02" 를 둔다.
//     이렇게 짝을 맞춰야 오갈 때 문 앞에서 시작한다.
public class SceneEntryPoint : MonoBehaviour
{
    public enum WalkDirection
    {
        Right = 1,  // 입구 왼쪽 안쪽에서 시작해 오른쪽으로 걸어 나온다
        Left = -1,  // 입구 오른쪽 안쪽에서 시작해 왼쪽으로 걸어 나온다
    }

    [Tooltip("AreaPortal의 Target Entry Id와 똑같이 맞출 것. 씬 안에서 겹치지 않게")]
    public string entryId = "";

    [Header("입장 연출")]
    // 씬이 바뀌자마자 플레이어가 도착 지점에 뿅 나타나면 두 맵이 끊겨 보인다.
    // 조금 안쪽(문 뒤)에서 시작해 걸어 나오게 해서 이어진 것처럼 보이게 한다.
    [Tooltip("켜면 도착 지점 안쪽에서 시작해 걸어 나온다. 끄면 예전처럼 도착 지점에 바로 선다")]
    public bool walkInOnArrival = true;

    [Tooltip("도착 지점에서 이만큼 안쪽에서 시작한다")]
    public float walkInDistance = 1.8f;

    [Tooltip("걸어 나오는 방향")]
    public WalkDirection walkInDirection = WalkDirection.Right;

    // ★ 이게 없으면 연출이 있어도 안 보인다.
    //   페이드 인이 1초인데 1.8유닛을 moveSpeed 5로 걸으면 0.36초 만에 끝난다 —
    //   화면이 3분의 1도 안 밝아졌을 때 이미 다 걸어와 서 있어서, 보는 사람에겐 페이드만 보인다.
    [Tooltip("화면이 충분히 밝아질 때까지 기다렸다가 걷기 시작한다. " +
             "ScreenFadeManager의 페이드 길이를 바꿔도 알아서 따라간다")]
    public bool waitForFadeIn = true;

    [Range(0f, 1f)]
    [Tooltip("검은 화면이 이 정도까지 옅어지면 걷기 시작한다 (1=완전히 검음, 0=완전히 밝음)")]
    public float walkStartScreenAlpha = 0.55f;

    [Tooltip("화면이 안 밝아지는 경우를 대비한 최대 대기 시간")]
    public float fadeWaitTimeout = 2f;

    [Range(0.1f, 1f)]
    [Tooltip("걸어 나오는 속도 (이동 속도 대비). 1이면 전력 질주라 순식간에 끝난다")]
    public float walkInSpeedRatio = 0.5f;

    [Tooltip("화면이 밝아진 뒤 추가로 더 기다릴 시간")]
    public float walkInDelay = 0f;

    [Tooltip("안전장치 — 지형에 막혀 도착 지점까지 못 가도 이 시간이 지나면 조작을 돌려준다")]
    public float walkInMaxDuration = 2.5f;

    // 어느 입구로 들어오는 중인지. 씬을 넘어가도 유지돼야 해서 static으로 둔다.
    public static string PendingEntryId { get; private set; }

    // 입장 연출이 도는 동안 켜진다. 이 사이엔 AreaPortal이 발동하지 않는다 —
    // 시작 지점이 문 안쪽이라 그대로 두면 들어오자마자 왔던 씬으로 도로 튕긴다.
    public static bool EntrySequenceActive { get; private set; }

    public static void RequestEntry(string id) => PendingEntryId = id;

    public static void ClearEntry()
    {
        PendingEntryId = null;
        EntrySequenceActive = false;
    }

    void Start()
    {
        if (string.IsNullOrEmpty(PendingEntryId)) return;
        if (PendingEntryId != entryId) return;

        PendingEntryId = null; // 한 번만 쓰고 지운다 (다음 씬 로드까지 남아 있으면 엉뚱한 곳으로 간다)
        StartCoroutine(PlacePlayer());
    }

    void OnDisable()
    {
        // 연출 도중 씬이 또 바뀌는 등으로 코루틴이 끊겨도 잠금이 남지 않게 한다
        if (EntrySequenceActive) EndSequence();
    }

    // 플레이어 등록(PlayerController.Start → RegisterPlayer)이 끝난 뒤에 옮겨야 한다
    IEnumerator PlacePlayer()
    {
        yield return null;

        PlayerManager m = PlayerManager.Instance;
        if (m == null || m.allPlayers.Count == 0)
        {
            Debug.LogWarning($"[SceneEntryPoint] '{entryId}': 씬에서 플레이어를 찾지 못했다.", this);
            yield break;
        }

        PlayerController main = m.allPlayers[0];
        if (main == null) yield break;

        Vector3 arrival = transform.position;
        int dir = (int)walkInDirection;
        bool doWalk = walkInOnArrival && walkInDistance > 0.01f;

        // 걸어 나올 거면 안쪽에서, 아니면 도착 지점에 바로 세운다
        main.transform.position = doWalk
            ? arrival - Vector3.right * (dir * walkInDistance)
            : arrival;

        // 이전 씬에서 이동하던 속도가 남아 튀어나가지 않게 정지시킨다
        Rigidbody2D rb = main.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // ★ 옮긴 직후에 카메라를 붙여야 한다. 안 하면 카메라가 씬에 저장된 원래 위치에서
        //   플레이어까지 부드럽게 따라오면서 화면이 한 번 흔들린 것처럼 보인다.
        CameraSnap.SnapNow();

        if (!doWalk) yield break;

        yield return WalkIn(main, arrival, dir);
    }

    // 조작을 잠근 채 도착 지점까지 걸어 나온다.
    // 위치를 직접 옮기지 않고 이동 입력 자리에 값을 넣는 이유는 Rigidbody와 싸우지 않기 위해서다
    // (EventTriggerZone의 '끌려가는 연출'과 같은 방식).
    IEnumerator WalkIn(PlayerController main, Vector3 arrival, int dir)
    {
        BeginSequence();

        if (waitForFadeIn)
            yield return WaitForScreen();

        if (walkInDelay > 0f)
            yield return new WaitForSeconds(walkInDelay);

        float elapsed = 0f;

        while (elapsed < walkInMaxDuration)
        {
            if (main == null) break;

            // 도착 지점을 지나쳤는지 진행 방향 기준으로 본다
            float remaining = (arrival.x - main.transform.position.x) * dir;
            if (remaining <= 0.05f) break;

            // 전속력으로 뛰면 순식간에 끝나 연출이 안 보인다 — 비율을 낮춰 걸어 나오게 한다
            main.SetScriptedMove(dir * Mathf.Clamp(walkInSpeedRatio, 0.1f, 1f));

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (main != null) main.ClearScriptedMove();
        EndSequence();
    }

    // 페이드 인이 끝나가는 것을 기다린다.
    // ScreenFadeManager가 검은 판(CanvasGroup)의 알파를 1 → 0으로 내리므로 그 값을 직접 본다.
    // (시간으로 맞추면 페이드 길이를 바꿀 때마다 여기도 같이 고쳐야 한다)
    IEnumerator WaitForScreen()
    {
        CanvasGroup fade = FindFadeCanvas();
        if (fade == null) yield break; // 페이드가 없는 씬이면 그냥 바로 걷는다

        float waited = 0f;
        while (fade.alpha > walkStartScreenAlpha && waited < fadeWaitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }
    }

    CanvasGroup FindFadeCanvas()
    {
        if (ScreenFadeManager.Instance == null) return null;
        return ScreenFadeManager.Instance.GetComponentInChildren<CanvasGroup>(true);
    }

    void BeginSequence()
    {
        EntrySequenceActive = true;
        PlayerInputLock.Acquire();
    }

    void EndSequence()
    {
        EntrySequenceActive = false;
        PlayerInputLock.Release();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1.6f, 0f));

        if (!walkInOnArrival || walkInDistance <= 0.01f) return;

        // 걸어 나오기 시작하는 자리 — 여기가 지형 안이면 연출이 막히므로 눈으로 확인할 수 있게 그린다
        Vector3 start = transform.position - Vector3.right * ((int)walkInDirection * walkInDistance);
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.7f);
        Gizmos.DrawWireCube(start, new Vector3(0.8f, 1.6f, 0f));
        Gizmos.DrawLine(start, transform.position);
    }
}
