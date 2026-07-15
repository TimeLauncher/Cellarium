using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("HP 바")]
    public Slider hpSlider;

    [Header("분열 게이지 (Q홀드)")]
    public Slider fissionGauge;

    [Header("대시 쿨다운 (Image - Filled 타입)")]
    public Image dashCooldownFill;

    [Header("분열체 슬롯 (인덱스 0=본체, 1~3=분열체)")]
    public Image[] fissionSlots = new Image[4];
    public Color slotActive = Color.white;
    public Color slotExists = Color.green;
    public Color slotEmpty = new Color(1f, 1f, 1f, 0.15f);

    void Update()
    {
        PlayerManager manager = PlayerManager.Instance;
        if (manager == null) return;

        PlayerController controlled = manager.currentPlayer;
        if (controlled == null) return;

        UpdateHp(controlled);
        UpdateFissionGauge(controlled);
        UpdateDashCooldown(controlled);
        UpdateFissionSlots(manager);
    }

    void UpdateHp(PlayerController player)
    {
        if (hpSlider == null) return;
        hpSlider.maxValue = player.maxHp;
        hpSlider.value = player.CurrentHp;
    }

    void UpdateFissionGauge(PlayerController player)
    {
        if (fissionGauge == null) return;
        fissionGauge.value = player.FissionHoldProgress;
    }

    void UpdateDashCooldown(PlayerController player)
    {
        if (dashCooldownFill == null) return;
        dashCooldownFill.fillAmount = player.DashCooldownProgress;
    }

    void UpdateFissionSlots(PlayerManager manager)
    {
        for (int i = 0; i < fissionSlots.Length; i++)
        {
            if (fissionSlots[i] == null) continue;

            if (i >= manager.allPlayers.Count)
                fissionSlots[i].color = slotEmpty;
            else if (manager.allPlayers[i] == manager.currentPlayer)
                fissionSlots[i].color = slotActive;
            else
                fissionSlots[i].color = slotExists;
        }
    }
}
