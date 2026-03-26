using UnityEngine;
using System.Collections.Generic;

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
    /// 回合管理器 - 管理20回合游戏循环
    /// </summary>
    public class RoundManager
    {
        public const int MAX_ROUNDS = 20;
        public const int RITUAL_INTERVAL = 3;  // 每3回合一次祭祀
        public const int SHOP_INTERVAL = 5;    // 每5回合开放商店

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

        /// <summary>
        /// 获取当前回合的事件列表
        /// </summary>
        public List<RoundEventType> GetRoundEvents()
        {
            List<RoundEventType> events = new List<RoundEventType>();

            // 每回合都有招募
            events.Add(RoundEventType.Recruitment);

            // 每3回合有祭祀
            if (CanDoRitual())
            {
                events.Add(RoundEventType.Ritual);
            }

            // 每5回合开放商店
            if (CanOpenShop())
            {
                events.Add(RoundEventType.Shop);
            }

            // 第20回合是Boss战
            if (IsFinalRound)
            {
                events.Add(RoundEventType.BossBattle);
            }

            return events;
        }

        /// <summary>
        /// 检查是否可以祭祀（每3回合）
        /// </summary>
        public bool CanDoRitual()
        {
            return _currentRound % RITUAL_INTERVAL == 0;
        }

        /// <summary>
        /// 检查是否可以开放商店（每5回合）
        /// </summary>
        public bool CanOpenShop()
        {
            return _currentRound % SHOP_INTERVAL == 0;
        }

        /// <summary>
        /// 检查是否是Boss战回合
        /// </summary>
        public bool IsBossBattleRound()
        {
            return _currentRound == MAX_ROUNDS;
        }

        /// <summary>
        /// 获取回合描述文本
        /// </summary>
        public string GetRoundDescription()
        {
            string desc = $"第 {_currentRound}/{MAX_ROUNDS} 回合";

            if (IsFinalRound)
            {
                desc += " [最终战]";
            }
            else if (CanDoRitual() && CanOpenShop())
            {
                desc += " [祭祀+商店]";
            }
            else if (CanDoRitual())
            {
                desc += " [祭祀]";
            }
            else if (CanOpenShop())
            {
                desc += " [商店]";
            }

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
        /// 获取下一回合的预告信息
        /// </summary>
        public string GetNextRoundPreview()
        {
            if (IsGameOver)
            {
                return "游戏已结束";
            }

            int nextRound = Mathf.Min(_currentRound + 1, MAX_ROUNDS);
            string preview = $"下回合预告: 第{nextRound}回";

            if (nextRound % RITUAL_INTERVAL == 0)
            {
                preview += " 有祭祀";
            }
            if (nextRound % SHOP_INTERVAL == 0)
            {
                preview += " 有商店";
            }
            if (nextRound == MAX_ROUNDS)
            {
                preview += " [最终Boss战！]";
            }

            return preview;
        }
    }
}
