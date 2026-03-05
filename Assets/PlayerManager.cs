using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    [Header("관리 설정")]
    public List<PlayerController> allPlayers = new List<PlayerController>();
    public PlayerController currentPlayer;

    // 최초 분열 가능 횟수는 1 (나중에 업그레이드 시 최대 3으로 변경)
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

    public void RegisterPlayer(PlayerController player)
    {
        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);

            if (allPlayers.Count == 1) // 본체 등록 시
            {
                currentPlayer = player;
                player.isControlled = true;
            }
            else // 분열체 등록 시
            {
                // 생성되자마자는 조종 권한이 없어야 함 (같이 움직임 방지)
                player.isControlled = false;

                // 기획서 반영: 분열 횟수 초과 시 가장 오래된 분열체(인덱스 1) 회수(파괴)
                if (allPlayers.Count > maxFissionCount + 1)
                {
                    PlayerController oldestClone = allPlayers[1];
                    Destroy(oldestClone.gameObject); // OnDestroy에서 Unregister 자동 호출됨
                    UnityEngine.Debug.Log("최대 분열 횟수 초과! 가장 오래된 분열체가 회수되었습니다.");
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
                SwitchControl(0); // 조종하던 애가 죽으면 본체로 제어권 복귀
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

        UnityEngine.Debug.Log($"{index + 1}번 캐릭터로 제어권 변경!");
    }
}