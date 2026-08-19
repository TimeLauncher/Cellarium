using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

// 카메라 연출: 잠깐 다른 곳을 비춰준 뒤 다시 조종 중인 캐릭터로 돌아온다.
//
// 왜 필요한가:
//   A02의 두 번째 버튼 문(Button_Door (1))은 버튼이 (-51.7, 4.2)/(1.2, 6.2)에 있는데
//   문은 (28.5, -23.5)에 있다. 80유닛 넘게 떨어져 있어서 버튼을 다 밟아도
//   "문이 열렸다"는 사실이 화면에 전혀 안 보인다. 그래서 문 쪽으로 카메라를 보냈다가
//   열리는 걸 보여주고 다시 본체로 돌아오는 연출이 필요하다.
//
// 어떻게 도는가:
//   카메라(Cinemachine)의 Follow 대상을 '임시 유도 오브젝트'로 잠깐 바꾸고,
//   그 오브젝트를 플레이어 → 목표 지점으로 부드럽게 옮긴다. 돌아올 때는 반대로 한다.
//   Follow만 목표로 바꿔버리면 시네머신 감쇠가 짧아서 순간이동처럼 튄다.
//
// 쓰는 법 (둘 다 코루틴)
//   ① 통짜로:  yield return CameraFocus.PlayRoutine(문Transform, 0.7f, 1.2f, 0.7f, true);
//   ② 중간에 뭔가 하려면:
//        CameraFocus focus = CameraFocus.Begin(문Transform, 0.7f, true);
//        yield return focus.WaitForArrive();   // 카메라가 문에 도착할 때까지
//        yield return 문열기연출();              // 도착한 뒤에 열어야 열리는 게 보인다
//        yield return new WaitForSeconds(1.2f);
//        yield return focus.End(0.7f);          // 다시 본체로
public class CameraFocus : MonoBehaviour
{
    // 지금 연출이 돌고 있는지. 겹쳐서 두 개가 동시에 카메라를 잡으면 엉키므로 확인용으로 둔다.
    public static bool IsPlaying => current != null;

    static CameraFocus current;

    CinemachineCamera cineCam;
    CameraFollow plainFollow;     // 시네머신을 안 쓰는 씬(SampleScene 등) 대비
    Transform originalFollow;
    Camera plainCamera;

    bool lockAcquired;
    bool arrived;

    // ── 시작 ────────────────────────────────────────────────────────────

    // 카메라를 target으로 보낸다. 돌아오려면 End()를 반드시 불러야 한다.
    public static CameraFocus Begin(Transform target, float travelTime, bool lockInput)
    {
        if (target == null)
        {
            Debug.LogWarning("[CameraFocus] 비출 대상이 비어 있습니다.");
            return null;
        }

        // 이미 다른 연출이 카메라를 잡고 있으면 그걸 먼저 정리한다 (Follow가 임시 오브젝트에 남는 것 방지)
        if (current != null) current.RestoreImmediately();

        GameObject go = new GameObject("CameraFocus");
        go.transform.position = CurrentAnchor();

        CameraFocus focus = go.AddComponent<CameraFocus>();
        current = focus;

        focus.Capture();

        if (lockInput)
        {
            PlayerInputLock.Acquire();
            focus.lockAcquired = true;
        }

        focus.StartCoroutine(focus.Travel(target, travelTime));
        return focus;
    }

    // 카메라가 목표 지점에 도착할 때까지 기다린다
    public IEnumerator WaitForArrive()
    {
        while (!arrived) yield return null;
    }

    // 다시 조종 중인 캐릭터로 돌아온다
    public IEnumerator End(float returnTime)
    {
        yield return ReturnToPlayer(returnTime);
        RestoreImmediately();
    }

    // 통짜 연출 — 갔다가 hold초 머문 뒤 돌아온다
    public static IEnumerator PlayRoutine(Transform target, float travelTime, float holdTime,
                                          float returnTime, bool lockInput)
    {
        CameraFocus focus = Begin(target, travelTime, lockInput);
        if (focus == null) yield break;

        yield return focus.WaitForArrive();
        yield return new WaitForSeconds(Mathf.Max(0f, holdTime));
        yield return focus.End(returnTime);
    }

