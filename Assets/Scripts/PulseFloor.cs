using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PulseFloor : MonoBehaviour
{
    [Min(0f)] public float collapseDelay = 0.5f;
    [Min(0.05f)] public float restoreDelay = 2f;
    public bool IsCollapsed { get; private set; }
    Collider2D surface;
    SpriteRenderer view;
    Color baseColor;
    float timer = -1f;
    void Awake()
    {
        surface = GetComponent<Collider2D>();
        surface.isTrigger = false;
        view = GetComponent<SpriteRenderer>();
        if (view != null) baseColor = view.color;
    }
    void Update()
    {
        if (timer < 0f)
        {
            foreach (var player in GimmickPhysics.PlayersIn(surface, 0.16f))
                if (GimmickPhysics.StandingOn(player, surface)) { timer = Mathf.Max(0f, collapseDelay); break; }
            return;
        }
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        if (!IsCollapsed)
        {
            IsCollapsed = true;
            surface.isTrigger = true; // Retain the region to check safe restoration.
            timer = Mathf.Max(0.05f, restoreDelay);
            if (view != null) view.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
        }
        else
        {
            if (GimmickPhysics.PlayersIn(surface).Count > 0) { timer = 0f; return; }
            ResetFloor();
        }
    }
    void ResetFloor()
    {
        IsCollapsed = false;
        timer = -1f;
        if (surface != null) surface.isTrigger = false;
        if (view != null) view.color = baseColor;
    }
    void OnDisable() => ResetFloor();
}
