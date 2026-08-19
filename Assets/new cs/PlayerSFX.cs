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
}