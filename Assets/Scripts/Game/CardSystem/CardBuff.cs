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
        KillHealSatiety,       // 饕餮：击杀回血 value% 最大生命 + 获得1层饱食
        AttackPerFriendlyUnit, // 牧群领袖：每个友方单位（含尸体）+value 攻击
        MarkTargetDamageAmp,   // 狩猎印记：攻击标记目标，被标记目标受伤 +value%
        DragonBreathOnCast,    // 龙语回响：施法时对随机敌人造成 value 火伤
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
                case TribeType.Orange:  return "innate_饕餮";
                case TribeType.Cow:     return "innate_牧群领袖";
                case TribeType.Tabby:   return "innate_狩猎印记";
                case TribeType.Siamese: return "innate_龙语回响";
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
                        buffId = "innate_饕餮",
                        displayName = "饕餮",
                        description = "击杀单位恢复10%最大生命，并获得1层饱食",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 3, // 金
                        duration = -1,
                        effectType = InnateEffectType.KillHealSatiety,
                        effectValue = 0.1f
                    };
                case TribeType.Cow:
                    return new TribeBuff
                    {
                        buffId = "innate_牧群领袖",
                        displayName = "牧群领袖",
                        description = "场上每有一个友方单位（含尸体），族长攻击力+1",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 2, // 绿
                        duration = -1,
                        effectType = InnateEffectType.AttackPerFriendlyUnit,
                        effectValue = 1f
                    };
                case TribeType.Tabby:
                    return new TribeBuff
                    {
                        buffId = "innate_狩猎印记",
                        displayName = "狩猎印记",
                        description = "攻击标记目标5秒，被标记目标受到的伤害+30%",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 0, // 红
                        duration = -1,
                        effectType = InnateEffectType.MarkTargetDamageAmp,
                        effectValue = 0.3f
                    };
                case TribeType.Siamese:
                    return new TribeBuff
                    {
                        buffId = "innate_龙语回响",
                        displayName = "龙语回响",
                        description = "每次施法，对随机敌人喷射小型龙息（10火伤，内置冷却1秒）",
                        category = BuffCategory.Special,
                        visible = true,
                        iconColorIndex = 4, // 紫
                        duration = -1,
                        effectType = InnateEffectType.DragonBreathOnCast,
                        effectValue = 10f
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
