using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaSystem
{
    /// <summary>
    /// 声望等级
    /// </summary>
    public enum ReputationLevel
    {
        Novice = 0,     // 新手
        Beginner = 1,   // 初学者
        Intermediate = 2, // 中级
        Advanced = 3,    // 高级
        Expert = 4,      // 专家
        Master = 5       // 大师
    }

    /// <summary>
    /// 解锁内容类型
    /// </summary>
    public enum UnlockContentType
    {
        InitialUnit,        // 新的初始单位选择
        RecruitableSpecies, // 新的可招募物种
        PassiveSkillPool,   // 新的被动技能池
        Skin                // 外观/皮肤
    }

    /// <summary>
    /// 解锁内容
    /// </summary>
    [Serializable]
    public class UnlockContent
    {
        public string contentId;            // 内容ID
        public UnlockContentType contentType; // 内容类型
        public string contentName;          // 内容名称
        public string description;          // 描述
        public int requiredReputation;      // 所需声望值
        public bool isUnlocked;             // 是否已解锁

        public UnlockContent()
        {
            contentId = "";
            contentType = UnlockContentType.InitialUnit;
            contentName = "";
            description = "";
            requiredReputation = 0;
            isUnlocked = false;
        }
    }

    /// <summary>
    /// 局外声望成长系统 - 每局结束后根据成绩获得声望，用于解锁新内容
    /// </summary>
    public class ReputationSystem
    {
        private const int MAX_REPUTATION = 10000;
        private const float REPUTATION_MULTIPLIER = 1.0f;

        private int _currentReputation;
        private ReputationLevel _currentLevel;
        private List<UnlockContent> _unlockContents;

        // 事件
        public event Action<int> OnReputationChanged;
        public event Action<ReputationLevel> OnLevelUp;
        public event Action<UnlockContent> OnContentUnlocked;

        public int CurrentReputation => _currentReputation;
        public ReputationLevel CurrentLevel => _currentLevel;

        public ReputationSystem()
        {
            _currentReputation = 0;
            _currentLevel = ReputationLevel.Novice;
            _unlockContents = new List<UnlockContent>();
            InitializeUnlockContents();
        }

        /// <summary>
        /// 初始化解锁内容列表
        /// </summary>
        private void InitializeUnlockContents()
        {
            // 初始单位解锁
            _unlockContents.Add(new UnlockContent
            {
                contentId = "unit_tabby_warrior",
                contentType = UnlockContentType.InitialUnit,
                contentName = "橘猫战士",
                description = "解锁橘猫战士作为初始单位",
                requiredReputation = 100
            });

            _unlockContents.Add(new UnlockContent
            {
                contentId = "unit_persian_mage",
                contentType = UnlockContentType.InitialUnit,
                contentName = "波斯猫法师",
                description = "解锁波斯猫法师作为初始单位",
                requiredReputation = 300
            });

            _unlockContents.Add(new UnlockContent
            {
                contentId = "unit_siamese_archer",
                contentType = UnlockContentType.InitialUnit,
                contentName = "暹罗猫弓手",
                description = "解锁暹罗猫弓手作为初始单位",
                requiredReputation = 500
            });

            // 可招募物种解锁
            _unlockContents.Add(new UnlockContent
            {
                contentId = "species_dog",
                contentType = UnlockContentType.RecruitableSpecies,
                contentName = "犬科",
                description = "解锁犬科物种作为可招募单位",
                requiredReputation = 200
            });

            _unlockContents.Add(new UnlockContent
            {
                contentId = "species_bird",
                contentType = UnlockContentType.RecruitableSpecies,
                contentName = "鸟类",
                description = "解锁鸟类物种作为可招募单位",
                requiredReputation = 400
            });

            // 被动技能池解锁
            _unlockContents.Add(new UnlockContent
            {
                contentId = "skill_fire_breath",
                contentType = UnlockContentType.PassiveSkillPool,
                contentName = "火焰吐息",
                description = "解锁火焰吐息被动技能",
                requiredReputation = 600
            });

            _unlockContents.Add(new UnlockContent
            {
                contentId = "skill_ice_shield",
                contentType = UnlockContentType.PassiveSkillPool,
                contentName = "冰霜护盾",
                description = "解锁冰霜护盾被动技能",
                requiredReputation = 800
            });

            // 外观解锁
            _unlockContents.Add(new UnlockContent
            {
                contentId = "skin_golden_tabby",
                contentType = UnlockContentType.Skin,
                contentName = "金色橘猫",
                description = "解锁金色橘猫皮肤",
                requiredReputation = 1000
            });
        }

        /// <summary>
        /// 结算一局游戏获得的声望
        /// </summary>
        public int SettleReputation(int battleWins, int totalBattles, int bossDefeated, bool gameCompleted)
        {
            // 计算基础声望
            float baseReputation = 0;

            // 战斗胜利奖励
            baseReputation += battleWins * 10;

            // 完成率奖励
            float completionRate = (float)battleWins / totalBattles;
            baseReputation += completionRate * 50;

            // Boss击败奖励
            baseReputation += bossDefeated * 100;

            // 游戏通关奖励
            if (gameCompleted)
            {
                baseReputation += 500;
            }

            // 应用倍率
            int finalReputation = Mathf.RoundToInt(baseReputation * REPUTATION_MULTIPLIER);

            // 添加声望
            AddReputation(finalReputation);

            return finalReputation;
        }

        /// <summary>
        /// 添加声望
        /// </summary>
        public void AddReputation(int amount)
        {
            if (amount <= 0) return;

            int oldReputation = _currentReputation;
            _currentReputation = Mathf.Min(_currentReputation + amount, MAX_REPUTATION);

            OnReputationChanged?.Invoke(_currentReputation);

            // 检查等级提升
            CheckLevelUp();

            // 检查内容解锁
            CheckContentUnlock();
        }

        /// <summary>
        /// 检查等级提升
        /// </summary>
        private void CheckLevelUp()
        {
            ReputationLevel newLevel = GetLevelForReputation(_currentReputation);
            if (newLevel != _currentLevel)
            {
                _currentLevel = newLevel;
                OnLevelUp?.Invoke(_currentLevel);
            }
        }

        /// <summary>
        /// 根据声望值获取等级
        /// </summary>
        private ReputationLevel GetLevelForReputation(int reputation)
        {
            if (reputation >= 5000) return ReputationLevel.Master;
            if (reputation >= 3000) return ReputationLevel.Expert;
            if (reputation >= 1500) return ReputationLevel.Advanced;
            if (reputation >= 500) return ReputationLevel.Intermediate;
            if (reputation >= 100) return ReputationLevel.Beginner;
            return ReputationLevel.Novice;
        }

        /// <summary>
        /// 检查内容解锁
        /// </summary>
        private void CheckContentUnlock()
        {
            foreach (var content in _unlockContents)
            {
                if (!content.isUnlocked && _currentReputation >= content.requiredReputation)
                {
                    content.isUnlocked = true;
                    OnContentUnlocked?.Invoke(content);
                }
            }
        }

        /// <summary>
        /// 获取所有已解锁内容
        /// </summary>
        public List<UnlockContent> GetUnlockedContents()
        {
            var result = new List<UnlockContent>();
            foreach (var content in _unlockContents)
            {
                if (content.isUnlocked)
                    result.Add(content);
            }
            return result;
        }

        /// <summary>
        /// 获取所有可解锁内容
        /// </summary>
        public List<UnlockContent> GetAvailableContents()
        {
            var result = new List<UnlockContent>();
            foreach (var content in _unlockContents)
            {
                if (!content.isUnlocked && _currentReputation < content.requiredReputation)
                    result.Add(content);
            }
            return result;
        }

        /// <summary>
        /// 检查内容是否已解锁
        /// </summary>
        public bool IsContentUnlocked(string contentId)
        {
            foreach (var content in _unlockContents)
            {
                if (content.contentId == contentId)
                    return content.isUnlocked;
            }
            return false;
        }

        /// <summary>
        /// 获取声望等级名称
        /// </summary>
        public string GetLevelName(ReputationLevel level)
        {
            switch (level)
            {
                case ReputationLevel.Novice: return "新手";
                case ReputationLevel.Beginner: return "初学者";
                case ReputationLevel.Intermediate: return "中级";
                case ReputationLevel.Advanced: return "高级";
                case ReputationLevel.Expert: return "专家";
                case ReputationLevel.Master: return "大师";
                default: return "未知";
            }
        }

        /// <summary>
        /// 保存数据
        /// </summary>
        public ReputationSaveData ToSaveData()
        {
            return new ReputationSaveData
            {
                reputation = _currentReputation,
                level = (int)_currentLevel,
                unlockedContentIds = GetUnlockedContentIds()
            };
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        public void LoadFromSaveData(ReputationSaveData data)
        {
            if (data == null) return;

            _currentReputation = data.reputation;
            _currentLevel = (ReputationLevel)data.level;

            // 标记已解锁内容
            foreach (var content in _unlockContents)
            {
                if (data.unlockedContentIds.Contains(content.contentId))
                {
                    content.isUnlocked = true;
                }
            }
        }

        private List<string> GetUnlockedContentIds()
        {
            var ids = new List<string>();
            foreach (var content in _unlockContents)
            {
                if (content.isUnlocked)
                    ids.Add(content.contentId);
            }
            return ids;
        }
    }

    /// <summary>
    /// 声望存档数据
    /// </summary>
    [Serializable]
    public class ReputationSaveData
    {
        public int reputation;
        public int level;
        public List<string> unlockedContentIds;

        public ReputationSaveData()
        {
            reputation = 0;
            level = 0;
            unlockedContentIds = new List<string>();
        }
    }
}
