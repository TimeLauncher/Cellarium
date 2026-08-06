using UnityEngine;
using UnityEngine.SceneManagement;

// PC 조작을 일시적으로 막는 공용 스위치.
//
// ★ 왜 PauseMenu(Time.timeScale = 0)를 쓰면 안 되는가:
//   대화·연출 중에는 캐릭터와 NPC의 애니메이션이 계속 돌아야 하고, 타이핑 연출도 진행돼야 한다.
//   timeScale을 0으로 만들면 그게 전부 멈춘다. 그래서 '입력만' 막는 별도 수단이 필요하다.
//   (물리는 그대로라 잠긴 채로 공중에 있으면 정상적으로 낙하한다)
//
// ★ 카운터인 이유:
//   대화 도중 이벤트 트리거가 또 걸리는 식으로 잠금이 겹칠 수 있다. bool이면 먼저 끝난 쪽이
//   풀어버려서 나머지가 잠긴 줄 알고 있는데 조작이 되는 상태가 된다. 획득/해제 짝을 세서
//   전부 풀렸을 때만 조작이 돌아오게 한다.
//
// 사용법
//   PlayerInputLock.Acquire();  // 대화 시작
//   PlayerInputLock.Release();  // 대화 종료
//   반드시 짝을 맞출 것. 코루틴 중간에 씬이 바뀌는 등으로 짝이 깨져도
//   씬 로드 시 자동으로 0으로 초기화되므로 조작이 영영 잠기지는 않는다.
public static class PlayerInputLock
{
    static int lockCount;

    public static bool IsLocked => lockCount > 0;
    public static int LockCount => lockCount;

    public static void Acquire()
    {
        lockCount++;
    }

    public static void Release()
    {
        lockCount = Mathf.Max(0, lockCount - 1);
    }

    public static void ClearAll()
    {
        lockCount = 0;
    }

    // 씬이 바뀌면 무조건 푼다.
    // 사망 시 RespawnManager가 씬을 통째로 리로드하는데, 그때 대화창을 띄운 채로 죽으면
    // Release를 부를 주체(DialogueManager)가 같이 파괴돼 잠금이 남는다 → 부활 후 조작 불가.
    //
    // static 필드는 도메인 리로드를 끄면 플레이 모드를 나갔다 들어와도 값이 남으므로
    // SubsystemRegistration으로도 한 번 더 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad()
    {
        lockCount = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lockCount = 0;
    }
}
