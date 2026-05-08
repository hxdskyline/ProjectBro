using System.Collections.Generic;
using UnityEngine;
using BattleSystem.Fighter;

namespace TribeSystem
{
    /// <summary>
    /// 战斗 Buff 生命周期服务 — 管理 LeaderData ↔ RuntimeAttributes 之间的 buff 同步
    /// </summary>
    public static class BattleBuffService
    {
        /// <summary>
        /// 战斗开始时，将 LeaderData 中的 Persistent / TemporaryRoundBased buff 恢复到 RuntimeAttributes，
        /// 确保跨战斗的层数不丢失。
        /// </summary>
        public static void RestorePersistentBuffsToRuntime(BattleFighter[] playerFighters)
        {
            if (playerFighters == null) return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            var tribes = dataManager?.PlayerData?.tribes;
            if (tribes == null) return;

            foreach (BattleFighter fighter in playerFighters)
            {
                if (fighter == null || !fighter.IsLeader || fighter.RuntimeAttributes == null) continue;

                LeaderData leader = FindLeaderData(tribes, fighter.TribeType);
                if (leader == null || leader.ActiveBuffs == null) continue;

                foreach (var buff in leader.ActiveBuffs)
                {
                    if (buff.persistence != BuffPersistence.Persistent
                        && buff.persistence != BuffPersistence.TemporaryRoundBased) continue;
                    if (buff.currentStacks <= 0) continue;

                    var clone = buff.Clone();
                    fighter.RuntimeAttributes.ApplyBuff(clone);
                }

                fighter.RuntimeAttributes.Recalculate();
            }
        }

        /// <summary>
        /// 战斗结束时，将战斗内 Persistent buff（如饱食层）从 RuntimeAttributes 同步回 LeaderData.ActiveBuffs，
        /// 以便跨战斗保留。
        /// </summary>
        public static void SyncPersistentBuffsToLeaderData(BattleFighter[] playerFighters)
        {
            if (playerFighters == null) return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            var tribes = dataManager?.PlayerData?.tribes;
            if (tribes == null) return;

            foreach (BattleFighter fighter in playerFighters)
            {
                if (fighter == null || !fighter.IsLeader || fighter.RuntimeAttributes == null) continue;

                LeaderData leader = FindLeaderData(tribes, fighter.TribeType);
                if (leader == null) continue;

                var runtimeBuffs = fighter.RuntimeAttributes.ActiveBuffs;
                var leaderBuffs = leader.ActiveBuffs;

                for (int i = runtimeBuffs.Count - 1; i >= 0; i--)
                {
                    var runtimeBuff = runtimeBuffs[i];
                    if (runtimeBuff.persistence != BuffPersistence.Persistent
                        && runtimeBuff.persistence != BuffPersistence.TemporaryRoundBased) continue;

                    bool found = false;
                    for (int j = 0; j < leaderBuffs.Count; j++)
                    {
                        if (leaderBuffs[j].buffId == runtimeBuff.buffId)
                        {
                            leaderBuffs[j].currentStacks = runtimeBuff.currentStacks;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        leader.AddUnifiedBuff(runtimeBuff.Clone());
                    }
                }
            }
        }

        private static LeaderData FindLeaderData(List<TribeRecord> tribes, TribeType tribeType)
        {
            for (int t = 0; t < tribes.Count; t++)
            {
                if (tribes[t] != null && tribes[t].tribeType == tribeType && tribes[t].leader != null)
                    return tribes[t].leader;
            }
            return null;
        }
    }
}
