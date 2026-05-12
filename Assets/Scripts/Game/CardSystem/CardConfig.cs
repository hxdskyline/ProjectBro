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
        public int deployCostPerCat;          // 每只小猫的出战消耗（猫粮）
        public int leaderFighterId;           // fighter_config.json 中的族长 fighterId
        public List<UnitTypeData> unitTypes;   // 单位类型列表（Tier1/2/3）

        public TribeConfig()
        {
            tribeType = TribeType.Tabby;
            tribeName = "";
            description = "";
            deployCostPerCat = 10;
            leaderFighterId = 0;
            unitTypes = new List<UnitTypeData>();
        }

        /// <summary>
        /// 获取指定等级的单位数据，找不到则返回 null
        /// </summary>
        public UnitTypeData GetUnitType(UnitTier tier)
        {
            if (unitTypes == null) return null;
            for (int i = 0; i < unitTypes.Count; i++)
            {
                if (unitTypes[i].tier == (int)tier)
                    return unitTypes[i];
            }
            return null;
        }
    }

    /// <summary>
    /// 单位类型数据（每个种族的 Tier1/2/3 单位）
    /// </summary>
    [Serializable]
    public class UnitTypeData
    {
        public int tier;            // UnitTier 枚举值 (1/2/3)
        public int fighterId;       // fighter_config.json 中的 fighterId

        public UnitTypeData()
        {
            tier = 1;
            fighterId = 0;
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
