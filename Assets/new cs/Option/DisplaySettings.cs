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

    private ColorAdjustments colorAdjustments;


    private void Start()
    {
        InitializeResolutions();
        InitializeScreenMode();
        InitializeBrightness();

        UpdateResolutionText();
        UpdateScreenModeText();
    }


    // =========================
    // 해상도
    // =========================

    private void InitializeResolutions()
    {
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

            if (resolution.width == Screen.width &&
                resolution.height == Screen.height)
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
        for (int i = 0; i < screenModes.Length; i++)
        {
            if (Screen.fullScreenMode == screenModes[i])
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

        Screen.SetResolution(
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
        if (globalVolume != null &&
            globalVolume.profile.TryGet(out colorAdjustments))
        {
            brightnessSlider.minValue = 0;
            brightnessSlider.maxValue = 100;
            brightnessSlider.wholeNumbers = true;

            brightnessSlider.value = 50;

            brightnessSlider.onValueChanged.AddListener(SetBrightness);

            SetBrightness(brightnessSlider.value);
        }
    }

    public void SetBrightness(float value)
    {
        if (colorAdjustments == null)
            return;

        // 0~100 → -2~+2 Exposure
        float exposure = Mathf.Lerp(-2f, 2f, value / 100f);

        colorAdjustments.postExposure.value = exposure;

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