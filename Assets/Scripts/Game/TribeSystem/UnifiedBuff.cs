using System;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// Buff 持久性分类
    /// </summary>
    public enum BuffPersistence
    {
        Persistent,     // 局内成长：跨战斗永久生效（族长技能加点、光环选择、物品）
        BattleOnly,     // 战斗内成长：仅当前战斗有效，战斗结束清零
    }

    /// <summary>
    /// Buff 叠加规则
    /// </summary>
    public enum BuffStackRule
    {
        None,           // 不可叠加，重复添加时刷新持续时间
        Stack,          // 可叠加，每层独立计算
        DurationRefresh // 不叠加层数，只刷新持续时间
    }

    /// <summary>
    /// 统一的 Buff 运行时表示。
    /// </summary>
    [Serializable]
    public class UnifiedBuff
    {
        // ── 标识 ──
        public string buffId;              // 唯一标识（如 "poison_3", "fullness_layer"）
        public string displayName;         // 显示名
        public string description;         // 描述
        public BuffSource source;          // 来源系统
        public string sourceId;            // 来源 ID（choiceId / equipmentId 等）

        // ── 持久性与叠加 ──
        public BuffPersistence persistence;
        public BuffStackRule stackRule;
        public int maxStacks;              // 最大叠加层数（1=不叠加）
        public int currentStacks;          // 当前层数

        // ── 简单属性修改效果 ──
        public StatType statType;          // 影响的属性
        public bool isPercent;             // 是否百分比
        public float value;                // 数值（每层）

        // ── 特殊效果 ──
        public GameEffect gameEffect;      // 特殊效果类型（DoT 等）
        public int gameEffectType;         // 光环特殊效果 ID（来自 tribe_aura_config.json）
        public float effectParam1;         // 效果参数1（如 DoT 每秒伤害）
        public float effectParam2;         // 效果参数2（如减速百分比）

        // ── 生命周期 ──
        public float remainingDuration;    // 剉余时间（秒），-1=永久
        public float tickInterval;         // 触发间隔（秒），0=不持续触发
        public float tickTimer;            // 当前 tick 计时器

        // ── 状态标记 ──
        public bool IsExpired => remainingDuration >= 0 && remainingDuration <= 0;
        public bool IsPermanent => remainingDuration < 0;
        public bool IsStackable => stackRule == BuffStackRule.Stack;

        /// <summary>
        /// 创建一个简单的属性修改 buff（永久）
        /// </summary>
        public static UnifiedBuff CreateStatBuff(
            string buffId, string displayName, BuffSource source, string sourceId,
            StatType statType, bool isPercent, float value,
            BuffScope scope = BuffScope.Leader, int gameEffectType = -1)
        {
            return new UnifiedBuff
            {
                buffId = buffId,
                displayName = displayName,
                source = source,
                sourceId = sourceId,
                persistence = BuffPersistence.Persistent,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                statType = statType,
                isPercent = isPercent,
                value = value,
                gameEffect = GameEffect.AttackPercent,
                gameEffectType = gameEffectType,
                remainingDuration = -1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建一个带持续时间的 buff
        /// </summary>
        public static UnifiedBuff CreateTimedBuff(
            string buffId, string displayName, BuffSource source, string sourceId,
            StatType statType, bool isPercent, float value,
            float duration, BuffStackRule stackRule = BuffStackRule.None, int maxStacks = 1)
        {
            return new UnifiedBuff
            {
                buffId = buffId,
                displayName = displayName,
                source = source,
                sourceId = sourceId,
                persistence = BuffPersistence.BattleOnly,
                stackRule = stackRule,
                maxStacks = maxStacks,
                currentStacks = 1,
                statType = statType,
                isPercent = isPercent,
                value = value,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 尝试叠加或刷新 buff。返回 true 表示叠加成功（或刷新），false 表示应创建新实例。
        /// </summary>
        public bool TryStackOrRefresh(UnifiedBuff incoming)
        {
            if (buffId != incoming.buffId) return false;

            switch (stackRule)
            {
                case BuffStackRule.Stack:
                    if (currentStacks < maxStacks)
                        currentStacks = Mathf.Min(currentStacks + incoming.currentStacks, maxStacks);
                    remainingDuration = Mathf.Max(remainingDuration, incoming.remainingDuration);
                    return true;

                case BuffStackRule.DurationRefresh:
                    remainingDuration = incoming.remainingDuration;
                    return true;

                case BuffStackRule.None:
                default:
                    // 刷新持续时间，取较长的
                    remainingDuration = Mathf.Max(remainingDuration, incoming.remainingDuration);
                    return true;
            }
        }

        /// <summary>
        /// 创建此 buff 的深拷贝
        /// </summary>
        public UnifiedBuff Clone()
        {
            return new UnifiedBuff
            {
                buffId = buffId,
                displayName = displayName,
                description = description,
                source = source,
                sourceId = sourceId,
                persistence = persistence,
                stackRule = stackRule,
                maxStacks = maxStacks,
                currentStacks = currentStacks,
                statType = statType,
                isPercent = isPercent,
                value = value,
                gameEffect = gameEffect,
                effectParam1 = effectParam1,
                effectParam2 = effectParam2,
                remainingDuration = remainingDuration,
                tickInterval = tickInterval,
                tickTimer = tickTimer,
            };
        }
    }
}
