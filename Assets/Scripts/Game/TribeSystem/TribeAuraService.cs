using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LitJson;

namespace TribeSystem
{
    /// <summary>
    /// 种族光环选择服务 — 管理每个种族按单位等级的光环 buff 选择
    /// </summary>
    public class TribeAuraService
    {
        private const string AURA_CONFIG_PATH = "Tables/tribe_aura_config.json";

        private AuraService _auraService;
        private DataManager _dataManager;
        private TribeAuraConfigTable _configTable;
        private bool _isLoaded;

        // 已选择的光环 ID 记录（防止重复选择）
        private HashSet<string> _chosenAuraIds = new HashSet<string>();

        public TribeAuraService(AuraService auraService)
        {
            _auraService = auraService;
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 加载光环配置
        /// </summary>
        public void LoadConfig()
        {
            if (_isLoaded) return;

            string filePath = Path.Combine(Application.streamingAssetsPath, AURA_CONFIG_PATH);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[TribeAuraService] Config not found: {filePath}");
                return;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                _configTable = JsonMapper.ToObject<TribeAuraConfigTable>(jsonText);
                _isLoaded = true;
                Debug.Log($"[TribeAuraService] Loaded aura config for {_configTable.tribeAuras.Count} tribes");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TribeAuraService] Failed to load config: {e.Message}");
            }
        }

        /// <summary>
        /// 获取指定种族和等级的可选光环列表
        /// </summary>
        public List<TribeAuraOption> GetAvailableAuras(TribeType tribeType, UnitTier tier)
        {
            if (!_isLoaded) LoadConfig();
            if (_configTable == null) return new List<TribeAuraOption>();

            var tribeAura = _configTable.GetTribeAura(tribeType);
            if (tribeAura == null) return new List<TribeAuraOption>();

            var tierAura = tribeAura.GetTierAura(tier);
            if (tierAura == null) return new List<TribeAuraOption>();

            // 返回尚未选择的光环
            var result = new List<TribeAuraOption>();
            foreach (var option in tierAura.options)
            {
                if (!_chosenAuraIds.Contains(option.auraId))
                    result.Add(option);
            }
            return result;
        }

        /// <summary>
        /// 获取指定种族的通用光环列表（过滤掉依赖兵种不存在的光环）
        /// </summary>
        public List<TribeAuraOption> GetAvailableGeneralAuras(TribeType tribeType, List<FighterData> cats)
        {
            if (!_isLoaded) LoadConfig();
            if (_configTable == null) return new List<TribeAuraOption>();

            var tribeAura = _configTable.GetTribeAura(tribeType);
            if (tribeAura == null || tribeAura.generalAuras == null)
                return new List<TribeAuraOption>();

            // 构建该族群拥有的兵种名集合
            var ownedUnitNames = new HashSet<string>();
            if (cats != null)
            {
                foreach (var cat in cats)
                {
                    var tierAura = tribeAura.GetTierAura(cat.tier);
                    if (tierAura != null && !string.IsNullOrEmpty(tierAura.unitName))
                        ownedUnitNames.Add(tierAura.unitName);
                }
            }

            var result = new List<TribeAuraOption>();
            foreach (var option in tribeAura.generalAuras.options)
            {
                if (_chosenAuraIds.Contains(option.auraId)) continue;
                // 检查依赖兵种是否存在
                if (!string.IsNullOrEmpty(option.requiredUnitName) && !ownedUnitNames.Contains(option.requiredUnitName))
                    continue;
                result.Add(option);
            }
            return result;
        }

        /// <summary>
        /// 获取指定种族和等级的光环选择（根据 selectionRule 返回 N 个可选项）
        /// Tier1: pick2of4 → 返回 4 个，玩家选 2 个
        /// Tier2: pick1of2 → 返回 2 个，玩家选 1 个（固定 buff 自动应用）
        /// </summary>
        public AuraChoiceResult GetAuraChoice(TribeType tribeType, UnitTier tier)
        {
            if (!_isLoaded) LoadConfig();
            if (_configTable == null) return null;

            var tribeAura = _configTable.GetTribeAura(tribeType);
            if (tribeAura == null) return null;

            var tierAura = tribeAura.GetTierAura(tier);
            if (tierAura == null) return null;

            var result = new AuraChoiceResult
            {
                tribeType = tribeType,
                tier = tier,
                unitName = tierAura.unitName,
                selectionRule = tierAura.selectionRule,
                fixedAura = tierAura.fixedAura,
                options = new List<TribeAuraOption>()
            };

            // 过滤已选择的
            foreach (var option in tierAura.options)
            {
                if (!_chosenAuraIds.Contains(option.auraId))
                    result.options.Add(option);
            }

            return result;
        }

