using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 掷骰子结果
    /// </summary>
    public enum DiceResult
    {
        Success,    // 成功（彩色）
        Failure     // 失败（黑白）
    }

    /// <summary>
    /// 招募卡片数据
    /// </summary>
    [Serializable]
    public class RecruitmentCard
    {
        public int fighterId;              // 兵种ID
        public string fighterName;         // 兵种名称
        public int populationCost;         // 人口占用
        public int recruitmentCost;        // 金币花费
        public float successRate;          // 成功率
        public DiceResult diceResult;      // 掷骰子结果
        public bool isRecruited;           // 是否已招募

        public RecruitmentCard()
        {
            fighterId = 0;
            fighterName = "";
            populationCost = 1;
            recruitmentCost = 100;
            successRate = 0.5f;
            diceResult = DiceResult.Failure;
            isRecruited = false;
        }
    }

    /// <summary>
    /// 招募系统 - 掷骰子招募
    /// 战斗胜利后触发，玩家对敌方兵种进行掷骰子招募
    /// </summary>
    public class RecruitmentDiceSystem
    {
        private DataManager _dataManager;
        private AuraService _auraService;

        // 招募历史
        private List<RecruitmentHistory> _history;

        // 事件
        public event Action<RecruitmentCard, DiceResult> OnDiceRolled;
        public event Action<RecruitmentCard> OnRecruitmentSuccess;
        public event Action OnRecruitmentComplete;

        public RecruitmentDiceSystem()
        {
            _dataManager = GameManager.Instance?.DataManager;
            _history = new List<RecruitmentHistory>();
        }

        public void SetAuraService(AuraService auraService)
        {
            _auraService = auraService;
        }

        /// <summary>
        /// 生成招募卡片列表
        /// </summary>
        public List<RecruitmentCard> GenerateRecruitmentCards(List<int> enemyFighterIds)
        {
            var cards = new List<RecruitmentCard>();

            if (enemyFighterIds == null || enemyFighterIds.Count == 0)
                return cards;

            foreach (int fighterId in enemyFighterIds)
            {
                var card = CreateRecruitmentCard(fighterId);
                if (card != null)
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        /// <summary>
        /// 创建招募卡片
        /// </summary>
        private RecruitmentCard CreateRecruitmentCard(int fighterId)
        {
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            if (fighterConfig == null)
                return null;

            var card = new RecruitmentCard
            {
                fighterId = fighterId,
                fighterName = fighterConfig.fighterName,
                populationCost = fighterConfig.populationCost,
                recruitmentCost = CalculateRecruitmentCost(fighterConfig),
                successRate = CalculateSuccessRate(fighterConfig)
            };

            return card;
        }

        /// <summary>
        /// 计算招募费用
        /// </summary>
        private int CalculateRecruitmentCost(FighterConfig config)
        {
            // 基础费用根据兵种等级和品质调整
            int baseCost = 100;

            // TODO: 根据config中的字段计算具体费用
            return baseCost;
        }

        /// <summary>
        /// 计算成功率
        /// </summary>
        private float CalculateSuccessRate(FighterConfig config)
        {
            // 基础成功率
            float baseRate = 0.5f;

            // TODO: 根据主角"咪格魅力"属性调整成功率
            // 目前返回基础值
            return baseRate;
        }

        /// <summary>
        /// 执行掷骰子
        /// </summary>
        public DiceResult RollDice(RecruitmentCard card)
        {
            if (card == null) return DiceResult.Failure;

            float roll = UnityEngine.Random.value;
            DiceResult result = roll < card.successRate ? DiceResult.Success : DiceResult.Failure;

            card.diceResult = result;

            OnDiceRolled?.Invoke(card, result);

            return result;
        }

        /// <summary>
        /// 招募单位
        /// </summary>
        public bool RecruitUnit(RecruitmentCard card)
        {
            if (card == null || card.diceResult != DiceResult.Success)
                return false;

            // 检查木天蓼叶是否足够
            int currentCatFood = (int)(_dataManager?.PlayerData?.catFood ?? 0);
            if (currentCatFood < card.recruitmentCost)
            {
                Debug.LogWarning("[RecruitmentDiceSystem] 木天蓼叶不足");
                return false;
            }

            // 消耗木天蓼叶
            _dataManager?.AddCatFood(-card.recruitmentCost);

            // 创建新单位
            var newUnit = CreateRecruitedUnit(card.fighterId);
            if (newUnit == null)
                return false;

            // 将单位添加到待上阵区
            AddUnitToPendingZone(newUnit);

            card.isRecruited = true;

            OnRecruitmentSuccess?.Invoke(card);

            return true;
        }

        /// <summary>
        /// 创建招募的单位
        /// </summary>
        private FighterData CreateRecruitedUnit(int fighterId)
        {
            // 随机品质
            CatQuality quality = GetRandomQuality();

            // 创建单位
            var unit = FighterData.CreateWithFighterId(fighterId, quality);

            // 设置区域为待上阵区
            unit.zone = UnitZone.Pending;

            return unit;
        }

        /// <summary>
        /// 获取随机品质
        /// </summary>
        private CatQuality GetRandomQuality()
        {
            float roll = UnityEngine.Random.value;

            if (roll < 0.4f)
                return CatQuality.White;
            else if (roll < 0.7f)
                return CatQuality.Blue;
            else if (roll < 0.9f)
                return CatQuality.Purple;
            else
                return CatQuality.Gold;
        }

        /// <summary>
        /// 将单位添加到待上阵区
        /// </summary>
        private void AddUnitToPendingZone(FighterData unit)
        {
            var playerData = _dataManager?.PlayerData;
            if (playerData == null || playerData.tribes == null)
                return;

            // 找到第一个族群，添加单位
            if (playerData.tribes.Count > 0)
            {
                var firstTribe = playerData.tribes[0];
                if (firstTribe.units == null)
                    firstTribe.units = new List<FighterData>();

                firstTribe.units.Add(unit);
            }
            else
            {
                // 如果没有族群，创建一个新的
                var newTribe = new TribeRecord
                {
                    tribeId = 0,
                    fighterId = unit.fighterId,
                    tribeType = TribeType.Tabby,
                    units = new List<FighterData> { unit }
                };
                playerData.tribes.Add(newTribe);
            }
        }

        /// <summary>
        /// 完成招募
        /// </summary>
        public void CompleteRecruitment()
        {
            OnRecruitmentComplete?.Invoke();
        }

        /// <summary>
        /// 记录招募历史
        /// </summary>
        public void RecordHistory(int round, List<int> enemyFighterIds, List<int> recruitedFighterIds)
        {
            var history = new RecruitmentHistory
            {
                round = round,
                enemyFighterIds = enemyFighterIds,
                recruitedFighterIds = recruitedFighterIds,
                timestamp = DateTime.Now
            };

            _history.Add(history);
        }

        /// <summary>
        /// 检查是否曾招募过某兵种
        /// </summary>
        public bool HasRecruitedFighter(int fighterId)
        {
            foreach (var h in _history)
            {
                if (h.recruitedFighterIds.Contains(fighterId))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 招募历史记录
    /// </summary>
    [Serializable]
    public class RecruitmentHistory
    {
        public int round;                          // 发生回合
        public List<int> enemyFighterIds;          // 敌方兵种ID列表
        public List<int> recruitedFighterIds;      // 招募的兵种ID列表
        public DateTime timestamp;                 // 时间戳

        public RecruitmentHistory()
        {
            enemyFighterIds = new List<int>();
            recruitedFighterIds = new List<int>();
        }
    }
}
