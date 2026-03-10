using UnityEngine;

[System.Serializable]
public struct UnitStaticAttributes
{
    [Min(1)] public int MaxHp;
    [Min(1)] public int Attack;
    [Min(0)] public int Defense;
    [Min(0.1f)] public float MoveSpeed;
    [Min(0.1f)] public float AttackRange;
}

[System.Serializable]
public class UnitRuntimeAttributes
{
    [Min(0)] public int CurrentHp;
    [Min(1)] public int Attack;
    [Min(0)] public int Defense;
    [Min(0.1f)] public float MoveSpeed;
    [Min(0.1f)] public float AttackRange;

    public UnitRuntimeAttributes(UnitStaticAttributes staticAttributes)
    {
        CurrentHp = Mathf.Max(0, staticAttributes.MaxHp);
        Attack = Mathf.Max(1, staticAttributes.Attack);
        Defense = Mathf.Max(0, staticAttributes.Defense);
        MoveSpeed = Mathf.Max(0.1f, staticAttributes.MoveSpeed);
        AttackRange = Mathf.Max(0.1f, staticAttributes.AttackRange);
    }
}

[CreateAssetMenu(fileName = "BattleUnitTypeConfig", menuName = "Game/Battle/Unit Type Config")]
public class BattleUnitTypeConfig : ScriptableObject
{
    [SerializeField] private int _unitTypeId;
    [SerializeField] private string _unitTypeName = "Unit";
    [SerializeField] private UnitStaticAttributes _baseAttributes = new UnitStaticAttributes
    {
        MaxHp = 60,
        Attack = 12,
        Defense = 3,
        MoveSpeed = 2.2f,
        AttackRange = 1.0f
    };

    public int UnitTypeId => _unitTypeId;
    public string UnitTypeName => string.IsNullOrEmpty(_unitTypeName) ? name : _unitTypeName;
    public UnitStaticAttributes BaseAttributes => _baseAttributes;

    public UnitRuntimeAttributes CreateRuntimeAttributes()
    {
        return new UnitRuntimeAttributes(_baseAttributes);
    }
}
