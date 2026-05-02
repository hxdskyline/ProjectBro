using UnityEngine;
using System.Collections.Generic;
using TribeSystem.BuffEffects;

namespace TribeSystem
{
    /// <summary>
    /// TickBuffs 的返回结果 —— 由 BattleSimulation 消费
    /// </summary>
    public struct BuffTickResult
    {
        public int dotDamage;
        public float freezeDuration;
        public bool needsRecalculate;

        public static BuffTickResult Empty => new BuffTickResult();
    }

    [System.Serializable]
    public struct UnitStaticAttributes
    {
        [Min(1)] public int MaxHp;
        [Min(1)] public int Attack;
        [Min(0)] public int Defense;
        [Min(0)] public int MoveSpeed;
        [Min(0)] public int AttackSpeed;
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
        public float AttackPercentBuff;
        public float DefensePercentBuff;
        public float HpPercentBuff;
        public float SpeedPercentBuff;
        public float AttackSpeedPercentBuff;

        public int AttackFlatBuff;
        public int DefenseFlatBuff;
        public int HpFlatBuff;
        public int SpeedFlatBuff;

        public float AttackPercentDebuff;
        public float DefensePercentDebuff;
        public float HpPercentDebuff;
        public float SpeedPercentDebuff;

        public int AttackFlatDebuff;
        public int DefenseFlatDebuff;
        public int HpFlatDebuff;
        public int SpeedFlatDebuff;

        // 伤害修正
        public float DamageReceivePercentBuff;
        public int DamageReceiveFlatBuff;

        // 技能相关
        public float SkillMultiplier;
        public int TrueDamage;

        // 速度派生属性
        public float CorrectedMoveSpeed;
        public float CorrectedAttackSpeed;

        // ── 战斗上下文（由 BattleSpawner 设置，用于 IBuffEffect 回调） ──
        [System.NonSerialized] public IBattleUnit OwnerFighter;
        [System.NonSerialized] public IBattleUnit[] Allies;
        [System.NonSerialized] public IBattleUnit[] Enemies;

