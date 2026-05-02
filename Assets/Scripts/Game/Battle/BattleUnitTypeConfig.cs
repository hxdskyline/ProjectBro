using UnityEngine;
using System.Collections.Generic;
using TribeSystem;
using TribeSystem.BuffEffects;

/// <summary>
/// TickBuffs 的返回结果 —— 由 BattleSimulation 消费
/// </summary>
public struct BuffTickResult
{
    public int dotDamage;           // DoT 总伤害（毒/流血/燃烧）
    public float freezeDuration;    // 冻结时间增量（>0 表示施加冻结）
    public bool needsRecalculate;   // 是否需要调用 Recalculate()

    public static BuffTickResult Empty => new BuffTickResult();
}

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

    // ── 战斗上下文（由 BattleSpawner 设置，用于 IBuffEffect 回调） ──
    [System.NonSerialized] public BattleFighter OwnerFighter;
    [System.NonSerialized] public BattleFighter[] Allies;
    [System.NonSerialized] public BattleFighter[] Enemies;

    // ── 统一 buff 列表（战斗内，TickBuffs 驱动） ──
    [System.NonSerialized] private List<UnifiedBuff> _activeBuffs;
    public List<UnifiedBuff> ActiveBuffs
    {
        get
        {
            if (_activeBuffs == null) _activeBuffs = new List<UnifiedBuff>();
            return _activeBuffs;
        }
    }

    /// <summary>
    /// 添加一个 buff 到战斗单位（自动叠加/刷新）
    /// </summary>
    public void ApplyBuff(UnifiedBuff buff)
    {
        if (buff == null) return;
        bool existed = false;
        for (int i = 0; i < ActiveBuffs.Count; i++)
        {
            if (ActiveBuffs[i].buffId == buff.buffId)
            {
                ActiveBuffs[i].TryStackOrRefresh(buff);
                existed = true;
                break;
            }
        }
        if (!existed)
        {
            ActiveBuffs.Add(buff.Clone());
            // 触发 IBuffEffect.OnBattleStart
            if (buff.gameEffectType > 0)
            {
                var effect = BuffEffectRegistry.Get(buff.gameEffectType);
                if (effect != null)
                {
                    var ctx = new BuffEffectContext(OwnerFighter, OwnerFighter, buff, Allies, Enemies);
                    effect.OnBattleStart(ctx);
                }
            }
        }
        // 立即应用被动效果（减速/标记等）
        ApplyBuffPassiveEffect(buff);
    }

    /// <summary>
    /// 移除指定 buffId 的 buff
    /// </summary>
    public bool RemoveBuff(string buffId)
    {
        for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
        {
            if (ActiveBuffs[i].buffId == buffId)
            {
                ActiveBuffs.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 移除所有指定来源的 buff
    /// </summary>
    public int RemoveBuffBySource(string sourceId)
    {
        int removed = 0;
        for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
        {
            if (ActiveBuffs[i].sourceId == sourceId)
            {
                ActiveBuffs.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    /// <summary>
    /// 每帧 tick 所有 buff：递减 duration、执行 DoT、移除过期 buff。
    /// 返回 DoT 伤害和冻结信息，由 BattleSimulation 消费。
    /// </summary>
    public BuffTickResult TickBuffs(float deltaTime)
    {
        var result = BuffTickResult.Empty;
        if (_activeBuffs == null || _activeBuffs.Count == 0) return result;

        // 每帧重新计算减速总量（因为多个减速 buff 可能在不同时间过期）
        RecalculateSlowDebuff();

        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            var buff = _activeBuffs[i];

            // 递减持续时间
            if (!buff.IsPermanent)
            {
                buff.remainingDuration -= deltaTime;
            }

            // tick 效果（DoT 等）
            if (buff.tickInterval > 0f)
            {
                buff.tickTimer -= deltaTime;
                if (buff.tickTimer <= 0f)
                {
                    buff.tickTimer = buff.tickInterval;
                    ApplyTickEffect(buff, ref result);
                }
            }

            // IBuffEffect.OnTick 回调
            if (buff.gameEffectType > 0)
            {
                var effect = BuffEffectRegistry.Get(buff.gameEffectType);
                if (effect != null)
                {
                    var ctx = new BuffEffectContext(OwnerFighter, OwnerFighter, buff, Allies, Enemies);
                    effect.OnTick(ctx, deltaTime);
                }
            }

            // 移除过期 buff
            if (buff.IsExpired)
            {
                OnBuffExpired(buff, ref result);
                _activeBuffs.RemoveAt(i);
            }
        }

        return result;
    }

    /// <summary>
    /// 清除所有战斗内 buff
    /// </summary>
    public int ClearBattleBuffs()
    {
        if (_activeBuffs == null) return 0;
        int removed = 0;
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            if (_activeBuffs[i].persistence == BuffPersistence.BattleOnly)
            {
                _activeBuffs.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    private void ApplyTickEffect(UnifiedBuff buff, ref BuffTickResult result)
    {
        switch (buff.gameEffect)
        {
            case GameEffect.Poison:
            case GameEffect.Bleed:
            case GameEffect.Burn:
                // DoT: effectParam1 = 每秒伤害，currentStacks = 层数
                result.dotDamage += Mathf.RoundToInt(buff.effectParam1 * buff.currentStacks);
                break;
        }
    }

    private void OnBuffExpired(UnifiedBuff buff, ref BuffTickResult result)
    {
        // IBuffEffect.OnExpire 回调
        if (buff.gameEffectType > 0)
        {
            var effect = BuffEffectRegistry.Get(buff.gameEffectType);
            if (effect != null)
            {
                var ctx = new BuffEffectContext(OwnerFighter, OwnerFighter, buff, Allies, Enemies);
                effect.OnExpire(ctx);
            }
        }

        // 过期时回退被动效果
        switch (buff.gameEffect)
        {
            case GameEffect.Slow:
                // 减速过期：重新计算总减速量
                RecalculateSlowDebuff();
                result.needsRecalculate = true;
                break;
            case GameEffect.Freeze:
                // 冻结过期：不需要特殊处理，FreezeTimer 自然递减
                break;
            case GameEffect.HuntMark:
                // 狩猎标记过期：移除易伤
                RecalculateHuntMarkDebuff();
                result.needsRecalculate = true;
                break;
        }
    }

    /// <summary>
    /// 重新计算所有减速 buff 的总 SpeedPercentDebuff
    /// </summary>
    private void RecalculateSlowDebuff()
    {
        float totalSlow = 0f;
        if (_activeBuffs != null)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].gameEffect == GameEffect.Slow && !_activeBuffs[i].IsExpired)
                {
                    // effectParam1 = 减速百分比（如 0.2 = -20%），currentStacks = 层数
                    totalSlow += _activeBuffs[i].effectParam1 * _activeBuffs[i].currentStacks;
                }
            }
        }
        SpeedPercentDebuff = Mathf.Min(totalSlow, 0.9f); // 上限 90% 减速
    }

    /// <summary>
    /// 重新计算所有狩猎标记的总 DamageReceivePercentBuff
    /// </summary>
    private void RecalculateHuntMarkDebuff()
    {
        float totalMarkBonus = 0f;
        if (_activeBuffs != null)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].gameEffect == GameEffect.HuntMark && !_activeBuffs[i].IsExpired)
                {
                    totalMarkBonus += _activeBuffs[i].effectParam1;
                }
            }
        }
        DamageReceivePercentBuff = totalMarkBonus;
    }

    /// <summary>
    /// 攻击命中目标时，触发所有活跃 buff 的 IBuffEffect.OnAttackHit 回调。
    /// 由 BattleSimulation 在攻击结算后调用。
    /// </summary>
    public void TriggerAttackEffects(BattleFighter target)
    {
        if (_activeBuffs == null || OwnerFighter == null) return;
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            var buff = _activeBuffs[i];
            if (buff.gameEffectType <= 0) continue;
            var effect = BuffEffectRegistry.Get(buff.gameEffectType);
            if (effect == null) continue;
            var ctx = new BuffEffectContext(OwnerFighter, target, buff, Allies, Enemies);
            effect.OnAttackHit(ctx);
        }
    }

    /// <summary>
    /// 击杀敌人时，触发所有活跃 buff 的 IBuffEffect.OnKill 回调。
    /// 由 BattleSimulation 在击杀结算后调用。
    /// </summary>
    public void TriggerKillEffects(BattleFighter killed)
    {
        if (_activeBuffs == null || OwnerFighter == null) return;
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            var buff = _activeBuffs[i];
            if (buff.gameEffectType <= 0) continue;
            var effect = BuffEffectRegistry.Get(buff.gameEffectType);
            if (effect == null) continue;
            var ctx = new BuffEffectContext(OwnerFighter, killed, buff, Allies, Enemies);
            effect.OnKill(ctx);
        }
    }

    /// <summary>
    /// 自身死亡时，触发所有活跃 buff 的 IBuffEffect.OnDeath 回调。
    /// 由 BattleSimulation 在死亡结算后调用。
    /// </summary>
    public void TriggerDeathEffects()
    {
        if (_activeBuffs == null || OwnerFighter == null) return;
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            var buff = _activeBuffs[i];
            if (buff.gameEffectType <= 0) continue;
            var effect = BuffEffectRegistry.Get(buff.gameEffectType);
            if (effect == null) continue;
            var ctx = new BuffEffectContext(OwnerFighter, OwnerFighter, buff, Allies, Enemies);
            effect.OnDeath(ctx);
        }
    }

    /// <summary>
    /// 当新 buff 被添加时，立即应用其被动效果（减速/标记等）
    /// </summary>
    public void ApplyBuffPassiveEffect(UnifiedBuff buff)
    {
        switch (buff.gameEffect)
        {
            case GameEffect.Slow:
                RecalculateSlowDebuff();
                Recalculate();
                break;
            case GameEffect.HuntMark:
                RecalculateHuntMarkDebuff();
                Recalculate();
                break;
            case GameEffect.Freeze:
                // 冻结由 BattleSimulation 设置 FreezeTimer，这里不需要处理
                break;
        }
    }

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
