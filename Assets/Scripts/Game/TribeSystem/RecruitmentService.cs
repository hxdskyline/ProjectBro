using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 招募&练兵服务 - 按需求优化：只有增加小猫和族长强化，无猫粮消耗
    /// </summary>
    public class RecruitmentService
    {
        private DataManager _dataManager;

        public RecruitmentService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 生成招募选项（增加小猫 + 族长强化，最多3个）
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
            if (currentTribeCount == 0)
                return options;

            // 每个已有族群生成增加小猫选项
            foreach (var tribe in playerData.tribes)
            {
                options.Add(CreateAddCatsOption(tribe));
            }

            // 每个已有族群生成族长强化选项（随机属性）
            foreach (var tribe in playerData.tribes)
            {
                options.Add(CreateLeaderBoostOption(tribe));
            }

            // 随机裁剪到最多3个
            while (options.Count > 3)
            {
                int index = Random.Range(0, options.Count);
                options.RemoveAt(index);
            }

            return options;
        }

        /// <summary>
        /// 执行增加小猫（不消耗猫粮）
        /// </summary>
        public int ExecuteAddCats(TribeRecord tribe, long cost)
        {
            // 不消耗猫粮
            var config = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
            if (config == null)
            {
                Debug.LogError($"[RecruitmentService] No config found for tribe type {tribe.tribeType}");
                return 0;
            }

            int catsToAdd = config.initialCatCount;

            for (int i = 0; i < catsToAdd; i++)
            {
                tribe.cats.Add(CatData.CreateWithRandomQuality());
            }

            _dataManager.SavePlayerData();
            Debug.Log($"[RecruitmentService] Added {catsToAdd} cats to tribe {tribe.tribeType} (free)");

            return catsToAdd;
        }

        /// <summary>
        /// 执行族长属性提升（不消耗猫粮）
        /// </summary>
        public bool ExecuteLeaderBoost(TribeRecord tribe, StatType statType, long cost)
        {
            // 不消耗猫粮
            var buffs = tribe.leader.permanentBuffs;
            float boostPercent = 0.2f;

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
            Debug.Log($"[RecruitmentService] Boosted {statType} of leader in tribe {tribe.tribeType} by 20% (free)");

            return true;
        }

        /// <summary>
        /// 执行新增族群招募（保留供新部族事件使用）
        /// </summary>
        public TribeRecord ExecuteNewTribeRecruitment(TribeType tribeType, long cost)
        {
            // 不消耗猫粮
            var config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
            if (config == null)
            {
                Debug.LogError($"[RecruitmentService] No config found for tribe type {tribeType}");
                return null;
            }

            var newTribe = new TribeRecord
            {
                tribeId = _dataManager.PlayerData.tribes.Count,
                tribeType = tribeType,
                leader = CreateLeader(config),
                cats = new List<CatData>(),
                isActive = true
            };

            for (int i = 0; i < config.initialCatCount; i++)
            {
                newTribe.cats.Add(CatData.CreateWithQuality(CatQuality.White));
            }

            _dataManager.AddTribe(newTribe);
            Debug.Log($"[RecruitmentService] Added new tribe: {tribeType}");

            return newTribe;
        }

        /// <summary>
        /// 执行品质进化（保留兼容旧UI调用）
        /// </summary>
        public int ExecuteQualityEvolution(TribeRecord tribe, long cost)
        {
            // 不消耗猫粮
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
        /// 获取玩家尚未拥有的族群类型列表（供新部族事件使用）
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
        /// 免费执行新增族群（供新部族事件使用）
        /// </summary>
        public TribeRecord ExecuteFreeNewTribeRecruitment(TribeType tribeType)
        {
            return ExecuteNewTribeRecruitment(tribeType, 0);
        }

        /// <summary>
        /// 创建族长数据
        /// </summary>
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

        #region Option Creation Methods

        private RecruitmentOption CreateAddCatsOption(TribeRecord tribe)
        {
            var tribeConfig = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);

            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.AddCats,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                description = $"{GetTribeTypeName(tribe.tribeType)}+{tribeConfig?.initialCatCount ?? 0}只小猫（免费）"
            };
        }

        private RecruitmentOption CreateLeaderBoostOption(TribeRecord tribe)
        {
            StatType randomStat = (StatType)Random.Range(0, 5);

            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.LeaderBoost,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                targetStatType = randomStat,
                description = $"{GetTribeTypeName(tribe.tribeType)}{GetStatTypeName(randomStat)}+20%（免费）"
            };
        }

        #endregion

        #region Helper Methods

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
