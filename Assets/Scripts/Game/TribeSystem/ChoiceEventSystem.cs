using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 抉择选项类型
    /// </summary>
    public enum ChoiceOptionType
    {
        LowRisk,    // 低风险
        HighRisk,   // 高风险
        Both        // 我全要了
    }

    /// <summary>
    /// 抉择事件
    /// </summary>
    [Serializable]
    public class ChoiceEvent
    {
        public string eventId;              // 事件ID
        public string eventName;            // 事件名称
        public string eventDescription;     // 事件描述
        public ChoiceEventOption lowRiskOption;    // 低风险选项
        public ChoiceEventOption highRiskOption;   // 高风险选项
        public ChoiceEventOption bothOption;       // 我全要了选项

        public ChoiceEvent()
        {
            eventId = "";
            eventName = "";
            eventDescription = "";
            lowRiskOption = new ChoiceEventOption();
            highRiskOption = new ChoiceEventOption();
            bothOption = new ChoiceEventOption();
        }
    }

    /// <summary>
    /// 抉择选项
    /// </summary>
    [Serializable]
    public class ChoiceEventOption
    {
        public ChoiceOptionType optionType;
        public string optionName;
        public string description;
        public List<RewardEffect> rewards;      // 奖励效果
        public List<DebuffEffect> debuffs;      // 负面效果

        public ChoiceEventOption()
        {
            optionType = ChoiceOptionType.LowRisk;
            optionName = "";
            description = "";
            rewards = new List<RewardEffect>();
            debuffs = new List<DebuffEffect>();
        }
    }

    /// <summary>
    /// 奖励效果
    /// </summary>
    [Serializable]
    public class RewardEffect
    {
        public RewardEffectType effectType;
        public float value;
        public int duration;        // 持续时间（关卡数）
        public StatType statType;   // 属性类型

        public RewardEffect()
        {
            effectType = RewardEffectType.CatFood;
            value = 0;
            duration = 0;
            statType = StatType.Attack;
        }
    }

    /// <summary>
    /// 负面效果
    /// </summary>
    [Serializable]
    public class DebuffEffect
    {
        public DebuffEffectType effectType;
        public float value;
        public int duration;
        public StatType statType;

        public DebuffEffect()
        {
            effectType = DebuffEffectType.AttackReduction;
            value = 0;
            duration = 0;
            statType = StatType.Attack;
        }
    }

    /// <summary>
    /// 奖励效果类型
    /// </summary>
    public enum RewardEffectType
    {
        CatFood,            // 木天蓼叶
        StatBoost,          // 属性提升
        Item,               // 道具
        Unit                // 单位
    }

    /// <summary>
    /// 负面效果类型
    /// </summary>
    public enum DebuffEffectType
    {
        AttackReduction,    // 攻击力降低
        DefenseReduction,   // 防御力降低
        SpeedReduction,     // 移动速度降低
        WeatherEffect       // 天气效果
    }

    /// <summary>
    /// 抉择系统 - 三选一随机事件
    /// 每个事件提供三个选项：低风险低回报、高风险高回报、我全要了
    /// </summary>
    public class ChoiceEventSystem
    {
        private DataManager _dataManager;

        // 事件池
        private List<ChoiceEvent> _eventPool;

        // 历史记录
        private List<ChoiceEventHistory> _history;

        // 事件
        public event Action<ChoiceEvent> OnEventTriggered;
        public event Action<ChoiceEventOption> OnOptionSelected;

        public ChoiceEventSystem()
        {
            _dataManager = GameManager.Instance?.DataManager;
            _history = new List<ChoiceEventHistory>();
            InitializeEventPool();
        }

        /// <summary>
        /// 初始化事件池
        /// </summary>
        private void InitializeEventPool()
        {
            _eventPool = new List<ChoiceEvent>();

            // 根据需求文档创建6个事件
            _eventPool.Add(CreateStrayCatGiftEvent());
            _eventPool.Add(CreateAncientTrainingGroundEvent());
            _eventPool.Add(CreateSecretTrainingCampEvent());
            _eventPool.Add(CreateMysteriousMerchantEvent());
            _eventPool.Add(CreateForbiddenPowerEvent());
            _eventPool.Add(CreateMerchantTrapEvent());
        }

        /// <summary>
        /// 获取当前关卡可触发的事件
        /// </summary>
        public ChoiceEvent GetEventForRound(int round)
        {
            // 根据需求文档：
            // 第5关：流浪猫的馈赠、古老的训练场、秘密训练营
            // 第10关：神秘商人、禁忌的力量、奸商的陷阱
            // 第15关：流浪猫的馈赠、禁忌的力量、秘密训练营

            List<ChoiceEvent> availableEvents = new List<ChoiceEvent>();

            if (round == 5)
            {
                availableEvents.Add(_eventPool[0]); // 流浪猫的馈赠
                availableEvents.Add(_eventPool[1]); // 古老的训练场
                availableEvents.Add(_eventPool[2]); // 秘密训练营
            }
            else if (round == 10)
            {
                availableEvents.Add(_eventPool[3]); // 神秘商人
                availableEvents.Add(_eventPool[4]); // 禁忌的力量
                availableEvents.Add(_eventPool[5]); // 奸商的陷阱
            }
            else if (round == 15)
            {
                availableEvents.Add(_eventPool[0]); // 流浪猫的馈赠
                availableEvents.Add(_eventPool[4]); // 禁忌的力量
                availableEvents.Add(_eventPool[2]); // 秘密训练营
            }
            else
            {
                // 其他关卡随机选择一个事件
                if (_eventPool.Count > 0)
                {
                    availableEvents.Add(_eventPool[UnityEngine.Random.Range(0, _eventPool.Count)]);
                }
            }

            if (availableEvents.Count == 0)
                return null;

            // 随机选择一个事件
            return availableEvents[UnityEngine.Random.Range(0, availableEvents.Count)];
        }

        /// <summary>
        /// 选择选项
        /// </summary>
        public void SelectOption(ChoiceEvent choiceEvent, ChoiceEventOption option)
        {
            if (choiceEvent == null || option == null) return;

            // 应用奖励
            foreach (var reward in option.rewards)
            {
                ApplyReward(reward);
            }

            // 应用负面效果
            foreach (var debuff in option.debuffs)
            {
                ApplyDebuff(debuff);
            }

            // 记录历史
            RecordHistory(choiceEvent, option);

            OnOptionSelected?.Invoke(option);
        }

        /// <summary>
        /// 应用奖励
        /// </summary>
        private void ApplyReward(RewardEffect reward)
        {
            switch (reward.effectType)
            {
                case RewardEffectType.CatFood:
                    _dataManager?.AddCatFood(Mathf.RoundToInt(reward.value));
                    break;
                case RewardEffectType.StatBoost:
                    // TODO: 应用属性提升
                    Debug.Log($"[ChoiceEventSystem] 应用属性提升: {reward.statType} +{reward.value}");
                    break;
                case RewardEffectType.Item:
                    // TODO: 应用道具奖励
                    break;
                case RewardEffectType.Unit:
                    // TODO: 应用单位奖励
                    break;
            }
        }

        /// <summary>
        /// 应用负面效果
        /// </summary>
        private void ApplyDebuff(DebuffEffect debuff)
        {
            // TODO: 根据负面效果类型应用
            Debug.Log($"[ChoiceEventSystem] 应用负面效果: {debuff.effectType}");
        }

        /// <summary>
        /// 记录历史
        /// </summary>
        private void RecordHistory(ChoiceEvent choiceEvent, ChoiceEventOption option)
        {
            var history = new ChoiceEventHistory
            {
                round = _dataManager?.GetCurrentRound() ?? 1,
                eventId = choiceEvent.eventId,
                eventName = choiceEvent.eventName,
                selectedOption = option.optionType,
                timestamp = DateTime.Now
            };

            _history.Add(history);
        }

        /// <summary>
        /// 检查事件是否已触发过
        /// </summary>
        public bool HasEventTriggered(string eventId)
        {
            foreach (var h in _history)
            {
                if (h.eventId == eventId)
                    return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  事件创建方法
        // ═══════════════════════════════════════════════════════════

        private ChoiceEvent CreateStrayCatGiftEvent()
        {
            return new ChoiceEvent
            {
                eventId = "stray_cat_gift",
                eventName = "流浪猫的馈赠",
                eventDescription = "一只流浪猫向你展示了它收藏的宝物...",
                lowRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.LowRisk,
                    optionName = "接受馈赠",
                    description = "获得 200 木天蓼叶",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.CatFood, value = 200 }
                    },
                    debuffs = new List<DebuffEffect>()
                },
                highRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.HighRisk,
                    optionName = "拿走所有宝物",
                    description = "获得 500 木天蓼叶，但下场战斗攻击力降低 10%",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.CatFood, value = 500 }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.AttackReduction,
                            value = 0.1f,
                            duration = 1,
                            statType = StatType.Attack
                        }
                    }
                },
                bothOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.Both,
                    optionName = "我全要了",
                    description = "获得 700 木天蓼叶，但下场战斗出现 2 种天气效果",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.CatFood, value = 700 }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.WeatherEffect,
                            value = 2,
                            duration = 1
                        }
                    }
                }
            };
        }

        private ChoiceEvent CreateAncientTrainingGroundEvent()
        {
            return new ChoiceEvent
            {
                eventId = "ancient_training_ground",
                eventName = "古老的训练场",
                eventDescription = "你发现了一处古老的训练场，似乎可以提升实力...",
                lowRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.LowRisk,
                    optionName = "基础训练",
                    description = "3 回合内攻击力 +20%",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Attack,
                            value = 0.2f,
                            duration = 3
                        }
                    },
                    debuffs = new List<DebuffEffect>()
                },
                highRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.HighRisk,
                    optionName = "极限训练",
                    description = "3 回合内攻击力 -10%，之后永久攻击力 +5%",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Attack,
                            value = 0.05f,
                            duration = -1 // 永久
                        }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.AttackReduction,
                            value = 0.1f,
                            duration = 3,
                            statType = StatType.Attack
                        }
                    }
                },
                bothOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.Both,
                    optionName = "超负荷训练",
                    description = "3 回合内攻击力 +30%，但下场战斗出现 2 种天气效果",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Attack,
                            value = 0.3f,
                            duration = 3
                        }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.WeatherEffect,
                            value = 2,
                            duration = 1
                        }
                    }
                }
            };
        }

        private ChoiceEvent CreateSecretTrainingCampEvent()
        {
            return new ChoiceEvent
            {
                eventId = "secret_training_camp",
                eventName = "秘密训练营",
                eventDescription = "一个神秘的训练营向你发出了邀请...",
                lowRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.LowRisk,
                    optionName = "防御训练",
                    description = "3 回合内防御力 +20%",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Defense,
                            value = 0.2f,
                            duration = 3
                        }
                    },
                    debuffs = new List<DebuffEffect>()
                },
                highRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.HighRisk,
                    optionName = "派出猫咪训练",
                    description = "2 只猫咪前往训练，2 回合后获得 4 只强化猫咪",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.Unit,
                            value = 4
                        }
                    },
                    debuffs = new List<DebuffEffect>()
                },
                bothOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.Both,
                    optionName = "全员强化训练",
                    description = "3 回合内防御力 +30%，但下场战斗出现 2 种天气效果",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Defense,
                            value = 0.3f,
                            duration = 3
                        }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.WeatherEffect,
                            value = 2,
                            duration = 1
                        }
                    }
                }
            };
        }

        private ChoiceEvent CreateMysteriousMerchantEvent()
        {
            return new ChoiceEvent
            {
                eventId = "mysterious_merchant",
                eventName = "神秘商人",
                eventDescription = "一位神秘的商人出现在你面前，展示了珍贵的商品...",
                lowRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.LowRisk,
                    optionName = "购买普通饰品",
                    description = "获得 1 个普通饰品",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.Item, value = 1 }
                    },
                    debuffs = new List<DebuffEffect>()
                },
                highRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.HighRisk,
                    optionName = "购买稀有饰品",
                    description = "获得 1 个稀有饰品，但下场战斗移动速度降低 15%",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.Item, value = 2 }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.SpeedReduction,
                            value = 0.15f,
                            duration = 1,
                            statType = StatType.MoveSpeed
                        }
                    }
                },
                bothOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.Both,
                    optionName = "购买全部饰品",
                    description = "获得 2 个饰品，但下场战斗出现 2 种天气效果",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.Item, value = 2 }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.WeatherEffect,
                            value = 2,
                            duration = 1
                        }
                    }
                }
            };
        }

        private ChoiceEvent CreateForbiddenPowerEvent()
        {
            return new ChoiceEvent
            {
                eventId = "forbidden_power",
                eventName = "禁忌的力量",
                eventDescription = "你感受到了一股禁忌的力量，它似乎可以增强你的实力...",
                lowRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.LowRisk,
                    optionName = "基础强化",
                    description = "2 回合内全属性 +10%",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Attack,
                            value = 0.1f,
                            duration = 2
                        }
                    },
                    debuffs = new List<DebuffEffect>()
                },
                highRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.HighRisk,
                    optionName = "禁忌契约",
                    description = "2 回合内全属性 -15%，之后永久全属性 +8%",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Attack,
                            value = 0.08f,
                            duration = -1 // 永久
                        }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.AttackReduction,
                            value = 0.15f,
                            duration = 2,
                            statType = StatType.Attack
                        }
                    }
                },
                bothOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.Both,
                    optionName = "全力爆发",
                    description = "2 回合内全属性 +20%，但下场战斗出现 2 种天气效果",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect
                        {
                            effectType = RewardEffectType.StatBoost,
                            statType = StatType.Attack,
                            value = 0.2f,
                            duration = 2
                        }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.WeatherEffect,
                            value = 2,
                            duration = 1
                        }
                    }
                }
            };
        }

        private ChoiceEvent CreateMerchantTrapEvent()
        {
            return new ChoiceEvent
            {
                eventId = "merchant_trap",
                eventName = "奸商的陷阱",
                eventDescription = "一位奸商向你展示了一个看似诱人的交易...",
                lowRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.LowRisk,
                    optionName = "安全交易",
                    description = "获得 300 木天蓼叶",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.CatFood, value = 300 }
                    },
                    debuffs = new List<DebuffEffect>()
                },
                highRiskOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.HighRisk,
                    optionName = "高风险交易",
                    description = "获得 800 木天蓼叶，但下回合商店价格 +20% 且无法刷新",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.CatFood, value = 800 }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.WeatherEffect, // 用天气效果表示商店debuff
                            value = 1,
                            duration = 1
                        }
                    }
                },
                bothOption = new ChoiceEventOption
                {
                    optionType = ChoiceOptionType.Both,
                    optionName = "全都要",
                    description = "获得 1100 木天蓼叶，但下场战斗出现 2 种天气效果",
                    rewards = new List<RewardEffect>
                    {
                        new RewardEffect { effectType = RewardEffectType.CatFood, value = 1100 }
                    },
                    debuffs = new List<DebuffEffect>
                    {
                        new DebuffEffect
                        {
                            effectType = DebuffEffectType.WeatherEffect,
                            value = 2,
                            duration = 1
                        }
                    }
                }
            };
        }
    }

    /// <summary>
    /// 抉择历史记录
    /// </summary>
    [Serializable]
    public class ChoiceEventHistory
    {
        public int round;                    // 发生回合
        public string eventId;               // 事件ID
        public string eventName;             // 事件名称
        public ChoiceOptionType selectedOption; // 选中的选项
        public DateTime timestamp;           // 时间戳
    }
}
