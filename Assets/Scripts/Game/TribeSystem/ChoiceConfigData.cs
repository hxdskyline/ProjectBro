using System;
using System.Collections.Generic;

namespace TribeSystem
{
    /// <summary>
    /// Choice 原型 — 从 choice_config.json 加载，各系统通过 ID 引用
    /// </summary>
    [Serializable]
    public class ChoiceArchetype
    {
        public string id;                    // 唯一标识，如 "recruit_buff_leader_hp_flat"
        public string displayName;           // 显示名
        public string descriptionTemplate;   // 描述模板，如 "族长生命值+{value}"
        public string source;                // recruitment / ritual / shop
        public string category;              // reinforcement / buff

        // buff 类
        public string buffScope;             // SingleTribeLeader / AllCats / ...
        public string buffApplyType;         // CurrentUnit / Aura
        public List<ArchetypeBuffEffect> buffEffects;

        // 增援类
        public string reinforcementType;     // AddCats / CatFood / ...
        public int reinforcementValue;

        // 权重（抽卡用）
        public int weight;
    }

    /// <summary>
    /// 原型中的单条 buff 效果
    /// </summary>
    [Serializable]
    public class ArchetypeBuffEffect
    {
        public string statType;     // Hp / Attack / Defense / MoveSpeed / AttackSpeed
        public bool isPercent;
        public float value;
        public int gameEffectType;  // -1 表示无特殊效果
    }

    /// <summary>
    /// choice_config.json 顶层包装
    /// </summary>
    [Serializable]
    public class ChoiceConfigWrapper
    {
        public List<ChoiceArchetype> archetypes;
    }
}
