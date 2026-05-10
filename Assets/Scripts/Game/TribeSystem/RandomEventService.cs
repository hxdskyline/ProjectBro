using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 抉择系统服务 - 处理随机事件逻辑
    /// </summary>
    public class RandomEventService
    {
        private DataManager _dataManager;

        public RandomEventService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 生成随机事件选项
        /// </summary>
        /// <param name="round">当前关卡</param>
        /// <returns>随机事件选项</returns>
        public RandomEvent GenerateRandomEvent(int round)
        {
            var eventPool = GetEventPool(round);
            if (eventPool.Count == 0)
            {
                Debug.LogWarning($"[RandomEventService] 关卡{round}没有可用的随机事件");
                return null;
            }

            // 随机选择一个事件
            int index = Random.Range(0, eventPool.Count);
            return eventPool[index];
        }

        /// <summary>
        /// 获取事件池
        /// </summary>
        private List<RandomEvent> GetEventPool(int round)
        {
            var events = new List<RandomEvent>();

            // 根据关卡添加不同类型的事件
            if (round == 5)
            {
                events.Add(CreateCatFoodEvent());
                events.Add(CreateBuffEvent());
                events.Add(CreateTrainingEvent());
            }
            else if (round == 10)
            {
                events.Add(CreateAccessoryEvent());
                events.Add(CreateRiskBuffEvent());
                events.Add(CreatePriceIncreaseEvent());
            }
            else if (round == 15)
            {
                events.Add(CreateCatFoodEvent());
                events.Add(CreateRiskBuffEvent());
                events.Add(CreateTrainingEvent());
            }

            return events;
        }

        /// <summary>
        /// 创建猫粮事件
        /// </summary>
        private RandomEvent CreateCatFoodEvent()
        {
            return new RandomEvent
            {
                eventId = "cat_food_001",
                eventName = "流浪猫的馈赠",
                lowRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.CatFood,
                    description = "获得200猫粮",
                    catFoodAmount = 200
                },
                highRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.CatFoodWithDebuff,
                    description = "获得500猫粮，但下场战斗攻击力降低10%",
                    catFoodAmount = 500,
                    debuffType = "attack",
                    debuffPercent = -0.1f,
                    debuffDuration = 1
                },
                bothOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.CatFoodWithWeather,
                    description = "获得700猫粮，但下场战斗出现2种天气效果",
                    catFoodAmount = 700,
                    extraWeatherCount = 2
                }
            };
        }

        /// <summary>
        /// 创建增益事件
        /// </summary>
        private RandomEvent CreateBuffEvent()
        {
            return new RandomEvent
            {
                eventId = "buff_001",
                eventName = "古老的训练场",
                lowRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.TemporaryBuff,
                    description = "3回合内攻击力+20%",
                    buffType = "attack",
                    buffPercent = 0.2f,
                    buffDuration = 3
                },
                highRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.PermanentBuffWithDebuff,
                    description = "3回合内攻击力-10%，之后永久攻击力+5%",
                    debuffType = "attack",
                    debuffPercent = -0.1f,
                    debuffDuration = 3,
                    permanentBuffType = "attack",
                    permanentBuffPercent = 0.05f
                },
                bothOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.TemporaryBuffWithWeather,
                    description = "3回合内攻击力+30%，但下场战斗出现2种天气效果",
                    buffType = "attack",
                    buffPercent = 0.3f,
                    buffDuration = 3,
                    extraWeatherCount = 2
                }
            };
        }

        /// <summary>
        /// 创建训练事件
        /// </summary>
        private RandomEvent CreateTrainingEvent()
        {
            return new RandomEvent
            {
                eventId = "training_001",
                eventName = "秘密训练营",
                lowRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.TemporaryBuff,
                    description = "3回合内防御力+20%",
                    buffType = "defense",
                    buffPercent = 0.2f,
                    buffDuration = 3
                },
                highRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.TrainCats,
                    description = "2只猫咪前往训练，2回合后获得4只强化猫咪",
                    catsToTrain = 2,
                    trainDuration = 2,
                    catsToReturn = 4
                },
                bothOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.TemporaryBuffWithWeather,
                    description = "3回合内防御力+30%，但下场战斗出现2种天气效果",
                    buffType = "defense",
                    buffPercent = 0.3f,
                    buffDuration = 3,
                    extraWeatherCount = 2
                }
            };
        }

        /// <summary>
        /// 创建饰品事件
        /// </summary>
        private RandomEvent CreateAccessoryEvent()
        {
            return new RandomEvent
            {
                eventId = "accessory_001",
                eventName = "神秘商人",
                lowRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.Accessory,
                    description = "获得1个普通饰品"
                },
                highRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.RareAccessoryWithDebuff,
                    description = "获得1个稀有饰品，但下场战斗移动速度降低15%",
                    debuffType = "moveSpeed",
                    debuffPercent = -0.15f,
                    debuffDuration = 1
                },
                bothOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.AccessoryWithWeather,
                    description = "获得2个饰品，但下场战斗出现2种天气效果",
                    extraWeatherCount = 2
                }
            };
        }

        /// <summary>
        /// 创建风险增益事件
        /// </summary>
        private RandomEvent CreateRiskBuffEvent()
        {
            return new RandomEvent
            {
                eventId = "risk_buff_001",
                eventName = "禁忌的力量",
                lowRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.TemporaryBuff,
                    description = "2回合内全属性+10%",
                    buffType = "all",
                    buffPercent = 0.1f,
                    buffDuration = 2
                },
                highRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.PermanentBuffWithDebuff,
                    description = "2回合内全属性-15%，之后永久全属性+8%",
                    debuffType = "all",
                    debuffPercent = -0.15f,
                    debuffDuration = 2,
                    permanentBuffType = "all",
                    permanentBuffPercent = 0.08f
                },
                bothOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.TemporaryBuffWithWeather,
                    description = "2回合内全属性+20%，但下场战斗出现2种天气效果",
                    buffType = "all",
                    buffPercent = 0.2f,
                    buffDuration = 2,
                    extraWeatherCount = 2
                }
            };
        }

        /// <summary>
        /// 创建价格上涨事件
        /// </summary>
        private RandomEvent CreatePriceIncreaseEvent()
        {
            return new RandomEvent
            {
                eventId = "price_increase_001",
                eventName = "奸商的陷阱",
                lowRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.CatFood,
                    description = "获得300猫粮",
                    catFoodAmount = 300
                },
                highRiskOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.CatFoodWithShopDebuff,
                    description = "获得800猫粮，但下回合商店价格+20%且无法刷新",
                    catFoodAmount = 800,
                    shopPriceIncrease = 0.2f,
                    shopRefreshLocked = true
                },
                bothOption = new RandomEventOption
                {
                    optionType = RandomEventOptionType.CatFoodWithWeather,
                    description = "获得1100猫粮，但下场战斗出现2种天气效果",
                    catFoodAmount = 1100,
                    extraWeatherCount = 2
                }
            };
        }

        /// <summary>
        /// 执行随机事件选择
        /// </summary>
        public bool ExecuteRandomEvent(RandomEventOption option)
        {
            if (option == null)
            {
                Debug.LogError("[RandomEventService] 随机事件选项为空");
                return false;
            }

            var playerData = _dataManager?.PlayerData;
            if (playerData == null)
            {
                Debug.LogError("[RandomEventService] PlayerData为空");
                return false;
            }

            switch (option.optionType)
            {
                case RandomEventOptionType.CatFood:
                case RandomEventOptionType.CatFoodWithDebuff:
                case RandomEventOptionType.CatFoodWithWeather:
                case RandomEventOptionType.CatFoodWithShopDebuff:
                    playerData.catFood += option.catFoodAmount;
                    Debug.Log($"[RandomEventService] 获得{option.catFoodAmount}猫粮");
                    break;

                case RandomEventOptionType.TemporaryBuff:
                case RandomEventOptionType.TemporaryBuffWithWeather:
                    // 临时增益会在战斗中应用
                    Debug.Log($"[RandomEventService] 获得临时增益：{option.buffType}+{option.buffPercent * 100}%，持续{option.buffDuration}回合");
                    break;

                case RandomEventOptionType.PermanentBuffWithDebuff:
                    // 永久增益会在战斗后应用
                    Debug.Log($"[RandomEventService] 获得永久增益：{option.permanentBuffType}+{option.permanentBuffPercent * 100}%");
                    break;

                case RandomEventOptionType.TrainCats:
                    // 训练猫咪会在后续回合返回
                    Debug.Log($"[RandomEventService] {option.catsToTrain}只猫咪前往训练，{option.trainDuration}回合后返回{option.catsToReturn}只");
                    break;

                case RandomEventOptionType.Accessory:
                case RandomEventOptionType.RareAccessoryWithDebuff:
                case RandomEventOptionType.AccessoryWithWeather:
                    // 饰品会在后续获得
                    Debug.Log($"[RandomEventService] 获得饰品");
                    break;
            }

            _dataManager.SavePlayerData();
            return true;
        }

        /// <summary>
        /// 判断当前关卡是否需要触发抉择
        /// </summary>
        public bool ShouldTriggerRandomEvent(int round)
        {
            // 第5、10、15关出现
            return round == 5 || round == 10 || round == 15;
        }
    }

    /// <summary>
    /// 随机事件
    /// </summary>
    [System.Serializable]
    public class RandomEvent
    {
        public string eventId;
        public string eventName;
        public RandomEventOption lowRiskOption;
        public RandomEventOption highRiskOption;
        public RandomEventOption bothOption;
    }

    /// <summary>
    /// 随机事件选项
    /// </summary>
    [System.Serializable]
    public class RandomEventOption
    {
        public RandomEventOptionType optionType;
        public string description;

        // 猫粮相关
        public int catFoodAmount;

        // 增益相关
        public string buffType;
        public float buffPercent;
        public int buffDuration;

        // 永久增益相关
        public string permanentBuffType;
        public float permanentBuffPercent;

        // 减益相关
        public string debuffType;
        public float debuffPercent;
        public int debuffDuration;

        // 训练猫咪相关
        public int catsToTrain;
        public int trainDuration;
        public int catsToReturn;

        // 商店相关
        public float shopPriceIncrease;
        public bool shopRefreshLocked;

        // 天气相关
        public int extraWeatherCount;
    }

    /// <summary>
    /// 随机事件选项类型
    /// </summary>
    public enum RandomEventOptionType
    {
        CatFood,                    // 获得猫粮
        CatFoodWithDebuff,          // 获得猫粮+减益
        CatFoodWithWeather,         // 获得猫粮+额外天气
        CatFoodWithShopDebuff,      // 获得猫粮+商店减益
        TemporaryBuff,              // 临时增益
        TemporaryBuffWithWeather,   // 临时增益+额外天气
        PermanentBuffWithDebuff,    // 永久增益+临时减益
        TrainCats,                  // 训练猫咪
        Accessory,                  // 获得饰品
        RareAccessoryWithDebuff,    // 稀有饰品+减益
        AccessoryWithWeather        // 饰品+额外天气
    }
}
