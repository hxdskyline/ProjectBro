using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 祭祀服务 - 处理祭祀流程和奖励生成
    /// </summary>
    public class RitualService
    {
        private DataManager _dataManager;

        public RitualService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 检查当前回合是否可以祭祀
        /// </summary>
        public bool CanDoRitual(int currentRound)
        {
            var config = TribeConfigLoader.Instance.GetRitualConfig();
            return currentRound >= config.startRound && (currentRound - config.startRound) % config.ritualInterval == 0;
        }

        /// <summary>
        /// 获取祭祀种族选项（三选一）
        /// </summary>
        public List<TribeRecord> GetRitualRaceOptions()
        {
            var options = new List<TribeRecord>();
            var playerData = _dataManager?.PlayerData;

            if (playerData?.tribes == null || playerData.tribes.Count == 0)
            {
                return options;
            }

            // 随机选择3个族群（或全部如果不足3个）
            var availableTribes = new List<TribeRecord>(playerData.tribes);

            while (availableTribes.Count > 0 && options.Count < 3)
            {
                int index = Random.Range(0, availableTribes.Count);
                options.Add(availableTribes[index]);
                availableTribes.RemoveAt(index);
            }

            return options;
        }

        /// <summary>
        /// 执行祭祀
        /// </summary>
        /// <param name="tribe">选中的族群</param>
        /// <param name="cost">消耗的猫粮</param>
        /// <returns>祭祀奖励</returns>
        public RitualReward ExecuteRitual(TribeRecord tribe, int cost)
        {
            if (!_dataManager.TrySpendCatFood(cost))
            {
                Debug.LogWarning("[RitualService] Not enough cat food for ritual");
                return null;
            }

            // 根据消耗确定奖励档次
            RitualTier tier = DetermineRitualTier(cost);

            // 生成奖励
            var reward = GenerateRitualReward(tier, tribe);

            // 应用奖励
            ApplyRitualReward(reward, tribe);

            _dataManager.SavePlayerData();
            Debug.Log($"[RitualService] Ritual executed for tribe {tribe.tribeType} with cost {cost}");

            return reward;
        }

        private RitualTier DetermineRitualTier(int cost)
        {
            var config = TribeConfigLoader.Instance.GetRitualConfig();

            foreach (var tier in config.tiers)
            {
                if (cost >= tier.costRange[0] && cost <= tier.costRange[1])
                {
                    return tier;
                }
            }

            // 默认返回最低档
            return config.tiers[0];
        }

        private RitualReward GenerateRitualReward(RitualTier tier, TribeRecord tribe)
        {
            var reward = new RitualReward();

            // 确定奖励数量
            int rewardCount = Random.Range(tier.rewardCount[0], tier.rewardCount[1] + 1);

            // 使用加权随机选择奖励类型
            for (int i = 0; i < rewardCount; i++)
            {
                var rewardItem = SelectRandomReward(tier, tribe);
                if (rewardItem != null)
                {
                    reward.rewards.Add(rewardItem);
                }
            }

            return reward;
        }

        private RitualRewardItem SelectRandomReward(RitualTier tier, TribeRecord tribe)
        {
            // 计算总权重
            int totalWeight = 0;
            foreach (var rewardConfig in tier.rewards)
            {
                totalWeight += rewardConfig.weight;
            }

            // 加权随机
            int randomWeight = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var rewardConfig in tier.rewards)
            {
                currentWeight += rewardConfig.weight;
                if (randomWeight < currentWeight)
                {
                    return CreateRewardItem(rewardConfig, tribe);
                }
            }

            return null;
        }

        private RitualRewardItem CreateRewardItem(RitualRewardConfig config, TribeRecord tribe)
        {
            var item = new RitualRewardItem();

            // 解析奖励类型
            if (System.Enum.TryParse<RitualRewardType>(config.type, out var rewardType))
            {
                item.rewardType = rewardType;

                switch (rewardType)
                {
                    case RitualRewardType.LeaderStatBoostTemporary:
                    case RitualRewardType.LeaderStatBoostPermanent:
                        item.statType = ParseStatType(config.statTypes[Random.Range(0, config.statTypes.Length)]);
                        item.amount = Random.Range(config.minAmount, config.maxAmount + 1);
                        item.catTribeType = tribe.tribeType;
                        break;

                    case RitualRewardType.LeaderStatBoostPercent:
                        item.statType = ParseStatType(config.statTypes[Random.Range(0, config.statTypes.Length)]);
                        item.amount = Mathf.RoundToInt(Random.Range(config.minPercent, config.maxPercent) * 100);
                        item.catTribeType = tribe.tribeType;
                        break;

                    case RitualRewardType.Cats:
                        item.catCount = Random.Range(config.minCount, config.maxCount + 1);
                        item.catTribeType = tribe.tribeType;
                        // 随机选择品质
                        if (config.qualities != null && config.qualities.Length > 0)
                        {
                            string qualityStr = config.qualities[Random.Range(0, config.qualities.Length)];
                            if (System.Enum.TryParse<CatQuality>(qualityStr, true, out var quality))
                            {
                                item.catQuality = quality;
                            }
                        }
                        break;

                    case RitualRewardType.Consumable:
                        item.consumableId = Random.Range(1, 10); // 暂时随机ID
                        item.amount = config.minCount;
                        break;

                    case RitualRewardType.CatFood:
                        if (config.multiplierMin > 0)
                        {
                            // 使用基础返还倍数（暂时使用固定范围）
                            item.amount = Random.Range(config.minAmount, config.maxAmount + 1);
                        }
                        else
                        {
                            item.amount = Random.Range(config.minAmount, config.maxAmount + 1);
                        }
                        break;

                    case RitualRewardType.Accessory:
                        item.accessoryId = Random.Range(1, 10); // 暂时随机ID
                        break;
                }
            }

            return item;
        }

        private void ApplyRitualReward(RitualReward reward, TribeRecord tribe)
        {
            foreach (var rewardItem in reward.rewards)
            {
                ApplyRewardItem(rewardItem, tribe);
            }
        }

        private void ApplyRewardItem(RitualRewardItem item, TribeRecord tribe)
        {
            switch (item.rewardType)
            {
                case RitualRewardType.LeaderStatBoostTemporary:
                    ApplyTemporaryStatBoost(tribe, item.statType ?? StatType.Attack, item.amount);
                    break;

                case RitualRewardType.LeaderStatBoostPermanent:
                    ApplyPermanentStatBoost(tribe, item.statType ?? StatType.Attack, item.amount);
                    break;

                case RitualRewardType.LeaderStatBoostPercent:
                    ApplyPermanentStatPercentBoost(tribe, item.statType ?? StatType.Attack, item.amount / 100f);
                    break;

                case RitualRewardType.Cats:
                    AddCatsToTribe(tribe, item.catCount, item.catQuality ?? CatQuality.White);
                    break;

                case RitualRewardType.Consumable:
                    // TODO: 添加到玩家背包
                    Debug.Log($"[RitualService] Received consumable item: {item.consumableId}");
                    break;

                case RitualRewardType.CatFood:
                    _dataManager.AddCatFood(item.amount);
                    break;

                case RitualRewardType.Accessory:
                    // TODO: 添加到玩家背包并解锁图鉴
                    Debug.Log($"[RitualService] Received accessory: {item.accessoryId}");
                    _dataManager.UnlockAccessory($"accessory_{item.accessoryId}");
                    break;
            }
        }

        private void ApplyTemporaryStatBoost(TribeRecord tribe, StatType stat, int amount)
        {
            if (tribe.leader.temporaryBuff == null)
            {
                tribe.leader.temporaryBuff = new TemporaryBuff();
            }

            var buff = tribe.leader.temporaryBuff;
            switch (stat)
            {
                case StatType.Attack:
                    buff.attackPercent += amount / 100f;
                    break;
                case StatType.Defense:
                    buff.defensePercent += amount / 100f;
                    break;
                case StatType.Hp:
                    buff.hpPercent += amount / 100f;
                    break;
                case StatType.Speed:
                    buff.speedPercent += amount / 100f;
                    break;
            }
            buff.duration = 1; // 临时加成持续1回合

            Debug.Log($"[RitualService] Applied temporary {stat} boost: +{amount}");
        }

        private void ApplyPermanentStatBoost(TribeRecord tribe, StatType stat, int amount)
        {
            var buffs = tribe.leader.permanentBuffs;
            switch (stat)
            {
                case StatType.Attack:
                    buffs.attackBonus += amount;
                    break;
                case StatType.Defense:
                    buffs.defenseBonus += amount;
                    break;
                case StatType.Hp:
                    buffs.hpBonus += amount;
                    break;
                case StatType.Speed:
                    buffs.speedBonus += amount;
                    break;
                case StatType.Command:
                    buffs.commandBonus += amount;
                    break;
            }

            Debug.Log($"[RitualService] Applied permanent {stat} boost: +{amount}");
        }

        private void ApplyPermanentStatPercentBoost(TribeRecord tribe, StatType stat, float percent)
        {
            var buffs = tribe.leader.permanentBuffs;
            switch (stat)
            {
                case StatType.Attack:
                    buffs.attackPercent += percent;
                    break;
                case StatType.Defense:
                    buffs.defensePercent += percent;
                    break;
                case StatType.Hp:
                    buffs.hpPercent += percent;
                    break;
                case StatType.Speed:
                    buffs.speedPercent += percent;
                    break;
                case StatType.Command:
                    buffs.commandPercent += percent;
                    break;
            }

            Debug.Log($"[RitualService] Applied permanent {stat} boost: +{percent * 100}%");
        }

        private void AddCatsToTribe(TribeRecord tribe, int count, CatQuality quality)
        {
            for (int i = 0; i < count; i++)
            {
                tribe.cats.Add(CatData.CreateWithQuality(quality));
            }
            Debug.Log($"[RitualService] Added {count} {quality} cats to tribe {tribe.tribeType}");
        }

        private StatType ParseStatType(string statStr)
        {
            if (System.Enum.TryParse<StatType>(statStr, true, out var stat))
            {
                return stat;
            }
            return StatType.Attack;
        }
    }
}
