using UnityEngine;
using UnityEngine.SceneManagement;

// 세이브포인트를 한 번도 안 찍었을 때 부활할 지점 (게임 시작 지점).
// A00에 빈 게임오브젝트를 하나 만들어 이걸 붙이고, 원하는 위치에 놓아두면 된다.
//
// RespawnManager는 자동 생성되는 싱글턴이라 인스펙터가 없다.
// 그래서 씬에 놓인 이 마커가 자기 위치를 매니저에 등록해주는 방식으로 설정한다.
// A00은 게임 시작 씬이므로, 한 번 지나가면 이후 어느 씬에서 죽어도 이 지점을 기억하고 있다.
public class DefaultRespawnPoint : MonoBehaviour
{
    [Tooltip("이 지점이 속한 씬 이름. 비우면 이 오브젝트가 놓인 씬 이름을 그대로 쓴다")]
    public string sceneNameOverride = "";

    void Awake()
    {
        string scene = string.IsNullOrEmpty(sceneNameOverride)
            ? gameObject.scene.name
            : sceneNameOverride;

        if (RespawnManager.Instance != null)
            RespawnManager.Instance.SetDefaultRespawn(scene, transform.position);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position + Vector3.down * 0.5f, transform.position + Vector3.up * 1.5f);
    }
}
