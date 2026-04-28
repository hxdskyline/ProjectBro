using UnityEngine;

[System.Serializable]
public struct UnitStaticAttributes
{
    [Min(1)] public int MaxHp;
    [Min(1)] public int Attack;
    [Min(0)] public int Defense;
    [Min(0)] public int MoveSpeed;     // 整数存储，实际值 = MoveSpeed / 1000f
    [Min(0)] public int AttackSpeed;   // 整数存储，实际值 = AttackSpeed / 1000f，表示攻击冷却时间(秒)，0=从MoveSpeed推导
    [Min(0.1f)] public float AttackRange;

    public static UnitStaticAttributes Default => new UnitStaticAttributes
    {
        MaxHp = 6,
        Attack = 1,
        Defense = 0,
        MoveSpeed = 2200,
        AttackSpeed = 0,
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

    // --- 四类修正体系 ---
    // 增益百分比 (PBUFF)
    public float AttackPercentBuff;
    public float DefensePercentBuff;
    public float HpPercentBuff;
    public float SpeedPercentBuff;

    // 增益绝对值 (ABUFF)
    public int AttackFlatBuff;
    public int DefenseFlatBuff;
    public int HpFlatBuff;
    public int SpeedFlatBuff;

    // 减益百分比 (PDEBUFF)
    public float AttackPercentDebuff;
    public float DefensePercentDebuff;
    public float HpPercentDebuff;
    public float SpeedPercentDebuff;

    // 减益绝对值 (ADEBUFF)
    public int AttackFlatDebuff;
    public int DefenseFlatDebuff;
    public int HpFlatDebuff;
    public int SpeedFlatDebuff;

    // 伤害修正（用于伤害公式）
    public float DamageReceivePercentBuff;  // 受到伤害的百分比增益
    public int DamageReceiveFlatBuff;       // 受到伤害的绝对值增益

    // 技能相关
    public float SkillMultiplier; // 1.0 = normal attack, 0~10 for skills
    public int TrueDamage;        // ignores all defense

    // 速度派生属性（整数域计算，最终 /1000 转回 float）
    public float CorrectedMoveSpeed; // 实际移速 = CorrectedMoveSpeed / 1000
    public float CorrectedAttackSpeed; // 实际攻击冷却时间(秒)，直接使用

    private UnitStaticAttributes _base;

    public UnitRuntimeAttributes(UnitStaticAttributes staticAttributes)
    {
        _base = staticAttributes;
        ResetModifiers();
        MaxHp = 0;
        CurrentHp = 0;
        Attack = 1;
        Defense = 0;
        MoveSpeed = 0.1f;
        AttackRange = 0.1f;
        CorrectedMoveSpeed = 0.001f;
        CorrectedAttackSpeed = 1.0f;
        Recalculate();
        CurrentHp = MaxHp;
    }

    /// <summary>
    /// 重置所有修正值为零
    /// </summary>
    public void ResetModifiers()
    {
        AttackPercentBuff = 0f;
        DefensePercentBuff = 0f;
        HpPercentBuff = 0f;
        SpeedPercentBuff = 0f;

        AttackFlatBuff = 0;
        DefenseFlatBuff = 0;
        HpFlatBuff = 0;
        SpeedFlatBuff = 0;

        AttackPercentDebuff = 0f;
        DefensePercentDebuff = 0f;
        HpPercentDebuff = 0f;
        SpeedPercentDebuff = 0f;

        AttackFlatDebuff = 0;
        DefenseFlatDebuff = 0;
        HpFlatDebuff = 0;
        SpeedFlatDebuff = 0;

        DamageReceivePercentBuff = 0f;
        DamageReceiveFlatBuff = 0;

        SkillMultiplier = 1f;
        TrueDamage = 0;
    }

    /// <summary>
    /// Recalculate final stats from base + modifiers.
    /// Formula: final = base * (1 + ΣPBUFF - ΣPDEBUFF) + ΣABUFF - ΣADEBUFF
    /// Speed conversion: MS = CS/1000, AS(cooldown) = base/1000
    /// </summary>
    public void Recalculate()
    {
        // 修正攻击 (CATK)
        float atkPercentMod = (1f + AttackPercentBuff - AttackPercentDebuff);
        int atkFlatMod = AttackFlatBuff - AttackFlatDebuff;
        Attack = Mathf.Max(1, Mathf.RoundToInt(_base.Attack * atkPercentMod + atkFlatMod));

        // 修正防御 (CDEF)
        float defPercentMod = (1f + DefensePercentBuff - DefensePercentDebuff);
        int defFlatMod = DefenseFlatBuff - DefenseFlatDebuff;
        Defense = Mathf.Max(0, Mathf.RoundToInt(_base.Defense * defPercentMod + defFlatMod));

        // 修正血量 (CHP)
        float hpPercentMod = (1f + HpPercentBuff - HpPercentDebuff);
        int hpFlatMod = HpFlatBuff - HpFlatDebuff;
        int prevMaxHp = MaxHp;
        MaxHp = Mathf.Max(1, Mathf.RoundToInt(_base.MaxHp * hpPercentMod + hpFlatMod));

        // 同步 CurrentHp
        if (prevMaxHp > 0 && MaxHp != prevMaxHp)
        {
            CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp);
        }

        // 修正速度 (CS) - 在整数域计算
        float spdPercentMod = (1f + SpeedPercentBuff - SpeedPercentDebuff);
        int spdFlatMod = SpeedFlatBuff - SpeedFlatDebuff;
        float correctedSpeed = _base.MoveSpeed * spdPercentMod + spdFlatMod;
        correctedSpeed = Mathf.Max(1f, correctedSpeed);

        // 速度派生: 实际值 = CS / 1000
        CorrectedMoveSpeed = Mathf.Max(0.001f, correctedSpeed / 1000f);
        // 攻速冷却: 优先使用静态配置的 AttackSpeed(冷却时间)，否则从移速推导
        if (_base.AttackSpeed > 0)
            CorrectedAttackSpeed = Mathf.Max(0.1f, _base.AttackSpeed / 1000f);
        else
            CorrectedAttackSpeed = Mathf.Max(0.1f, 2000f / correctedSpeed);

        MoveSpeed = CorrectedMoveSpeed;
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
