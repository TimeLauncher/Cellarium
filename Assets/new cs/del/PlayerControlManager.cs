using System.Collections.Generic;
using UnityEngine;

public class PlayerControlManager : MonoBehaviour
{
    public static PlayerControlManager Instance;

    [Header("관리 설정")]
    public List<PlayerControll> allPlayers = new List<PlayerControll>();
    public PlayerControll currentPlayer;

    public int maxFissionCount = 3;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchControl(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchControl(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchControl(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchControl(3);
    }

    public void RegisterPlayer(PlayerControll player)
    {
        if (player == null) return;

        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);

            if (allPlayers.Count == 1)
            {
                currentPlayer = player;
                player.isControlled = true;
                UpdateCameraTarget();
            }
            else
            {
                player.isControlled = false;

                if (allPlayers.Count > maxFissionCount + 1)
                {
                    PlayerControll oldestClone = allPlayers[1];
                    Destroy(oldestClone.gameObject);
                    Debug.Log("최대 분열 횟수 초과! 가장 오래된 분열체가 제거되었습니다.");
                }
            }
        }
    }

    public void UnregisterPlayer(PlayerControll player)
    {
        if (player == null) return;

        if (allPlayers.Contains(player))
        {
            if (currentPlayer == player && allPlayers.Count > 1)
            {
                allPlayers.Remove(player);
                SwitchControl(0);
                return;
            }

            allPlayers.Remove(player);

            if (currentPlayer == player)
                currentPlayer = null;
        }
    }

    public void SwitchControl(int index)
    {
        if (index < 0 || index >= allPlayers.Count) return;
        if (allPlayers[index] == null) return;

        foreach (var p in allPlayers)
        {
            if (p == null) continue;
            p.isControlled = false;
            p.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f);
        }

        currentPlayer = allPlayers[index];
        currentPlayer.isControlled = true;

        if (index == 0)
            currentPlayer.GetComponent<SpriteRenderer>().color = Color.white;
        else
            currentPlayer.GetComponent<SpriteRenderer>().color = Color.green;

        UpdateCameraTarget();

        Debug.Log($"{index + 1}번 캐릭터로 제어권 변경!");
    }

    void UpdateCameraTarget()
    {
        if (currentPlayer == null) return;

        if (PlayerCameraTracker.Instance != null)
            PlayerCameraTracker.Instance.SetTarget(currentPlayer.transform);
    }
}