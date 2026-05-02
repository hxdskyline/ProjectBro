using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// Choice 分类：增援类 / Buff 类
    /// </summary>
    public enum ChoiceCategory
    {
        Reinforcement,   // 增援（加小猫、新部族、猫粮等）
        Buff,            // 加 buff（属性修改）
        AddCats,         // 增加小猫
        QualityEvolution // 品质进化
    }

    /// <summary>
    /// Choice 来源系统
    /// </summary>
    public enum ChoiceSource
    {
        Recruitment,     // 招募
        Ritual,          // 祈祀
        NewTribeEvent,   // 新部族事件
        Shop             // 商店
    }

    /// <summary>
    /// 增援类型
    /// </summary>
    public enum ReinforcementType
    {
        None = 0,
        AddCats,          // 给已有族群增加小猫
        NewTribe,         // 新增一个族群
        QualityEvolution, // 品质进化
        CatFood           // 获得猫粮
    }

    /// <summary>
    /// Buff 影响范围（旧枚举，保留用于存档兼容）
    /// </summary>
    [Obsolete("Use BuffScopeFilter instead")]
    public enum BuffApplyScope
    {
        All,
        AllLeaders,
        AllCats,
        SingleTribeLeader,
        SingleTribeCat,
        SingleTribeAll
    }

    /// <summary>
    /// Buff 影响类型
    /// </summary>
    public enum BuffApplyType
    {
        CurrentUnit,  // 只影响当前已有单位
        Aura          // 光环：当前单位 + 未来新获得的单位自动继承
    }

    // ─── 新 Scope Filter 系统 ──────────────────────────────────

    public enum ScopeRoleFilter
    {
        Any = 0,      // 不限角色（族长+士兵都生效）
        Leader = 1,   // 仅族长
        Soldier = 2   // 仅士兵（非族长）
    }

    public enum ScopeTierFilter
    {
        Any = 0,  // 不限等级
        T1 = 1,
        T2 = 2,
        T3 = 3
    }

    /// <summary>
    /// 标签化 Buff 作用域过滤器。所有非默认字段使用 AND 逻辑。
    /// JSON 格式：用 | 分隔标签，如 "Orange | Leader | T1"
    /// </summary>
    [Serializable]
    public struct BuffScopeFilter
    {
        public ScopeRoleFilter role;
        public ScopeTierFilter tier;
        public TribeType? tribe;

        public static readonly BuffScopeFilter All = new BuffScopeFilter
        {
            role = ScopeRoleFilter.Any,
            tier = ScopeTierFilter.Any,
            tribe = null
        };

        public bool IsDefault => role == ScopeRoleFilter.Any
                              && tier == ScopeTierFilter.Any
                              && !tribe.HasValue;

        /// <summary>
        /// 检查此过滤器是否匹配指定单位
        /// </summary>
        public bool Matches(bool isLeader, TribeType unitTribe, UnitTier? unitTier)
        {
            if (role == ScopeRoleFilter.Leader && !isLeader) return false;
            if (role == ScopeRoleFilter.Soldier && isLeader) return false;

            if (tribe.HasValue && tribe.Value != unitTribe) return false;

            if (tier != ScopeTierFilter.Any && unitTier.HasValue)
            {
                if ((int)tier != (int)unitTier.Value) return false;
            }

            return true;
        }

        /// <summary>
        /// 解析管道分隔的标签字符串，如 "Orange | Leader | T1"
        /// </summary>
        public static BuffScopeFilter Parse(string scopeStr)
        {
            if (string.IsNullOrEmpty(scopeStr))
                return All;

            // 兼容旧枚举值
            switch (scopeStr)
            {
                case "All": return All;
                case "AllLeaders": return new BuffScopeFilter { role = ScopeRoleFilter.Leader };
                case "AllCats": return new BuffScopeFilter { role = ScopeRoleFilter.Soldier };
                case "SingleTribeLeader": return new BuffScopeFilter { role = ScopeRoleFilter.Leader };
                case "SingleTribeCat": return new BuffScopeFilter { role = ScopeRoleFilter.Soldier };
                case "SingleTribeAll": return new BuffScopeFilter();
            }

            var result = new BuffScopeFilter();
            string[] tags = scopeStr.Split('|');
            foreach (string rawTag in tags)
            {
                string tag = rawTag.Trim();
                if (string.IsNullOrEmpty(tag)) continue;

                if (tag == "Leader") { result.role = ScopeRoleFilter.Leader; continue; }
                if (tag == "Soldier") { result.role = ScopeRoleFilter.Soldier; continue; }

                if (tag == "T1") { result.tier = ScopeTierFilter.T1; continue; }
                if (tag == "T2") { result.tier = ScopeTierFilter.T2; continue; }
                if (tag == "T3") { result.tier = ScopeTierFilter.T3; continue; }

                switch (tag)
                {
                    case "Tabby": result.tribe = TribeType.Tabby; continue;
                    case "Orange": result.tribe = TribeType.Orange; continue;
                    case "Cow": result.tribe = TribeType.Cow; continue;
                    case "Siamese": result.tribe = TribeType.Siamese; continue;
                }

                Debug.LogWarning($"[BuffScopeFilter] 未知 scope 标签: '{tag}'");
            }

            return result;
        }

        /// <summary>
        /// 从旧枚举迁移
        /// </summary>
        public static BuffScopeFilter FromLegacy(BuffApplyScope legacy, TribeType? targetTribe)
        {
            switch (legacy)
            {
                case BuffApplyScope.All: return All;
                case BuffApplyScope.AllLeaders:
                    return new BuffScopeFilter { role = ScopeRoleFilter.Leader };
                case BuffApplyScope.AllCats:
                    return new BuffScopeFilter { role = ScopeRoleFilter.Soldier };
                case BuffApplyScope.SingleTribeLeader:
                    return new BuffScopeFilter { role = ScopeRoleFilter.Leader, tribe = targetTribe };
                case BuffApplyScope.SingleTribeCat:
                    return new BuffScopeFilter { role = ScopeRoleFilter.Soldier, tribe = targetTribe };
                case BuffApplyScope.SingleTribeAll:
                    return new BuffScopeFilter { tribe = targetTribe };
                default: return All;
            }
        }

        /// <summary>
        /// 中文描述
        /// </summary>
        public string GetDisplayString()
        {
            var parts = new List<string>();

            if (tribe.HasValue)
            {
                switch (tribe.Value)
                {
                    case TribeType.Tabby: parts.Add("狸花猫族"); break;
                    case TribeType.Orange: parts.Add("大橘猫族"); break;
                    case TribeType.Cow: parts.Add("奶牛猫族"); break;
                    case TribeType.Siamese: parts.Add("暹罗猫族"); break;
                }
            }

            switch (role)
            {
                case ScopeRoleFilter.Leader: parts.Add("族长"); break;
                case ScopeRoleFilter.Soldier: parts.Add("小猫"); break;
            }

            switch (tier)
            {
                case ScopeTierFilter.T1: parts.Add("一级兵"); break;
                case ScopeTierFilter.T2: parts.Add("二级兵"); break;
                case ScopeTierFilter.T3: parts.Add("三级兵"); break;
            }

            if (parts.Count == 0) return "全体";
            return string.Join("·", parts);
        }
    }

    /// <summary>
    /// 单条 Buff 效果
    /// </summary>
    [Serializable]
    public class BuffEffectItem
    {
        public StatType statType;
        public bool isPercent;
        public float value;
        public int gameEffectType;

        public BuffEffectItem() { }

        public BuffEffectItem(StatType stat, bool percent, float val, int geType = -1)
        {
            statType = stat;
            isPercent = percent;
            value = val;
            gameEffectType = geType;
        }

        public string GetDisplayString()
        {
            if (isPercent) return $"+{Mathf.RoundToInt(value * 100)}%";
            return $"+{Mathf.RoundToInt(value)}";
        }
    }

    /// <summary>
    /// 统一的 Choice 类 — 所有事件系统（招募、祭祀、新部族、商店）共用
    /// </summary>
    [Serializable]
    public class GameChoice
    {
        public string choiceId;
        public string displayName;
        public string description;
        public ChoiceCategory category;
        public ChoiceSource source;

        // 增援属性
        public ReinforcementType reinforcementType;
        public int reinforcementValue;
        public TribeType? targetTribeType;
        public int targetTribeId;

        // Buff 属性
        public BuffApplyScope buffScope;          // 旧字段，存档兼容
        public BuffScopeFilter buffScopeFilter;   // 新字段（LitJson 可能丢失 nullable enum）
        public string buffScopeText;              // scope 文本备份（如 "Orange | T1 | Soldier"），确保跨存档可靠
        public BuffApplyType buffApplyType;
        public List<BuffEffectItem> buffEffects;

        public GameChoice()
        {
            choiceId = "";
            displayName = "";
            description = "";
            category = ChoiceCategory.Buff;
            source = ChoiceSource.Recruitment;
            reinforcementType = ReinforcementType.None;
            reinforcementValue = 0;
            targetTribeType = null;
            targetTribeId = -1;
            buffScope = BuffApplyScope.All;
            buffScopeFilter = BuffScopeFilter.All;
            buffScopeText = "";
            buffApplyType = BuffApplyType.CurrentUnit;
            buffEffects = new List<BuffEffectItem>();
        }

        /// <summary>
        /// 获取 scope filter，自动从旧字段迁移。优先级：buffScopeFilter > buffScopeText > buffScope > All
        /// </summary>
        public BuffScopeFilter GetScopeFilter()
        {
            if (!buffScopeFilter.IsDefault)
                return buffScopeFilter;

            // 从文本备份还原（LitJson 序列化 string 最可靠）
            if (!string.IsNullOrEmpty(buffScopeText))
            {
                buffScopeFilter = BuffScopeFilter.Parse(buffScopeText);
                return buffScopeFilter;
            }

            // 从旧枚举迁移
            if (buffScope != BuffApplyScope.All)
            {
                buffScopeFilter = BuffScopeFilter.FromLegacy(buffScope, targetTribeType);
                return buffScopeFilter;
            }

            // 用 targetTribeType 构造（最后一个 fallback）
            if (targetTribeType.HasValue)
            {
                buffScopeFilter = new BuffScopeFilter { tribe = targetTribeType.Value };
                return buffScopeFilter;
            }

            return BuffScopeFilter.All;
        }

        /// <summary>
        /// 便捷构造：创建一条 buff choice（新 API）
        /// </summary>
        public static GameChoice CreateBuff(
            string id, string name, string desc,
            ChoiceSource src,
            BuffScopeFilter scopeFilter, BuffApplyType applyType,
            List<BuffEffectItem> effects,
            TribeType? tribe = null)
        {
            return new GameChoice
            {
                choiceId = id,
                displayName = name,
                description = desc,
                category = ChoiceCategory.Buff,
                source = src,
                buffScopeFilter = scopeFilter,
                buffScopeText = scopeFilter.GetDisplayString(),
                buffApplyType = applyType,
                buffEffects = effects ?? new List<BuffEffectItem>(),
                targetTribeType = tribe,
                targetTribeId = tribe.HasValue ? (int)tribe.Value : -1
            };
        }

        /// <summary>
        /// 便捷构造：创建一条增援 choice
        /// </summary>
        public static GameChoice CreateReinforcement(
            string id, string name, string desc,
            ChoiceSource src,
            ReinforcementType rType, int value,
            TribeType? tribe = null)
        {
            return new GameChoice
            {
                choiceId = id,
                displayName = name,
                description = desc,
                category = ChoiceCategory.Reinforcement,
                source = src,
                reinforcementType = rType,
                reinforcementValue = value,
                targetTribeType = tribe,
                targetTribeId = tribe.HasValue ? (int)tribe.Value : -1
            };
        }

        public string GetScopeDisplayString()
        {
            return GetScopeFilter().GetDisplayString();
        }

        public string GetApplyTypeDisplayString()
        {
            return buffApplyType == BuffApplyType.Aura ? "光环" : "即时";
        }
    }

    /// <summary>
    /// 装备/饰品记录
    /// </summary>
    [Serializable]
    public class EquipmentRecord
    {
        public string equipmentId;
        public string configId;
        public string displayName;
        public string description;
        public BuffApplyScope buffScope;          // 旧字段
        public BuffScopeFilter buffScopeFilter;   // 新字段（LitJson 可能丢失 nullable enum）
        public string buffScopeText;              // scope 文本备份，确保跨存档可靠
        public BuffApplyType buffApplyType;
        public List<BuffEffectItem> effects;
        public int acquiredRound;

        public EquipmentRecord()
        {
            equipmentId = "";
            configId = "";
            displayName = "";
            description = "";
            buffScope = BuffApplyScope.All;
            buffScopeFilter = BuffScopeFilter.All;
            buffScopeText = "";
            buffApplyType = BuffApplyType.Aura;
            effects = new List<BuffEffectItem>();
            acquiredRound = 0;
        }

        public BuffScopeFilter GetScopeFilter()
        {
            if (!buffScopeFilter.IsDefault)
                return buffScopeFilter;

            if (!string.IsNullOrEmpty(buffScopeText))
            {
                buffScopeFilter = BuffScopeFilter.Parse(buffScopeText);
                return buffScopeFilter;
            }

            buffScopeFilter = BuffScopeFilter.FromLegacy(buffScope, null);
            return buffScopeFilter;
        }

        public string GetScopeDisplayString()
        {
            return GetScopeFilter().GetDisplayString();
        }

        public string GetApplyTypeDisplayString()
        {
            return buffApplyType == BuffApplyType.Aura ? "光环" : "即时";
        }
    }
}
