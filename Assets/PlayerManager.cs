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

    // 보유 재화
    public int cellCurrency;
    public int darkCellCurrency;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        int playerLayer = LayerMask.NameToLayer("player");
        int monsterLayer = LayerMask.NameToLayer("monster");
        if (playerLayer >= 0 && monsterLayer >= 0)
            Physics2D.IgnoreLayerCollision(playerLayer, monsterLayer, true);
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
                if (allPlayers.Count > maxFissionCount + 1)
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

        foreach (var p in allPlayers)
        {
            p.isControlled = false;
            p.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
        }

        currentPlayer = allPlayers[index];
        currentPlayer.isControlled = true;

        if (index == 0) currentPlayer.GetComponent<SpriteRenderer>().color = Color.white;
        else currentPlayer.GetComponent<SpriteRenderer>().color = Color.green;

        UnityEngine.Debug.Log($"{index + 1}�� ĳ���ͷ� ����� ����!");
    }
}