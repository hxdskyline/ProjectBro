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
                return new LeaderStats(0, 0, 0, 1.0f, 0.5f, 0);
            }

            float atk = leader.baseAttack;
            float def = leader.baseDefense;
            float hp = leader.baseHp;
            float moveSpd = leader.baseMoveSpeed;
            float atkSpd = 0.5f; // 默认攻速

            // 应用永久加成
            if (leader.permanentBuffs != null)
            {
                var b = leader.permanentBuffs;
                atk = ApplyModifiers(atk, b.attackPercent, b.attackBonus);
                def = ApplyModifiers(def, b.defensePercent, b.defenseBonus);
                hp = ApplyModifiers(hp, b.hpPercent, b.hpBonus);
                moveSpd = ApplyModifiers(moveSpd, b.speedPercent, 0f);
            }

            // 应用限时加成（只有百分比）
            if (leader.temporaryBuff != null && leader.temporaryBuff.IsActive())
            {
                var t = leader.temporaryBuff;
                atk = ApplyModifiers(atk, t.attackPercent, 0f);
                def = ApplyModifiers(def, t.defensePercent, 0f);
                hp = ApplyModifiers(hp, t.hpPercent, 0f);
                moveSpd = ApplyModifiers(moveSpd, t.speedPercent, 0f);
            }

            // 应用心情修正
            if (!string.IsNullOrEmpty(moodId))
            {
                var moodMod = GetMoodModifier(moodId);
                atk = ApplyModifiers(atk, moodMod.atkPercent, 0f);
                def = ApplyModifiers(def, moodMod.defPercent, 0f);
                hp = ApplyModifiers(hp, moodMod.hpPercent, 0f);
                moveSpd = ApplyModifiers(moveSpd, moodMod.spdPercent, 0f);
            }

            return new LeaderStats(
                Mathf.Max(1, Mathf.RoundToInt(atk)),
                Mathf.Max(1, Mathf.RoundToInt(def)),
                Mathf.Max(1, Mathf.RoundToInt(hp)),
                Mathf.Max(0.001f, moveSpd),
                Mathf.Max(0.001f, atkSpd),
                leader.command
            );
        }

        /// <summary>
        /// 计算小猫的实际属性（基于小猫基础属性和品质乘数）
        /// </summary>
        public static CatStats CalculateCatStats(CatData cat)
        {
            if (cat == null)
            {
                return new CatStats(0, 0, 0, 1.0f, 0.5f);
            }

            float catAtk = cat.staticAttack;
            float catDef = cat.staticDefense;
            float catHp = cat.staticHp;
            float catMoveSpd = cat.staticMoveSpeed;
            float catAtkSpd = cat.staticAttackSpeed > 0 ? cat.staticAttackSpeed : 0.5f;

            float atkPercentSum = 0f;
            float defPercentSum = 0f;
            float hpPercentSum = 0f;
            int atkFlatSum = 0;
            int defFlatSum = 0;
            int hpFlatSum = 0;

            // 应用小猫自身的 buff（攻防血）
            if (cat.buffEntries != null && cat.buffEntries.Count > 0)
            {
                foreach (var entry in cat.buffEntries)
                {
                    // 跳过全局奇物 buff，由 PlayerData.globalCatAttackFlatBonus 动态计算
                    if (entry.source == BuffSource.Artifact && entry.choiceId == "Artifact_CatAttackFlat_Global")
                        continue;

                    switch (entry.statType)
                    {
                        case StatType.Attack:
                            if (entry.isPercent) atkPercentSum += entry.value;
                            else atkFlatSum += Mathf.RoundToInt(entry.value);
                            break;
                        case StatType.Defense:
                            if (entry.isPercent) defPercentSum += entry.value;
                            else defFlatSum += Mathf.RoundToInt(entry.value);
                            break;
                        case StatType.Hp:
                            if (entry.isPercent) hpPercentSum += entry.value;
                            else hpFlatSum += Mathf.RoundToInt(entry.value);
                            break;
                    }
                }
            }

            // 全局奇物加成：直接从 PlayerData 读取，确保所有小猫一致
            var globalBonus = GameManager.Instance?.DataManager?.PlayerData?.globalCatAttackFlatBonus ?? 0;
            if (globalBonus > 0)
                atkFlatSum += globalBonus;

            catAtk = ApplyModifiers(catAtk, atkPercentSum, atkFlatSum);
            catDef = ApplyModifiers(catDef, defPercentSum, defFlatSum);
            catHp = ApplyModifiers(catHp, hpPercentSum, hpFlatSum);

            return new CatStats(
                Mathf.Max(1, Mathf.RoundToInt(catAtk)),
                Mathf.Max(1, Mathf.RoundToInt(catDef)),
                Mathf.Max(1, Mathf.RoundToInt(catHp)),
                Mathf.Max(0.001f, catMoveSpd),
                Mathf.Max(0.001f, catAtkSpd));
        }


        /// <summary>
        /// 计算品质对应的属性比例范围（从 quality_config.json 读取）
        /// </summary>
        public static (float min, float max) GetQualityRatioRange(CatQuality quality)
        {
            var config = TribeConfigLoader.Instance?.GetQualityConfig(quality);
            if (config != null)
            {
                return (config.minRatio, config.maxRatio);
            }
            // 兜底默认值
            return (1.0f, 1.0f);
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
