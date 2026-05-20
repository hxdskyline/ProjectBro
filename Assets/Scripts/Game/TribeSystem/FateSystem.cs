using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 祈愿档次
    /// </summary>
    public enum PrayerTier
    {
        Free,       // 免费祈愿
        Normal,     // 普通祈愿
        Grand       // 盛大祈愿
    }

    /// <summary>
    /// 祈愿奖励类型
    /// </summary>
    public enum PrayerRewardType
    {
        TempStatBoost,      // 临时属性强化
        PermanentStatBoost, // 永久属性强化
        PercentStatBoost,   // 百分比永久强化
        Item,               // 道具
        CatFood,            // 木天蓼叶
        Skill               // 固有技能
    }

    /// <summary>
    /// 祈愿奖励
    /// </summary>
    [Serializable]
    public class PrayerReward
    {
        public PrayerRewardType rewardType;
        public StatType statType;           // 属性类型
        public float value;                 // 数值
        public int duration;                // 持续时间（关卡数）
        public string skillId;              // 技能ID
        public string displayName;          // 显示名称
        public string description;          // 描述

        public PrayerReward()
        {
            rewardType = PrayerRewardType.CatFood;
            statType = StatType.Attack;
            value = 0;
            duration = 0;
            skillId = "";
            displayName = "";
            description = "";
        }
    }

    /// <summary>
    /// 命运系统 - 祈愿玩法
    /// 玩家选择一个祈愿档次，消耗木天蓼叶后从祝福池中抽取奖励
    /// </summary>
    public class FateSystem
    {
        private DataManager _dataManager;
        private AuraService _auraService;

        // 祈愿档次配置
        private readonly Dictionary<PrayerTier, int> _tierCosts = new Dictionary<PrayerTier, int>
        {
            { PrayerTier.Free, 0 },
            { PrayerTier.Normal, 300 },
            { PrayerTier.Grand, 600 }
        };

        // 基础保底奖励
        private const int BASE_GUARANTEE_REWARD = 100;

        // 事件
        public event Action<PrayerReward> OnRewardReceived;
        public event Action OnPrayerComplete;

        public FateSystem()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        public void SetAuraService(AuraService auraService)
        {
            _auraService = auraService;
        }

        /// <summary>
        /// 获取祈愿档次费用
        /// </summary>
        public int GetTierCost(PrayerTier tier)
        {
            return _tierCosts.ContainsKey(tier) ? _tierCosts[tier] : 0;
        }

        /// <summary>
        /// 检查是否可以选择某个档次
        /// </summary>
        public bool CanSelectTier(PrayerTier tier)
        {
            int cost = GetTierCost(tier);
            int currentCatFood = (int)(_dataManager?.PlayerData?.catFood ?? 0);

            return currentCatFood >= cost;
        }

        /// <summary>
        /// 执行祈愿
        /// </summary>
        public List<PrayerReward> PerformPrayer(PrayerTier tier)
        {
            var rewards = new List<PrayerReward>();

            // 检查是否可以选择
            if (!CanSelectTier(tier))
            {
                Debug.LogWarning($"[FateSystem] 无法选择档次 {tier}，木天蓼叶不足");
                return rewards;
            }

            // 消耗木天蓼叶
            int cost = GetTierCost(tier);
            if (cost > 0)
            {
                _dataManager?.AddCatFood(-cost);
            }

            // 生成奖励
            rewards = GenerateRewards(tier);

            // 添加保底木天蓼叶
            var guaranteeReward = new PrayerReward
            {
                rewardType = PrayerRewardType.CatFood,
                value = BASE_GUARANTEE_REWARD,
                displayName = "保底木天蓼叶",
                description = $"获得 {BASE_GUARANTEE_REWARD} 木天蓼叶"
            };
            rewards.Add(guaranteeReward);

            // 应用奖励
            foreach (var reward in rewards)
            {
                ApplyReward(reward);
                OnRewardReceived?.Invoke(reward);
            }

            OnPrayerComplete?.Invoke();

            return rewards;
        }

        /// <summary>
        /// 生成祈愿奖励（3条祝福）
        /// </summary>
        private List<PrayerReward> GenerateRewards(PrayerTier tier)
        {
            var rewards = new List<PrayerReward>();

            switch (tier)
            {
                case PrayerTier.Free:
                    rewards = GenerateFreeRewards();
                    break;
                case PrayerTier.Normal:
                    rewards = GenerateNormalRewards();
                    break;
                case PrayerTier.Grand:
                    rewards = GenerateGrandRewards();
                    break;
            }

            // 尝试生成固有技能选项
            var skillReward = GenerateSkillReward();
            if (skillReward != null)
            {
                rewards.Add(skillReward);
            }

            return rewards;
        }

        /// <summary>
        /// 生成免费祈愿奖励
        /// </summary>
        private List<PrayerReward> GenerateFreeRewards()
        {
            var rewards = new List<PrayerReward>();

            // 临时属性强化 30%
            if (UnityEngine.Random.value < 0.3f)
            {
                var reward = GenerateTempStatBoost(10, 30, 3);
                rewards.Add(reward);
            }
            // 道具 30%
            else if (UnityEngine.Random.value < 0.3f)
            {
                var reward = GenerateItemReward();
                rewards.Add(reward);
            }
            // 木天蓼叶 20%
            else if (UnityEngine.Random.value < 0.2f)
            {
                var reward = GenerateCatFoodReward(50, 150);
                rewards.Add(reward);
            }

            return rewards;
        }

        /// <summary>
        /// 生成普通祈愿奖励
        /// </summary>
        private List<PrayerReward> GenerateNormalRewards()
        {
            var rewards = new List<PrayerReward>();

            // 永久属性强化 40%
            if (UnityEngine.Random.value < 0.4f)
            {
                var reward = GeneratePermanentStatBoost(20, 50);
                rewards.Add(reward);
            }
            // 道具 30%
            else if (UnityEngine.Random.value < 0.3f)
            {
                var reward = GenerateItemReward();
                rewards.Add(reward);
                // 可能获得2个道具
                if (UnityEngine.Random.value < 0.5f)
                {
                    rewards.Add(GenerateItemReward());
                }
            }
            // 木天蓼叶 20%
            else if (UnityEngine.Random.value < 0.2f)
            {
                var reward = GenerateCatFoodReward(100, 300);
                rewards.Add(reward);
            }

            return rewards;
        }

        /// <summary>
        /// 生成盛大祈愿奖励
        /// </summary>
        private List<PrayerReward> GenerateGrandRewards()
        {
            var rewards = new List<PrayerReward>();

            // 百分比永久强化 50%
            if (UnityEngine.Random.value < 0.5f)
            {
                var reward = GeneratePercentStatBoost(5, 15);
                rewards.Add(reward);
            }
            // 木天蓼叶 50%
            else
            {
                var reward = GenerateCatFoodReward(300, 800);
                rewards.Add(reward);
            }

            return rewards;
        }

        /// <summary>
        /// 生成临时属性强化奖励
        /// </summary>
        private PrayerReward GenerateTempStatBoost(float minValue, float maxValue, int duration)
        {
            var statType = GetRandomStatType();
            float value = UnityEngine.Random.Range(minValue, maxValue);

            return new PrayerReward
            {
                rewardType = PrayerRewardType.TempStatBoost,
                statType = statType,
                value = value,
                duration = duration,
                displayName = $"{GetStatName(statType)} +{value:F0}（{duration}回合）",
                description = $"{duration}回合内{GetStatName(statType)} +{value:F0}"
            };
        }

        /// <summary>
        /// 生成永久属性强化奖励
        /// </summary>
        private PrayerReward GeneratePermanentStatBoost(float minValue, float maxValue)
        {
            var statType = GetRandomStatType();
            float value = UnityEngine.Random.Range(minValue, maxValue);

            return new PrayerReward
            {
                rewardType = PrayerRewardType.PermanentStatBoost,
                statType = statType,
                value = value,
                displayName = $"永久 {GetStatName(statType)} +{value:F0}",
                description = $"永久提升{GetStatName(statType)} {value:F0}"
            };
        }

        /// <summary>
        /// 生成百分比永久强化奖励
        /// </summary>
        private PrayerReward GeneratePercentStatBoost(float minPercent, float maxPercent)
        {
            var statType = GetRandomStatType();
            float percent = UnityEngine.Random.Range(minPercent, maxPercent);

            return new PrayerReward
            {
                rewardType = PrayerRewardType.PercentStatBoost,
                statType = statType,
                value = percent,
                displayName = $"永久 {GetStatName(statType)} +{percent:F1}%",
                description = $"永久提升{GetStatName(statType)} {percent:F1}%"
            };
        }

        /// <summary>
        /// 生成道具奖励
        /// </summary>
        private PrayerReward GenerateItemReward()
        {
            // TODO: 从道具池中随机选择道具
            return new PrayerReward
            {
                rewardType = PrayerRewardType.Item,
                displayName = "随机道具",
                description = "获得1个随机道具"
            };
        }

        /// <summary>
        /// 生成木天蓼叶奖励
        /// </summary>
        private PrayerReward GenerateCatFoodReward(float minValue, float maxValue)
        {
            int amount = Mathf.RoundToInt(UnityEngine.Random.Range(minValue, maxValue));

            return new PrayerReward
            {
                rewardType = PrayerRewardType.CatFood,
                value = amount,
                displayName = $"{amount} 木天蓼叶",
                description = $"获得 {amount} 木天蓼叶"
            };
        }

        /// <summary>
        /// 生成固有技能奖励
        /// </summary>
        private PrayerReward GenerateSkillReward()
        {
            // 30%概率生成固有技能选项
            if (UnityEngine.Random.value > 0.3f)
                return null;

            // TODO: 从技能配置中读取未解锁的技能
            // 目前返回null
            return null;
        }

        /// <summary>
        /// 应用奖励
        /// </summary>
        private void ApplyReward(PrayerReward reward)
        {
            switch (reward.rewardType)
            {
                case PrayerRewardType.TempStatBoost:
                    ApplyTempStatBoost(reward);
                    break;
                case PrayerRewardType.PermanentStatBoost:
                    ApplyPermanentStatBoost(reward);
                    break;
                case PrayerRewardType.PercentStatBoost:
                    ApplyPercentStatBoost(reward);
                    break;
                case PrayerRewardType.CatFood:
                    _dataManager?.AddCatFood(Mathf.RoundToInt(reward.value));
                    break;
                case PrayerRewardType.Item:
                    // TODO: 应用道具奖励
                    break;
                case PrayerRewardType.Skill:
                    // TODO: 解锁技能
                    break;
            }
        }

        /// <summary>
        /// 应用临时属性强化
        /// </summary>
        private void ApplyTempStatBoost(PrayerReward reward)
        {
            // TODO: 为所有单位添加临时buff
            Debug.Log($"[FateSystem] 应用临时强化: {reward.displayName}");
        }

        /// <summary>
        /// 应用永久属性强化
        /// </summary>
        private void ApplyPermanentStatBoost(PrayerReward reward)
        {
            // TODO: 为所有单位添加永久buff
            Debug.Log($"[FateSystem] 应用永久强化: {reward.displayName}");
        }

        /// <summary>
        /// 应用百分比永久强化
        /// </summary>
        private void ApplyPercentStatBoost(PrayerReward reward)
        {
            // TODO: 为所有单位添加百分比永久buff
            Debug.Log($"[FateSystem] 应用百分比强化: {reward.displayName}");
        }

        /// <summary>
        /// 获取随机属性类型
        /// </summary>
        private StatType GetRandomStatType()
        {
            var statTypes = new StatType[] { StatType.Attack, StatType.Defense, StatType.Hp, StatType.MoveSpeed };
            return statTypes[UnityEngine.Random.Range(0, statTypes.Length)];
        }

        /// <summary>
        /// 获取属性名称
        /// </summary>
        private string GetStatName(StatType statType)
        {
            switch (statType)
            {
                case StatType.Attack: return "攻击力";
                case StatType.Defense: return "防御力";
                case StatType.Hp: return "生命值";
                case StatType.MoveSpeed: return "移动速度";
                default: return statType.ToString();
            }
        }
    }
}
