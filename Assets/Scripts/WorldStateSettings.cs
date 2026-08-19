using UnityEngine;

// 부활할 때 무엇을 유지할지 카테고리별로 고르는 설정판.
// WorldState는 자동 생성되는 싱글턴이라 인스펙터가 없어서, 값을 만지려면 이 컴포넌트를 쓴다.
//
// 사용법: 빈 게임오브젝트에 붙여서 시작 씬(A00)에 하나만 두면 된다.
//   WorldState는 DontDestroyOnLoad라 여기서 한 번 정한 값이 게임 내내 유지된다.
//   여러 씬에 두면 나중에 로드된 쪽 값으로 덮어써지니 하나만 두는 걸 권장.
public class WorldStateSettings : MonoBehaviour
{
    [Tooltip("버튼 퍼즐로 열린 문. 세이브 후 죽어도 열린 채로 두려면 RestoreToCheckpoint")]
    public PersistMode doors = PersistMode.RestoreToCheckpoint;

    [Tooltip("셀 덩어리 등 주워 먹는 재화. AlwaysPersist로 두면 재화만 세이브 시점으로 돌아가 손해가 난다 — 아래 주의 참고")]
    public PersistMode pickups = PersistMode.RestoreToCheckpoint;

    [Tooltip("다크셀 잔재(능력 해금). AlwaysPersist여도 해금은 유지되도록 RespawnManager가 따로 보정한다")]
    public PersistMode darkCells = PersistMode.RestoreToCheckpoint;

    [Tooltip("세이브포인트 활성화 표시. 보통 되돌릴 이유가 없어 AlwaysPersist")]
    public PersistMode savePoints = PersistMode.AlwaysPersist;

    void Awake()
    {
        Apply();
    }

    void Apply()
    {
        WorldState w = WorldState.Instance;
        if (w == null) return; // BeforeSceneLoad 부트스트랩이 먼저 도므로 보통 null이 아니다

        w.SetMode(WorldCategory.Door, doors);
        w.SetMode(WorldCategory.Pickup, pickups);
        w.SetMode(WorldCategory.DarkCell, darkCells);
        w.SetMode(WorldCategory.SavePoint, savePoints);
    }

    // 플레이 중 인스펙터에서 바꾼 값을 바로 반영 (튜닝용)
    void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }
}
