using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraTracker : MonoBehaviour
{
    public static PlayerCameraTracker Instance;

    [SerializeField] private CinemachineCamera cinemachineCamera;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();
    }

    public void SetTarget(Transform target)
    {
        if (cinemachineCamera == null || target == null) return;

        cinemachineCamera.Follow = target;
        cinemachineCamera.LookAt = target;
    }
}