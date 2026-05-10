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
        private TribeAuraService _tribeAuraService;

        public RecruitmentService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        public void SetAuraService(AuraService auraService)
        {
            _auraService = auraService;
        }

        public void SetTribeAuraService(TribeAuraService tribeAuraService)
        {
            _tribeAuraService = tribeAuraService;
        }

        /// <summary>
        /// 生成撸铁选项（3个词缀选项，3选1）
        /// </summary>
        public List<RecruitmentOption> GenerateOptions()
        {
            var options = new List<RecruitmentOption>();
            var playerData = _dataManager?.PlayerData;

            if (playerData == null || playerData.tribes == null || playerData.tribes.Count == 0)
            {
                Debug.LogError("[RecruitmentService] PlayerData is null or no tribes");
                return options;
            }

            // 收集所有拥有的猫的 fighterId
            var allFighterIds = new List<int>();
            foreach (var tribe in playerData.tribes)
            {
                // 族长的 fighterId 直接从 TribeRecord 获取
                if (tribe.fighterId > 0)
                    allFighterIds.Add(tribe.fighterId);

                // 小猫的 fighterId 从 unitType 配置获取
                if (tribe.cats != null)
                {
                    foreach (var cat in tribe.cats)
                    {
                        var tribeConfig = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
                        if (tribeConfig != null)
                        {
                            var unitType = tribeConfig.GetUnitType(cat.tier);
                            if (unitType != null && unitType.fighterId > 0)
                                allFighterIds.Add(unitType.fighterId);
                        }
                    }
                }
            }

            if (allFighterIds.Count == 0)
            {
                Debug.LogWarning("[RecruitmentService] 没有可用的兵种");
                return options;
            }

            // 随机选择一个兵种来生成词缀选项
            int fighterId = allFighterIds[Random.Range(0, allFighterIds.Count)];

            // 获取当前关卡
            int currentRound = GameManager.Instance?.BattleCampaignRuntime?.CurrentBattleNumber ?? 1;

            // 生成3个词缀选项
            options = GenerateAffixOptions(currentRound, fighterId);

            // 从 choice_config 补充 buff 选项（招募来源）
            var buffArchetypes = TribeConfigLoader.Instance?.GetArchetypesBySource("recruitment");
            if (buffArchetypes != null && buffArchetypes.Count > 0)
            {
                // 随机选1个buff选项混入
                var arch = buffArchetypes[Random.Range(0, buffArchetypes.Count)];
                var randomTribe = playerData.tribes[Random.Range(0, playerData.tribes.Count)];
                var buffOption = CreateBuffOptionFromArchetype(randomTribe, arch);
                if (buffOption != null)
                {
                    options.Add(buffOption);
                    Debug.Log($"[RecruitmentService] 补充 buff 选项: {arch.displayName} (scope={arch.buffScope})");
                }
            }

            return options;
        }

        /// <summary>
        /// 从 archetype 创建招募 buff 选项
        /// </summary>
        private RecruitmentOption CreateBuffOptionFromArchetype(TribeRecord tribe, ChoiceArchetype archetype)
        {
            // 解析 scope
            BuffScopeFilter scopeFilter = ParseBuffScope(archetype.buffScope);
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
                FormatDescription(archetype.descriptionTemplate, tribe.tribeType, tribe.fighterId, buffEffects),
                ChoiceSource.Recruitment,
                scopeFilter,
                applyType,
                buffEffects,
                tribe.tribeType);

            // 使用 fighter 表中的名称
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
            string fighterName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";

            return new RecruitmentOption
            {
                optionType = ChoiceCategory.Buff,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                bonusAttack = GetEffectValue(buffEffects, StatType.Attack),
                bonusHp = GetEffectValue(buffEffects, StatType.Hp),
                description = $"{fighterName}\n{archetype.displayName}",
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
                // 有 gameChoice 用 choiceId 去重，否则用 optionType+tribe+tier 作为 key
                string archetypeId = opt.gameChoice != null
                    ? opt.gameChoice.choiceId
                    : $"{opt.optionType}_{opt.targetTribeType}_{opt.targetTier}";
                if (seenArchetypes.Add(archetypeId))
                {
                    result.Add(opt);
                    if (result.Count >= count) break;
                }
            }

            return result;
        }

        private string FormatDescription(string template, TribeType tribeType, int fighterId, List<BuffEffectItem> effects)
        {
            if (string.IsNullOrEmpty(template)) return template;

            // 使用 fighter 表中的名称
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            string fighterName = fighterConfig?.fighterName ?? $"兵种{fighterId}";

            string result = template.Replace("{tribe_name}", fighterName);
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

        private BuffScopeFilter ParseBuffScope(string scope)
        {
            return BuffScopeFilter.Parse(scope);
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
        /// 执行增加兵种（不消耗猫粮）
        /// </summary>
        public int ExecuteAddCats(TribeRecord tribe, long cost, UnitTier? tier = null)
        {
            // 不消耗猫粮
            var config = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
            if (config == null)
            {
                Debug.LogError($"[RecruitmentService] No config found for tribe type {tribe.tribeType}");
                return 0;
            }

            // 每次只增加一只兵种
            int catsToAdd = 1;

            var cat = CatData.CreateWithRandomQuality(tribe.tribeType);
            if (tier.HasValue)
                cat.tier = tier.Value;
            _auraService?.ApplyAurasToNewCat(cat, tribe.tribeType);
            tribe.cats.Add(cat);

            _dataManager.SavePlayerData();
            string tierLog = tier.HasValue ? $" (tier={tier.Value})" : "";
            Debug.Log($"[RecruitmentService] Added {catsToAdd} cat to tribe {tribe.tribeType} (free){tierLog}");

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
                new BuffScopeFilter { role = ScopeRoleFilter.Leader, tribe = tribe.tribeType },
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
                fighterId = config.leaderFighterId,
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
            // 从 fighter_config.json 读取族长属性
            var leaderConfig = TribeConfigLoader.Instance?.GetFighterConfig(config.leaderFighterId);

            return new LeaderData
            {
                leaderId = Random.Range(1000, 9999),
                name = leaderConfig?.fighterName ?? $"{config.tribeName}族长",
                baseAttack = leaderConfig?.attack ?? 0,
                baseDefense = leaderConfig?.defense ?? 0,
                baseHp = leaderConfig?.hp ?? 0,
                baseMoveSpeed = leaderConfig?.moveSpeed ?? 1.0f,
                skillIds = new List<int>(),
                permanentBuffs = buffs
            };
        }

        #region Option Creation Methods

        private RecruitmentOption CreateAddCatsOption(TribeRecord tribe)
        {
            // 使用 fighter 表中的名称
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
            string fighterName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";

            return new RecruitmentOption
            {
                optionType = ChoiceCategory.AddCats,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                description = $"招募 {fighterName}\n+1只"
            };
        }

        private RecruitmentOption CreateAddCatsOptionWithTier(TribeRecord tribe, UnitTier tier)
        {
            string tierName = GetUnitTierName(tier);
            // 从 fighter_config.json 读取单位名
            string unitName = "";
            var config = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
            if (config != null)
            {
                var unitType = config.GetUnitType(tier);
                if (unitType != null && unitType.fighterId > 0)
                {
                    var fighterConfig = TribeConfigLoader.Instance.GetFighterConfig(unitType.fighterId);
                    if (fighterConfig != null)
                        unitName = fighterConfig.fighterName;
                }
            }

            // 使用 fighterName 而不是 tribeName
            string fighterDisplayName = !string.IsNullOrEmpty(unitName) ? unitName : $"兵种{tribe.fighterId}";
            string display = $"招募 {fighterDisplayName}\n+1只";

            return new RecruitmentOption
            {
                optionType = ChoiceCategory.AddCats,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                targetTier = tier,
                description = display
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

            // 使用 fighter 表中的名称
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
            string fighterName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";

            return new RecruitmentOption
            {
                optionType = ChoiceCategory.Buff,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                bonusAttack = attackBonus,
                bonusHp = hpBonus,
                description = $"{fighterName}\n{bonusText}"
            };
        }

        private RecruitmentOption CreateAuraBuffOption(TribeRecord tribe, TribeAuraOption aura)
        {
            // 将 TribeAuraOption.effects 转换为 BuffEffectItem 列表
            var buffEffects = new List<BuffEffectItem>();
            if (aura.effects != null)
            {
                foreach (var eff in aura.effects)
                {
                    buffEffects.Add(new BuffEffectItem(
                        ParseStatType(eff.statType),
                        eff.isPercent,
                        eff.value,
                        eff.gameEffectType));
                }
            }

            // 创建 GameChoice — scope 从 JSON 配置读取
            var choice = GameChoice.CreateBuff(
                aura.auraId,
                aura.auraName,
                aura.description,
                ChoiceSource.Recruitment,
                BuffScopeFilter.Parse(aura.scope),
                BuffApplyType.Aura,
                buffEffects,
                tribe.tribeType);

            // 使用 fighter 表中的名称
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
            string fighterName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";

            return new RecruitmentOption
            {
                optionType = ChoiceCategory.Buff,
                cost = 0,
                targetTribeType = null,
                targetTribeId = tribe.tribeId,
                description = $"{fighterName}\n{aura.auraName}\n{aura.description}",
                gameChoice = choice
            };
        }

        #endregion

        #region Helper Methods

        private string GetUnitTierName(UnitTier tier)
        {
            switch (tier)
            {
                case UnitTier.Tier1: return "一级兵";
                case UnitTier.Tier2: return "二级兵";
                case UnitTier.Tier3: return "三级兵";
                default: return "兵种";
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

        #region 摇人系统方法

        /// <summary>
        /// 生成摇人选项（3选1）
        /// </summary>
        /// <param name="excludeTribeTypes">需要排除的族群类型</param>
        /// <returns>3个摇人选项</returns>
        public List<NewTribeEventOption> GenerateRecruitOptions(List<TribeType> excludeTribeTypes = null)
        {
            var options = new List<NewTribeEventOption>();
            var availableTypes = GetAvailableTribeTypes();

            // 排除指定的族群类型
            if (excludeTribeTypes != null)
            {
                availableTypes.RemoveAll(t => excludeTribeTypes.Contains(t));
            }

            // 如果可用族群不足3个，则全部返回
            int count = Mathf.Min(3, availableTypes.Count);

            // 随机打乱
            for (int i = 0; i < availableTypes.Count; i++)
            {
                int swapIdx = UnityEngine.Random.Range(i, availableTypes.Count);
                var temp = availableTypes[i];
                availableTypes[i] = availableTypes[swapIdx];
                availableTypes[swapIdx] = temp;
            }

            // 取前count个
            for (int i = 0; i < count; i++)
            {
                var tribeType = availableTypes[i];
                var config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
                if (config != null)
                {
                    options.Add(new NewTribeEventOption
                    {
                        optionType = NewTribeEventOptionType.NewTribe,
                        tribeType = tribeType,
                        description = $"{config.tribeName}\n{config.description}",
                        catCount = config.recruitCountA
                    });
                }
            }

            return options;
        }

        /// <summary>
        /// 生成增加已有族群猫咪数量的选项（3选1）
        /// </summary>
        /// <returns>3个增加猫咪数量的选项</returns>
        public List<NewTribeEventOption> GenerateAddCatOptions()
        {
            var options = new List<NewTribeEventOption>();
            var playerData = _dataManager?.PlayerData;

            if (playerData?.tribes == null || playerData.tribes.Count == 0)
            {
                return options;
            }

            // 为每个已有的族群生成选项
            foreach (var tribe in playerData.tribes)
            {
                // 使用 fighter 表中的名称
                var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
                string fighterName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";

                options.Add(new NewTribeEventOption
                {
                    optionType = NewTribeEventOptionType.AddCats,
                    tribeType = tribe.tribeType,
                    tribeId = tribe.tribeId,
                    fighterId = tribe.fighterId,
                    description = $"{fighterName}\n+1只",
                    catCount = 1
                });
            }

            // 随机打乱
            for (int i = 0; i < options.Count; i++)
            {
                int swapIdx = UnityEngine.Random.Range(i, options.Count);
                var temp = options[i];
                options[i] = options[swapIdx];
                options[swapIdx] = temp;
            }

            // 取前3个
            int count = Mathf.Min(3, options.Count);
            return options.GetRange(0, count);
        }

        /// <summary>
        /// 执行摇人选择（获得新族群）
        /// </summary>
        /// <param name="option">选择的选项</param>
        /// <returns>是否成功</returns>
        public bool ExecuteRecruitSelection(NewTribeEventOption option)
        {
            if (option == null)
            {
                Debug.LogError("[RecruitmentService] 摇人选项为空");
                return false;
            }

            if (option.optionType == NewTribeEventOptionType.NewTribe)
            {
                // 获得新族群（使用 recruitCountA）
                var config = TribeConfigLoader.Instance.GetTribeConfig(option.tribeType);
                int catCount = config?.recruitCountA ?? 1;

                var newTribe = ExecuteFreeNewTribeRecruitment(option.tribeType);
                if (newTribe != null)
                {
                    Debug.Log($"[RecruitmentService] 摇人成功：获得{option.tribeType}，数量{catCount}");
                    return true;
                }
            }
            else if (option.optionType == NewTribeEventOptionType.AddCats)
            {
                // 增加已有族群猫咪数量（使用 option.catCount）
                var playerData = _dataManager?.PlayerData;
                var tribe = playerData?.tribes?.Find(t => t.tribeId == option.tribeId);
                if (tribe != null)
                {
                    int catCount = option.catCount;

                    for (int i = 0; i < catCount; i++)
                    {
                        var cat = CatData.CreateWithRandomQuality(tribe.tribeType);
                        _auraService?.ApplyAurasToNewCat(cat, tribe.tribeType);
                        tribe.cats.Add(cat);
                    }

                    _dataManager.SavePlayerData();
                    Debug.Log($"[RecruitmentService] 摇人成功：{option.tribeType}增加{catCount}只猫");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断当前关卡是否需要触发摇人
        /// </summary>
        /// <param name="round">当前关卡</param>
        /// <param name="isNewGame">是否是新游戏</param>
        /// <returns>是否需要触发摇人</returns>
        public bool ShouldTriggerRecruit(int round, bool isNewGame)
        {
            // 时机1：新游戏开局
            if (isNewGame && round == 1)
            {
                return true;
            }

            // 时机2：第10关
            if (round == 10)
            {
                var playerData = _dataManager?.PlayerData;
                if (playerData?.tribes != null && playerData.tribes.Count < 3)
                {
                    return true;
                }
            }

            // 时机3：第3、5、7、9、11、13、15、17、19关
            if (round >= 3 && round <= 19 && round % 2 == 1)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取摇人类型（时机1/2/3）
        /// </summary>
        /// <param name="round">当前关卡</param>
        /// <param name="isNewGame">是否是新游戏</param>
        /// <returns>摇人类型</returns>
        public RecruitType GetRecruitType(int round, bool isNewGame)
        {
            // 时机1：新游戏开局
            if (isNewGame && round == 1)
            {
                return RecruitType.NewTribe;
            }

            // 时机2：第10关
            if (round == 10)
            {
                var playerData = _dataManager?.PlayerData;
                if (playerData?.tribes != null && playerData.tribes.Count < 3)
                {
                    return RecruitType.NewTribe;
                }
            }

            // 时机3：第3、5、7、9、11、13、15、17、19关
            if (round >= 3 && round <= 19 && round % 2 == 1)
            {
                return RecruitType.AddCats;
            }

            return RecruitType.None;
        }

        #endregion

        #region 撸铁系统方法

        /// <summary>
        /// 生成词缀选项（3选1）
        /// </summary>
        /// <param name="level">当前关卡</param>
        /// <param name="fighterId">目标兵种ID</param>
        /// <returns>3个词缀选项</returns>
        public List<RecruitmentOption> GenerateAffixOptions(int level, int fighterId)
        {
            var options = new List<RecruitmentOption>();

            // 获取已拥有的词缀
            var playerData = _dataManager?.PlayerData;
            var ownedAffixes = playerData?.ownedAffixes ?? new List<string>();

            // 使用上一关的难度（如果没有则使用普通难度）
            DifficultyLevel difficulty = DifficultyLevel.Normal;
            if (playerData != null && playerData.lastBattleDifficulty > 0)
            {
                difficulty = (DifficultyLevel)playerData.lastBattleDifficulty;
            }

            // 使用 AffixDrawService 抽取词缀
            var affixDrawService = new AffixDrawService();
            var affixes = affixDrawService.DrawAffixes(level, difficulty, fighterId, ownedAffixes, 3);

            foreach (var affix in affixes)
            {
                options.Add(new RecruitmentOption
                {
                    optionType = ChoiceCategory.Affix,
                    cost = 0,
                    description = affix.description,
                    affixData = affix
                });
            }

            return options;
        }

        /// <summary>
        /// 执行词缀选择
        /// </summary>
        /// <param name="option">选择的词缀选项</param>
        /// <returns>是否成功</returns>
        public bool ExecuteAffixSelection(RecruitmentOption option)
        {
            if (option == null || option.affixData == null)
            {
                Debug.LogError("[RecruitmentService] 词缀选项为空");
                return false;
            }

            // 添加词缀到玩家已拥有的词缀列表
            var playerData = _dataManager?.PlayerData;
            if (playerData != null)
            {
                if (playerData.ownedAffixes == null)
                    playerData.ownedAffixes = new List<string>();

                playerData.ownedAffixes.Add(option.affixData.affixId);
                _dataManager.SavePlayerData();

                Debug.Log($"[RecruitmentService] 获得词缀：{option.affixData.displayName}（兵种{option.affixData.fighterId}）");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断当前关卡是否需要触发撸铁
        /// </summary>
        /// <param name="round">当前关卡</param>
        /// <returns>是否需要触发撸铁</returns>
        public bool ShouldTriggerAffix(int round)
        {
            // 第2~19关出现
            return round >= 2 && round <= 19;
        }

        /// <summary>
        /// 判断是否需要触发双倍撸铁（极难难度通关后）
        /// </summary>
        /// <param name="previousRoundDifficulty">上一关难度</param>
        /// <returns>是否需要双倍撸铁</returns>
        public bool ShouldTriggerDoubleAffix(DifficultyLevel previousRoundDifficulty)
        {
            // 极难难度通关后，下一关出现2次撸铁选择
            return previousRoundDifficulty == DifficultyLevel.Bloodbath;
        }

        #endregion
    }

    /// <summary>
    /// 摇人类型
    /// </summary>
    public enum RecruitType
    {
        None,       // 无摇人
        NewTribe,   // 获得新族群
        AddCats     // 增加已有族群猫咪数量
    }
}
