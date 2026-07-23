using System;

[Serializable]
public class SaveData
{
    public int slotIndex;

    public string currentSceneName;
    public float playerPositionX;
    public float playerPositionY;

    public int currentHP;
    public int maxHP;

    public float playTime;

    public string saveDate;

    public SaveData(int slot)
    {
        slotIndex = slot;

        currentSceneName = "Heart A00";

        playerPositionX = 0f;
        playerPositionY = 0f;

        currentHP = 100;
        maxHP = 100;

        playTime = 0f;

        saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}