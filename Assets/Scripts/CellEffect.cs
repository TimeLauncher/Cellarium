using UnityEngine;

public class CellEffect : MonoBehaviour
{
    [Header("획득 이펙트 설정")]
    [Tooltip("획득 시 재생할 Particle System. 비워두면 자식 오브젝트 'CellGain'을 자동으로 찾습니다.")]
    [SerializeField] private ParticleSystem cellGain;

    [Tooltip("획득 이펙트가 재생된 뒤 삭제되기까지의 시간")]
    [SerializeField] private float effectLifeTime = 1.5f;

    private bool effectPlayed = false;

    private void Awake()
    {
        // Inspector에서 직접 지정하지 않았다면
        // BigCell의 자식 중 이름이 CellGain인 오브젝트를 자동으로 찾음
        if (cellGain == null)
        {
            Transform gainTransform = transform.Find("CellGain");

            if (gainTransform != null)
            {
                cellGain = gainTransform.GetComponent<ParticleSystem>();
            }
        }

        // 게임 시작과 동시에 재생되지 않도록 정지
        if (cellGain != null)
        {
            cellGain.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 이미 한 번 재생했다면 다시 실행하지 않음
        if (effectPlayed)
            return;

        // 플레이어인지 확인
        PlayerController pc = other.GetComponent<PlayerController>();

        if (pc == null)
            return;

        effectPlayed = true;

        PlayCollectEffect();
    }

    private void PlayCollectEffect()
    {
        if (cellGain == null)
        {
            Debug.LogWarning(
                "CellEffect: CellGain Particle System을 찾지 못했습니다.",
                gameObject
            );

            return;
        }

        // 현재 위치와 회전을 유지한 채 BigCell에서 분리
        cellGain.transform.SetParent(null, true);

        // 획득 파티클 재생
        cellGain.Play();

        // 파티클이 재생될 시간을 준 뒤 CellGain만 삭제
        Destroy(cellGain.gameObject, effectLifeTime);
    }
}