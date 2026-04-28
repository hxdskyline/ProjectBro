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
            if (choice == null) return;
            _dataManager.PlayerData.runChoices.Add(choice);
            ApplyToExistingUnits(choice.buffScope, choice.buffApplyType, choice.buffEffects, choice.displayName, choice.targetTribeType);
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 注册一件装备（存入 runEquipments + 立即应用）
        /// </summary>
        public void RegisterEquipment(EquipmentRecord equip)
        {
            if (equip == null) return;
            _dataManager.PlayerData.runEquipments.Add(equip);
            ApplyToExistingUnits(equip.buffScope, equip.buffApplyType, equip.effects, equip.displayName, null);
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 当新 leader 创建后调用，遍历所有 Aura 类型的 choice/equipment 补发 buff
        /// </summary>
        public void ApplyAurasToNewLeader(LeaderData leader, TribeType tribeType)
        {
            if (leader == null) return;

            var playerData = _dataManager.PlayerData;

            // 遍历 runChoices 中的 Aura buff
            foreach (var choice in playerData.runChoices)
            {
                if (choice.category != ChoiceCategory.Buff) continue;
                if (choice.buffApplyType != BuffApplyType.Aura) continue;
                if (!MatchesLeader(choice.buffScope, tribeType)) continue;

                ApplyEffectsToLeader(leader, choice.buffEffects, choice.displayName);
            }

            // 遍历 runEquipments 中的 Aura buff
            foreach (var equip in playerData.runEquipments)
            {
                if (equip.buffApplyType != BuffApplyType.Aura) continue;
                if (!MatchesLeader(equip.buffScope, tribeType)) continue;

                ApplyEffectsToLeader(leader, equip.effects, equip.displayName);
            }
        }

        /// <summary>
        /// 当新 cat 创建后调用，遍历所有 Aura 类型的 choice/equipment 补发 buff
        /// </summary>
        public void ApplyAurasToNewCat(CatData cat, TribeType tribeType)
        {
            if (cat == null) return;

            var playerData = _dataManager.PlayerData;

            // 遍历 runChoices 中的 Aura buff
            foreach (var choice in playerData.runChoices)
            {
                if (choice.category != ChoiceCategory.Buff) continue;
                if (choice.buffApplyType != BuffApplyType.Aura) continue;
                if (!MatchesCat(choice.buffScope, tribeType)) continue;

                ApplyEffectsToCat(cat, choice.buffEffects, choice.displayName);
            }

            // 遍历 runEquipments 中的 Aura buff
            foreach (var equip in playerData.runEquipments)
            {
                if (equip.buffApplyType != BuffApplyType.Aura) continue;
                if (!MatchesCat(equip.buffScope, tribeType)) continue;

                ApplyEffectsToCat(cat, equip.effects, equip.displayName);
            }
        }

        // ─── 私有方法 ──────────────────────────────────────────

        /// <summary>
        /// 按 scope 分发 buff 到当前已有的 leader/cat
        /// </summary>
        private void ApplyToExistingUnits(BuffApplyScope scope, BuffApplyType applyType,
            List<BuffEffectItem> effects, string displayName, TribeType? targetTribeType)
        {
            if (effects == null || effects.Count == 0) return;
            // Aura 和 CurrentUnit 类型都需要应用到已有的单位
            // Aura 类型用于新单位创建时的补发，CurrentUnit 用于立即应用

            var playerData = _dataManager.PlayerData;
            if (playerData.tribes == null) return;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe == null || !tribe.isActive) continue;

                switch (scope)
                {
                    case BuffApplyScope.All:
                        ApplyToLeader(tribe, effects, displayName);
                        ApplyToCats(tribe, effects, displayName);
                        break;
                    case BuffApplyScope.AllLeaders:
                        ApplyToLeader(tribe, effects, displayName);
                        break;
                    case BuffApplyScope.AllCats:
                        ApplyToCats(tribe, effects, displayName);
                        break;
                    case BuffApplyScope.SingleTribeLeader:
                        if (targetTribeType.HasValue && tribe.tribeType == targetTribeType.Value)
                            ApplyToLeader(tribe, effects, displayName);
                        break;
                    case BuffApplyScope.SingleTribeCat:
                        if (targetTribeType.HasValue && tribe.tribeType == targetTribeType.Value)
                            ApplyToCats(tribe, effects, displayName);
                        break;
                }
            }
        }

        private void ApplyToLeader(TribeRecord tribe, List<BuffEffectItem> effects, string displayName)
        {
            if (tribe.leader == null) return;
            ApplyEffectsToLeader(tribe.leader, effects, displayName);
        }

        private void ApplyEffectsToLeader(LeaderData leader, List<BuffEffectItem> effects, string displayName)
        {
            if (leader.permanentBuffs == null)
                leader.permanentBuffs = new PermanentBuffs();

            foreach (var eff in effects)
            {
                leader.permanentBuffs.AddBuffEntry(
                    BuffSource.Equipment, displayName, eff.statType, eff.isPercent, eff.value,
                    displayName);
            }
        }

        private void ApplyToCats(TribeRecord tribe, List<BuffEffectItem> effects, string displayName)
        {
            if (tribe.cats == null) return;
            foreach (var cat in tribe.cats)
            {
                ApplyEffectsToCat(cat, effects, displayName);
            }
        }

        private void ApplyEffectsToCat(CatData cat, List<BuffEffectItem> effects, string displayName)
        {
            foreach (var eff in effects)
            {
                cat.AddBuffEntry(BuffSource.Equipment, displayName, eff.statType, eff.isPercent, eff.value, displayName);
            }
        }

        /// <summary>
        /// scope 是否匹配族长
        /// </summary>
        private bool MatchesLeader(BuffApplyScope scope, TribeType tribeType)
        {
            switch (scope)
            {
                case BuffApplyScope.All:
                case BuffApplyScope.AllLeaders:
                    return true;
                case BuffApplyScope.SingleTribeLeader:
                    return true; // 由调用方已确定 tribeType 匹配
                default:
                    return false;
            }
        }

        /// <summary>
        /// scope 是否匹配小猫
        /// </summary>
        private bool MatchesCat(BuffApplyScope scope, TribeType tribeType)
        {
            switch (scope)
            {
                case BuffApplyScope.All:
                case BuffApplyScope.AllCats:
                    return true;
                case BuffApplyScope.SingleTribeCat:
                    return true; // 由调用方已确定 tribeType 匹配
                default:
                    return false;
            }
        }
    }
}
