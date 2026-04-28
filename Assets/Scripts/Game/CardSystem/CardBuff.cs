using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// Buff 来源系统
    /// </summary>
    public enum BuffSource
    {
        None = 0,
        Recruitment,   // 招募族长强化
        Artifact,      // 商店奇物
        Ritual,        // 祈祀奖励
        Equipment,     // 饰品（全局）
        Mood,          // 心情修正
        Innate,        // 天生被动
        Consumable,    // 消耗品
    }

    /// <summary>
    /// Buff 影响范围
    /// </summary>
    public enum BuffScope
    {
        Leader,   // 只影响族长
        Cat,      // 只影响小猫
        All,      // 影响全体（族长+小猫）
    }

    /// <summary>
    /// 天生 Buff 效果类型
    /// </summary>
    public enum InnateEffectType
    {
        None = 0,
        DamageReduce,          // 受到伤害 -value（固定值）
        AttackPerAliveCat,     // 每有一只活着的本族小猫，攻击 +value
        AttackPerDefeatedCat,  // 每有一只被击败的本族小猫，攻击 +value
        DoubleHit,             // value% 概率造成双倍伤害
        SpeedFlat,             // 速度 +value（固定值）
    }

    /// <summary>
    /// 单条 Buff 来源记录
    /// </summary>
    [Serializable]
    public class BuffEntry
    {
        public BuffSource source;     // 来源系统
        public BuffScope scope;       // 影响范围
        public string choiceId;       // 选择ID（如 "LeaderBoost"、"Artifact_CatAttackFlat"）
        public StatType statType;     // 影响的属性
        public bool isPercent;        // true=百分比, false=固定值
        public float value;           // 数值（百分比存小数如0.1，固定值存整数如6）
        public string displayName;    // 显示名（如 "招募强化"、"奇物：小猫利爪"）

        public string GetValueString()
        {
            if (isPercent) return $"+{Mathf.RoundToInt(value * 100)}%";
            return $"+{Mathf.RoundToInt(value)}";
        }
    }
    /// <summary>
    /// 通用 Buff 条目（用于 UI 显示的特殊 buff）
    /// </summary>
    [Serializable]
    public class TribeBuff
    {
        public string buffId;
        public string displayName;
        public string description;
        public BuffCategory category;
        public bool visible;
        public int iconColorIndex;  // 0红,1蓝,2绿,3金,4紫
        public int duration;        // -1=永久

        // 效果数据（数据驱动，替代硬编码）
        public InnateEffectType effectType;
        public float effectValue;

        public TribeBuff()
        {
            buffId = "";
            displayName = "";
            description = "";
            category = BuffCategory.Special;
            visible = true;
            iconColorIndex = 0;
            duration = -1;
            effectType = InnateEffectType.None;
            effectValue = 0f;
        }

        public bool IsPermanent => duration < 0;
        public bool IsExpired => !IsPermanent && duration <= 0;
    }

    /// <summary>
    /// 永久加成
    /// </summary>
    [Serializable]
    public class PermanentBuffs
    {
        public int attackBonus;
        public float attackPercent;
        public int defenseBonus;
        public float defensePercent;
        public int hpBonus;
        public float hpPercent;
        public int speedBonus;
        public float speedPercent;
        public List<TribeBuff> specialBuffs;
        public List<BuffEntry> buffEntries;

        // 显示控制：false = 在 buff 栏中隐藏该属性
        public bool attackVisible = true;
        public bool defenseVisible = true;
        public bool hpVisible = true;
        public bool speedVisible = true;

        public PermanentBuffs()
        {
            attackBonus = 0;
            attackPercent = 0f;
            defenseBonus = 0;
            defensePercent = 0f;
            hpBonus = 0;
            hpPercent = 0f;
            speedBonus = 0;
            speedPercent = 0f;
            specialBuffs = new List<TribeBuff>();
            buffEntries = new List<BuffEntry>();
        }

        /// <summary>
        /// 添加一条 buff 记录并同步更新汇总字段
        /// </summary>
        public void AddBuffEntry(BuffSource source, string choiceId, StatType stat, bool isPercent, float value, string displayName, BuffScope scope = BuffScope.Leader)
        {
            if (buffEntries == null) buffEntries = new List<BuffEntry>();
            buffEntries.Add(new BuffEntry
            {
                source = source,
                scope = scope,
                choiceId = choiceId,
                statType = stat,
                isPercent = isPercent,
                value = value,
                displayName = displayName
            });
            ApplyEntryToBonus(stat, isPercent, value);
        }

        private void ApplyEntryToBonus(StatType stat, bool isPercent, float value)
        {
            switch (stat)
            {
                case StatType.Attack:
                    if (isPercent) attackPercent += value; else attackBonus += Mathf.RoundToInt(value);
                    break;
                case StatType.Defense:
                    if (isPercent) defensePercent += value; else defenseBonus += Mathf.RoundToInt(value);
                    break;
                case StatType.Hp:
                    if (isPercent) hpPercent += value; else hpBonus += Mathf.RoundToInt(value);
                    break;
                case StatType.MoveSpeed:
                    if (isPercent) speedPercent += value; else speedBonus += Mathf.RoundToInt(value);
                    break;
            }
        }

        /// <summary>
        /// 查询某属性的所有 buff 条目
        /// </summary>
        public List<BuffEntry> GetBuffEntriesForStat(StatType stat)
        {
            if (buffEntries == null) return new List<BuffEntry>();
            return buffEntries.FindAll(e => e.statType == stat);
        }

        /// <summary>
        /// 确保族长拥有天生特殊 buff（加载存档时调用）
        /// </summary>
        public void EnsureInnateBuffs(TribeType tribeType)
        {
            if (specialBuffs == null) specialBuffs = new List<TribeBuff>();
            string innateId = GetInnateBuffId(tribeType);
            if (string.IsNullOrEmpty(innateId)) return;

            // 去重：只保留第一个匹配的 innate buff，删除多余的
            bool found = false;
            for (int i = specialBuffs.Count - 1; i >= 0; i--)
            {
                if (specialBuffs[i].buffId == innateId)
                {
                    if (found)
                        specialBuffs.RemoveAt(i);
                    else
                        found = true;
                }
            }
            if (!found)
                specialBuffs.Add(CreateInnateBuff(tribeType));
        }

        private static string GetInnateBuffId(TribeType tribeType)
        {
            switch (tribeType)
            {
                case TribeType.Orange:  return "innate_damage_reduce";
                case TribeType.Cow:     return "innate_defeated_attack";
                case TribeType.Tabby:   return "innate_double_hit";
                case TribeType.Siamese: return "innate_teleport";
                default: return null;
            }
        }

        /// <summary>
        /// 创建各族天生 buff
        /// </summary>
        public static TribeBuff CreateInnateBuff(TribeType tribeType)
        {
            switch (tribeType)
            {
                case TribeType.Orange:
                    return new TribeBuff
                    {
                        buffId = "innate_damage_reduce",
                        displayName = "厚甲",
                        description = "受到的所有伤害-1",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 3, // 金
                        duration = -1,
                        effectType = InnateEffectType.DamageReduce,
                        effectValue = 1f
                    };
                case TribeType.Cow:
                    return new TribeBuff
                    {
                        buffId = "innate_defeated_attack",
                        displayName = "薄葬",
                        description = "每只被击败的小猫为族长提供+9攻击力",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 2, // 绿
                        duration = -1,
                        effectType = InnateEffectType.AttackPerDefeatedCat,
                        effectValue = 9f
                    };
                case TribeType.Tabby:
                    return new TribeBuff
                    {
                        buffId = "innate_double_hit",
                        displayName = "致命射击",
                        description = "15%概率造成双倍伤害",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 0, // 红
                        duration = -1,
                        effectType = InnateEffectType.DoubleHit,
                        effectValue = 0.15f
                    };
                case TribeType.Siamese:
                    return new TribeBuff
                    {
                        buffId = "innate_teleport",
                        displayName = "传送",
                        description = "瞬移代替走路",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 4, // 紫
                        duration = -1,
                        effectType = InnateEffectType.SpeedFlat,
                        effectValue = 99999f
                    };
                default:
                    return null;
            }
        }
    }

    [Serializable]
    public class TemporaryBuff
    {
        public float attackPercent;
        public float defensePercent;
        public float hpPercent;
        public float speedPercent;
        public int duration; // 剩余回合数

        public TemporaryBuff()
        {
            attackPercent = 0f;
            defensePercent = 0f;
            hpPercent = 0f;
            speedPercent = 0f;
            duration = 0;
        }

        public bool IsActive()
        {
            return duration > 0;
        }

        public void DecreaseDuration()
        {
            if (duration > 0)
            {
                duration--;
            }
        }
    }
}