        // ── 统一 buff 列表 ──
        [System.NonSerialized] private List<UnifiedBuff> _activeBuffs;
        public List<UnifiedBuff> ActiveBuffs
        {
            get
            {
                if (_activeBuffs == null) _activeBuffs = new List<UnifiedBuff>();
                return _activeBuffs;
            }
        }

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
                var clone = buff.Clone();
                ActiveBuffs.Add(clone);
                if (clone.gameEffectType > 0)
                {
                    var effect = BuffEffectRegistry.Get(clone.gameEffectType);
                    if (effect != null)
                    {
                        var ctx = new BuffEffectContext(OwnerFighter, OwnerFighter, clone, Allies, Enemies);
                        effect.OnBattleStart(ctx);
                    }
                }
            }
            ApplyBuffPassiveEffect(buff);
        }

        public UnifiedBuff GetBuff(string buffId)
        {
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].buffId == buffId)
                    return ActiveBuffs[i];
            }
            return null;
        }

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

        public BuffTickResult TickBuffs(float deltaTime)
        {
            var result = BuffTickResult.Empty;
            if (_activeBuffs == null || _activeBuffs.Count == 0) return result;

            RecalculateSlowDebuff();

            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];

                if (!buff.IsPermanent)
                {
                    buff.remainingDuration -= deltaTime;
                }

                if (buff.tickInterval > 0f)
                {
                    buff.tickTimer -= deltaTime;
                    if (buff.tickTimer <= 0f)
                    {
                        buff.tickTimer = buff.tickInterval;
                        ApplyTickEffect(buff, ref result);
                    }
                }

                if (buff.gameEffectType > 0)
                {
                    var effect = BuffEffectRegistry.Get(buff.gameEffectType);
                    if (effect != null)
                    {
                        var ctx = new BuffEffectContext(OwnerFighter, OwnerFighter, buff, Allies, Enemies);
                        effect.OnTick(ctx, deltaTime);
                    }
                }

                if (buff.IsExpired)
                {
                    Debug.Log($"[TickBuffs] buff 过期移除: buffId={buff.buffId}, gameEffectType={buff.gameEffectType}, remainingDuration={buff.remainingDuration}");
                    OnBuffExpired(buff, ref result);
                    _activeBuffs.RemoveAt(i);
                }
            }

            return result;
        }

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
                    result.dotDamage += Mathf.RoundToInt(buff.effectParam1 * buff.currentStacks);
                    break;
            }
        }

        private void OnBuffExpired(UnifiedBuff buff, ref BuffTickResult result)
        {
            if (buff.gameEffectType > 0)
            {
                var effect = BuffEffectRegistry.Get(buff.gameEffectType);
                if (effect != null)
                {
                    var ctx = new BuffEffectContext(OwnerFighter, OwnerFighter, buff, Allies, Enemies);
                    effect.OnExpire(ctx);
                }
            }

            switch (buff.gameEffect)
            {
                case GameEffect.Slow:
                    RecalculateSlowDebuff();
                    result.needsRecalculate = true;
                    break;
                case GameEffect.Freeze:
                    break;
                case GameEffect.HuntMark:
                    RecalculateHuntMarkDebuff();
                    result.needsRecalculate = true;
                    break;
            }

            // buff 过期后重新计算属性（确保移除的属性加成生效）
            result.needsRecalculate = true;
        }

        private void RecalculateSlowDebuff()
        {
            float totalSlow = 0f;
            if (_activeBuffs != null)
            {
                for (int i = 0; i < _activeBuffs.Count; i++)
                {
                    if (_activeBuffs[i].gameEffect == GameEffect.Slow && !_activeBuffs[i].IsExpired)
                    {
                        totalSlow += _activeBuffs[i].effectParam1 * _activeBuffs[i].currentStacks;
                    }
                }
            }
            SpeedPercentDebuff = Mathf.Min(totalSlow, 0.9f);
        }

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

        public void TriggerAttackEffects(IBattleUnit target)
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

        public void TriggerKillEffects(IBattleUnit killed)
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

        public void ResetModifiers()
        {
            AttackPercentBuff = 0f;
            DefensePercentBuff = 0f;
            HpPercentBuff = 0f;
            SpeedPercentBuff = 0f;
            AttackSpeedPercentBuff = 0f;

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

        public void Recalculate()
        {
            // 从 ActiveBuffs 汇总属性修正（临时变量，不写字段）
            float abAtkPct = 0f, abDefPct = 0f, abHpPct = 0f, abSpdPct = 0f, abAtkSpdPct = 0f;
            int abAtkFlat = 0, abDefFlat = 0, abHpFlat = 0, abSpdFlat = 0;

            if (_activeBuffs != null)
            {
                for (int i = 0; i < _activeBuffs.Count; i++)
                {
                    var buff = _activeBuffs[i];
                    if (buff.IsExpired) continue;
                    float totalVal = buff.value * buff.currentStacks;
                    switch (buff.statType)
                    {
                        case StatType.Attack:
                            if (buff.isPercent) abAtkPct += totalVal; else abAtkFlat += Mathf.RoundToInt(totalVal);
                            break;
                        case StatType.Defense:
                            if (buff.isPercent) abDefPct += totalVal; else abDefFlat += Mathf.RoundToInt(totalVal);
                            break;
                        case StatType.Hp:
                            if (buff.isPercent) abHpPct += totalVal; else abHpFlat += Mathf.RoundToInt(totalVal);
                            break;
                        case StatType.MoveSpeed:
                            if (buff.isPercent) abSpdPct += totalVal; else abSpdFlat += Mathf.RoundToInt(totalVal);
                            break;
                        case StatType.AttackSpeed:
                            if (buff.isPercent) abAtkSpdPct += totalVal;
                            break;
                    }
                }
            }

            // 最终修正 = ActiveBuffs + 外部字段（地形/天气/装备等直接修改） - debuff
            float atkPct  = abAtkPct  + AttackPercentBuff  - AttackPercentDebuff;
            float defPct  = abDefPct  + DefensePercentBuff - DefensePercentDebuff;
            float hpPct   = abHpPct   + HpPercentBuff     - HpPercentDebuff;
            float spdPct  = abSpdPct  + SpeedPercentBuff  - SpeedPercentDebuff;
            float atkSpdPct = abAtkSpdPct + AttackSpeedPercentBuff;

            int atkFlat  = abAtkFlat  + AttackFlatBuff  - AttackFlatDebuff;
            int defFlat  = abDefFlat  + DefenseFlatBuff - DefenseFlatDebuff;
            int hpFlat   = abHpFlat   + HpFlatBuff     - HpFlatDebuff;
            int spdFlat  = abSpdFlat  + SpeedFlatBuff  - SpeedFlatDebuff;

            // 计算最终属性
            Attack = Mathf.Max(1, Mathf.RoundToInt(_base.Attack * (1f + atkPct) + atkFlat));
            Defense = Mathf.Max(0, Mathf.RoundToInt(_base.Defense * (1f + defPct) + defFlat));

            int prevMaxHp = MaxHp;
            MaxHp = Mathf.Max(1, Mathf.RoundToInt(_base.MaxHp * (1f + hpPct) + hpFlat));
            if (prevMaxHp > 0 && MaxHp != prevMaxHp)
                CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp);

            float correctedSpeed = Mathf.Max(1f, _base.MoveSpeed * (1f + spdPct) + spdFlat);
            CorrectedMoveSpeed = Mathf.Max(0.001f, correctedSpeed / 1000f);

            if (_base.AttackSpeed > 0)
                CorrectedAttackSpeed = Mathf.Max(0.1f, _base.AttackSpeed / 1000f / (1f + atkSpdPct));
            else
                CorrectedAttackSpeed = Mathf.Max(0.1f, 2000f / correctedSpeed);

            MoveSpeed = CorrectedMoveSpeed;
            AttackRange = Mathf.Max(0.1f, _base.AttackRange);
        }
    }
}
