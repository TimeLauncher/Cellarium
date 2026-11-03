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

    // 걷기 클립의 Animation Event에서도 직접 부를 수 있다
    public void PlayMoveSound()
    {
        if (audioSource == null || moveSound == null) return;

        // PlayOneShot은 AudioSource의 pitch를 따르므로 잠깐 바꿨다가 되돌린다
        float basePitch = audioSource.pitch;
        if (footstepPitchVariance > 0f)
            audioSource.pitch = basePitch * (1f + Random.Range(-footstepPitchVariance, footstepPitchVariance));
        audioSource.PlayOneShot(moveSound);
        audioSource.pitch = basePitch;
    }

    // PlayerController가 매 프레임 호출 - 땅 위에서 걷는 동안 footstepInterval마다 발소리
    public void TickFootsteps(bool walking)
    {
        if (!walking || footstepInterval <= 0f)
        {
            footstepTimer = 0f; // 다시 걷기 시작하면 첫 걸음 소리가 바로 나게
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