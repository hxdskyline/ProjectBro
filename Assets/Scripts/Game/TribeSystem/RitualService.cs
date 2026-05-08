using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LitJson;
using BattleSystem;

namespace TribeSystem
{
    /// <summary>
    /// 祈愿（祭祀）服务 - 需求优化版
    /// 三种效果：气运/战舞/通灵
    /// 三档消耗：0/300/600
    /// 必定额外获得饰品或猫粮
    /// </summary>
    public class RitualService
    {
        private DataManager _dataManager;
        private AuraService _auraService;

        // 消耗档次配置（可配置）
        private static readonly int[] CostTiers = { 0, 300, 600 };

        // 每档消耗对应的可出品质
        // 0消耗: 蓝/紫; 300消耗: 紫/金; 600消耗: 金/橙
        private static readonly PrayerGrade[][] GradesByCostTier = {
            new[] { PrayerGrade.Blue, PrayerGrade.Purple },
            new[] { PrayerGrade.Purple, PrayerGrade.Gold },
            new[] { PrayerGrade.Gold, PrayerGrade.Orange }
        };

        public RitualService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        public void SetAuraService(AuraService auraService)
        {
            _auraService = auraService;
        }

        // ─── 兼容旧 UI 的方法（RitualPanel 调用）────────────────────

        /// <summary>
        /// 获取所有可用档次（兼容旧UI）
        /// </summary>
        public List<RitualTier> GetTiers()
        {
            return TribeConfigLoader.Instance.GetRitualConfig()?.tiers ?? new List<RitualTier>();
        }

        /// <summary>
        /// 从指定档次的祝福池中按权重抽取 drawCount 条（兼容旧UI）
        /// </summary>
        public List<RitualRewardItem> DrawBlessings(RitualTier tier)
        {
            var result = new List<RitualRewardItem>();
            if (tier?.blessings == null || tier.blessings.Count == 0) return result;

            int count = Mathf.Max(1, tier.drawCount);
            HashSet<string> generatedKeys = new HashSet<string>();

            int maxAttempts = count * 10;
            int attempts = 0;

            while (result.Count < count && attempts < maxAttempts)
            {
                attempts++;
                var item = SelectAndCreateBlessing(tier);
                if (item != null)
                {
                    // 生成唯一标识，避免同一类型和子类型的重复
                    string key = $"{item.rewardType}_{item.statType}_{item.catQuality}";
                    if (!generatedKeys.Contains(key))
                    {
                        generatedKeys.Add(key);
                        result.Add(item);
                    }
                }
            }

            // 额外生成1个族长技能选项（如果可用）
            var skillOption = GenerateLeaderSkillOption();
            if (skillOption != null)
            {
                result.Add(skillOption);
            }

            return result;
        }

        /// <summary>
        /// 执行祭祀（兼容旧UI）：扣猫粮，应用选中的祝福
        /// </summary>
        public bool ExecuteRitual(RitualTier tier, RitualRewardItem blessing)
        {
            if (tier == null || blessing == null) return false;

            if (!_dataManager.TrySpendCatFood(tier.cost))
            {
                Debug.LogWarning("[RitualService] Not enough cat food for ritual");
                return false;
            }

            TribeRecord targetTribe = null;
            if (blessing.rewardType == RitualRewardType.LeaderStatBoostTemporary
             || blessing.rewardType == RitualRewardType.LeaderStatBoostPermanent
             || blessing.rewardType == RitualRewardType.LeaderStatBoostPercent
             || blessing.rewardType == RitualRewardType.Cats)
            {
                targetTribe = GetRandomActiveTribe();
                if (targetTribe != null)
                    blessing.catTribeType = targetTribe.tribeType;
            }

            ApplyRewardItem(blessing, targetTribe);

            // 必定额外获得饰品或猫粮
            ApplyGuaranteedBonusLegacy(blessing);

            _dataManager.SavePlayerData();
            Debug.Log($"[RitualService] Ritual executed: {blessing.displayName} (tier={tier.tierName}, cost={tier.cost})");
            return true;
        }

        /// <summary>
        /// 获取消耗档次列表
        /// </summary>
        public int[] GetCostTiers()
        {
            return CostTiers;
        }

        /// <summary>
        /// 获取祈愿效果类型列表
        /// </summary>
        public PrayerEffectType[] GetEffectTypes()
        {
            return new[] { PrayerEffectType.Luck, PrayerEffectType.WarDance, PrayerEffectType.SpiritCommunion };
        }

