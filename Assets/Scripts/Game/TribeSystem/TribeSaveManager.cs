using UnityEngine;

namespace TribeSystem
{
    /// <summary>
    /// 存档点类型
    /// </summary>
    public enum SavePointType
    {
        RoundStart,         // 回合开始前
        AfterRecruitment,   // 招募结束后
        AfterRitual,        // 祭祀结束后
        AfterShop,          // 商店购买后
        AfterBattle,        // 战斗结束后
        BeforeBossBattle    // Boss战前
    }

    /// <summary>
    /// 族群存档管理器 - 管理关键存档点
    /// </summary>
    public class TribeSaveManager
    {
        private DataManager _dataManager;
        private const string SAVE_KEY_PREFIX = "TribeSave_";

        public TribeSaveManager(DataManager dataManager)
        {
            _dataManager = dataManager;
        }

        /// <summary>
        /// 在回合开始前存档
        /// </summary>
        public void SaveBeforeRound(int round)
        {
            Debug.Log($"[TribeSaveManager] 存档: 回合{round}开始前");
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 在招募结束后存档
        /// </summary>
        public void SaveAfterRecruitment(int round)
        {
            Debug.Log($"[TribeSaveManager] 存档: 回合{round}招募后");
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 在祭祀结束后存档
        /// </summary>
        public void SaveAfterRitual(int round)
        {
            Debug.Log($"[TribeSaveManager] 存档: 回合{round}祭祀后");
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 在商店购买后存档
        /// </summary>
        public void SaveAfterShopPurchase(int round)
        {
            Debug.Log($"[TribeSaveManager] 存档: 回合{round}商店购买后");
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 在战斗结束后存档
        /// </summary>
        public void SaveAfterBattle(int round, bool victory)
        {
            Debug.Log($"[TribeSaveManager] 存档: 回合{round}战斗后 {(victory ? "胜利" : "失败")}");
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 在Boss战前存档（自动存档点）
        /// </summary>
        public void SaveBeforeBossBattle(int round)
        {
            Debug.Log($"[TribeSaveManager] ===== 存档: Boss战前（回合{round}）=====");
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 创建快速存档（手动存档）
        /// </summary>
        public void CreateQuickSave()
        {
            int currentRound = _dataManager.GetCurrentRound();
            Debug.Log($"[TribeSaveManager] 快速存档: 回合{currentRound}");
            _dataManager.SavePlayerData();
        }

        /// <summary>
        /// 检查存档是否有效
        /// </summary>
        public bool ValidateSaveData()
        {
            var playerData = _dataManager.PlayerData;
            if (playerData == null)
            {
                Debug.LogError("[TribeSaveManager] 存档数据无效: PlayerData为空");
                return false;
            }

            // 检查回合数（从配置中获取最大关卡数）
            var roundManager = new RoundManager();
            int maxRounds = roundManager.MaxRounds;
            if (playerData.currentRound < 1 || playerData.currentRound > maxRounds + 1)
            {
                Debug.LogError($"[TribeSaveManager] 存档数据无效: 回合数{playerData.currentRound}超出范围");
                return false;
            }

            // 检查族群数据
            if (playerData.tribes == null)
            {
                Debug.LogError("[TribeSaveManager] 存档数据无效: tribes为空");
                return false;
            }

            // 检查每个族群的数据完整性
            foreach (var tribe in playerData.tribes)
            {
                if (tribe.leader == null)
                {
                    Debug.LogError($"[TribeSaveManager] 存档数据无效: 族群{tribe.tribeId}的leader为空");
                    return false;
                }

                if (tribe.cats == null)
                {
                    Debug.LogError($"[TribeSaveManager] 存档数据无效: 族群{tribe.tribeId}的cats为空");
                    return false;
                }
            }

            Debug.Log("[TribeSaveManager] 存档数据验证通过");
            return true;
        }

        /// <summary>
        /// 获取存档摘要信息
        /// </summary>
        public string GetSaveSummary()
        {
            var playerData = _dataManager.PlayerData;
            if (playerData == null)
            {
                return "无存档";
            }

            int round = playerData.currentRound;
            long catFood = playerData.catFood;
            int tribeCount = playerData.tribes?.Count ?? 0;
            int totalCats = 0;

            if (playerData.tribes != null)
            {
                foreach (var tribe in playerData.tribes)
                {
                    totalCats += tribe.GetCatCount();
                }
            }

            var roundManager = new RoundManager();
            return $"回合{round}/{roundManager.MaxRounds} | 猫粮{catFood} | 族群{tribeCount} | 小猫{totalCats}";
        }

        /// <summary>
        /// 删除存档（开始新游戏）
        /// </summary>
        public void DeleteSave()
        {
            Debug.Log("[TribeSaveManager] 删除存档，准备开始新游戏");
            // TODO: 实现存档删除功能
            // 暂时通过重置PlayerData来实现
            if (_dataManager.PlayerData != null)
            {
                _dataManager.PlayerData.currentRound = 1;
                _dataManager.PlayerData.catFood = 1000;
                _dataManager.PlayerData.tribes?.Clear();
                _dataManager.SavePlayerData();
            }
        }
    }
}
