using UnityEngine;
using UnityEngine.SceneManagement;

public class MapPositionTracker : MonoBehaviour
{
    private Transform player;

    private PolygonCollider2D cameraLine;

    private MapArea currentArea;
    [Header("Map UI")]
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform playerMarker;

    private void Start()
    {
        RefreshReferences();
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[Map] 씬 변경 감지: {scene.name}");

        RefreshReferences();
    }

    private void Update()
    {
        if (player == null)
            FindPlayer();

        UpdateFog();

        if (!PauseMenu.IsMapOpen)
            return;

        UpdatePlayerMarker();
    }

    private void RefreshReferences()
    {
        player = null;
        cameraLine = null;
        currentArea = null;

        FindPlayer();
        FindCameraBounds();
        FindCurrentMapArea();
    }

    // =========================
    // Player 자동 검색
    // =========================

    private void FindPlayer()
    {
        GameObject playerObject = null;

        try
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }
        catch
        {
            // Player 태그가 없는 경우 아래 fallback 사용
        }

        if (playerObject != null)
        {
            player = playerObject.transform;
            return;
        }

        // Player 태그 검색 실패 시 PlayerController로 검색
        PlayerController controller =
            FindFirstObjectByType<PlayerController>();

        if (controller != null)
        {
            player = controller.transform;
            return;
        }

        Debug.LogWarning(
            "[Map] 현재 Player를 찾을 수 없습니다."
        );
    }

    // =========================
    // Player Marker 자동 검색
    // =========================

 

    // =========================
    // camera line 자동 검색
    // =========================

    private void FindCameraBounds()
    {
        MapCameraBounds bounds =
            FindFirstObjectByType<MapCameraBounds>();

        if (bounds == null)
        {
            Debug.LogWarning(
                "[Map] 현재 Scene에서 MapCameraBounds를 찾을 수 없습니다."
            );

            return;
        }

        cameraLine = bounds.Collider;
    }

    // =========================
    // 현재 Scene의 지도 조각 검색
    // =========================

    private void FindCurrentMapArea()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        MapArea[] mapAreas =
    content.GetComponentsInChildren<MapArea>(true);

        Debug.Log($"[Map] 현재 Scene = {sceneName}");
        Debug.Log($"[Map] 찾은 MapArea 개수 = {mapAreas.Length}");

        foreach (MapArea area in mapAreas)
        {
            Debug.Log(
                $"[Map] 발견한 MapArea = {area.gameObject.name} / SceneName = {area.SceneName}"
            );

            if (area.SceneName == sceneName)
            {
                currentArea = area;

                playerMarker.SetParent(
                    currentArea.RectTransform,
                    false
                );

                playerMarker.anchorMin =
                    new Vector2(0.5f, 0.5f);

                playerMarker.anchorMax =
                    new Vector2(0.5f, 0.5f);

                playerMarker.pivot =
                    new Vector2(0.5f, 0.5f);

                playerMarker.localScale =
                    Vector3.one;

                playerMarker.localRotation =
                    Quaternion.identity;

                if (currentArea.Fog != null)
                {
                    currentArea.Fog.ResetTrackingPosition();

                    // 현재 플레이어 위치 즉시 공개
                    RevealFogNow();
                }

                Debug.Log($"[Map] 연결 성공 = {area.gameObject.name}");

                return;
            }
        }

        Debug.LogWarning(
            $"[Map] {sceneName}에 대응하는 MapArea를 찾을 수 없습니다."
        );
    }
    private void RevealFogNow()
    {
        if (player == null ||
            cameraLine == null ||
            currentArea == null ||
            currentArea.Fog == null)
        {
            return;
        }

        Bounds worldBounds = cameraLine.bounds;

        float normalizedX = Mathf.InverseLerp(
            worldBounds.min.x,
            worldBounds.max.x,
            player.position.x
        );

        float normalizedY = Mathf.InverseLerp(
            worldBounds.min.y,
            worldBounds.max.y,
            player.position.y
        );

        currentArea.Fog.Reveal(
            new Vector2(
                normalizedX,
                normalizedY
            )
        );
    }

    // =========================
    // Player Marker 갱신
    // =========================

    private void UpdatePlayerMarker()
    {
        if (player == null ||
            playerMarker == null ||
            cameraLine == null ||
            currentArea == null)
        {
            return;
        }

        Bounds worldBounds = cameraLine.bounds;

        Vector2 playerPosition = player.position;

        // 실제 맵에서 플레이어 위치를 0 ~ 1로 변환
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

        RectTransform areaRect = currentArea.RectTransform;
        Rect rect = areaRect.rect;

        // 현재 지도 조각 내부의 위치 계산
        float markerX = Mathf.Lerp(
            rect.xMin,
            rect.xMax,
            normalizedX
        );

        float markerY = Mathf.Lerp(
            rect.yMin,
            rect.yMax,
            normalizedY
        );

        Vector2 correctedPosition =
    new Vector2(markerX, markerY)
    + currentArea.MarkerOffset;

        playerMarker.localPosition =
            new Vector3(
                correctedPosition.x,
                correctedPosition.y,
                0f
            );
    }
    private void UpdateFog()
    {
        if (player == null ||
            cameraLine == null ||
            currentArea == null ||
            currentArea.Fog == null)
        {
            return;
        }

        Bounds worldBounds = cameraLine.bounds;

        float normalizedX = Mathf.InverseLerp(
            worldBounds.min.x,
            worldBounds.max.x,
            player.position.x
        );

        float normalizedY = Mathf.InverseLerp(
            worldBounds.min.y,
            worldBounds.max.y,
            player.position.y
        );

        currentArea.Fog.Reveal(
            new Vector2(normalizedX, normalizedY)
        );
    }
}