        /// <summary>
        /// 应用玩家选择的光环
        /// </summary>
        public void ApplyChosenAuras(TribeType tribeType, UnitTier tier, List<string> chosenAuraIds)
        {
            if (chosenAuraIds == null || chosenAuraIds.Count == 0) return;

            var tribeAura = _configTable?.GetTribeAura(tribeType);
            if (tribeAura == null) return;

            var tierAura = tribeAura.GetTierAura(tier);
            if (tierAura == null) return;

            // 自动应用固定 buff
            if (tierAura.fixedAura != null && !_chosenAuraIds.Contains(tierAura.fixedAura.auraId))
            {
                ApplySingleAura(tierAura.fixedAura, tribeType);
                _chosenAuraIds.Add(tierAura.fixedAura.auraId);
            }

            // 应用玩家选择的 buff
            foreach (var optionId in chosenAuraIds)
            {
                if (_chosenAuraIds.Contains(optionId)) continue;

                var option = tierAura.options.Find(o => o.auraId == optionId);
                if (option != null)
                {
                    ApplySingleAura(option, tribeType);
                    _chosenAuraIds.Add(option.auraId);
                }
            }

            _dataManager?.SavePlayerData();
        }

        /// <summary>
        /// 应用通用光环
        /// </summary>
        public void ApplyGeneralAura(TribeType tribeType, string auraId)
        {
            if (_chosenAuraIds.Contains(auraId)) return;

            var tribeAura = _configTable?.GetTribeAura(tribeType);
            if (tribeAura?.generalAuras == null) return;

            var option = tribeAura.generalAuras.options.Find(o => o.auraId == auraId);
            if (option != null)
            {
                ApplySingleAura(option, tribeType);
                _chosenAuraIds.Add(auraId);
                _dataManager?.SavePlayerData();
            }
        }

        /// <summary>
        /// 重置所有已选光环（新局开始时调用）
        /// </summary>
        public void Reset()
        {
            _chosenAuraIds.Clear();
        }

        /// <summary>
        /// 检查某个光环是否已被选择
        /// </summary>
        public bool IsAuraChosen(string auraId)
        {
            return _chosenAuraIds.Contains(auraId);
        }

        private void ApplySingleAura(TribeAuraOption aura, TribeType tribeType)
        {
            if (aura.effects == null || aura.effects.Count == 0) return;

            var buffEffects = new List<BuffEffectItem>();
            foreach (var eff in aura.effects)
            {
                buffEffects.Add(new BuffEffectItem(
                    ParseStatType(eff.statType),
                    eff.isPercent,
                    eff.value,
                    eff.gameEffectType));
            }

            var scopeFilter = ParseScope(aura.scope);

            // 创建 GameChoice 并注册
            var choice = GameChoice.CreateBuff(
                aura.auraId,
                aura.auraName,
                aura.description,
                ChoiceSource.Recruitment,
                scopeFilter,
                BuffApplyType.Aura,
                buffEffects,
                tribeType);

            _auraService?.RegisterChoice(choice);
            Debug.Log($"[TribeAuraService] Applied aura: {aura.auraName} to tribe {tribeType}, scope={scopeFilter.GetDisplayString()}");
        }

        private BuffScopeFilter ParseScope(string scopeStr)
        {
            return BuffScopeFilter.Parse(scopeStr);
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
    }

    // ═══════════════════════════════════════════════════════════
    //  数据结构（JSON 反序列化用）
    // ═══════════════════════════════════════════════════════════

    [Serializable]
    public class TribeAuraConfigTable
    {
        public List<TribeAuraData> tribeAuras;

        public TribeAuraData GetTribeAura(TribeType tribeType)
        {
            if (tribeAuras == null) return null;
            int target = (int)tribeType;
            foreach (var ta in tribeAuras)
            {
                if (ta.tribeType == target) return ta;
            }
            return null;
        }
    }

    [Serializable]
    public class TribeAuraData
    {
        public int tribeType;
        public string tribeName;
        public List<TierAuraData> tierAuras;
        public GeneralAuraData generalAuras;

        public TierAuraData GetTierAura(UnitTier tier)
        {
            if (tierAuras == null) return null;
            int target = (int)tier;
            foreach (var ta in tierAuras)
            {
                if (ta.tier == target) return ta;
            }
            return null;
        }
    }

    [Serializable]
    public class TierAuraData
    {
        public int tier;
        public string unitName;
        public string selectionRule;    // "pick2of4", "pick1of2"
        public TribeAuraOption fixedAura;
        public List<TribeAuraOption> options;
    }

    [Serializable]
    public class GeneralAuraData
    {
        public string selectionRule;    // "pick1of4"
        public List<TribeAuraOption> options;
    }

    [Serializable]
    public class TribeAuraOption
    {
        public string auraId;
        public string auraName;
        public string description;
        public string scope;            // 影响范围：Tabby | T1 | Soldier 等
        public string requiredUnitName; // 依赖的兵种名（为空则始终可用），如 "矛猫"
        public List<TribeAuraEffect> effects;
    }

    [Serializable]
    public class TribeAuraEffect
    {
        public string statType;
        public bool isPercent;
        public float value;
        public int gameEffectType;
    }

    /// <summary>
    /// 光环选择结果（传给 UI 面板）
    /// </summary>
    public class AuraChoiceResult
    {
        public TribeType tribeType;
        public UnitTier tier;
        public string unitName;
        public string selectionRule;
        public TribeAuraOption fixedAura;
        public List<TribeAuraOption> options;
    }
}
