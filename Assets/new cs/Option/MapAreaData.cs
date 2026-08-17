using UnityEngine;

[System.Serializable]
public class MapAreaData
{
    [Header("Scene")]
    public string sceneName;

    [Header("Map Piece")]
    public RectTransform mapArea;
}