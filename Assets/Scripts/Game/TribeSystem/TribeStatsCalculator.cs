using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 族群属性计算器 - 负责计算族长和小猫的最终属性
    /// </summary>
    public static class TribeStatsCalculator
    {
        /// <summary>
        /// 计算族长的最终属性（包含所有加成）
        /// </summary>
        public static LeaderStats CalculateLeaderStats(LeaderData leader)
        {
            if (leader == null)
            {
                return new LeaderStats(0, 0, 0, 0, 0);
            }

            int baseAttack = leader.baseAttack;
            int baseDefense = leader.baseDefense;
            int baseHp = leader.baseHp;
            int baseSpeed = leader.baseSpeed;
            int baseCommand = leader.command;

            // 应用永久加成
            if (leader.permanentBuffs != null)
            {
                var buffs = leader.permanentBuffs;

                // 先乘百分比加成
                baseAttack = Mathf.RoundToInt(baseAttack * (1f + buffs.attackPercent));
                baseDefense = Mathf.RoundToInt(baseDefense * (1f + buffs.defensePercent));
                baseHp = Mathf.RoundToInt(baseHp * (1f + buffs.hpPercent));
                baseSpeed = Mathf.RoundToInt(baseSpeed * (1f + buffs.speedPercent));
                baseCommand = Mathf.RoundToInt(baseCommand * (1f + buffs.commandPercent));

                // 再加绝对值加成
                baseAttack += buffs.attackBonus;
                baseDefense += buffs.defenseBonus;
                baseHp += buffs.hpBonus;
                baseSpeed += buffs.speedBonus;
                baseCommand += buffs.commandBonus;
            }

            // 应用限时加成
            if (leader.temporaryBuff != null && leader.temporaryBuff.IsActive())
            {
                var temp = leader.temporaryBuff;
                baseAttack = Mathf.RoundToInt(baseAttack * (1f + temp.attackPercent));
                baseDefense = Mathf.RoundToInt(baseDefense * (1f + temp.defensePercent));
                baseHp = Mathf.RoundToInt(baseHp * (1f + temp.hpPercent));
                baseSpeed = Mathf.RoundToInt(baseSpeed * (1f + temp.speedPercent));
            }

            // 确保属性不低于1
            baseAttack = Mathf.Max(1, baseAttack);
            baseDefense = Mathf.Max(1, baseDefense);
            baseHp = Mathf.Max(1, baseHp);
            baseSpeed = Mathf.Max(1, baseSpeed);
            baseCommand = Mathf.Max(1, baseCommand);

            return new LeaderStats(baseAttack, baseDefense, baseHp, baseSpeed, baseCommand);
        }

        /// <summary>
        /// 计算小猫的实际属性（基于族长属性）
        /// </summary>
        public static CatStats CalculateCatStats(CatData cat, LeaderStats leaderStats)
        {
            if (cat == null)
            {
                return new CatStats(0, 0, 0, 0);
            }

            int attack = Mathf.RoundToInt(leaderStats.attack * cat.attackMultiplier);
            int defense = Mathf.RoundToInt(leaderStats.defense * cat.defenseMultiplier);
            int hp = Mathf.RoundToInt(leaderStats.hp * cat.hpMultiplier);
            int speed = Mathf.RoundToInt(leaderStats.speed * cat.speedMultiplier);

            // 确保属性不低于1
            attack = Mathf.Max(1, attack);
            defense = Mathf.Max(1, defense);
            hp = Mathf.Max(1, hp);
            speed = Mathf.Max(1, speed);

            return new CatStats(attack, defense, hp, speed);
        }

        /// <summary>
        /// 计算统帅惩罚（当小猫数量超过统帅力时）
        /// </summary>
        /// <returns>速度惩罚系数（0.5~1.0），1.0表示无惩罚</returns>
        public static float CalculateSpeedPenaltyCoefficient(int catCount, int command)
        {
            if (catCount <= command)
            {
                return 1.0f; // 无惩罚
            }

            // 计算超出比例
            float overRatio = (float)(catCount - command) / command;

            // 每超出10%速度下降10%，最多下降50%
            float penalty = Mathf.Min(overRatio * 10f, 50f) / 100f;

            return 1.0f - penalty;
        }

        /// <summary>
        /// 计算应用统帅惩罚后的速度
        /// </summary>
        public static int ApplyCommandPenaltyToSpeed(int baseSpeed, int catCount, int command)
        {
            float penaltyCoefficient = CalculateSpeedPenaltyCoefficient(catCount, command);
            return Mathf.RoundToInt(baseSpeed * penaltyCoefficient);
        }

        /// <summary>
        /// 计算小猫在实战中的实际速度（考虑统帅惩罚）
        /// </summary>
        public static int CalculateCatEffectiveSpeed(CatData cat, LeaderStats leaderStats, int totalCatCount, int command)
        {
            CatStats catStats = CalculateCatStats(cat, leaderStats);
            return ApplyCommandPenaltyToSpeed(catStats.speed, totalCatCount, command);
        }

        /// <summary>
        /// 计算品质对应的属性比例范围
        /// </summary>
        public static (float min, float max) GetQualityRatioRange(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White:
                    return (0.1f, 0.2f);
                case CatQuality.Blue:
                    return (0.2f, 0.3f);
                case CatQuality.Purple:
                    return (0.3f, 0.4f);
                case CatQuality.Gold:
                    return (0.4f, 0.5f);
                default:
                    return (0.1f, 0.2f);
            }
        }

        /// <summary>
        /// 随机生成指定品质的属性比例
        /// </summary>
        public static float RandomQualityMultiplier(CatQuality quality)
        {
            var (min, max) = GetQualityRatioRange(quality);
            return Random.Range(min, max);
        }

        /// <summary>
        /// 根据基础概率随机生成品质
        /// </summary>
        public static CatQuality RandomCatQuality()
        {
            float roll = Random.value;
            if (roll < 0.4f) return CatQuality.White;      // 40%
            if (roll < 0.7f) return CatQuality.Blue;       // 30%
            if (roll < 0.9f) return CatQuality.Purple;     // 20%
            return CatQuality.Gold;                        // 10%
        }

        /// <summary>
        /// 计算伤害（技能系数 × 攻击力）
        /// </summary>
        public static int CalculateDamage(float skillCoefficient, int attack)
        {
            return Mathf.RoundToInt(skillCoefficient * attack);
        }

        /// <summary>
        /// 计算受到的伤害（考虑防御）
        /// 公式：受到伤害 = MAX{(敌方攻击 - 我方防御), 0} / 敌方攻击 × 技能系数
        /// </summary>
        public static int CalculateReceivedDamage(float skillCoefficient, int enemyAttack, int myDefense)
        {
            int damageReduction = Mathf.Max(0, enemyAttack - myDefense);
            float damageRatio = (float)damageReduction / Mathf.Max(1, enemyAttack);
            return Mathf.RoundToInt(skillCoefficient * enemyAttack * damageRatio);
        }

        /// <summary>
        /// 计算实际移动速度（基于速度属性）
        /// 标准速度1000 = 1单位/秒
        /// </summary>
        public static float CalculateMovementSpeed(int speedAttribute)
        {
            return ((speedAttribute - 1000) / 1000f) + 1f;
        }

        /// <summary>
        /// 计算攻击频率（基于速度属性）
        /// 标准速度1000 = 0.5（2秒攻击1次）
        /// </summary>
        public static float CalculateAttackFrequency(int speedAttribute)
        {
            return speedAttribute / 2000f;
        }
    }
}
