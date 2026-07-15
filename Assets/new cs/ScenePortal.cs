using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ScenePortal : MonoBehaviour
{
    [Header("이동할 씬")]

#if UNITY_EDITOR
    [SerializeField] private SceneAsset nextScene;
#endif

    [SerializeField] private string nextSceneName;

    [Header("한 번만 실행")]
    public bool canTriggerOnce = true;

    private bool triggered = false;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (nextScene != null)
            nextSceneName = nextScene.name;
    }
#endif

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Trigger Enter: " + other.name);

        if (triggered && canTriggerOnce)
            return;

        PlayerControll player = other.GetComponentInParent<PlayerControll>();

        if (player == null)
            return;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("이동할 씬 이름이 비어있음! Next Scene을 인스펙터에 넣었는지 확인해줘.");
            return;
        }

        triggered = true;

        Debug.Log("씬 이동: " + nextSceneName);
        SceneManager.LoadScene(nextSceneName);
    }
}