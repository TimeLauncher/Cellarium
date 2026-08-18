using UnityEditor;
using UnityEngine;

public class MapController : MonoBehaviour
{
    [Header("Map")]
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform playerMarker;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.15f;
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 3f;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 500f;

    
    

    private float currentZoom = 1f;

    private void Update()
    {
        if (!PauseMenu.IsMapOpen)
            return;

        HandleZoom();
        HandleMove();
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (scroll == 0f)
            return;

        currentZoom += scroll * zoomSpeed;

        currentZoom =
            Mathf.Clamp(currentZoom, minZoom, maxZoom);

        content.localScale =
            Vector3.one * currentZoom;
    }

    private void HandleMove()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector2 input =
            new Vector2(horizontal, vertical);

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        content.anchoredPosition -=
            input * moveSpeed * Time.unscaledDeltaTime;
    }
    public void CenterOnPlayer()
    {
        if (playerMarker == null || content == null || viewport == null)
            return;

        Vector3 markerWorldPos = playerMarker.position;
        Vector3 viewportCenterWorld = viewport.TransformPoint(viewport.rect.center);

        Vector3 delta = viewportCenterWorld - markerWorldPos;

        Vector3 contentWorldPos = content.position + delta;
        content.position = contentWorldPos;
    }
}