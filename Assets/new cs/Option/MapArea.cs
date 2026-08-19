using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MapArea : MonoBehaviour
{
    [Header("연결될 Scene 이름")]
    [SerializeField] private string sceneName;

    [Header("플레이어 마커 미세 보정")]
    [SerializeField] private Vector2 markerOffset;

    private MapFog fog;

    public MapFog Fog
    {
        get
        {
            if (fog == null)
            {
                fog =
                    GetComponentInChildren<MapFog>(
                        true
                    );
            }

            return fog;
        }
    }

    private RectTransform rectTransform;

    public string SceneName => sceneName;
    public Vector2 MarkerOffset => markerOffset;

    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            return rectTransform;
        }
    }
}