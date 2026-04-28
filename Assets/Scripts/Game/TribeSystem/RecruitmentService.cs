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
        private AuraService _auraService;

        public RecruitmentService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        public void SetAuraService(AuraService auraService)
        {
            _auraService = auraService;
        }

        /// <summary>
        /// 生成招募选项（1个增加小猫 + 2个buff选项，从choice_config.json读取）
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

            // 2. 从 choice_config.json 读取 buff 原型，按权重随机选 2 个
            var buffArchetypes = TribeConfigLoader.Instance.GetArchetypesBySource("recruitment");
            if (buffArchetypes != null && buffArchetypes.Count > 0)
            {
                // 为每个族群生成对应的 buff 选项
                var allBuffOptions = new List<RecruitmentOption>();
                foreach (var tribe in playerData.tribes)
                {
                    foreach (var archetype in buffArchetypes)
                    {
                        if (archetype.category != "buff") continue;
                        allBuffOptions.Add(CreateBuffOptionFromArchetype(tribe, archetype));
                    }
                }

                // 按权重随机抽取（去重：同一原型只选一次）
                var selectedBuffOptions = WeightedRandomSelect(allBuffOptions, 2);
                options.AddRange(selectedBuffOptions);
            }

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

        /// <summary>
        /// 从 archetype 创建招募 buff 选项
        /// </summary>
        private RecruitmentOption CreateBuffOptionFromArchetype(TribeRecord tribe, ChoiceArchetype archetype)
        {
            // 解析 scope
            BuffApplyScope scope = ParseBuffScope(archetype.buffScope);
            BuffApplyType applyType = archetype.buffApplyType == "Aura" ? BuffApplyType.Aura : BuffApplyType.CurrentUnit;

            // 构造 GameChoice 用于后续执行
            var buffEffects = new List<BuffEffectItem>();
            if (archetype.buffEffects != null)
            {
                foreach (var eff in archetype.buffEffects)
                {
                    StatType stat = ParseStatType(eff.statType);
                    buffEffects.Add(new BuffEffectItem(stat, eff.isPercent, eff.value, eff.gameEffectType));
                }
            }

            var choice = GameChoice.CreateBuff(
                archetype.id,
                archetype.displayName,
                FormatDescription(archetype.descriptionTemplate, tribe.tribeType, buffEffects),
                ChoiceSource.Recruitment,
                scope,
                applyType,
                buffEffects,
                tribe.tribeType);

            return new RecruitmentOption
            {
                optionType = ChoiceCategory.Buff,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                bonusAttack = GetEffectValue(buffEffects, StatType.Attack),
                bonusHp = GetEffectValue(buffEffects, StatType.Hp),
                description = $"{GetTribeTypeName(tribe.tribeType)}\n{archetype.displayName}",
                // 附加 GameChoice 供执行时使用
                gameChoice = choice
            };
        }

        /// <summary>
        /// 按权重随机选取 N 个不重复选项
        /// </summary>
        private List<RecruitmentOption> WeightedRandomSelect(List<RecruitmentOption> options, int count)
        {
            var result = new List<RecruitmentOption>();
            var remaining = new List<RecruitmentOption>(options);

            // 打乱
            for (int i = 0; i < remaining.Count; i++)
            {
                int swapIdx = Random.Range(i, remaining.Count);
                var temp = remaining[i];
                remaining[i] = remaining[swapIdx];
                remaining[swapIdx] = temp;
            }

            // 按 archetype id 去重，选最多 count 个
            var seenArchetypes = new HashSet<string>();
            foreach (var opt in remaining)
            {
                if (opt.gameChoice == null) continue;
                string archetypeId = opt.gameChoice.choiceId;
                if (seenArchetypes.Add(archetypeId))
                {
                    result.Add(opt);
                    if (result.Count >= count) break;
                }
            }

            return result;
        }

        private string FormatDescription(string template, TribeType tribeType, List<BuffEffectItem> effects)
        {
            if (string.IsNullOrEmpty(template)) return template;
            string result = template.Replace("{tribe_name}", GetTribeTypeName(tribeType));
            foreach (var eff in effects)
            {
                result = result.Replace("{value}", Mathf.RoundToInt(eff.value).ToString());
            }
            return result;
        }

        private int GetEffectValue(List<BuffEffectItem> effects, StatType stat)
        {
            foreach (var eff in effects)
            {
                if (eff.statType == stat) return Mathf.RoundToInt(eff.value);
            }
            return 0;
        }

        private BuffApplyScope ParseBuffScope(string scope)
        {
            switch (scope)
            {
                case "All": return BuffApplyScope.All;
                case "AllLeaders": return BuffApplyScope.AllLeaders;
                case "AllCats": return BuffApplyScope.AllCats;
                case "SingleTribeLeader": return BuffApplyScope.SingleTribeLeader;
                case "SingleTribeCat": return BuffApplyScope.SingleTribeCat;
                default: return BuffApplyScope.All;
            }
        }

        private StatType ParseStatType(string stat)
        {
            switch (stat)
            {
                case "Attack": return StatType.Attack;
                case "Defense": return StatType.Defense;
                case "Hp": return StatType.Hp;
                case "MoveSpeed": return StatType.MoveSpeed;
                case "AttackSpeed": return StatType.AttackSpeed;
                default: return StatType.Attack;
            }
        }

        private string GetCoreLogicDescription(RecruitmentOption opt)
        {
            TribeType tType = TribeType.Tabby;
            if (opt.targetTribeType.HasValue)
            {
                tType = opt.targetTribeType.Value;
            }
            else
            {
                var tribe = _dataManager?.PlayerData?.tribes?.Find(t => t.tribeId == opt.targetTribeId);
                if (tribe != null) tType = tribe.tribeType;
            }

            if (opt.optionType == ChoiceCategory.AddCats)
            {
                return $"{tType}_AddCats";
            }
            if (opt.optionType == ChoiceCategory.Buff)
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

            int catsToAdd = config.initialCatCount;

            for (int i = 0; i < catsToAdd; i++)
            {
                var cat = CatData.CreateWithRandomQuality(tribe.tribeType);
                cat.ApplyGlobalArtifactBonus();
                _auraService?.ApplyAurasToNewCat(cat, tribe.tribeType);
                tribe.cats.Add(cat);
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
            var effects = new List<BuffEffectItem>();
            if (attackBonus != 0)
                effects.Add(new BuffEffectItem(StatType.Attack, false, attackBonus));
            if (hpBonus != 0)
                effects.Add(new BuffEffectItem(StatType.Hp, false, hpBonus));

            var choice = GameChoice.CreateBuff(
                $"Recruit_LeaderBoost_{tribe.tribeType}_{attackBonus}_{hpBonus}",
                "招募强化",
                $"族长属性提升",
                ChoiceSource.Recruitment,
                BuffApplyScope.SingleTribeLeader,
                BuffApplyType.CurrentUnit,
                effects,
                tribe.tribeType);

            _auraService?.RegisterChoice(choice);

            Debug.Log($"[RecruitmentService] Boosted leader in tribe {tribe.tribeType}: +{attackBonus} atk, +{hpBonus} hp");

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

            _dataManager.AddTribe(newTribe);

            // 为新族长补发 aura buff
            _auraService?.ApplyAurasToNewLeader(newTribe.leader, tribeType);

            for (int i = 0; i < config.initialCatCount; i++)
            {
                var cat = CatData.CreateWithQuality(CatQuality.White, tribeType);
                cat.ApplyGlobalArtifactBonus();
                _auraService?.ApplyAurasToNewCat(cat, tribeType);
                newTribe.cats.Add(cat);
            }

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
                if (type == TribeType.None) continue;
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
            var buffs = new PermanentBuffs();
            // 添加天生特殊 buff
            var innateBuff = PermanentBuffs.CreateInnateBuff(config.tribeType);
            if (innateBuff != null)
                buffs.specialBuffs.Add(innateBuff);

            return new LeaderData
            {
                leaderId = Random.Range(1000, 9999),
                name = $"{config.tribeName}族长",
                baseAttack = config.leaderBaseStats.attack,
                baseDefense = config.leaderBaseStats.defense,
                baseHp = config.leaderBaseStats.hp,
                baseMoveSpeed = config.leaderBaseStats.moveSpeed,
                command = config.leaderBaseStats.command,
                skillIds = new List<int>(),
                permanentBuffs = buffs,
                temporaryBuff = null
            };
        }

        #region Option Creation Methods

        private RecruitmentOption CreateAddCatsOption(TribeRecord tribe)
        {
            var config = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
            int catCount = config != null ? config.initialCatCount : 1;
            return new RecruitmentOption
            {
                optionType = ChoiceCategory.AddCats,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                description = $"{GetTribeTypeName(tribe.tribeType)}\n+{catCount}只小猫"
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
                optionType = ChoiceCategory.Buff,
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
                case TribeType.Tabby: return "狸花猫族";
                case TribeType.Orange: return "大橘猫族";
                case TribeType.Cow: return "奶牛猫族";
                case TribeType.Siamese: return "暹罗猫族";
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
                case StatType.MoveSpeed: return "移速";
                default: return stat.ToString();
            }
        }

        #endregion
    }
}
