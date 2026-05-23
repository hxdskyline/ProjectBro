using System;
using UnityEngine;

namespace LeaderSystem
{
    /// <summary>
    /// 主角属性数据 - 存储主角的等级和各项属性
    /// 主角不上场战斗，在场边作为"总大将"存在
    /// </summary>
    [Serializable]
    public class LeaderData
    {
        /// <summary>
        /// 主角等级（通过战斗经验提升）
        /// </summary>
        public int level = 1;

        /// <summary>
        /// 当前经验值
        /// </summary>
        public int currentExp = 0;

        /// <summary>
        /// 升级所需经验值（可配置）
        /// </summary>
        public int expToNextLevel = 100;

        /// <summary>
        /// 领导力 - 决定人口上限（初始 3，每级 +1）
        /// </summary>
        public int leadership = 3;

        /// <summary>
        /// 领导力上限（可配置）
        /// </summary>
        public int maxLeadership = 20;

        /// <summary>
        /// 街头情报 - 决定战前情报精度（Lv.0~3），影响地图节点敌人信息显示
        /// </summary>
        public int streetIntel = 0;

        /// <summary>
        /// 街头情报上限
        /// </summary>
        public int maxStreetIntel = 3;

        /// <summary>
        /// 咪格魅力 - 提升首领招募概率
        /// </summary>
        public int charm = 0;

        /// <summary>
        /// 咪格魅力上限
        /// </summary>
        public int maxCharm = 10;

        /// <summary>
        /// 技能点（每次升级获得 1 点，基础版暂不实现技能树）
        /// </summary>
        public int skillPoints = 0;

        /// <summary>
        /// 添加经验值
        /// </summary>
        /// <param name="exp">获得的经验值</param>
        /// <returns>是否升级</returns>
        public bool AddExperience(int exp)
        {
            if (exp <= 0) return false;

            currentExp += exp;
            bool leveledUp = false;

            while (currentExp >= expToNextLevel && level < 99)
            {
                currentExp -= expToNextLevel;
                LevelUp();
                leveledUp = true;
            }

            return leveledUp;
        }

        /// <summary>
        /// 升级
        /// </summary>
        private void LevelUp()
        {
            level++;

            // 领导力每级+1
            if (leadership < maxLeadership)
            {
                leadership++;
            }

            // 每3级街头情报+1
            if (level % 3 == 0 && streetIntel < maxStreetIntel)
            {
                streetIntel++;
            }

            // 每5级咪格魅力+1
            if (level % 5 == 0 && charm < maxCharm)
            {
                charm++;
            }

            // 获得技能点
            skillPoints++;

            // 更新升级所需经验值（指数增长）
            expToNextLevel = Mathf.RoundToInt(100 * Mathf.Pow(1.2f, level - 1));

            Debug.Log($"[LeaderData] 主角升级! 等级: {level}, 领导力: {leadership}, 街头情报: {streetIntel}, 咪格魅力: {charm}");
        }

        /// <summary>
        /// 获取当前经验值进度（0~1）
        /// </summary>
        public float GetExpProgress()
        {
            if (expToNextLevel <= 0) return 1f;
            return (float)currentExp / expToNextLevel;
        }

        /// <summary>
        /// 获取领导力等级（用于UI显示）
        /// </summary>
        public int GetLeadershipLevel()
        {
            // 领导力初始3，每级+1，所以等级 = leadership - 2
            return Mathf.Max(1, leadership - 2);
        }

        /// <summary>
        /// 获取街头情报等级描述
        /// </summary>
        public string GetStreetIntelDescription()
        {
            switch (streetIntel)
            {
                case 0: return "无情报";
                case 1: return "模糊描述";
                case 2: return "大致范围";
                case 3: return "精确数字";
                default: return $"Lv.{streetIntel}";
            }
        }
    }
}
