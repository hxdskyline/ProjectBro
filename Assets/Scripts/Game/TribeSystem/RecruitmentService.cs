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
        /// 生成招募选项（1个增加小猫 + 2个族长强化）
        /// 族长强化固定为三种：+6攻击、+4攻击+50血、+80血
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

            // 1. 随机选 1 个加小猫选项
            var addCatsOptions = new List<RecruitmentOption>();
            foreach (var tribe in playerData.tribes)
            {
                addCatsOptions.Add(CreateAddCatsOption(tribe));
            }
            if (addCatsOptions.Count > 0)
            {
                int idx = Random.Range(0, addCatsOptions.Count);
                options.Add(addCatsOptions[idx]);
            }

            // 2. 随机选 2 个不同的族长强化选项（从3种固定奖励中选）
            var leaderBoostOptions = new List<RecruitmentOption>();
            foreach (var tribe in playerData.tribes)
            {
                leaderBoostOptions.Add(CreateLeaderBoostOption(tribe, 6, 0));   // +6攻击
                leaderBoostOptions.Add(CreateLeaderBoostOption(tribe, 4, 50));  // +4攻击+50血
                leaderBoostOptions.Add(CreateLeaderBoostOption(tribe, 0, 80));  // +80血
            }

            // 打乱后选2个不同类型的
            for (int i = 0; i < leaderBoostOptions.Count; i++)
            {
                int swapIdx = Random.Range(i, leaderBoostOptions.Count);
                var temp = leaderBoostOptions[i];
                leaderBoostOptions[i] = leaderBoostOptions[swapIdx];
                leaderBoostOptions[swapIdx] = temp;
            }

            var selectedBoosts = new List<RecruitmentOption>();
            var seenTypes = new HashSet<string>();
            foreach (var opt in leaderBoostOptions)
            {
                string typeKey = $"{opt.bonusAttack}_{opt.bonusHp}";
                if (seenTypes.Add(typeKey))
                {
                    selectedBoosts.Add(opt);
                    if (selectedBoosts.Count >= 2) break;
                }
            }
            options.AddRange(selectedBoosts);

            // 3. 随机打乱这 3 个选项的顺序
            for (int i = 0; i < options.Count; i++)
            {
                int swapIdx = Random.Range(i, options.Count);
                var temp = options[i];
                options[i] = options[swapIdx];
                options[swapIdx] = temp;
            }

            return options;
        }

        private string GetCoreLogicDescription(RecruitmentOption opt)
        {
            TribeType tType = TribeType.Maine;
            if (opt.targetTribeType.HasValue)
            {
                tType = opt.targetTribeType.Value;
            }
            else
            {
                var tribe = _dataManager?.PlayerData?.tribes?.Find(t => t.tribeId == opt.targetTribeId);
                if (tribe != null) tType = tribe.tribeType;
            }

            if (opt.optionType == RecruitmentOptionType.AddCats)
            {
                return $"{tType}_AddCats";
            }
            if (opt.optionType == RecruitmentOptionType.LeaderBoost)
            {
                return $"{tType}_LeaderBoost_{opt.targetStatType}";
            }
            return opt.description;
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

            int catsToAdd = 1;

            for (int i = 0; i < catsToAdd; i++)
            {
                tribe.cats.Add(CatData.CreateWithRandomQuality(tribe.tribeType));
            }

            _dataManager.SavePlayerData();
            Debug.Log($"[RecruitmentService] Added {catsToAdd} cats to tribe {tribe.tribeType} (free)");

            return catsToAdd;
        }

        /// <summary>
        /// 执行族长固定属性提升（不消耗猫粮）
        /// </summary>
        public bool ExecuteLeaderBoost(TribeRecord tribe, int attackBonus, int hpBonus)
        {
            var buffs = tribe.leader.permanentBuffs;
            buffs.attackBonus += attackBonus;
            buffs.hpBonus += hpBonus;

            _dataManager.SavePlayerData();
            Debug.Log($"[RecruitmentService] Boosted leader in tribe {tribe.tribeType}: +{attackBonus} atk, +{hpBonus} hp (free)");

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
                newTribe.cats.Add(CatData.CreateWithQuality(CatQuality.White, tribeType));
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
                temporaryBuff = null
            };
        }

        #region Option Creation Methods

        private RecruitmentOption CreateAddCatsOption(TribeRecord tribe)
        {
            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.AddCats,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                description = $"{GetTribeTypeName(tribe.tribeType)}\n+1只小猫"
            };
        }

        private RecruitmentOption CreateLeaderBoostOption(TribeRecord tribe, int attackBonus, int hpBonus)
        {
            string bonusText = "";
            if (attackBonus > 0 && hpBonus > 0)
                bonusText = $"+{attackBonus}攻击 +{hpBonus}血";
            else if (attackBonus > 0)
                bonusText = $"+{attackBonus}攻击";
            else
                bonusText = $"+{hpBonus}血";

            return new RecruitmentOption
            {
                optionType = RecruitmentOptionType.LeaderBoost,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                bonusAttack = attackBonus,
                bonusHp = hpBonus,
                description = $"{GetTribeTypeName(tribe.tribeType)}\n{bonusText}"
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