    // ── 내부 ────────────────────────────────────────────────────────────

    void Capture()
    {
        cineCam = FindFirstObjectByType<CinemachineCamera>();

        if (cineCam != null)
        {
            originalFollow = cineCam.Follow;
            cineCam.Follow = transform;
            return;
        }

        // 시네머신이 없는 씬: CameraFollow를 잠시 끄고 카메라를 직접 옮긴다
        plainFollow = FindFirstObjectByType<CameraFollow>();
        plainCamera = Camera.main;
        if (plainFollow != null) plainFollow.enabled = false;
    }

    IEnumerator Travel(Transform target, float travelTime)
    {
        Vector3 start = transform.position;
        float t = 0f;
        float limit = Mathf.Max(0.01f, travelTime);

        while (t < limit)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / limit);

            // 목표가 움직이는 오브젝트일 수도 있으니 매 프레임 다시 읽는다
            Vector3 goal = target != null ? target.position : start;
            MoveTo(Vector3.Lerp(start, goal, k));
            yield return null;
        }

        if (target != null) MoveTo(target.position);
        arrived = true;
    }

    IEnumerator ReturnToPlayer(float returnTime)
    {
        Vector3 start = transform.position;
        float t = 0f;
        float limit = Mathf.Max(0.01f, returnTime);

        while (t < limit)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / limit);

            // 돌아오는 동안 플레이어가 움직일 수 있으므로 매 프레임 현재 위치를 본다
            MoveTo(Vector3.Lerp(start, CurrentAnchor(), k));
            yield return null;
        }
    }

    void MoveTo(Vector3 position)
    {
        transform.position = position;

        // 시네머신이 없으면 카메라를 직접 옮긴다 (z는 원래 값 유지)
        if (cineCam == null && plainCamera != null)
        {
            Vector3 p = position;
            p.z = plainCamera.transform.position.z;
            plainCamera.transform.position = p;
        }
    }

    // 연출을 끝내고 카메라를 원래 주인에게 돌려준다.
    // 씬이 바뀌거나 연출이 중간에 끊겨도 잠금과 Follow가 남지 않게 OnDestroy에서도 안전하게 돈다.
    void RestoreImmediately()
    {
        if (cineCam != null)
        {
            Transform back = originalFollow;

            // 조종 대상이 바뀌었으면(분열체 전환 등) 지금 조종 중인 쪽으로 돌려준다
            if (PlayerManager.Instance != null && PlayerManager.Instance.currentPlayer != null)
                back = PlayerManager.Instance.currentPlayer.transform;

            cineCam.Follow = back;
        }

        if (plainFollow != null) plainFollow.enabled = true;

        if (lockAcquired)
        {
            PlayerInputLock.Release();
            lockAcquired = false;
        }

        if (current == this) current = null;

        if (this != null && gameObject != null) Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Destroy가 먼저 불린 경우(씬 전환 등)에도 잠금/Follow는 반드시 되돌린다
        if (lockAcquired)
        {
            PlayerInputLock.Release();
            lockAcquired = false;
        }

        if (cineCam != null && cineCam.Follow == transform)
        {
            Transform back = originalFollow;
            if (PlayerManager.Instance != null && PlayerManager.Instance.currentPlayer != null)
                back = PlayerManager.Instance.currentPlayer.transform;
            cineCam.Follow = back;
        }

        if (plainFollow != null) plainFollow.enabled = true;

        if (current == this) current = null;
    }

    // 카메라가 '평소에 보고 있어야 할 자리' = 지금 조종 중인 캐릭터
    static Vector3 CurrentAnchor()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.currentPlayer != null)
            return PlayerManager.Instance.currentPlayer.transform.position;

        PlayerController any = FindFirstObjectByType<PlayerController>();
        if (any != null) return any.transform.position;

        return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    }
}
