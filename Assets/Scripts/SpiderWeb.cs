using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class SpiderWeb : MonoBehaviour
{
    [Min(0f)] public float lingerDuration = 1f;
    [Range(0.05f, 1f)] public float moveMultiplier = 0.5f;
    [Range(0.05f, 1f)] public float jumpMultiplier = 0.7f;
    [Range(0.05f, 1f)] public float dashMultiplier = 0.5f;
    Collider2D area;
    void Awake() { area = GetComponent<Collider2D>(); area.isTrigger = true; }
    void FixedUpdate()
    {
        foreach (var player in GimmickPhysics.PlayersIn(area))
            player.Traversal.ApplyWeb(this, Mathf.Max(lingerDuration, Time.fixedDeltaTime * 2f),
                moveMultiplier, jumpMultiplier, dashMultiplier);
    }
}
