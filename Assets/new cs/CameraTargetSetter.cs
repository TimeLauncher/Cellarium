using UnityEngine;
using Unity.Cinemachine;

public class CameraTargetSetter : MonoBehaviour
{
    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        CinemachineCamera cam = GetComponent<CinemachineCamera>();

        if (cam != null)
        {
            cam.Target.TrackingTarget = player.transform;
        }
    }
}