using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 小猫静态属性（从 cat_stats_table.json 读取）
    /// </summary>
    [Serializable]
    public class CatStaticStats
    {
        public int tribeType;
        public int quality;
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;
    }

    /// <summary>
    /// 族群配置
    /// </summary>
    [Serializable]
    public class TribeConfig
    {
        public TribeType tribeType;
        public string tribeName;
        public string description;
        public int initialCatCount;
        public int deployCostPerCat;          // 每只小猫的出战消耗（猫粮）
        public string avatarDefinitionAddress; // 战斗 avatar 模型地址
        public LeaderBaseStats leaderBaseStats;
        public CatBaseStats catBaseStats;      // 小猫基础属性

        public TribeConfig()
        {
            tribeType = TribeType.Tabby;
            tribeName = "";
            description = "";
            initialCatCount = 3;
            deployCostPerCat = 10;
            avatarDefinitionAddress = "";
            leaderBaseStats = new LeaderBaseStats();
            catBaseStats = new CatBaseStats();
        }
    }

    /// <summary>
    /// 族长基础属性配置
    /// </summary>
    [Serializable]
    public class LeaderBaseStats
    {
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;
        public int command;

        public LeaderBaseStats()
        {
            attack = 100;
            defense = 80;
            hp = 1000;
            moveSpeed = 1.0f;
            attackSpeed = 0.5f;
            command = 10;
        }
    }

    /// <summary>
    /// 小猫基础属性配置
    /// </summary>
    [Serializable]
    public class CatBaseStats
    {
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;
        public int command;

        public CatBaseStats()
        {
            attack = 5;
            defense = 10;
            hp = 50;
            moveSpeed = 1.0f;
            attackSpeed = 0.5f;
            command = 0;
        }
    }

    /// <summary>
    /// 品质配置
    /// </summary>
    [Serializable]
    public class QualityConfig
    {
        public CatQuality quality;
        public string qualityName;
        public float minRatio;
        public float maxRatio;
        public float baseProbability;

        public QualityConfig()
        {
            quality = CatQuality.White;
            qualityName = "菜鸟";
            minRatio = 0.1f;
            maxRatio = 0.2f;
            baseProbability = 0.4f;
        }
    }

    /// <summary>
    /// 种族在特定地形/天气下的属性修正
    /// </summary>
    public struct TerrainWeatherBuff
    {
        public float attackPercent;
        public float defensePercent;
        public float hpPercent;
        public float speedPercent;

        public bool IsNeutral =>
            Mathf.Approximately(attackPercent, 0f) &&
            Mathf.Approximately(defensePercent, 0f) &&
            Mathf.Approximately(hpPercent, 0f) &&
            Mathf.Approximately(speedPercent, 0f);

        public string GetDescription()
        {
            var parts = new List<string>();
            if (!Mathf.Approximately(attackPercent, 0f))
                parts.Add($"攻{(attackPercent > 0 ? "+" : "")}{Mathf.RoundToInt(attackPercent * 100)}%");
            if (!Mathf.Approximately(defensePercent, 0f))
                parts.Add($"防{(defensePercent > 0 ? "+" : "")}{Mathf.RoundToInt(defensePercent * 100)}%");
            if (!Mathf.Approximately(hpPercent, 0f))
                parts.Add($"血{(hpPercent > 0 ? "+" : "")}{Mathf.RoundToInt(hpPercent * 100)}%");
            if (!Mathf.Approximately(speedPercent, 0f))
                parts.Add($"速{(speedPercent > 0 ? "+" : "")}{Mathf.RoundToInt(speedPercent * 100)}%");
            return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : "无修正";
        }
    }
}
