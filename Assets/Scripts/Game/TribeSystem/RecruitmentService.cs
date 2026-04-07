using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 招募&练兵服务 - 处理招募选项生成和执行
    /// </summary>
    public class RecruitmentService
    {
        private DataManager _dataManager;

        public RecruitmentService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 生成三个随机招募选项
        /// </summary>
        public List<RecruitmentOption> GenerateOptions()
        {
            var options = new List<RecruitmentOption>();
            var playerData = _dataManager?.PlayerData;

            if (playerData == null)
            {
                Debug.LogError("[RecruitmentService] PlayerData is null");
                return options;
            }

            int currentTribeCount = playerData.tribes?.Count ?? 0;

            // 选项1：新增族群（如果当前族群数<4）
            if (currentTribeCount < 4)
            {
                options.Add(CreateNewTribeOption());
            }

            // 选项2：增加小猫（如果有已有族群）
            if (currentTribeCount > 0)
            {
                options.Add(CreateAddCatsOption());
            }

            // 选项3：品质进化（如果有已有族群）
            if (currentTribeCount > 0)
            {
                options.Add(CreateQualityEvolutionOption());
            }

            // 选项4：族长强化（如果有已有族群）
            if (currentTribeCount > 0)
            {
                options.Add(CreateLeaderBoostOption());
            }

            // 从可用选项中随机选择3个
            while (options.Count > 3)
            {
                int index = Random.Range(0, options.Count);
                options.RemoveAt(index);
            }

            // 如果选项不足3个，添加族长强化选项补充
            while (options.Count < 3 && currentTribeCount > 0)
            {
                options.Add(CreateLeaderBoostOption());
            }

            return options;
        }

        /// <summary>
        /// 执行新增族群招募
        /// </summary>
        public TribeRecord ExecuteNewTribeRecruitment(TribeType tribeType, long cost)
        {
            if (!_dataManager.TrySpendCatFood(cost))
            {
                Debug.LogWarning("[RecruitmentService] Not enough cat food for new tribe");
                return null;
            }

            // 加载族群配置
            var config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
            if (config == null)
            {
                Debug.LogError($"[RecruitmentService] No config found for tribe type {tribeType}");
                return null;
            }

            // 创建新族群
            var newTribe = new TribeRecord
            {
                tribeId = _dataManager.PlayerData.tribes.Count,
                tribeType = tribeType,
                leader = CreateLeader(config),
                cats = new List<CatData>(),
                isActive = true
            };

            // 添加初始小猫（白色品质）
            for (int i = 0; i < config.initialCatCount; i++)
            {
                newTribe.cats.Add(CatData.CreateWithQuality(CatQuality.White));
            }

            _dataManager.AddTribe(newTribe);
            Debug.Log($"[RecruitmentService] Added new tribe: {tribeType}");

            return newTribe;
        }

        /// <summary>
        /// 执行增加小猫
        /// </summary>
        public int ExecuteAddCats(TribeRecord tribe, long cost)
        {
            if (!_dataManager.TrySpendCatFood(cost))
            {
                Debug.LogWarning("[RecruitmentService] Not enough cat food for adding cats");
                return 0;
            }

            var config = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
            if (config == null)
            {
                Debug.LogError($"[RecruitmentService] No config found for tribe type {tribe.tribeType}");
                return 0;
            }

            int catsToAdd = config.initialCatCount;

            // 随机品质（白40% 蓝30% 紫20% 金10%）
            for (int i = 0; i < catsToAdd; i++)
            {
                tribe.cats.Add(CatData.CreateWithRandomQuality());
            }

            _dataManager.SavePlayerData();
            Debug.Log($"[RecruitmentService] Added {catsToAdd} cats to tribe {tribe.tribeType}");

            return catsToAdd;
        }

        /// <summary>
        /// 执行品质进化
        /// </summary>
        public int ExecuteQualityEvolution(TribeRecord tribe, long cost)
        {
            if (!_dataManager.TrySpendCatFood(cost))
            {
                Debug.LogWarning("[RecruitmentService] Not enough cat food for quality evolution");
                return 0;
            }

            int evolvedCount = 0;
            foreach (var cat in tribe.cats)
            {
                if (cat.TryEvolve())
                {
                    evolvedCount++;
                }
            }

            _dataManager.SavePlayerData();
            Debug.Log($"[RecruitmentService] Evolved {evolvedCount} cats in tribe {tribe.tribeType}");

            return evolvedCount;
        }

        /// <summary>
        /// 执行族长属性提升
        /// </summary>
        public bool ExecuteLeaderBoost(TribeRecord tribe, StatType statType, long cost)
        {
            if (!_dataManager.TrySpendCatFood(cost))
            {
                Debug.LogWarning("[RecruitmentService] Not enough cat food for leader boost");
                return false;
            }

            var buffs = tribe.leader.permanentBuffs;
            float boostPercent = 0.2f; // 20%提升

            switch (statType)
            {
                case StatType.Attack:
                    buffs.attackPercent += boostPercent;
                    break;
                case StatType.Defense:
                    buffs.defensePercent += boostPercent;
                    break;
                case StatType.Hp:
                    buffs.hpPercent += boostPercent;
                    break;
                case StatType.Speed:
                    buffs.speedPercent += boostPercent;
                    break;
                case StatType.Command:
                    buffs.commandPercent += boostPercent;
                    break;
            }

            _dataManager.SavePlayerData();
            Debug.Log($"[RecruitmentService] Boosted {statType} of leader in tribe {tribe.tribeType} by 20%");

            return true;
        }

        #region Option Creation Methods

        /// <summary>
        /// 获取玩家尚未拥有的族群类型列表
        /// </summary>
        public List<TribeType> GetAvailableTribeTypes()
        {
            var playerData = _dataManager?.PlayerData;
            var available = new List<TribeType>();
            if (playerData?.tribes == null) return available;

            foreach (TribeType type in System.Enum.GetValues(typeof(TribeType)))
            {
                bool hasType = false;
                foreach (var tribe in playerData.tribes)
                {
                    if (tribe.tribeType == type)
                    {
                        hasType = true;
                        break;
                    }
                }
                if (!hasType)
                    available.Add(type);
            }
            return available;
        }

        /// <summary>
        /// 免费执行新增族群（用于新部族事件，不消耗猫粮）
        /// </summary>
        public TribeRecord ExecuteFreeNewTribeRecruitment(TribeType tribeType)
        {
            return ExecuteNewTribeRecruitment(tribeType, 0);
        }

        public RecruitmentOption CreateNewTribeOption()
        {
            var config = TribeConfigLoader.Instance.GetRecruitmentConfig();
            var playerData = _dataManager.PlayerData;

            // 找出玩家还没有的族群
            var availableTypes = new List<TribeType>();
            foreach (TribeType type in System.Enum.GetValues(typeof(TribeType)))
            {
                bool hasType = false;
                foreach (var tribe in playerData.tribes)
                {
                    if (tribe.tribeType == type)
                    {
                        hasType = true;
                        break;
                    }
                }
                if (!hasType)
                {
                    availableTypes.Add(type);
                }
            }

            // 随机选择一个可用类型
            TribeType selectedType = availableTypes[Random.Range(0, availableTypes.Count)];

            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.NewTribe,
                cost = config.newTribe.cost,
                targetTribeType = selectedType,
                targetTribeId = -1,
                description = $"新增族群：{GetTribeTypeName(selectedType)}（消耗{config.newTribe.cost}猫粮）"
            };
        }

        private RecruitmentOption CreateAddCatsOption()
        {
            var config = TribeConfigLoader.Instance.GetRecruitmentConfig();
            var playerData = _dataManager.PlayerData;

            // 随机选择一个已有族群
            if (playerData.tribes.Count == 0)
                return null;

            var randomTribe = playerData.tribes[Random.Range(0, playerData.tribes.Count)];
            var tribeConfig = TribeConfigLoader.Instance.GetTribeConfig(randomTribe.tribeType);

            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.AddCats,
                cost = config.addCats.cost,
                targetTribeType = null,
                targetTribeId = randomTribe.tribeId,
                description = $"{GetTribeTypeName(randomTribe.tribeType)}+{tribeConfig.initialCatCount}只小猫（消耗{config.addCats.cost}猫粮）"
            };
        }

        private RecruitmentOption CreateQualityEvolutionOption()
        {
            var config = TribeConfigLoader.Instance.GetRecruitmentConfig();
            var playerData = _dataManager.PlayerData;

            // 随机选择一个已有族群
            if (playerData.tribes.Count == 0)
                return null;

            var randomTribe = playerData.tribes[Random.Range(0, playerData.tribes.Count)];

            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.QualityEvolution,
                cost = config.qualityEvolution.cost,
                targetTribeType = null,
                targetTribeId = randomTribe.tribeId,
                description = $"{GetTribeTypeName(randomTribe.tribeType)}品质进化（50%概率，消耗{config.qualityEvolution.cost}猫粮）"
            };
        }

        private RecruitmentOption CreateLeaderBoostOption()
        {
            var config = TribeConfigLoader.Instance.GetRecruitmentConfig();
            var playerData = _dataManager.PlayerData;

            // 随机选择一个已有族群
            if (playerData.tribes.Count == 0)
                return null;

            var randomTribe = playerData.tribes[Random.Range(0, playerData.tribes.Count)];
            StatType randomStat = (StatType)Random.Range(0, 5);

            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.LeaderBoost,
                cost = config.leaderBoost.cost,
                targetTribeType = null,
                targetTribeId = randomTribe.tribeId,
                targetStatType = randomStat,
                description = $"{GetTribeTypeName(randomTribe.tribeType)}{GetStatTypeName(randomStat)}+20%（消耗{config.leaderBoost.cost}猫粮）"
            };
        }

        #endregion

        #region Helper Methods

        private LeaderData CreateLeader(TribeConfig config)
        {
            return new LeaderData
            {
                leaderId = Random.Range(1000, 9999),
                name = $"{config.tribeName}族长",
                baseAttack = config.leaderBaseStats.attack,
                baseDefense = config.leaderBaseStats.defense,
                baseHp = config.leaderBaseStats.hp,
                baseSpeed = config.leaderBaseStats.speed,
                command = config.leaderBaseStats.command,
                skillIds = new List<int>(),
                permanentBuffs = new PermanentBuffs(),
                temporaryBuff = null,
                restTurns = 0
            };
        }

        private string GetTribeTypeName(TribeType type)
        {
            switch (type)
            {
                case TribeType.Maine: return "缅因猫族";
                case TribeType.Tabby: return "狸花猫族";
                case TribeType.Orange: return "大橘猫族";
                case TribeType.Cow: return "奶牛猫族";
                case TribeType.Siamese: return "暹罗猫族";
                case TribeType.Ragdoll: return "布偶猫族";
                default: return type.ToString();
            }
        }

        private string GetStatTypeName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Attack: return "攻击";
                case StatType.Defense: return "防御";
                case StatType.Hp: return "血量";
                case StatType.Speed: return "速度";
                case StatType.Command: return "统帅";
                default: return stat.ToString();
            }
        }

        #endregion
    }
}
