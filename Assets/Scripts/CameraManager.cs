using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private CinemachineCamera camera2D;

    public void FollowTarget(Transform target)
    {
        camera2D.Follow = target;
    }
}