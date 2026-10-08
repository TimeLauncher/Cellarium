using UnityEngine;
using UnityEngine.Serialization;

// 배경 스프라이트용 헬퍼.
// 빈 게임오브젝트에 SpriteRenderer + 이 스크립트를 붙이고 배경 이미지를 넣으면
// ① 항상 다른 오브젝트 뒤에 그려지고 ② 카메라를 따라다니며 ③ 원하면 화면에 꽉 차게 맞춰준다.
//
// parallaxFactorX / parallaxFactorY (가로·세로 따로)
//   0   = 카메라에 완전히 고정 (하늘/먼 배경처럼 아무리 움직여도 안 흐름)
//   0.3 = 카메라보다 30%만 따라감 (원근감 있는 중간 배경)
//   1   = 월드에 고정 (일반 오브젝트와 동일. 이 스크립트가 필요 없는 경우)
//
// ★ 화면 전체 배경이 아니라 '배경 오브젝트'(멀리 있는 건물·기둥 등)를 놓을 때는
//   Center On Camera / Fit To Camera를 둘 다 끌 것. 켜두면 시작하자마자 카메라 중앙으로 끌려오고
//   화면 크기로 늘어나서 놓은 자리와 달라진다. 둘 다 끄면 '카메라가 그 오브젝트를 정면으로 볼 때'
//   씬에 놓은 위치·크기 그대로 보인다.
// ★ DefaultExecutionOrder(1000) — 반드시 카메라가 움직인 뒤에 배경을 옮겨야 한다.
//    CameraFollow / CinemachineBrain 둘 다 LateUpdate에서 카메라를 옮기는데, 실행 순서를 지정하지 않으면
//    배경이 먼저 돌아 '한 프레임 전 카메라 위치'를 기준으로 따라간다. 카메라가 Lerp로 부드럽게 움직이는 동안
//    그 1프레임 지연이 매 프레임 방향이 바뀌면서 배경이 좌우로 미세하게 떨리는 것처럼 보인다.
[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
[DefaultExecutionOrder(1000)]
public class ParallaxBackground : MonoBehaviour
{
    [Header("따라다니기")]
    [Tooltip("비우면 MainCamera를 자동으로 찾는다")]
    public Transform targetCamera;
    [Tooltip("가로 시차. 0 = 카메라에 고정, 1 = 월드에 고정")]
    [FormerlySerializedAs("parallaxFactor")]
    [Range(0f, 1f)] public float parallaxFactorX = 0f;
    [Tooltip("세로 시차. 0 = 카메라에 고정, 1 = 월드에 고정")]
    [Range(0f, 1f)] public float parallaxFactorY = 0f;
    // 예전 단일 parallaxFactor 값을 세로에도 한 번 복사했는지 (기존 씬 값 이전용)
    [SerializeField, HideInInspector] bool parallaxYMigrated;
    public bool followX = true;
    public bool followY = true;
    [Tooltip("켜면: 플레이 시작 시 배경을 카메라 정중앙으로 끌어온다 (화면 전체 배경용).\n" +
             "끄면: 씬에 놓은 위치를 기준으로 시차를 준다 — 카메라가 그 지점을 볼 때 놓은 자리에 정확히 보인다 (배경 오브젝트용)")]
    public bool centerOnCamera = true;

    [Header("그리는 순서")]
    [Tooltip("배경은 음수로 둬야 캐릭터·오브젝트 뒤에 그려진다")]
    public int sortingOrder = -100;
    [Tooltip("배경을 카메라보다 뒤(양수 Z)로 밀어 다른 스프라이트와 겹치지 않게 한다")]
    public float zDepth = 10f;

    [Header("화면 채우기")]
    [Tooltip("카메라 화면에 꽉 차도록 스케일을 자동 조절 (직교 카메라 전용)")]
    public bool fitToCamera = true;
    [Tooltip("가장자리가 비지 않도록 살짝 키우는 여유분")]
    public float fitPadding = 1.02f;

    private SpriteRenderer spr;
    private Camera cam;

    // 기준점 — 플레이 시작 시점의 배경/카메라 위치. 이 둘의 차이로 시차를 계산한다.
    private Vector3 anchorPos;
    private Vector3 anchorCamPos;
    private bool anchored;

    void OnEnable()
    {
        MigrateParallaxY();
        spr = GetComponent<SpriteRenderer>();
        anchored = false;
        ApplyLook();
    }

    void OnValidate() => MigrateParallaxY();

    // 예전엔 parallaxFactor 하나로 가로·세로를 같이 썼다. 가로는 FormerlySerializedAs로 넘어오지만
    // 세로는 새 칸이라 0이 되므로, 처음 한 번만 가로 값을 복사해 기존 씬의 움직임을 그대로 유지한다.
    void MigrateParallaxY()
    {
        if (parallaxYMigrated) return;
        parallaxFactorY = parallaxFactorX;
        parallaxYMigrated = true;
    }

    void ResolveCamera()
    {
        if (targetCamera != null)
        {
            cam = targetCamera.GetComponent<Camera>();
            if (cam != null) return;
        }

        cam = Camera.main;
        if (cam != null) targetCamera = cam.transform;
    }

    // 보이는 것에만 관여하는 부분 — 에디터에서도 돌려서 배치하면서 바로 확인할 수 있게 한다
    void ApplyLook()
    {
        if (spr == null) spr = GetComponent<SpriteRenderer>();
        if (cam == null) ResolveCamera();

        if (spr.sortingOrder != sortingOrder) spr.sortingOrder = sortingOrder;
        if (fitToCamera) FitToCamera();
    }

    void LateUpdate()
    {
        ApplyLook();

        // ★ 위치 이동은 플레이 중에만 한다.
        //   에디터에서도 움직이면 LateUpdate가 옮긴 위치를 다음 OnEnable이 기준점으로 다시 잡아
        //   씬을 열고 닫을 때마다 배경이 조금씩 밀려나 결국 화면 밖으로 사라진다.
        if (!Application.isPlaying) return;
        if (cam == null) return;

        if (!anchored)
        {
            if (centerOnCamera)
            {
                // 씬에 배경을 어디에 놓아뒀든 시작 시점엔 화면 정중앙에 오게 맞춘다.
                // (A01처럼 플레이어 시작 지점이 원점에서 멀리 떨어진 씬은, 이걸 안 하면
                //  배경이 놓인 자리와 카메라 사이의 간격이 그대로 유지돼 화면 밖에 남는다)
                anchorCamPos = cam.transform.position;
                anchorPos = new Vector3(anchorCamPos.x, anchorCamPos.y, transform.position.z);
            }
            else
            {
                // 배경 오브젝트: 기준 카메라 위치를 '오브젝트가 놓인 자리'로 잡는다.
                // ★ 예전엔 첫 프레임의 카메라 위치를 기준으로 삼았는데, 그 직후 카메라가 플레이어에게
                //   순간이동(CameraSnap)하면 그 이동량만큼 오브젝트가 같이 밀려 놓은 자리와 어긋났다.
                anchorPos = transform.position;
                anchorCamPos = new Vector3(anchorPos.x, anchorPos.y, cam.transform.position.z);
            }

            anchored = true;
        }

        // 카메라가 움직인 만큼의 (1 - 시차)를 배경도 같이 움직여 상대적으로 덜 흐르게 만든다.
        Vector3 camDelta = cam.transform.position - anchorCamPos;
        Vector3 p = anchorPos;
        if (followX) p.x = anchorPos.x + camDelta.x * (1f - parallaxFactorX);
        if (followY) p.y = anchorPos.y + camDelta.y * (1f - parallaxFactorY);

        transform.position = new Vector3(p.x, p.y, cam.transform.position.z + zDepth);
    }

    // 스프라이트 원본 크기를 카메라 화면 크기에 맞춰 스케일로 늘린다.
    void FitToCamera()
    {
        if (cam == null || !cam.orthographic) return;
        if (spr.sprite == null) return;

        Vector2 spriteSize = spr.sprite.bounds.size; // 로컬 스케일 1일 때의 월드 크기
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;

        // 가로/세로 중 더 많이 늘려야 하는 쪽에 맞춰야 빈 곳이 안 생긴다
        float scale = Mathf.Max(camW / spriteSize.x, camH / spriteSize.y) * fitPadding;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    // 설정이 빠진 채로 조용히 안 보이는 일이 잦아서, 원인을 Console에 찍어준다
    void Start()
    {
        if (!Application.isPlaying) return;

        if (spr.sprite == null)
            Debug.LogWarning($"[ParallaxBackground] '{name}': Sprite Renderer에 Sprite가 비어 있어 아무것도 안 보인다. " +
                             "PNG의 Texture Type=Sprite, Sprite Mode=Single로 임포트했는지 확인할 것.", this);

        if (cam == null)
            Debug.LogWarning($"[ParallaxBackground] '{name}': 카메라를 못 찾았다. " +
                             "MainCamera 태그가 붙은 카메라가 없으면 Target Camera에 직접 연결할 것.", this);
    }
}
