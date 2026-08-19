using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeText;

    // 마스터를 실제로 걸고 있는 인스턴스. 복제본이 파괴될 때 엉뚱하게 해제하지 않기 위한 것.
    private static AudioSettings owner;

    private void Start()
    {
        // 마스터 볼륨은 이 믹서가 맡는다고 GameSettings에 알린다.
        // (안 알리면 GameSettings가 AudioListener에도 같은 값을 걸어서 두 번 깎인다)
        if (audioMixer != null)
        {
            owner = this;
            GameSettings.SetExternalMasterVolume(true);
        }

        // 지난번에 저장해둔 값으로 시작한다. 슬라이더를 먼저 맞춰두고,
        // SetValueWithoutNotify로 넣어야 여기서 콜백이 도로 불리지 않는다.
        float saved = GameSettings.MasterVolume * 100f;
        volumeSlider.SetValueWithoutNotify(Mathf.Clamp(saved, volumeSlider.minValue, volumeSlider.maxValue));

        volumeSlider.onValueChanged.AddListener(SetVolume);

        SetVolume(volumeSlider.value);
    }

    private void OnDestroy()
    {
        if (owner != this) return;

        owner = null;
        GameSettings.SetExternalMasterVolume(false);
    }

    private void OnDisable()
    {
        // 설정창을 닫을 때 디스크에 기록한다 (드래그 중엔 메모리에만 써서 끊김을 막는다)
        GameSettings.Flush();
    }

    public void SetVolume(float value)
    {
        // 값 저장은 GameSettings가 맡는다 — 게임을 껐다 켜도 유지되게.
        GameSettings.MasterVolume = value / 100f;

        if (audioMixer != null)
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
        }

        if (volumeText != null)
            volumeText.text = $"{Mathf.RoundToInt(value)}%";
    }
}