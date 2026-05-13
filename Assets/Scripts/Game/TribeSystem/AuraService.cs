using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 光环服务 — 管理 choice/equipment 的注册与光环 buff 的自动补发
    /// </summary>
    public class AuraService
    {
        private DataManager _dataManager;

        public AuraService()
        {
            _dataManager = GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 注册一条 choice（存入 runChoices + 立即应用到现有单位）
        /// </summary>
        public void RegisterChoice(GameChoice choice)
        {
            if (choice == null) { Debug.LogWarning("[AuraService] RegisterChoice: choice is null"); return; }
            Debug.Log($"[AuraService] RegisterChoice: id={choice.choiceId}, name={choice.displayName}, effects={choice.buffEffects?.Count ?? 0}, scope={choice.GetScopeDisplayString()}");
            _dataManager.PlayerData.runChoices.Add(choice);
            Debug.Log($"[AuraService] runChoices count now = {_dataManager.PlayerData.runChoices.Count}");
            var scopeFilter = choice.GetScopeFilter();
            ApplyToExistingUnits(scopeFilter, choice.buffEffects, choice.displayName, choice.choiceId, choice.description, BuffSource.Equipment);
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 直接给所有匹配单位添加回合制临时 buff（不存入 runChoices）
        /// </summary>
        public void ApplyRoundBasedBuffToAll(BuffScopeFilter scopeFilter, List<BuffEffectItem> effects,
            int rounds, string displayName, string buffIdPrefix, string description = null)
        {
            if (effects == null || effects.Count == 0) return;
            var playerData = _dataManager.PlayerData;
            if (playerData.tribes == null) return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe == null || !tribe.isActive) continue;

                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                    {
                        if (scopeFilter.Matches(false, tribe.tribeType, unit.tier))
                        {
                            ApplyRoundBasedEffects(unit, effects, rounds, displayName, buffIdPrefix, description);
                        }
                    }
                }
            }
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 注册一件装备（存入 runEquipments + 立即应用）
        /// </summary>
        public void RegisterEquipment(EquipmentRecord equip)
        {
            if (equip == null) return;
            _dataManager.PlayerData.runEquipments.Add(equip);
            var scopeFilter = equip.GetScopeFilter();
            ApplyToExistingUnits(scopeFilter, equip.effects, equip.displayName, equip.equipmentId, equip.description, BuffSource.Equipment);
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 注销一条 choice（从 runChoices 移除 + 回退所有已应用的 buff）
        /// </summary>
        public bool UnregisterChoice(string choiceId)
        {
            if (string.IsNullOrEmpty(choiceId)) return false;
            var playerData = _dataManager.PlayerData;

            // 从 runChoices 列表中移除
            bool removed = playerData.runChoices.RemoveAll(c => c.choiceId == choiceId) > 0;

            // 回退已应用的 buff
            BuffService.RemoveChoiceBuffs(choiceId);

            if (removed)
            {
                _dataManager.SavePlayerData();
                Debug.Log($"[AuraService] 注销 choice: {choiceId}");
            }
            return removed;
        }

        /// <summary>
        /// 注销一件装备（从 runEquipments 移除 + 回退所有已应用的 buff）
        /// </summary>
        public bool UnregisterEquipment(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId)) return false;
            var playerData = _dataManager.PlayerData;

            bool removed = playerData.runEquipments.RemoveAll(e => e.equipmentId == equipmentId) > 0;

            BuffService.RemoveEquipmentBuffs(equipmentId);

            if (removed)
            {
                _dataManager.SavePlayerData();
                Debug.Log($"[AuraService] 注销 equipment: {equipmentId}");
            }
            return removed;
        }

        /// <summary>
        /// 当新单位创建后调用，遍历所有 Aura 类型的 choice/equipment 补发 buff
        /// </summary>
        public void ApplyAurasToNewUnit(FighterData unit, TribeType tribeType)
        {
            if (unit == null) return;

            var playerData = _dataManager.PlayerData;

            foreach (var choice in playerData.runChoices)
            {
                if (choice.category != ChoiceCategory.Buff) continue;
                if (choice.buffApplyType != BuffApplyType.Aura) continue;
                var filter = choice.GetScopeFilter();
                if (!filter.Matches(false, tribeType, unit.tier)) continue;

                ApplyEffects(unit, choice.buffEffects, choice.displayName, choice.choiceId, choice.description, BuffSource.Equipment);
            }

            foreach (var equip in playerData.runEquipments)
            {
                if (equip.buffApplyType != BuffApplyType.Aura) continue;
                var filter = equip.GetScopeFilter();
                if (!filter.Matches(false, tribeType, unit.tier)) continue;

                ApplyEffects(unit, equip.effects, equip.displayName, equip.equipmentId, equip.description, BuffSource.Equipment);
            }
        }

        // ─── 私有方法 ──────────────────────────────────────────

        /// <summary>
        /// 按 scopeFilter 分发 buff 到当前已有的单位
        /// </summary>
        private void ApplyToExistingUnits(BuffScopeFilter scopeFilter,
            List<BuffEffectItem> effects, string displayName, string uniqueId, string description, BuffSource source)
        {
            if (effects == null || effects.Count == 0) return;

            var playerData = _dataManager.PlayerData;
            if (playerData.tribes == null) return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe == null || !tribe.isActive) continue;

                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                    {
                        if (scopeFilter.Matches(false, tribe.tribeType, unit.tier))
                        {
                            ApplyEffects(unit, effects, displayName, uniqueId, description, source);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 给单个单位应用永久属性 buff
        /// </summary>
        private static void ApplyEffects(IHasBuffs unit, List<BuffEffectItem> effects,
            string displayName, string uniqueId, string description, BuffSource source)
        {
            foreach (var eff in effects)
            {
                var buff = UnifiedBuff.CreateStatBuff(
                    $"aura_{uniqueId}_{eff.statType}", displayName,
                    source, uniqueId,
                    eff.statType, eff.isPercent, eff.value,
                    gameEffectType: eff.gameEffectType,
                    description: description);
                unit.AddUnifiedBuff(buff);
            }
        }

        /// <summary>
        /// 给单个单位应用回合制 buff
        /// </summary>
        private static void ApplyRoundBasedEffects(IHasBuffs unit, List<BuffEffectItem> effects,
            int rounds, string displayName, string buffIdPrefix, string description)
        {
            foreach (var eff in effects)
            {
                var buff = UnifiedBuff.CreateRoundBasedBuff(
                    $"{buffIdPrefix}_{eff.statType}", displayName,
                    BuffSource.Ritual, buffIdPrefix,
                    eff.statType, eff.isPercent, eff.value,
                    rounds, description);
                unit.AddUnifiedBuff(buff);
            }
        }
    }
}
