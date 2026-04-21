using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerControll))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFission : MonoBehaviour
{
    [Header("분열 시스템")]
    public GameObject playerPrefab;

    [Header("분열 대시")]
    public float fissionDashSpeed = 20f;
    public float dashDuration = 0.2f;

    [Header("충돌 무시")]
    public float cloneIgnoreCollisionTime = 0.2f;

    [Header("분열 가능 여부")]
    [SerializeField] private bool canUseFission = true;

    private PlayerControll playerControll;
    private Rigidbody2D rb;
    private PlayerDashEat playerDashEat;

    private bool isDashReady;
    private bool isFissionDashing;
    private float dashTimer;

    public bool IsDashReady => isDashReady;
    public bool IsFissionDashing => isFissionDashing;
    public bool CanUseFission => canUseFission;

    void Awake()
    {
        playerControll = GetComponent<PlayerControll>();
        rb = GetComponent<Rigidbody2D>();
        playerDashEat = GetComponent<PlayerDashEat>();
    }

    void Update()
    {
        if (!playerControll.IsControlled)
            return;

        if (isFissionDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isFissionDashing = false;
                rb.gravityScale = 1f;
            }
        }

        if (!canUseFission)
            return;

        if (playerDashEat != null && playerDashEat.IsDashing)
            return;

        if (Input.GetKeyDown(KeyCode.F))
            Fission();

        if (Input.GetMouseButtonDown(1))
        {
            isFissionDashing = false;
            isDashReady = true;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        if (Input.GetMouseButtonUp(1) && isDashReady)
        {
            isDashReady = false;
            FissionDash();
        }
    }

    Vector3 GetMouseWorld(Camera cam)
    {
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = Mathf.Abs(cam.transform.position.z - transform.position.z);

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = transform.position.z;
        return mouseWorld;
    }

    public void SetAsClone()
    {
        canUseFission = false;
        isDashReady = false;
        isFissionDashing = false;
    }

    void Fission()
    {
        if (!canUseFission) return;
        if (playerPrefab == null) return;

        GameObject clone = Instantiate(playerPrefab, transform.position, Quaternion.identity);

        SetupClone(clone);

        Debug.Log("분열체 생성됨!");
    }

    void FissionDash()
    {
        if (!canUseFission) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorld = GetMouseWorld(cam);
        Vector2 dashDir = ((Vector2)(mouseWorld - transform.position)).normalized;

        if (dashDir.sqrMagnitude < 0.001f)
            return;

        rb.gravityScale = 0f;
        rb.linearVelocity = dashDir * fissionDashSpeed;

        isFissionDashing = true;
        dashTimer = dashDuration;

        if (playerPrefab != null)
        {
            GameObject clone = Instantiate(playerPrefab, transform.position, Quaternion.identity);

            SetupClone(clone);

            PlayerControll cloneCtrl = clone.GetComponent<PlayerControll>();
            Rigidbody2D cloneRb = clone.GetComponent<Rigidbody2D>();

            if (cloneCtrl != null)
                cloneCtrl.thrownTimer = dashDuration;

            if (cloneRb != null)
                cloneRb.linearVelocity = -dashDir * fissionDashSpeed;
        }

        Debug.Log("분열 대시!");
    }

    void SetupClone(GameObject clone)
    {
        if (clone == null) return;

        SpriteRenderer cloneSprite = clone.GetComponent<SpriteRenderer>();
        if (cloneSprite != null)
            cloneSprite.color = Color.green;

        PlayerFission cloneFission = clone.GetComponent<PlayerFission>();
        if (cloneFission != null)
            cloneFission.SetAsClone();

        PlayerDashEat cloneDashEat = clone.GetComponent<PlayerDashEat>();
        if (cloneDashEat != null)
            cloneDashEat.SetCanEat(false);

        IgnoreCollisionTemporarily(clone);
    }

    void IgnoreCollisionTemporarily(GameObject clone)
    {
        Collider2D[] myCols = GetComponents<Collider2D>();
        Collider2D[] cloneCols = clone.GetComponents<Collider2D>();

        foreach (var myCol in myCols)
        {
            foreach (var cloneCol in cloneCols)
            {
                if (myCol == null || cloneCol == null) continue;
                Physics2D.IgnoreCollision(myCol, cloneCol, true);
                StartCoroutine(RestoreCollision(myCol, cloneCol, cloneIgnoreCollisionTime));
            }
        }
    }

    IEnumerator RestoreCollision(Collider2D a, Collider2D b, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (a != null && b != null)
            Physics2D.IgnoreCollision(a, b, false);
    }
}