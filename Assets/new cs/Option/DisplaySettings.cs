using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DisplaySettings : MonoBehaviour
{
    [Header("해상도")]
    [SerializeField] private TMP_Text resolutionText;

    [Header("화면 모드")]
    [SerializeField] private TMP_Text screenModeText;

    [Header("화면 밝기")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TMP_Text brightnessText;
    [SerializeField] private Volume globalVolume;

    private List<Resolution> resolutions = new List<Resolution>();
    private int resolutionIndex = 0;

    private readonly FullScreenMode[] screenModes =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed
    };

    private readonly string[] screenModeNames =
    {
        "전체 화면",
        "테두리 없는 창",
        "창 모드"
    };

    private int screenModeIndex = 0;



    private void Start()
    {
        InitializeResolutions();
        InitializeScreenMode();
        InitializeBrightness();

        UpdateResolutionText();
        UpdateScreenModeText();
    }

    private void OnDisable()
    {
        // 설정창을 닫을 때 디스크에 기록한다 (드래그 중엔 메모리에만 써서 끊김을 막는다)
        GameSettings.Flush();
    }


    // =========================
    // 해상도
    // =========================

    private void InitializeResolutions()
    {
        // 지난번에 고른 해상도(GameSettings에 저장돼 있다)를 현재 항목으로 잡는다.
        // 저장된 적이 없으면 지금 화면 크기가 들어 있다.
        Vector2Int saved = GameSettings.Resolution;

        Resolution[] allResolutions = Screen.resolutions;

        foreach (Resolution resolution in allResolutions)
        {
            // 정확히 16:9만
            if (resolution.width * 9 != resolution.height * 16)
                continue;

            bool duplicate = resolutions.Exists(r =>
                r.width == resolution.width &&
                r.height == resolution.height);

            if (duplicate)
                continue;

            resolutions.Add(resolution);

            if (resolution.width == saved.x &&
                resolution.height == saved.y)
            {
                resolutionIndex = resolutions.Count - 1;
            }
        }
    }

    public void PreviousResolution()
    {
        resolutionIndex--;

        if (resolutionIndex < 0)
            resolutionIndex = resolutions.Count - 1;

        ApplyDisplaySettings();
    }

    public void NextResolution()
    {
        resolutionIndex++;

        if (resolutionIndex >= resolutions.Count)
            resolutionIndex = 0;

        ApplyDisplaySettings();
    }


    // =========================
    // 화면 모드
    // =========================

    private void InitializeScreenMode()
    {
        FullScreenMode savedMode = GameSettings.ScreenMode;

        for (int i = 0; i < screenModes.Length; i++)
        {
            if (savedMode == screenModes[i])
            {
                screenModeIndex = i;
                break;
            }
        }
    }

    public void PreviousScreenMode()
    {
        screenModeIndex--;

        if (screenModeIndex < 0)
            screenModeIndex = screenModes.Length - 1;

        ApplyDisplaySettings();
    }

    public void NextScreenMode()
    {
        screenModeIndex++;

        if (screenModeIndex >= screenModes.Length)
            screenModeIndex = 0;

        ApplyDisplaySettings();
    }


    // =========================
    // 해상도 + 화면 모드 적용
    // =========================

    private void ApplyDisplaySettings()
    {
        Resolution resolution = resolutions[resolutionIndex];
        FullScreenMode mode = screenModes[screenModeIndex];

        // Screen.SetResolution을 직접 부르지 않고 GameSettings를 거친다.
        // 그래야 고른 값이 저장돼서 다음에 게임을 켤 때도 그대로 적용된다.
        GameSettings.SetResolution(
            resolution.width,
            resolution.height,
            mode
        );

        UpdateResolutionText();
        UpdateScreenModeText();
    }


    // =========================
    // 화면 밝기
    // =========================

    private void InitializeBrightness()
    {
        // 인스펙터에 연결된 Global Volume을 GameSettings에 알려준다 (씩을 훑지 않게)
        GameSettings.SetBrightnessVolume(globalVolume);

        if (brightnessSlider == null)
            return;

        // 예전엔 globalVolume이 있을 때만 슬라이더를 연결했는데,
        // Global Volume은 Heart A00에만 있어서 다른 씩에선 밝기 조절이 아예 안 먹었다.
        // 실제로 어떻게 적용할지(URP 후처리 / 검은 판)는 GameSettings가 씩마다 알아서 고른다.
        brightnessSlider.minValue = 0;
        brightnessSlider.maxValue = 100;
        brightnessSlider.wholeNumbers = true;

        // 지난번에 저장해둔 값으로 시작 (기본 50 = 원본 밝기)
        brightnessSlider.SetValueWithoutNotify(
            Mathf.Round(GameSettings.Brightness * 100f)
        );

        brightnessSlider.onValueChanged.AddListener(SetBrightness);

        SetBrightness(brightnessSlider.value);
    }

    public void SetBrightness(float value)
    {
        // 적용과 저장은 GameSettings가 맡는다.
        // 0~100 → 0~1 (50 = 원본), GameSettings가 -2~+2 Exposure로 환산한다.
        GameSettings.Brightness = value / 100f;

        if (brightnessText != null)
            brightnessText.text = $"{Mathf.RoundToInt(value)}%";
    }


    // =========================
    // UI 텍스트
    // =========================

    private void UpdateResolutionText()
    {
        Resolution resolution = resolutions[resolutionIndex];

        resolutionText.text =
            $"{resolution.width}x{resolution.height} [16:9]";
    }

    private void UpdateScreenModeText()
    {
        screenModeText.text =
            screenModeNames[screenModeIndex];
    }
}