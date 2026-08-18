using UnityEngine;
using UnityEngine.UI;

public class MapFog : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage fogImage;


    [Header("Texture")]
    [SerializeField] private int textureWidth = 512;
    [SerializeField] private int textureHeight = 512;

    [Header("Reveal")]
    [SerializeField] private int revealRadius = 24;

    [Range(0f, 1f)]
    [SerializeField] private float softEdgeStart = 0.65f;

    [SerializeField] private float revealSpacing = 0.01f;

    private Texture2D fogTexture;
    private Color32[] pixels;

    private Vector2 previousPosition;
    private bool hasPreviousPosition;

    private void Awake()
    {
        InitializeFog();
    }

    private void InitializeFog()
    {
        // 이미 초기화되어 있으면 다시 만들지 않음
        if (fogTexture != null && pixels != null)
        {
            return;
        }

        if (fogImage == null)
        {
            fogImage = GetComponent<RawImage>();
        }

        if (fogImage == null)
        {
            Debug.LogError(
                $"[MapFog] {gameObject.name}에 RawImage가 없습니다."
            );
            return;
        }

        fogTexture = new Texture2D(
            textureWidth,
            textureHeight,
            TextureFormat.RGBA32,
            false
        );

        fogTexture.wrapMode = TextureWrapMode.Clamp;

        pixels = new Color32[
            textureWidth * textureHeight
        ];

        Color32 fogColor =
            new Color32(0, 0, 0, 255);

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = fogColor;
        }

        fogTexture.SetPixels32(pixels);
        fogTexture.Apply(false);

        fogImage.texture = fogTexture;
    }

    public void Reveal(Vector2 normalizedPosition)
    {
        // 비활성 상태 등으로 Awake가 제대로 처리되지 않았을 경우
        if (fogTexture == null || pixels == null)
        {
            InitializeFog();
        }

        if (fogTexture == null || pixels == null)
            return;

        normalizedPosition.x =
            Mathf.Clamp01(normalizedPosition.x);

        normalizedPosition.y =
            Mathf.Clamp01(normalizedPosition.y);

        // 첫 지점
        if (!hasPreviousPosition)
        {
            RevealCircle(normalizedPosition);

            previousPosition =
                normalizedPosition;

            hasPreviousPosition = true;

            ApplyTexture();

            return;
        }

        float distance =
            Vector2.Distance(
                previousPosition,
                normalizedPosition
            );

        int steps = Mathf.Max(
            1,
            Mathf.CeilToInt(
                distance / revealSpacing
            )
        );

        // 이전 위치에서 현재 위치까지 이어서 지움
        for (int i = 1; i <= steps; i++)
        {
            float t =
                (float)i / steps;

            Vector2 position =
                Vector2.Lerp(
                    previousPosition,
                    normalizedPosition,
                    t
                );

            RevealCircle(position);
        }

        previousPosition =
            normalizedPosition;

        // 중요:
        // Reveal될 때 즉시 텍스처 갱신
        ApplyTexture();
    }

    private void RevealCircle(
        Vector2 normalizedPosition
    )
    {
        if (pixels == null)
            return;

        int centerX =
            Mathf.RoundToInt(
                normalizedPosition.x
                * (textureWidth - 1)
            );

        int centerY =
            Mathf.RoundToInt(
                normalizedPosition.y
                * (textureHeight - 1)
            );

        for (
            int y = -revealRadius;
            y <= revealRadius;
            y++
        )
        {
            for (
                int x = -revealRadius;
                x <= revealRadius;
                x++
            )
            {
                float distance =
                    Mathf.Sqrt(
                        x * x + y * y
                    );

                if (distance > revealRadius)
                    continue;

                int pixelX =
                    centerX + x;

                int pixelY =
                    centerY + y;

                if (
                    pixelX < 0 ||
                    pixelX >= textureWidth ||
                    pixelY < 0 ||
                    pixelY >= textureHeight
                )
                {
                    continue;
                }

                float normalizedDistance =
                    distance / revealRadius;

                byte targetAlpha;

                if (
                    normalizedDistance
                    <= softEdgeStart
                )
                {
                    // 중앙 완전 공개
                    targetAlpha = 0;
                }
                else
                {
                    // 가장자리 부드럽게
                    float edgeT =
                        Mathf.InverseLerp(
                            softEdgeStart,
                            1f,
                            normalizedDistance
                        );

                    targetAlpha =
                        (byte)Mathf.Lerp(
                            0,
                            255,
                            edgeT
                        );
                }

                int index =
                    pixelY * textureWidth
                    + pixelX;

                // 이미 더 많이 공개된 영역은 유지
                if (targetAlpha < pixels[index].a)
                {
                    pixels[index] =
                        new Color32(
                            0,
                            0,
                            0,
                            targetAlpha
                        );
                }
            }
        }
    }

    private void ApplyTexture()
    {
        if (fogTexture == null || pixels == null)
            return;

        fogTexture.SetPixels32(pixels);
        fogTexture.Apply(false);
    }

    public void ResetTrackingPosition()
    {
        hasPreviousPosition = false;
    }
}