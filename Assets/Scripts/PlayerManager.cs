using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    [Header("���� ����")]
    public List<PlayerController> allPlayers = new List<PlayerController>();
    public PlayerController currentPlayer;

    // ���� �п� ���� Ƚ���� 1 (���߿� ���׷��̵� �� �ִ� 3���� ����)
    public int maxFissionCount = 3;

    [Tooltip("켜면 최대 분열 횟수에 도달했을 때 새 분열을 아예 막는다(가장 오래된 분열체를 회수하지 않음). 튜토리얼에서 분열 1회만 허용하려면 이 값을 켜고 Max Fission Count = 1 로 둔다")]
    public bool hardFissionCap = false;

    // 보유 재화
    public int cellCurrency;
    public int darkCellCurrency;

    [Header("능력 해금")]
    // A00 시작 시엔 이동/점프/대시/섭취만 가능. A02에서 분열 능력 획득 전까지 false 유지.
    // 테스트 씬에서 바로 분열을 쓰려면 인스펙터에서 체크해두면 됨.
    public bool fissionUnlocked = false;

    // A02의 분열 능력 획득 지점(트리거/이벤트)에서 호출
    public void UnlockFission()
    {
        fissionUnlocked = true;
        UnityEngine.Debug.Log("분열 능력 해금!");
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        int playerLayer = LayerMask.NameToLayer("player");
        int monsterLayer = LayerMask.NameToLayer("monster");

        // 플레이어는 몬스터를 밀거나 타고 올라갈 수 없다 (접촉 데미지는 MonsterBase가 겹침 검사로 따로 처리)
        if (playerLayer >= 0 && monsterLayer >= 0)
            Physics2D.IgnoreLayerCollision(playerLayer, monsterLayer, true);

        // 몬스터끼리도 밀지 않는다 — 벽에 붙는 거미균은 위치를 직접 지정하는데,
        // 서로 밀치면 표면에서 떨어져 맵 끝까지 밀려나는 문제가 생김
        if (monsterLayer >= 0)
            Physics2D.IgnoreLayerCollision(monsterLayer, monsterLayer, true);
    }

    public void AddCell(int amount)
    {
        cellCurrency += amount;
        NotifyCurrencyChanged();
    }

    public void AddDarkCell(int amount)
    {
        darkCellCurrency += amount;
        NotifyCurrencyChanged();
    }

    void NotifyCurrencyChanged()
    {
        PlayerHUD hud = FindFirstObjectByType<PlayerHUD>();
        if (hud != null) hud.SetCurrency(cellCurrency, darkCellCurrency);
    }

    void Update()
    {
        // 분열체 조종 전환(숫자키)/분열 회수(R)도 다크셀 잔재로 능력을 해금하기 전까진 잠긴다
        if (!fissionUnlocked) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchControl(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchControl(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchControl(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchControl(3);
        if (Input.GetKeyDown(KeyCode.R)) RecallAllClones();
    }

    public void RecallAllClones()
    {
        // 본체(인덱스 0)만 남기고 분열체 전부 제거
        for (int i = allPlayers.Count - 1; i >= 1; i--)
        {
            Destroy(allPlayers[i].gameObject);
        }
        SwitchControl(0);
        UnityEngine.Debug.Log("분열체 전체 회수!");
    }

    // 지금 새 분열체를 만들 수 있는가.
    // 하드 캡이 켜져 있으면 현재 분열체 수가 최대치 미만일 때만 허용(도달하면 분열 차단).
    // 꺼져 있으면 항상 허용 — 초과 시 RegisterPlayer가 가장 오래된 분열체를 회수(기존 동작).
    public bool CanSpawnClone()
    {
        if (!hardFissionCap) return true;
        int cloneCount = allPlayers.Count - 1; // 본체(인덱스 0) 제외
        return cloneCount < maxFissionCount;
    }

    public void RegisterPlayer(PlayerController player)
    {
        if (!allPlayers.Contains(player))
        {
            // 분열체끼리만 충돌 무시, 분열체↔본체는 충돌 허용
            Collider2D newCol = player.GetComponent<Collider2D>();
            if (newCol != null && player.isClone)
            {
                foreach (var other in allPlayers)
                {
                    if (!other.isClone) continue;
                    Collider2D otherCol = other.GetComponent<Collider2D>();
                    if (otherCol != null)
                        Physics2D.IgnoreCollision(newCol, otherCol, true);
                }

                // 조직 그물망은 분열체만 통과 가능 (본체는 항상 막힘)
                TissueMesh.RegisterClone(player);
            }

            allPlayers.Add(player);

            if (allPlayers.Count == 1) // ��ü ��� ��
            {
                currentPlayer = player;
                player.isControlled = true;
            }
            else // �п�ü ��� ��
            {
                // �������ڸ��ڴ� ���� ������ ����� �� (���� ������ ����)
                player.isControlled = false;

                // ��ȹ�� �ݿ�: �п� Ƚ�� �ʰ� �� ���� ������ �п�ü(�ε��� 1) ȸ��(�ı�)
                if (!hardFissionCap && allPlayers.Count > maxFissionCount + 1)
                {
                    PlayerController oldestClone = allPlayers[1];
                    Destroy(oldestClone.gameObject); // OnDestroy���� Unregister �ڵ� ȣ���
                    UnityEngine.Debug.Log("�ִ� �п� Ƚ�� �ʰ�! ���� ������ �п�ü�� ȸ���Ǿ����ϴ�.");
                }
            }
        }
    }

    public void UnregisterPlayer(PlayerController player)
    {
        if (allPlayers.Contains(player))
        {
            if (currentPlayer == player && allPlayers.Count > 1)
            {
                SwitchControl(0); // �����ϴ� �ְ� ������ ��ü�� ����� ����
            }
            allPlayers.Remove(player);
        }
    }

    public void SwitchControl(int index)
    {
        if (index >= allPlayers.Count) return;

        // 색은 각 PlayerController가 LateUpdate에서 스스로 처리한다 (원래 스프라이트 색 보존)
        foreach (var p in allPlayers)
            p.isControlled = false;

        currentPlayer = allPlayers[index];
        currentPlayer.isControlled = true;

        UnityEngine.Debug.Log($"{index + 1}번 캐릭터로 조종 전환!");
    }
}