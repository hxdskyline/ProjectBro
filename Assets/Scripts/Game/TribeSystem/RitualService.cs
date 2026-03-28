using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 祭祀服务
    /// 流程：选择档次 → 从该档次的祝福池中抽取若干条 → 玩家选一条 → 执行
    /// </summary>
    public class RitualService
    {
        private DataManager _dataManager;

        public RitualService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 获取所有可用档次（用于 Step1 展示）
        /// </summary>
        public List<RitualTier> GetTiers()
        {
            return TribeConfigLoader.Instance.GetRitualConfig()?.tiers ?? new List<RitualTier>();
        }

        /// <summary>
        /// 从指定档次的祝福池中按权重抽取 drawCount 条（具体数值已经 roll 好）
        /// </summary>
        public List<RitualRewardItem> DrawBlessings(RitualTier tier)
        {
            var result = new List<RitualRewardItem>();
            if (tier?.blessings == null || tier.blessings.Count == 0) return result;

            int count = Mathf.Max(1, tier.drawCount);
            for (int i = 0; i < count; i++)
            {
                var item = SelectAndCreateBlessing(tier);
                if (item != null) result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// 执行祭祀：扣猫粮，应用选中的祝福
        /// 需要族长时自动选取随机活跃族群
        /// </summary>
        public bool ExecuteRitual(RitualTier tier, RitualRewardItem blessing)
        {
            if (tier == null || blessing == null) return false;

            if (!_dataManager.TrySpendCatFood(tier.cost))
            {
                Debug.LogWarning("[RitualService] Not enough cat food for ritual");
                return false;
            }

            // 族长类祝福需要指定族群
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
            _dataManager.SavePlayerData();
            Debug.Log($"[RitualService] Ritual executed: {blessing.displayName} (tier={tier.tierName}, cost={tier.cost})");
            return true;
        }

        // ─── 内部：抽取并实例化一条祝福 ───────────────────────────────────────

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
                    item.amount   = Random.Range(cfg.minAmount, cfg.maxAmount + 1);
                    item.displayName = $"{GetStatName(item.statType)} +{item.amount}"
                        + (rType == RitualRewardType.LeaderStatBoostTemporary ? "（临时）" : "（永久）");
                    break;

                case RitualRewardType.LeaderStatBoostPercent:
                    item.statType = ParseStat(cfg);
                    float pct = Random.Range(cfg.minPercent, cfg.maxPercent);
                    item.amount  = Mathf.RoundToInt(pct * 100);
                    item.displayName = $"{GetStatName(item.statType)} +{item.amount}%（永久）";
                    break;

                case RitualRewardType.Cats:
                    item.catCount   = Random.Range(cfg.minCount, cfg.maxCount + 1);
                    item.catQuality = ParseQuality(cfg);
                    item.displayName = $"获得 {item.catCount} 只{GetQualityName(item.catQuality)} 小猫";
                    break;

                case RitualRewardType.Consumable:
                    item.amount      = Random.Range(cfg.minCount, cfg.maxCount + 1);
                    item.consumableId = Random.Range(1, 10);
                    item.displayName = $"获得 {item.amount} 个道具";
                    break;

                case RitualRewardType.CatFood:
                    item.amount      = Random.Range(cfg.minAmount, cfg.maxAmount + 1);
                    item.displayName = $"获得 {item.amount} 猫粮";
                    break;

                case RitualRewardType.Accessory:
                    item.accessoryId = Random.Range(1, 10);
                    item.displayName = "获得随机饰品";
                    break;
            }
            return item;
        }

        // ─── 内部：应用祝福效果 ────────────────────────────────────────────────

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
                case RitualRewardType.Accessory:
                    _dataManager.UnlockAccessory($"accessory_{item.accessoryId}");
                    Debug.Log($"[RitualService] Received accessory: {item.accessoryId}");
                    break;
            }
        }

        private void ApplyTemporaryStatBoost(TribeRecord tribe, StatType stat, int amount)
        {
            if (tribe.leader.temporaryBuff == null)
                tribe.leader.temporaryBuff = new TemporaryBuff();
            var buff = tribe.leader.temporaryBuff;
            float pct = amount / 100f;
            switch (stat)
            {
                case StatType.Attack:  buff.attackPercent  += pct; break;
                case StatType.Defense: buff.defensePercent += pct; break;
                case StatType.Hp:      buff.hpPercent      += pct; break;
                case StatType.Speed:   buff.speedPercent   += pct; break;
            }
            buff.duration = 1;
        }

        private void ApplyPermanentStatBoost(TribeRecord tribe, StatType stat, int amount)
        {
            var b = tribe.leader.permanentBuffs;
            switch (stat)
            {
                case StatType.Attack:  b.attackBonus  += amount; break;
                case StatType.Defense: b.defenseBonus += amount; break;
                case StatType.Hp:      b.hpBonus      += amount; break;
                case StatType.Speed:   b.speedBonus   += amount; break;
                case StatType.Command: b.commandBonus += amount; break;
            }
        }

        private void ApplyPermanentStatPercentBoost(TribeRecord tribe, StatType stat, float pct)
        {
            var b = tribe.leader.permanentBuffs;
            switch (stat)
            {
                case StatType.Attack:  b.attackPercent  += pct; break;
                case StatType.Defense: b.defensePercent += pct; break;
                case StatType.Hp:      b.hpPercent      += pct; break;
                case StatType.Speed:   b.speedPercent   += pct; break;
                case StatType.Command: b.commandPercent += pct; break;
            }
        }

        private void AddCatsToTribe(TribeRecord tribe, int count, CatQuality quality)
        {
            for (int i = 0; i < count; i++)
                tribe.cats.Add(CatData.CreateWithQuality(quality));
        }

        // ─── 工具方法 ─────────────────────────────────────────────────────────

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
                case StatType.Attack:  return "攻击";
                case StatType.Defense: return "防御";
                case StatType.Hp:      return "血量";
                case StatType.Speed:   return "速度";
                case StatType.Command: return "统御";
                default: return "属性";
            }
        }

        private string GetQualityName(CatQuality? q)
        {
            switch (q)
            {
                case CatQuality.White:  return "白色";
                case CatQuality.Blue:   return "蓝色";
                case CatQuality.Purple: return "紫色";
                case CatQuality.Gold:   return "金色";
                default: return "";
            }
        }
    }
}
