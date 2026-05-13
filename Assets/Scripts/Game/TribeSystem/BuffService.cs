using System.Collections.Generic;
using UnityEngine;
using BattleSystem.Fighter;

namespace TribeSystem
{
    /// <summary>
    /// Buff 管理服务 — 静态工具类，统一管理 buff 的添加、移除、替换、清理
    /// </summary>
    public static class BuffService
    {
        private static DataManager GetDataManager()
        {
            return GameManager.Instance?.DataManager;
        }

        /// <summary>
        /// 移除指定 choiceId 对应的所有 buff（从所有单位的 activeBuffs 中移除）
        /// </summary>
        public static int RemoveChoiceBuffs(string choiceId)
        {
            if (string.IsNullOrEmpty(choiceId)) return 0;
            int totalRemoved = 0;
            var dataManager = GetDataManager();
            var playerData = dataManager?.PlayerData;
            if (playerData?.tribes == null) return 0;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe == null || !tribe.isActive) continue;

                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                    {
                        if (unit != null)
                            totalRemoved += unit.RemoveBuffBySource(choiceId);
                    }
                }
            }

            if (totalRemoved > 0)
                Debug.Log($"[BuffService] 移除 choice '{choiceId}' 的 buff: {totalRemoved} 条");
            return totalRemoved;
        }

        /// <summary>
        /// 移除指定 equipmentId 对应的所有 buff
        /// </summary>
        public static int RemoveEquipmentBuffs(string equipmentId)
        {
            return RemoveChoiceBuffs(equipmentId); // 统一用 sourceId 匹配
        }

        /// <summary>
        /// 替换指定单位的指定 buff（先移除旧的，再添加新的）
        /// </summary>
        public static bool ReplaceBuff(IHasBuffs unit, string oldBuffId, UnifiedBuff newBuff)
        {
            if (unit == null || newBuff == null) return false;
            if (unit is FighterData fighter)
            {
                fighter.RemoveBuff(oldBuffId);
                return fighter.AddUnifiedBuff(newBuff);
            }
            return false;
        }

        /// <summary>
        /// 清除指定单位的所有战斗内 buff
        /// </summary>
        public static int ClearBattleBuffs(IHasBuffs unit)
        {
            if (unit is FighterData fighter)
                return fighter.ClearBattleBuffs();
            return 0;
        }

        /// <summary>
        /// 清除战斗单位运行时属性的所有战斗内 buff
        /// </summary>
        public static int ClearBattleBuffs(UnitRuntimeAttributes runtime)
        {
            if (runtime == null) return 0;
            return runtime.ClearBattleBuffs();
        }

        /// <summary>
        /// 清除所有族群的所有战斗内 buff（战斗结束时的批量调用）
        /// </summary>
        public static int ClearAllBattleBuffs()
        {
            int total = 0;
            var dataManager = GetDataManager();
            var playerData = dataManager?.PlayerData;
            if (playerData?.tribes == null) return 0;

            foreach (var tribe in playerData.tribes)
            {
                if (tribe == null || !tribe.isActive) continue;
                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                        total += ClearBattleBuffs(unit);
                }
            }

            if (total > 0)
                Debug.Log($"[BuffService] 清除战斗内 buff: {total} 条");
            return total;
        }
    }
}
