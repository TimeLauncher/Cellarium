using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    public WorldActivator[] activators = new WorldActivator[0];
    public Vector2 destinationOffset = new Vector2(0f, 4f);
    [Min(0.01f)] public float speed = 2f;
    Vector2 origin;
    Rigidbody2D body;
    Collider2D surface;
    readonly RaycastHit2D[] sweep = new RaycastHit2D[32];

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        surface = GetComponent<Collider2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        surface.isTrigger = false;
        origin = body.position;
    }

    void FixedUpdate()
    {
        Vector2 target = origin + (WorldActivator.AnyActive(activators) ? destinationOffset : Vector2.zero);
        Vector2 step = Vector2.MoveTowards(body.position, target, Mathf.Max(0.01f, speed) * Time.fixedDeltaTime) - body.position;
        if (step.sqrMagnitude < 0.000001f) return;
        var riders = GimmickPhysics.PlayersIn(surface, 0.16f);
        riders.RemoveAll(p => !GimmickPhysics.StandingOn(p, surface));

        // Stop before crushing a rider against a ceiling/wall.
        float fraction = 1f;
        var filter = new ContactFilter2D { useTriggers = false };
        foreach (var rider in riders)
        {
            var rb = rider.GetComponent<Rigidbody2D>();
            int count = rb.Cast(step.normalized, filter, sweep, step.magnitude + 0.02f);
            for (int i = 0; i < count; i++)
            {
                var hit = sweep[i];
                if (hit.collider == surface || hit.collider.GetComponentInParent<PlayerController>() != null ||
                    hit.collider.GetComponentInParent<MonsterBase>() != null ||
                    Vector2.Dot(hit.normal, step) >= -0.0001f) continue;
                fraction = Mathf.Min(fraction, Mathf.Max(0f, hit.distance - 0.02f) / step.magnitude);
            }
        }
        step *= fraction;
        foreach (var rider in riders) rider.GetComponent<Rigidbody2D>().position += step;
        body.MovePosition(body.position + step);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? (Vector3)origin : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, start + (Vector3)destinationOffset);
        Gizmos.DrawWireCube(start + (Vector3)destinationOffset, transform.lossyScale);
    }
}
