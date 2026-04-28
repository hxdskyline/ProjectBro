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
    /// Buff 影响范围
    /// </summary>
    public enum BuffApplyScope
    {
        All,                // 全体（所有族长+所有小猫）
        AllLeaders,         // 全体族长
        AllCats,            // 全体小猫
        SingleTribeLeader,  // 单族族长
        SingleTribeCat      // 单族小猫
    }

    /// <summary>
    /// Buff 影响类型
    /// </summary>
    public enum BuffApplyType
    {
        CurrentUnit,  // 只影响当前已有单位
        Aura          // 光环：当前单位 + 未来新获得的单位自动继承
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
        public int gameEffectType;  // 原始 GameEffect 枚举值，用于特殊效果（如 LeaderSpeedFlat=14, LeaderAttackPerDeadCat=15）

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

        // 增援属性（category == Reinforcement 时使用）
        public ReinforcementType reinforcementType;
        public int reinforcementValue;
        public TribeType? targetTribeType;
        public int targetTribeId;

        // Buff 属性（category == Buff 时使用）
        public BuffApplyScope buffScope;
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
            buffApplyType = BuffApplyType.CurrentUnit;
            buffEffects = new List<BuffEffectItem>();
        }

        /// <summary>
        /// 便捷构造：创建一条 buff choice
        /// </summary>
        public static GameChoice CreateBuff(
            string id, string name, string desc,
            ChoiceSource src,
            BuffApplyScope scope, BuffApplyType applyType,
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
                buffScope = scope,
                buffApplyType = applyType,
                buffEffects = effects ?? new List<BuffEffectItem>(),
                targetTribeType = tribe
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
                targetTribeType = tribe
            };
        }

        /// <summary>
        /// 获取影响范围的中文描述
        /// </summary>
        public string GetScopeDisplayString()
        {
            switch (buffScope)
            {
                case BuffApplyScope.All: return "全体";
                case BuffApplyScope.AllLeaders: return "全体族长";
                case BuffApplyScope.AllCats: return "全体小猫";
                case BuffApplyScope.SingleTribeLeader: return "单族族长";
                case BuffApplyScope.SingleTribeCat: return "单族小猫";
                default: return "";
            }
        }

        /// <summary>
        /// 获取影响类型的中文描述
        /// </summary>
        public string GetApplyTypeDisplayString()
        {
            return buffApplyType == BuffApplyType.Aura ? "光环" : "即时";
        }
    }

    /// <summary>
    /// 装备/饰品记录 — 与 GameChoice 使用相同 buff 规则，单独存放
    /// </summary>
    [Serializable]
    public class EquipmentRecord
    {
        public string equipmentId;
        public string configId;       // accessory_config.json 中的 id
        public string displayName;
        public string description;
        public BuffApplyScope buffScope;
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
            buffApplyType = BuffApplyType.Aura;
            effects = new List<BuffEffectItem>();
            acquiredRound = 0;
        }

        /// <summary>
        /// 获取影响范围的中文描述
        /// </summary>
        public string GetScopeDisplayString()
        {
            switch (buffScope)
            {
                case BuffApplyScope.All: return "全体";
                case BuffApplyScope.AllLeaders: return "全体族长";
                case BuffApplyScope.AllCats: return "全体小猫";
                case BuffApplyScope.SingleTribeLeader: return "单族族长";
                case BuffApplyScope.SingleTribeCat: return "单族小猫";
                default: return "";
            }
        }

        /// <summary>
        /// 获取影响类型的中文描述
        /// </summary>
        public string GetApplyTypeDisplayString()
        {
            return buffApplyType == BuffApplyType.Aura ? "光环" : "即时";
        }
    }
}
