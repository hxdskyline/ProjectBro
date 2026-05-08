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

                // 移除族长的 buff
                if (tribe.leader != null)
                    totalRemoved += tribe.leader.RemoveBuffBySource(choiceId);

                // 移除小猫的 buff
                if (tribe.cats != null)
                {
                    foreach (var cat in tribe.cats)
                    {
                        if (cat != null)
                            totalRemoved += cat.RemoveBuffBySource(choiceId);
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
        /// 替换族长的指定 buff（先移除旧的，再添加新的）
        /// </summary>
        public static bool ReplaceBuff(LeaderData leader, string oldBuffId, UnifiedBuff newBuff)
        {
            if (leader == null || newBuff == null) return false;
            leader.RemoveBuff(oldBuffId);
            return leader.AddUnifiedBuff(newBuff);
        }

        /// <summary>
        /// 替换小猫的指定 buff
        /// </summary>
        public static bool ReplaceBuff(CatData cat, string oldBuffId, UnifiedBuff newBuff)
        {
            if (cat == null || newBuff == null) return false;
            cat.RemoveBuff(oldBuffId);
            return cat.AddUnifiedBuff(newBuff);
        }

        /// <summary>
        /// 清除族长的所有战斗内 buff（战斗结束时调用）
        /// </summary>
        public static int ClearBattleBuffs(LeaderData leader)
        {
            if (leader == null) return 0;
            return leader.ClearBattleBuffs();
        }

        /// <summary>
        /// 清除小猫的所有战斗内 buff
        /// </summary>
        public static int ClearBattleBuffs(CatData cat)
        {
            if (cat == null) return 0;
            return cat.ClearBattleBuffs();
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
                if (tribe.leader != null)
                    total += ClearBattleBuffs(tribe.leader);
                if (tribe.cats != null)
                {
                    foreach (var cat in tribe.cats)
                        total += ClearBattleBuffs(cat);
                }
            }

            if (total > 0)
                Debug.Log($"[BuffService] 清除战斗内 buff: {total} 条");
            return total;
        }
    }
}
