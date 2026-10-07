using System.Collections.Generic;
using UnityEngine;

public static class GimmickPhysics
{
    public static List<PlayerController> PlayersIn(Collider2D area, float margin = 0f)
    {
        var players = new List<PlayerController>();
        if (area == null || !area.enabled) return players;
        foreach (var hit in Physics2D.OverlapBoxAll(area.bounds.center,
                     (Vector2)area.bounds.size + Vector2.one * margin, 0f))
        {
            if (hit.isTrigger) continue;
            var player = hit.GetComponentInParent<PlayerController>();
            if (player == null || players.Contains(player)) continue;
            var distance = area.Distance(hit);
            if (distance.isOverlapped || distance.distance <= margin) players.Add(player);
        }
        return players;
    }

    public static bool StandingOn(PlayerController player, Collider2D surface)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb == null || rb.linearVelocity.y > 0.1f) return false;
        foreach (var body in player.GetComponents<Collider2D>())
        {
            if (!body.enabled || body.isTrigger) continue;
            Bounds p = body.bounds, s = surface.bounds;
            if (p.max.x > s.min.x && p.min.x < s.max.x &&
                Mathf.Abs(p.min.y - s.max.y) <= 0.15f) return true;
        }
        return false;
    }
}
