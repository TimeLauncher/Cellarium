using UnityEngine;

// 결절의 코어 — 섭취(IConsumable)를 결절에 전달한다. HeartNode가 Awake에서 자동으로 붙인다.
public class HeartNodeCore : MonoBehaviour, IConsumable
{
    [HideInInspector] public HeartNode owner;

    public bool IsConsumable => owner != null && !owner.IsActivated && isActiveAndEnabled;

    // PlayerController가 이 호출 직후 코어 오브젝트를 Destroy한다 = 코어가 사라지는 연출
    public void OnConsumed(PlayerController consumer)
    {
        if (owner != null) owner.OnCoreConsumed(consumer);
    }
}
