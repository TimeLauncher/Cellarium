// 좌클릭 '섭취'로 상호작용 가능한 오브젝트 공용 인터페이스 (몬스터/회복셀/다크셀 잔재 등)
public interface IConsumable
{
    bool IsConsumable { get; }
    void OnConsumed(PlayerController consumer);
}
