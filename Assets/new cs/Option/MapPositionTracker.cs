using UnityEngine;
using UnityEngine.SceneManagement;

public class MapPositionTracker : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Camera Bounds")]
    [SerializeField] private PolygonCollider2D cameraLine;

    [Header("Map UI")]
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform playerMarker;

    [Header("Area Data")]
    [SerializeField] private MapAreaData[] areas;

    [Header("Marker Offset")]
    [SerializeField] private Vector2 markerOffset;

    private MapAreaData currentArea;

    private void Start()
    {
        FindCurrentArea();
    }

    private void Update()
    {
        if (!PauseMenu.IsMapOpen)
            return;

        UpdatePlayerMarker();
    }

    private void FindCurrentArea()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        foreach (MapAreaData area in areas)
        {
            if (area.sceneName == currentScene)
            {
                currentArea = area;
                return;
            }
        }

        Debug.LogWarning(
            $"MapAreaData를 찾을 수 없습니다: {currentScene}"
        );
    }

    private void UpdatePlayerMarker()
    {
        if (player == null ||
            playerMarker == null ||
            cameraLine == null ||
            content == null ||
            currentArea == null ||
            currentArea.mapArea == null)
        {
            return;
        }

        // 실제 게임 월드에서 camera line이 차지하는 범위
        Bounds worldBounds = cameraLine.bounds;

        Vector2 playerPosition = player.position;

        // 플레이어가 현재 맵의 몇 % 위치에 있는지 계산
        float normalizedX = Mathf.InverseLerp(
            worldBounds.min.x,
            worldBounds.max.x,
            playerPosition.x
        );

        float normalizedY = Mathf.InverseLerp(
            worldBounds.min.y,
            worldBounds.max.y,
            playerPosition.y
        );

        RectTransform mapArea = currentArea.mapArea;

        // 지도 조각 내부의 위치 계산
        Rect rect = mapArea.rect;

        Vector2 localPosition = new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedY)
        );

        // MapArea의 로컬 좌표를 월드 좌표로 변환
        Vector3 worldPosition =
            mapArea.TransformPoint(localPosition);

        // 다시 Content의 로컬 좌표로 변환
        Vector3 contentPosition =
            content.InverseTransformPoint(worldPosition);

        playerMarker.anchoredPosition =
    new Vector2(contentPosition.x, contentPosition.y) + markerOffset;
    }
}