using UnityEngine;

public class HitEffectPreview : MonoBehaviour
{
    [Header("피격 이펙트")]
    [SerializeField] private ParticleSystem hitSpark;
    [SerializeField] private Animator hitFlash;

    [Header("테스트")]
    [SerializeField] private KeyCode testKey = KeyCode.H;

    private void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            PlayEffect();
        }
    }

    public void PlayEffect()
    {
        if (hitSpark != null)
        {
            hitSpark.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            hitSpark.Play();
        }

        if (hitFlash != null)
        {
            hitFlash.Play(0, 0, 0f);
        }
    }
}
