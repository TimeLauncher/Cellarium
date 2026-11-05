using UnityEngine;

public class PersistentPlayerRoot : MonoBehaviour
{
    private static PersistentPlayerRoot instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}