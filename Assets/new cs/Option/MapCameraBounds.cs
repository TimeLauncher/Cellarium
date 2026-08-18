using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class MapCameraBounds : MonoBehaviour
{
    public PolygonCollider2D Collider { get; private set; }

    private void Awake()
    {
        Collider = GetComponent<PolygonCollider2D>();
    }
}