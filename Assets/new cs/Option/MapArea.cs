using UnityEngine;

public class MapArea : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public string SceneName => sceneName;
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
    }
}