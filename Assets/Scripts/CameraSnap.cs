using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// 카메라를 지금 즉시 추적 대상 위로 옮긴다(따라가는 보간 없이).
//
// ★ 왜 필요한가: 씬을 넘어오면 카메라는 새 씬에 저장된 자기 위치에서 시작하는데,
//   플레이어는 그 다음 프레임에 SceneEntryPoint(또는 RespawnManager)가 도착 지점으로 옮긴다.
//   그러면 카메라가 원래 자리에서 도착 지점까지 부드럽게 따라오면서
//   "맵 이동 직후 화면이 순간적으로 흔들리는" 것처럼 보인다.
//   위치를 옮긴 직후 이걸 부르면 그 프레임엔 감쇠를 건너뛰어 흔들림이 사라진다.
//
// 씬이 로드될 때마다 자동으로도 한 번 걸린다 — 도착 지점을 안 쓰는 이동(그냥 LoadScene)도
// 같은 문제가 생기기 때문. 씬에 배치할 필요는 없다(RespawnManager/GameSession과 같은 자동 생성 방식).
public class CameraSnap : MonoBehaviour
{
    public static void SnapNow()
    {
        // Cinemachine: 이전 프레임 상태를 무효로 만들면 이번 업데이트에서 감쇠 없이
        // 목표 위치로 바로 간다 (Heart 씬들은 전부 Cinemachine을 쓴다)
        CinemachineCore.ResetCameraState();

        // Cinemachine을 안 쓰는 씬을 위한 처리
        foreach (CameraFollow follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
            follow.SnapToTarget();
    }

    // ── 씬 로드 시 자동 스냅 ──────────────────────────────────────

    static CameraSnap runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (runner != null) return;
        runner = new GameObject("CameraSnap").AddComponent<CameraSnap>();
    }

    void Awake()
    {
        if (runner != null && runner != this) { Destroy(gameObject); return; }
        runner = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(SnapAfterLoad());

    IEnumerator SnapAfterLoad()
    {
        SnapNow();

        // 플레이어 등록(PlayerController.Start → RegisterPlayer)이 끝나야 추적 대상이 생긴다.
        // 그 뒤에 한 번 더 걸어야 로드 첫 프레임의 엉뚱한 위치가 남지 않는다.
        yield return null;
        SnapNow();
    }
}
