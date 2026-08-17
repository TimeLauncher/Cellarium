using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeText;

    private void Start()
    {
        volumeSlider.onValueChanged.AddListener(SetVolume);

        SetVolume(volumeSlider.value);
    }

    public void SetVolume(float value)
    {
        if (value <= 0f)
        {
            audioMixer.SetFloat("MasterVolume", -80f);
        }
        else
        {
            float normalizedValue = value / 100f;
            float decibels = Mathf.Log10(normalizedValue) * 20f;

            audioMixer.SetFloat("MasterVolume", decibels);
        }

        if (volumeText != null)
            volumeText.text = $"{Mathf.RoundToInt(value)}%";
    }
}