        /// <summary>
        /// 执行祈愿
        /// </summary>
        /// <param name="effectType">效果类型（气运/战舞/通灵）</param>
        /// <param name="costTierIndex">消耗档次索引（0=免费, 1=300, 2=600）</param>
        /// <param name="targetTribe">祈愿种族</param>
        /// <returns>祈愿结果</returns>
        public PrayerResult ExecutePrayer(PrayerEffectType effectType, int costTierIndex, TribeRecord targetTribe)
        {
            if (targetTribe == null)
            {
                Debug.LogWarning("[RitualService] No target tribe for prayer");
                return null;
            }

            costTierIndex = Mathf.Clamp(costTierIndex, 0, CostTiers.Length - 1);
            int cost = CostTiers[costTierIndex];

            // 扣猫粮
            if (cost > 0 && !_dataManager.TrySpendCatFood(cost))
            {
                Debug.LogWarning("[RitualService] Not enough cat food for prayer");
                return null;
            }

            // 根据消耗档次确定可出品质范围
            PrayerGrade[] availableGrades = GradesByCostTier[costTierIndex];
            PrayerGrade grade = availableGrades[Random.Range(0, availableGrades.Length)];

            // 创建祈愿结果
            var result = new PrayerResult
            {
                effectType = effectType,
                grade = grade,
                costSpent = cost,
                targetTribeType = targetTribe.tribeType
            };

            // 应用主要效果
            ApplyMainEffect(effectType, grade, targetTribe, result);

            // 必定额外获得饰品或猫粮
            ApplyGuaranteedBonus(effectType, grade, costTierIndex, targetTribe, result);

            _dataManager.SavePlayerData();
            Debug.Log($"[RitualService] Prayer executed: {effectType} {grade} grade, cost={cost}, tribe={targetTribe.tribeType}");

            return result;
        }

        /// <summary>
        /// 应用主要祈愿效果
        /// </summary>
        private void ApplyMainEffect(PrayerEffectType effectType, PrayerGrade grade, TribeRecord tribe, PrayerResult result)
        {
            switch (effectType)
            {
                case PrayerEffectType.Luck:
                    ApplyLuckEffect(grade, tribe, result);
                    break;
                case PrayerEffectType.WarDance:
                    ApplyWarDanceEffect(grade, tribe, result);
                    break;
                case PrayerEffectType.SpiritCommunion:
                    ApplySpiritCommunionEffect(grade, tribe, result);
                    break;
            }
        }

        /// <summary>
        /// 气运效果：影响地形/天气出现概率
        /// 品质越高，优势地形/天气出现概率越高
        /// </summary>
        private void ApplyLuckEffect(PrayerGrade grade, TribeRecord tribe, PrayerResult result)
        {
            float bonusPercent = GetGradeBonusPercent(grade);
            // 存储气运加成，供 BattleCampaignRuntime 在生成场景选项时使用
            // 暂时记录到结果中，后续需要一个持久化机制
            result.mainEffectDescription = $"气运加成: {tribe.tribeType}族优势地形/天气出现概率+{Mathf.RoundToInt(bonusPercent * 100)}%";

            // 暂时用 Debug 记录，实际需要存储到 DataManager 或 BattleCampaignRuntime
            Debug.Log($"[RitualService] Luck effect: {tribe.tribeType} terrain/weather probability +{bonusPercent:P0}");
        }

        /// <summary>
        /// 战舞效果：提升小猫品质
        /// 品质越高，提升的小猫数量越多/品质越高
        /// </summary>
        private void ApplyWarDanceEffect(PrayerGrade grade, TribeRecord tribe, PrayerResult result)
        {
            int evolveCount = GetWarDanceEvolveCount(grade);
            int actualEvolved = 0;

            // 随机选择小猫进行品质提升
            if (tribe.cats != null && tribe.cats.Count > 0)
            {
                var indices = new List<int>();
                for (int i = 0; i < tribe.cats.Count; i++)
                    indices.Add(i);

                // 打乱顺序
                for (int i = indices.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int temp = indices[i];
                    indices[i] = indices[j];
                    indices[j] = temp;
                }

                int toEvolve = Mathf.Min(evolveCount, indices.Count);
                for (int i = 0; i < toEvolve; i++)
                {
                    if (tribe.cats[indices[i]].TryEvolve())
                        actualEvolved++;
                }
            }

            result.mainEffectDescription = $"战舞: {tribe.tribeType}族 {actualEvolved}只小猫品质提升";
        }

