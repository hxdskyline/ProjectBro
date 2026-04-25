using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 族群属性计算器 - 负责计算族长和小猫的最终属性
    /// </summary>
    public static class TribeStatsCalculator
    {
        /// <summary>
        /// 通用属性修正方法
        /// 公式：final = base * (1 + SUM(percentBuffs) - SUM(percentDebuffs)) + SUM(flatBuffs) - SUM(flatDebuffs)
        /// </summary>
        public static float ApplyModifiers(float baseValue, float percentBuffSum, float flatBuffSum,
            float percentDebuffSum = 0f, float flatDebuffSum = 0f)
        {
            return baseValue * (1f + percentBuffSum - percentDebuffSum) + flatBuffSum - flatDebuffSum;
        }

        /// <summary>
        /// 计算族长的最终属性（包含所有加成）
        /// </summary>
        public static LeaderStats CalculateLeaderStats(LeaderData leader, string moodId = null)
        {
            if (leader == null)
            {
                return new LeaderStats(0, 0, 0, 0, 0);
            }

            float atk = leader.baseAttack;
            float def = leader.baseDefense;
            float hp = leader.baseHp;
            float spd = leader.baseSpeed;
            float cmd = leader.command;

            // 应用永久加成
            if (leader.permanentBuffs != null)
            {
                var b = leader.permanentBuffs;
                atk = ApplyModifiers(atk, b.attackPercent, b.attackBonus);
                def = ApplyModifiers(def, b.defensePercent, b.defenseBonus);
                hp = ApplyModifiers(hp, b.hpPercent, b.hpBonus);
                spd = ApplyModifiers(spd, b.speedPercent, b.speedBonus);
                cmd = ApplyModifiers(cmd, b.commandPercent, b.commandBonus);
            }

            // 应用限时加成（只有百分比，不影响统帅）
            if (leader.temporaryBuff != null && leader.temporaryBuff.IsActive())
            {
                var t = leader.temporaryBuff;
                atk = ApplyModifiers(atk, t.attackPercent, 0f);
                def = ApplyModifiers(def, t.defensePercent, 0f);
                hp = ApplyModifiers(hp, t.hpPercent, 0f);
                spd = ApplyModifiers(spd, t.speedPercent, 0f);
            }

            // 应用心情修正
            if (!string.IsNullOrEmpty(moodId))
            {
                var moodMod = GetMoodModifier(moodId);
                atk = ApplyModifiers(atk, moodMod.atkPercent, 0f);
                def = ApplyModifiers(def, moodMod.defPercent, 0f);
                hp = ApplyModifiers(hp, moodMod.hpPercent, 0f);
                spd = ApplyModifiers(spd, moodMod.spdPercent, 0f);
            }

            return new LeaderStats(
                Mathf.Max(1, Mathf.RoundToInt(atk)),
                Mathf.Max(1, Mathf.RoundToInt(def)),
                Mathf.Max(1, Mathf.RoundToInt(hp)),
                Mathf.Max(1, Mathf.RoundToInt(spd)),
                Mathf.Max(1, Mathf.RoundToInt(cmd))
            );
        }

        /// <summary>
        /// 计算小猫的实际属性（基于小猫基础属性和品质乘数）
        /// 注：小猫现在有独立的catBaseStats配置，而不是基于族长属性
        /// </summary>
        public static CatStats CalculateCatStats(CatData cat, LeaderStats catBaseStats, PermanentBuffs leaderBuffs = null)
        {
            if (cat == null)
            {
                return new CatStats(0, 0, 0, 0);
            }

            // 新系统：使用静态属性 + 大猫永久加成
            if (cat.staticAttack > 0 || cat.staticDefense > 0 || cat.staticHp > 0)
            {
                int catAtk = cat.staticAttack + (leaderBuffs?.attackBonus ?? 0);
                int catDef = cat.staticDefense + (leaderBuffs?.defenseBonus ?? 0);
                int catHp = cat.staticHp + (leaderBuffs?.hpBonus ?? 0);
                int catSpd = cat.staticSpeed + (leaderBuffs?.speedBonus ?? 0);
                return new CatStats(
                    Mathf.Max(1, catAtk),
                    Mathf.Max(1, catDef),
                    Mathf.Max(1, catHp),
                    Mathf.Max(1, catSpd));
            }

            // 兼容旧存档：使用乘数计算
            int attack = Mathf.Max(1, Mathf.RoundToInt(catBaseStats.attack * cat.attackMultiplier));
            int defense = Mathf.Max(1, Mathf.RoundToInt(catBaseStats.defense * cat.defenseMultiplier));
            int hp = Mathf.Max(1, Mathf.RoundToInt(catBaseStats.hp * cat.hpMultiplier));
            int speed = Mathf.Max(1, Mathf.RoundToInt(catBaseStats.speed * cat.speedMultiplier));

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
        public static int CalculateCatEffectiveSpeed(CatData cat, LeaderStats catBaseStats, int totalCatCount, int command, PermanentBuffs leaderBuffs = null)
        {
            // 使用小猫的基础属性计算速度
            CatStats catStats = CalculateCatStats(cat, catBaseStats, leaderBuffs);
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
                    return (0.3f, 0.4f);
                case CatQuality.Blue:
                    return (0.4f, 0.5f);
                case CatQuality.Purple:
                    return (0.5f, 0.6f);
                case CatQuality.Gold:
                    return (0.6f, 0.7f);
                default:
                    return (0.3f, 0.4f);
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
        /// 计算最终伤害（新公式）
        /// FDMG = MAX(DMG * DR * SKILLMULT, 1) + TD
        /// DMG = MAX(CATK - CDEF, 0)
        /// DR = MAX(1 - CDEF / (CDEF + 100), 0.2)
        /// </summary>
        public static int CalculateDamageNew(int correctedAttack, int correctedDefense,
            float skillMultiplier = 1f, int trueDamage = 0)
        {
            int rawDmg = Mathf.Max(0, correctedAttack - correctedDefense);
            float dr = Mathf.Max(0.2f, 1f - (float)correctedDefense / (correctedDefense + 100f));
            float finalF = rawDmg * dr * skillMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(finalF)) + trueDamage;
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

        // ─── 心情修正 ─────────────────────────────────────────────

        private struct MoodModifier
        {
            public float atkPercent;
            public float defPercent;
            public float hpPercent;
            public float spdPercent;
        }

        private static MoodModifier GetMoodModifier(string moodId)
        {
            switch (moodId)
            {
                case "sad":      return new MoodModifier { atkPercent = -0.1f, defPercent = -0.1f, hpPercent = -0.1f, spdPercent = -0.1f };
                case "normal":   return new MoodModifier { atkPercent = 0f,    defPercent = 0f,    hpPercent = 0f,    spdPercent = 0f };
                case "happy":    return new MoodModifier { atkPercent = 0.1f,  defPercent = 0.1f,  hpPercent = 0.1f,  spdPercent = 0.1f };
                case "ecstatic": return new MoodModifier { atkPercent = 0.2f,  defPercent = 0.2f,  hpPercent = 0.2f,  spdPercent = 0.2f };
                default:         return new MoodModifier { atkPercent = 0f,    defPercent = 0f,    hpPercent = 0f,    spdPercent = 0f };
            }
        }
    }
}
