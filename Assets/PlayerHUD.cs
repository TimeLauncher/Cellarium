using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("체력")]
    public Slider hpSlider;

    [Header("분열 게이지 (Q홀드)")]
    public Slider fissionGauge;

    [Header("대시 쿨다운 (Image - Filled 타입)")]
    public Image dashCooldownFill;

    [Header("분열 가능 횟수 - 본체 아이콘")]
    public Image mainBodyIcon;
    public Sprite mainBodyDefaultSprite;      // 조종 중일 때
    public Sprite mainBodyUncontrolledSprite; // 비조종일 때

    [Header("분열 가능 횟수 - 분열체 아이콘 (좌→우, 소모는 우측부터)")]
    public Image[] fissionSlotIcons;
    public Sprite slotDefaultSprite;      // 미사용
    public Sprite slotControlledSprite;   // 조종 중인 분열체
    public Sprite slotUncontrolledSprite; // 존재하지만 비조종인 분열체

    [Header("보유 재화")]
    public Image cellIcon;
    public Text cellAmountText;
    public Image darkCellIcon;
    public Text darkCellAmountText;

    void Update()
    {
        PlayerManager manager = PlayerManager.Instance;
        if (manager == null) return;

        PlayerController controlled = manager.currentPlayer;
        if (controlled == null) return;

        // 체력·대시는 현재 조종 중인 캐릭터 기준
        UpdateHp(controlled);
        UpdateDashCooldown(controlled);

        // 분열 게이지는 항상 본체(allPlayers[0]) 기준
        PlayerController mainBody = manager.allPlayers.Count > 0 ? manager.allPlayers[0] : controlled;
        UpdateFissionGauge(mainBody);

        UpdateFissionIcons(manager);
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
        fissionGauge.maxValue = player.MaxFissionGauge;
        fissionGauge.value = player.CurrentFissionGauge;
    }

    void UpdateDashCooldown(PlayerController player)
    {
        if (dashCooldownFill == null) return;
        dashCooldownFill.fillAmount = player.DashCooldownProgress;
    }

    void UpdateFissionIcons(PlayerManager manager)
    {
        if (manager.allPlayers.Count == 0) return;

        if (mainBodyIcon != null)
        {
            bool mainControlled = manager.currentPlayer == manager.allPlayers[0];
            mainBodyIcon.sprite = mainControlled ? mainBodyDefaultSprite : mainBodyUncontrolledSprite;
        }

        if (fissionSlotIcons == null) return;

        int cloneCount = manager.allPlayers.Count - 1; // 본체를 제외한 분열체 수
        int totalSlots = fissionSlotIcons.Length;

        for (int i = 0; i < totalSlots; i++)
        {
            if (fissionSlotIcons[i] == null) continue;

            // 분열 가능 횟수를 소모할 때 우측부터 채워지도록 인덱스를 뒤집어서 매핑
            int fromRight = totalSlots - 1 - i;
            if (fromRight >= cloneCount)
            {
                fissionSlotIcons[i].sprite = slotDefaultSprite;
                continue;
            }

            PlayerController clone = manager.allPlayers[1 + fromRight];
            fissionSlotIcons[i].sprite = clone == manager.currentPlayer ? slotControlledSprite : slotUncontrolledSprite;
        }
    }

    // 재화 시스템 완성 전까지 외부(재화 매니저 등)에서 호출해 갱신
    public void SetCurrency(int cellCount, int darkCellCount)
    {
        if (cellAmountText != null) cellAmountText.text = cellCount.ToString();
        if (darkCellAmountText != null) darkCellAmountText.text = darkCellCount.ToString();
    }
}