        /// <summary>
        /// 通灵效果：改变心情
        /// 品质越高，心情改变效果越好
        /// </summary>
        private void ApplySpiritCommunionEffect(PrayerGrade grade, TribeRecord tribe, PrayerResult result)
        {
            // 心情系统尚未完全实现，暂时用 moodId 字段存储
            string mood = GetMoodByGrade(grade);
            tribe.moodId = mood;

            result.mainEffectDescription = $"通灵: {tribe.tribeType}族心情变为{GetMoodDisplayName(mood)}";
            Debug.Log($"[RitualService] Spirit communion: {tribe.tribeType} mood changed to {mood}");
        }

        /// <summary>
        /// 必定额外获得猫粮
        /// </summary>
        private void ApplyGuaranteedBonus(PrayerEffectType effectType, PrayerGrade grade, int costTierIndex, TribeRecord tribe, PrayerResult result)
        {
            int catFoodBonus = GetBonusCatFoodAmount(grade, costTierIndex);
            _dataManager.AddCatFood(catFoodBonus);
            result.bonusDescription = $"额外获得: {catFoodBonus}猫粮";
        }

        // ─── 品质/效果数值配置 ─────────────────────────────────────────

        private float GetGradeBonusPercent(PrayerGrade grade)
        {
            switch (grade)
            {
                case PrayerGrade.Blue: return 0.1f;
                case PrayerGrade.Purple: return 0.2f;
                case PrayerGrade.Gold: return 0.3f;
                case PrayerGrade.Orange: return 0.5f;
                default: return 0.1f;
            }
        }

        private int GetWarDanceEvolveCount(PrayerGrade grade)
        {
            switch (grade)
            {
                case PrayerGrade.Blue: return 1;
                case PrayerGrade.Purple: return 2;
                case PrayerGrade.Gold: return 3;
                case PrayerGrade.Orange: return 5;
                default: return 1;
            }
        }

        private string GetMoodByGrade(PrayerGrade grade)
        {
            switch (grade)
            {
                case PrayerGrade.Blue: return "sad";
                case PrayerGrade.Purple: return "normal";
                case PrayerGrade.Gold: return "happy";
                case PrayerGrade.Orange: return "ecstatic";
                default: return "normal";
            }
        }

        private string GetMoodDisplayName(string mood)
        {
            switch (mood)
            {
                case "sad": return "低落";
                case "normal": return "平静";
                case "happy": return "开心";
                case "ecstatic": return "狂喜";
                default: return mood;
            }
        }

        private int GetBonusCatFoodAmount(PrayerGrade grade, int costTierIndex)
        {
            int baseAmount = (costTierIndex + 1) * 50;
            switch (grade)
            {
                case PrayerGrade.Blue: return baseAmount;
                case PrayerGrade.Purple: return baseAmount * 2;
                case PrayerGrade.Gold: return baseAmount * 3;
                case PrayerGrade.Orange: return baseAmount * 5;
                default: return baseAmount;
            }
        }

        // ─── 兼容旧 UI 的内部方法 ────────────────────────────────────

        private RitualRewardItem SelectAndCreateBlessing(RitualTier tier)
        {
            int totalWeight = 0;
            foreach (var b in tier.blessings) totalWeight += b.weight;
            if (totalWeight <= 0) return null;

            int roll = Random.Range(0, totalWeight);
            int acc = 0;
            foreach (var cfg in tier.blessings)
            {
                acc += cfg.weight;
                if (roll < acc)
                    return CreateBlessingItem(cfg);
            }
            return null;
        }

        private RitualRewardItem CreateBlessingItem(RitualRewardConfig cfg)
        {
            var item = new RitualRewardItem();
            if (!System.Enum.TryParse<RitualRewardType>(cfg.type, out var rType)) return null;
            item.rewardType = rType;

            switch (rType)
            {
                case RitualRewardType.LeaderStatBoostTemporary:
                case RitualRewardType.LeaderStatBoostPermanent:
                    item.statType = ParseStat(cfg);
                    item.amount = Random.Range(cfg.minAmount, cfg.maxAmount + 1);
                    item.displayName = $"{GetStatName(item.statType)} +{item.amount}"
                        + (rType == RitualRewardType.LeaderStatBoostTemporary ? "（临时）" : "（永久）");
                    break;

                case RitualRewardType.LeaderStatBoostPercent:
                    item.statType = ParseStat(cfg);
                    float pct = Random.Range(cfg.minPercent, cfg.maxPercent);
                    item.amount = Mathf.RoundToInt(pct * 100);
                    item.displayName = $"{GetStatName(item.statType)} +{item.amount}%（永久）";
                    break;

                case RitualRewardType.Cats:
                    item.catCount = Random.Range(cfg.minCount, cfg.maxCount + 1);
                    item.catQuality = ParseQuality(cfg);
                    item.displayName = $"获得 {item.catCount} 只{GetQualityName(item.catQuality)} 小猫";
                    break;

                case RitualRewardType.Consumable:
                    item.amount = Random.Range(cfg.minCount, cfg.maxCount + 1);
                    item.consumableId = Random.Range(1, 10);
                    item.displayName = $"获得 {item.amount} 个道具";
                    break;

                case RitualRewardType.CatFood:
                    item.amount = Random.Range(cfg.minAmount, cfg.maxAmount + 1);
                    item.displayName = $"获得 {item.amount} 猫粮";
                    break;
            }
            return item;
        }

