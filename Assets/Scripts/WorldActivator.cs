using UnityEngine;

// Common signal for buttons and future devices. Disabled devices never drive a target.
public class WorldActivator : MonoBehaviour
{
    public bool IsActive { get; protected set; }
    public virtual void SetActive(bool active) => IsActive = active;

    public static bool AnyActive(WorldActivator[] sources)
    {
        if (sources == null) return false;
        foreach (var source in sources)
            if (source != null && source.isActiveAndEnabled && source.IsActive) return true;
        return false;
    }
}
