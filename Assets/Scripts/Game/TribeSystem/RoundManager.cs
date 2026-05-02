using UnityEngine;
using System.Collections.Generic;
using BattleSystem;

namespace TribeSystem
{
    /// <summary>
    /// 回合事件类型
    /// </summary>
    public enum RoundEventType
    {
        Recruitment,    // 每回强制：招募&练兵
        Ritual,         // 每3回强制：祭祀
        Shop,           // 每5回可选：商店
        BossBattle      // 第20回：Boss战
    }

    /// <summary>
    /// 回合管理器 - 管理游戏关卡循环
    /// 各关卡的事件（招募/祭祀/商店）由 levels_config.json 配置，
    /// 通过 BattleCampaignRuntime 查询，不在代码中硬编码。
    /// </summary>
    public class RoundManager
    {
        public const int MAX_ROUNDS = 10;

        private int _currentRound = 1;

        public int CurrentRound => _currentRound;
        public bool IsFinalRound => _currentRound == MAX_ROUNDS;
        public bool IsGameOver => _currentRound > MAX_ROUNDS;

        /// <summary>
        /// 开始新回合
        /// </summary>
        public void StartRound()
        {
            Debug.Log($"[RoundManager] ===== 开始第 {_currentRound} 回合 =====");

            // 记录回合开始时间（可用于统计）
            LogRoundEvents();
        }

        /// <summary>
        /// 结束当前回合并推进到下一回合
        /// </summary>
        public void EndRound()
        {
            Debug.Log($"[RoundManager] ===== 结束第 {_currentRound} 回合 =====");

            if (!IsFinalRound)
            {
                _currentRound++;
                Debug.Log($"[RoundManager] 进入第 {_currentRound} 回合");
            }
            else
            {
                _currentRound++;
                Debug.Log($"[RoundManager] 游戏结束！共完成 {MAX_ROUNDS} 回合");
            }
        }

        private BattleCampaignRuntime Campaign => GameManager.Instance?.BattleCampaignRuntime;

        /// <summary>
        /// 获取当前回合的事件列表
        /// </summary>
        public List<RoundEventType> GetRoundEvents()
        {
            List<RoundEventType> events = new List<RoundEventType>();

            if (CanDoRecruitment()) events.Add(RoundEventType.Recruitment);
            if (CanDoRitual())      events.Add(RoundEventType.Ritual);
            if (CanOpenShop())      events.Add(RoundEventType.Shop);
            if (IsFinalRound)       events.Add(RoundEventType.BossBattle);

            return events;
        }

        public bool CanDoRecruitment()  => Campaign?.HasRecruitmentForBattle(_currentRound)   ?? false;
        public bool CanDoRitual()       => Campaign?.HasRitualForBattle(_currentRound)        ?? false;
        public bool CanOpenShop()       => Campaign?.HasShopForBattle(_currentRound)          ?? false;
        public bool CanDoNewTribeEvent()=> Campaign?.HasNewTribeEventForBattle(_currentRound) ?? false;
        public bool IsBossBattleRound() => IsFinalRound;

        /// <summary>
        /// 获取本回合所有需要弹出的事件，按配置优先级从高到低排序
        /// </summary>
        public System.Collections.Generic.List<string> GetSortedPopupEvents()
        {
            return Campaign?.GetSortedPopupEvents(_currentRound) ?? new System.Collections.Generic.List<string>();
        }

        /// <summary>
        /// 获取回合描述文本
        /// </summary>
        public string GetRoundDescription()
        {
            string desc = $"第 {_currentRound}/{MAX_ROUNDS} 关";

            if (IsFinalRound)                          desc += " [最终战]";
            else if (CanDoRitual() && CanOpenShop())   desc += " [祭祀+商店]";
            else if (CanDoRitual())                    desc += " [祭祀]";
            else if (CanOpenShop())                    desc += " [商店]";

            return desc;
        }

        /// <summary>
        /// 记录回合事件信息
        /// </summary>
        private void LogRoundEvents()
        {
            List<RoundEventType> events = GetRoundEvents();
            string eventList = string.Join(", ", events);
            Debug.Log($"[RoundManager] 本回合事件: {eventList}");
        }

        /// <summary>
        /// 重置到第1回合（新游戏）
        /// </summary>
        public void Reset()
        {
            _currentRound = 1;
            Debug.Log("[RoundManager] 回合管理器已重置到第1回合");
        }

        /// <summary>
        /// 设置当前回合（用于加载存档）
        /// </summary>
        public void SetRound(int round)
        {
            _currentRound = Mathf.Clamp(round, 1, MAX_ROUNDS + 1);
            Debug.Log($"[RoundManager] 设置当前回合为 {_currentRound}");
        }

        /// <summary>
        /// 获取下一关的预告信息
        /// </summary>
        public string GetNextRoundPreview()
        {
            if (IsGameOver) return "游戏已结束";

            int next = Mathf.Min(_currentRound + 1, MAX_ROUNDS);
            string preview = $"下一关预告: 第{next}关";

            var c = Campaign;
            if (c?.HasRitualForBattle(next) == true)  preview += " 有祭祀";
            if (c?.HasShopForBattle(next) == true)    preview += " 有商店";
            if (next == MAX_ROUNDS)                   preview += " [最终战！]";

            return preview;
        }
    }
}