        private void ApplyRewardItem(RitualRewardItem item, TribeRecord tribe)
        {
            switch (item.rewardType)
            {
                case RitualRewardType.LeaderStatBoostTemporary:
                    if (tribe != null) ApplyTemporaryStatBoost(tribe, item.statType ?? StatType.Attack, item.amount);
                    break;
                case RitualRewardType.LeaderStatBoostPermanent:
                    if (tribe != null) ApplyPermanentStatBoost(tribe, item.statType ?? StatType.Attack, item.amount);
                    break;
                case RitualRewardType.LeaderStatBoostPercent:
                    if (tribe != null) ApplyPermanentStatPercentBoost(tribe, item.statType ?? StatType.Attack, item.amount / 100f);
                    break;
                case RitualRewardType.Cats:
                    if (tribe != null) AddCatsToTribe(tribe, item.catCount, item.catQuality ?? CatQuality.White);
                    break;
                case RitualRewardType.Consumable:
                    Debug.Log($"[RitualService] Received consumable item: {item.consumableId} x{item.amount}");
                    break;
                case RitualRewardType.CatFood:
                    _dataManager.AddCatFood(item.amount);
                    break;
                case RitualRewardType.LeaderSkill:
                    if (tribe != null && item.leaderSkillId > 0)
                    {
                        if (!tribe.leader.skillIds.Contains(item.leaderSkillId))
                        {
                            tribe.leader.skillIds.Add(item.leaderSkillId);
                            Debug.Log($"[RitualService] Unlocked leader skill {item.leaderSkillId} for tribe {tribe.tribeType}");
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 旧祈愿的必定额外奖励
        /// </summary>
        private void ApplyGuaranteedBonusLegacy(RitualRewardItem blessing)
        {
            int bonus = 100;
            _dataManager.AddCatFood(bonus);
            Debug.Log($"[RitualService] Bonus cat food: {bonus}");
        }

        /// <summary>
        /// 生成族长技能选项（从 leader_skill_config.json 读取）
        /// </summary>
        private RitualRewardItem GenerateLeaderSkillOption()
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData?.tribes == null || playerData.tribes.Count == 0) return null;

            // 加载技能配置
            var config = LoadLeaderSkillConfig();
            if (config == null) return null;

            // 收集所有可用技能（玩家已有种族 + 未解锁的）
            var availableSkills = new List<LeaderSkillData>();
            foreach (var tribe in playerData.tribes)
            {
                if (tribe.leader == null) continue;
                var tribeSkills = config.GetSkillsForTribe(tribe.tribeType);
                if (tribeSkills == null) continue;

                foreach (var skill in tribeSkills)
                {
                    // 跳过已解锁的技能
                    if (tribe.leader.skillIds.Contains(skill.skillId)) continue;
                    availableSkills.Add(skill);
                }
            }

            if (availableSkills.Count == 0) return null;

            // 随机选1个
            var selected = availableSkills[Random.Range(0, availableSkills.Count)];

            // 找到对应的族长
            TribeRecord targetTribe = null;
            foreach (var tribe in playerData.tribes)
            {
                if (tribe.leader == null) continue;
                var tribeSkills = config.GetSkillsForTribe(tribe.tribeType);
                if (tribeSkills == null) continue;
                foreach (var s in tribeSkills)
                {
                    if (s.skillId == selected.skillId)
                    {
                        targetTribe = tribe;
                        break;
                    }
                }
                if (targetTribe != null) break;
            }

            return new RitualRewardItem
            {
                rewardType = RitualRewardType.LeaderSkill,
                leaderSkillId = selected.skillId,
                catTribeType = targetTribe?.tribeType ?? TribeType.Tabby,
                displayName = $"技能: {selected.skillName}"
            };
        }

        private LeaderSkillConfigTable LoadLeaderSkillConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Tables/leader_skill_config.json");
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonMapper.ToObject<LeaderSkillConfigTable>(json);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RitualService] 加载首领技能配置失败: {e.Message}");
            }
            return null;
        }


