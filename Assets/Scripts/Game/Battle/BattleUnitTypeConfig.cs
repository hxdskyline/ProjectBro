using UnityEngine;

[System.Serializable]
public struct UnitStaticAttributes
{
    [Min(1)] public int MaxHp;
    [Min(1)] public int Attack;
    [Min(0)] public int Defense;
    [Min(0.1f)] public float MoveSpeed;
    [Min(0.1f)] public float AttackRange;

    public static UnitStaticAttributes Default => new UnitStaticAttributes
    {
        MaxHp = 60,
        Attack = 12,
        Defense = 3,
        MoveSpeed = 2.2f,
        AttackRange = 1.0f
    };
}

[System.Serializable]
public class UnitRuntimeAttributes
{
    [Min(0)] public int CurrentHp;
    [Min(1)] public int MaxHp;
    [Min(1)] public int Attack;
    [Min(0)] public int Defense;
    [Min(0.1f)] public float MoveSpeed;
    [Min(0.1f)] public float AttackRange;

    // Modifier fields
    public float AttackPercentBuff;
    public int AttackFlatBuff;
    public float DefensePercentBuff;
    public int DefenseFlatBuff;
    public float SkillMultiplier; // 1.0 = normal attack, 0~10 for skills
    public int TrueDamage;        // ignores all defense

    private UnitStaticAttributes _base;

    public UnitRuntimeAttributes(UnitStaticAttributes staticAttributes)
    {
        _base = staticAttributes;
        AttackPercentBuff = 0f;
        AttackFlatBuff = 0;
        DefensePercentBuff = 0f;
        DefenseFlatBuff = 0;
        SkillMultiplier = 1f;
        TrueDamage = 0;
        MaxHp = 0;
        CurrentHp = 0;
        Attack = 1;
        Defense = 0;
        MoveSpeed = 0.1f;
        AttackRange = 0.1f;
        Recalculate();
        CurrentHp = MaxHp;
    }

    /// <summary>
    /// Recalculate final stats from base + modifiers.
    /// Formula: final = base * (1 + percentBuff) + flatBuff
    /// </summary>
    public void Recalculate()
    {
        MaxHp = Mathf.Max(1, Mathf.RoundToInt(_base.MaxHp * (1f + 0f) + 0f));
        Attack = Mathf.Max(1, Mathf.RoundToInt(_base.Attack * (1f + AttackPercentBuff) + AttackFlatBuff));
        Defense = Mathf.Max(0, Mathf.RoundToInt(_base.Defense * (1f + DefensePercentBuff) + DefenseFlatBuff));
        MoveSpeed = Mathf.Max(0.1f, _base.MoveSpeed);
        AttackRange = Mathf.Max(0.1f, _base.AttackRange);
    }
}

[CreateAssetMenu(fileName = "BattleUnitTypeConfig", menuName = "Game/Battle/Unit Type Config")]
public class BattleUnitTypeConfig : ScriptableObject
{
    [SerializeField] private int _unitTypeId;
    [SerializeField] private string _unitTypeName = "Unit";
    [SerializeField] private UnitStaticAttributes _baseAttributes = UnitStaticAttributes.Default;

    public int UnitTypeId => _unitTypeId;
    public string UnitTypeName => string.IsNullOrEmpty(_unitTypeName) ? name : _unitTypeName;
    public UnitStaticAttributes BaseAttributes => _baseAttributes;

    public UnitRuntimeAttributes CreateRuntimeAttributes()
    {
        return new UnitRuntimeAttributes(_baseAttributes);
    }
}
