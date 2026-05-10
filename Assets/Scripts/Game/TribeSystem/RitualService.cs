using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LitJson;
using BattleSystem;

namespace TribeSystem
{
    /// <summary>
    /// 祈愿（祭祀）服务
    /// </summary>
    public class RitualService
    {
        private DataManager _dataManager;
        private AuraService _auraService;

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
             || blessing.rewardType == RitualRewardType.LeaderStatBoostPercent)
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

        // ─── 内部方法 ────────────────────────────────────

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