        private void ApplyTemporaryStatBoost(TribeRecord tribe, StatType stat, int amount)
        {
            float pct = amount / 100f;
            var effects = new List<BuffEffectItem> { new BuffEffectItem(stat, true, pct) };
            string buffId = $"Ritual_Temp_{tribe.tribeType}_{stat}_{amount}";
            string displayName = $"祈愿祝福：{GetStatName(stat)} +{amount}%（3回合）";
            var scopeFilter = new BuffScopeFilter { tribe = tribe.tribeType };
            _auraService?.ApplyRoundBasedBuffToAll(
                scopeFilter, effects, 3,
                displayName, buffId, displayName);
        }

        private void ApplyPermanentStatBoost(TribeRecord tribe, StatType stat, int amount)
        {
            var effects = new List<BuffEffectItem> { new BuffEffectItem(stat, false, amount) };
            var scopeFilter = new BuffScopeFilter { role = ScopeRoleFilter.Leader, tribe = tribe.tribeType };
            var choice = GameChoice.CreateBuff(
                $"Ritual_StatBoost_{tribe.tribeType}_{stat}_{amount}",
                "祈愿强化", $"祈愿：{GetStatName(stat)} +{amount}",
                ChoiceSource.Ritual,
                scopeFilter, BuffApplyType.CurrentUnit,
                effects, tribe.tribeType);
            _auraService?.RegisterChoice(choice);
        }

        private void ApplyPermanentStatPercentBoost(TribeRecord tribe, StatType stat, float pct)
        {
            var effects = new List<BuffEffectItem> { new BuffEffectItem(stat, true, pct) };
            var scopeFilter = new BuffScopeFilter { role = ScopeRoleFilter.Leader, tribe = tribe.tribeType };
            var choice = GameChoice.CreateBuff(
                $"Ritual_StatPct_{tribe.tribeType}_{stat}_{Mathf.RoundToInt(pct * 100)}",
                "祈愿强化", $"祈愿：{GetStatName(stat)} +{Mathf.RoundToInt(pct * 100)}%",
                ChoiceSource.Ritual,
                scopeFilter, BuffApplyType.CurrentUnit,
                effects, tribe.tribeType);
            _auraService?.RegisterChoice(choice);
        }

        private void AddCatsToTribe(TribeRecord tribe, int count, CatQuality quality)
        {
            for (int i = 0; i < count; i++)
            {
                var cat = CatData.CreateWithQuality(quality, tribe.tribeType);
                _auraService?.ApplyAurasToNewCat(cat, tribe.tribeType);
                tribe.cats.Add(cat);
            }
        }

        private TribeRecord GetRandomActiveTribe()
        {
            var tribes = _dataManager?.PlayerData?.tribes;
            if (tribes == null || tribes.Count == 0) return null;
            return tribes[Random.Range(0, tribes.Count)];
        }

        private StatType ParseStat(RitualRewardConfig cfg)
        {
            if (cfg.statTypes == null || cfg.statTypes.Length == 0) return StatType.Attack;
            string s = cfg.statTypes[Random.Range(0, cfg.statTypes.Length)];
            return System.Enum.TryParse<StatType>(s, true, out var v) ? v : StatType.Attack;
        }

        private CatQuality? ParseQuality(RitualRewardConfig cfg)
        {
            if (cfg.qualities == null || cfg.qualities.Length == 0) return CatQuality.White;
            string q = cfg.qualities[Random.Range(0, cfg.qualities.Length)];
            return System.Enum.TryParse<CatQuality>(q, true, out var v) ? v : CatQuality.White;
        }

        private string GetStatName(StatType? stat)
        {
            switch (stat)
            {
                case StatType.Attack: return "攻击";
                case StatType.Defense: return "防御";
                case StatType.Hp: return "血量";
                case StatType.MoveSpeed: return "移速";
                default: return "属性";
            }
        }

        private string GetQualityName(CatQuality? q)
        {
            switch (q)
            {
                case CatQuality.White: return "白色";
                case CatQuality.Blue: return "蓝色";
                case CatQuality.Purple: return "紫色";
                case CatQuality.Gold: return "金色";
                default: return "";
            }
        }
    }

    /// <summary>
    /// 祈愿结果数据
    /// </summary>
    [System.Serializable]
    public class PrayerResult
    {
        public PrayerEffectType effectType;
        public PrayerGrade grade;
        public int costSpent;
        public TribeType targetTribeType;
        public string mainEffectDescription;
        public string bonusDescription;
    }
}
