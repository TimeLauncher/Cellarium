using UnityEngine;

public class PlayerSFX : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX")]
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip eatSound;
    [SerializeField] private AudioClip jumpSound;

    // 개발시트 SFX001 SFX_WhiteCell_Move / SFX003 SFX_WhiteCell_Landing
    [Header("걷기 (SFX_WhiteCell_Move)")]
    [Tooltip("땅 위에서 걷는 동안 반복 재생할 발소리")]
    [SerializeField] private AudioClip moveSound;
    [Tooltip("발소리 간격(초). 0이면 코드에서 재생하지 않는다 — 걷기 클립의 Animation Event에 PlayMoveSound를 걸어 발 닿는 프레임에 맞출 때 사용")]
    [SerializeField] private float footstepInterval = 0.3f;
    [Tooltip("발소리마다 피치를 이 범위 안에서 랜덤으로 흔든다 (같은 소리 반복이 덜 거슬리게). 0이면 그대로")]
    [SerializeField, Range(0f, 0.3f)] private float footstepPitchVariance = 0.08f;

    [Header("착지 (SFX_WhiteCell_Landing)")]
    [SerializeField] private AudioClip landingSound;
    [Tooltip("이 속도보다 빠르게 떨어지다 땅에 닿을 때만 착지음을 낸다 (턱 하나 내려오는 정도는 무시)")]
    [SerializeField] private float landingMinFallSpeed = 4f;

    private float footstepTimer;

    // 발소리 전용 소스. PlayOneShot은 멈출 수가 없어서, 걷다 멈추거나 턱을 오르내려 다시 땅을 밟을 때마다
    // 앞 발소리가 끝나기 전에 새 발소리가 겹쳐 쌓였다 (기획서 A12~A13 그 외 메모). 소스 하나로 Play/Stop 한다.
    private AudioSource footstepSource;

    public void PlayDashSound()
    {
        if (audioSource != null && dashSound != null)
            audioSource.PlayOneShot(dashSound);
    }

    public void PlayAttackSound()
    {
        if (audioSource != null && attackSound != null)
            audioSource.PlayOneShot(attackSound);
    }

    public void PlayDamageSound()
    {
        if (audioSource != null && damageSound != null)
            audioSource.PlayOneShot(damageSound);
    }

    public void PlayEatSound()
    {
        if (audioSource != null && eatSound != null)
            audioSource.PlayOneShot(eatSound);
    }

    public void PlayJumpSound()
    {
        if (audioSource != null && jumpSound != null)
            audioSource.PlayOneShot(jumpSound);
    }

    // 걷기 클립의 Animation Event에서도 직접 부를 수 있다.
    // 새 발소리는 이전 발소리를 끊고 시작한다 (겹쳐 쌓이지 않음)
    public void PlayMoveSound()
    {
        if (audioSource == null || moveSound == null) return;

        AudioSource src = GetFootstepSource();
        src.clip = moveSound;
        src.pitch = audioSource.pitch;
        if (footstepPitchVariance > 0f)
            src.pitch *= 1f + Random.Range(-footstepPitchVariance, footstepPitchVariance);
        src.Play();
    }

    // 걷기를 멈추거나 다른 행동(점프·대시 등)을 하면 즉시 발소리를 끊는다
    public void StopMoveSound()
    {
        if (footstepSource != null && footstepSource.isPlaying) footstepSource.Stop();
    }

    // 원래 소스의 볼륨·믹서 출력·3D 설정을 그대로 따라가는 발소리 전용 소스
    AudioSource GetFootstepSource()
    {
        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.loop = false;
        }
        footstepSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        footstepSource.volume = audioSource.volume;
        footstepSource.spatialBlend = audioSource.spatialBlend;
        footstepSource.priority = audioSource.priority;
        footstepSource.mute = audioSource.mute;
        return footstepSource;
    }

    // PlayerController가 매 프레임 호출 - 땅 위에서 걷는 동안 footstepInterval마다 발소리
    public void TickFootsteps(bool walking)
    {
        if (!walking)
        {
            StopMoveSound();
            footstepTimer = 0f; // 다시 걷기 시작하면 첫 걸음 소리가 바로 나게
            return;
        }
        if (footstepInterval <= 0f)
        {
            footstepTimer = 0f; // 애니메이션 이벤트로 재생하는 경우 — 멈출 때 끊는 것만 한다
            return;
        }

        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f) return;

        PlayMoveSound();
        footstepTimer = footstepInterval;
    }

    // PlayerController가 공중 -> 땅으로 바뀐 프레임에 호출. fallSpeed = 공중에 있는 동안 가장 빨랐던 낙하 속도(양수)
    public void PlayLandingSound(float fallSpeed)
    {
        if (fallSpeed < landingMinFallSpeed) return;
        if (audioSource != null && landingSound != null)
            audioSource.PlayOneShot(landingSound);
    }
}