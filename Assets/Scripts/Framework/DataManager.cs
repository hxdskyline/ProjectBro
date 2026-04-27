using UnityEngine;
using System.IO;
using LitJson;

/// <summary>
/// 数据管理器 - 负责玩家数据的保存、加载和管理
/// </summary>
public class DataManager : MonoBehaviour
    , ICurrencyStorage
{
    private PlayerData _playerData;
    private string _savePath;

    public PlayerData PlayerData => _playerData;
    public string SaveId => _playerData != null ? _playerData.playerId : string.Empty;

    public void Initialize()
    {
        _savePath = Path.Combine(Application.persistentDataPath, "PlayerData");
        
        // 如果目录不存在则创建
        if (!Directory.Exists(_savePath))
        {
            Directory.CreateDirectory(_savePath);
        }

        Debug.Log($"[DataManager] Initialized at: {_savePath}");
    }

    /// <summary>
    /// 加载玩家数据
    /// </summary>
    public void LoadPlayerData()
    {
        string filePath = Path.Combine(_savePath, "playerdata.json");

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                _playerData = JsonUtility.FromJson<PlayerData>(json);
                EnsurePlayerDataDefaults();
                Debug.Log("[DataManager] Player data loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataManager] Error loading player data: {e.Message}");
                CreateNewPlayerData();
            }
        }
        else
        {
            CreateNewPlayerData();
        }
    }

    /// <summary>
    /// 保存玩家数据
    /// </summary>
    public void SavePlayerData()
    {
        if (_playerData == null)
            return;

        try
        {
            string filePath = Path.Combine(_savePath, "playerdata.json");
            string json = JsonUtility.ToJson(_playerData, true);
            File.WriteAllText(filePath, json);
            Debug.Log("[DataManager] Player data saved successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataManager] Error saving player data: {e.Message}");
        }
    }

    /// <summary>
    /// 创建新的玩家数据
    /// </summary>
    private void CreateNewPlayerData()
    {
        _playerData = new PlayerData();
        _playerData.playerId = System.Guid.NewGuid().ToString();
        _playerData.playerName = "Player";
        _playerData.level = 1;
        _playerData.currentLevel = 1;
        _playerData.gold = 0;
        _playerData.diamond = 0;
        _playerData.currencies = new System.Collections.Generic.List<CurrencyData>();
        SetCurrencyAmount(CurrencyManager.GetCurrencyKey(CurrencyType.Gold), 0, false);
        SetCurrencyAmount(CurrencyManager.GetCurrencyKey(CurrencyType.Diamond), 0, false);

        // Initialize TribeSystem fields
        _playerData.tribes = new System.Collections.Generic.List<TribeSystem.TribeRecord>();
        _playerData.currentRound = 1;
        _playerData.catFood = 1000; // Initial cat food
        _playerData.unlockedAccessories = new System.Collections.Generic.List<string>();
        _playerData.shopRefreshCount = 0;
        _playerData.lastShopRound = 0;

        SavePlayerData();
        Debug.Log("[DataManager] New player data created");
    }

    /// <summary>
    /// 重置玩家数据
    /// </summary>
    public void ResetPlayerData()
    {
        string filePath = Path.Combine(_savePath, "playerdata.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        CreateNewPlayerData();
        Debug.Log("[DataManager] Player data reset");
    }

    /// <summary>
    /// 删除存档数据
    /// </summary>
    public void DeleteSaveData()
    {
        string filePath = Path.Combine(_savePath, "playerdata.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // 创建一个全新且干净的数据覆盖当前内存
        CreateNewPlayerData();
        Debug.Log("[DataManager] Player save data deleted and reset to initial state");
    }

    public long GetCurrencyAmount(string currencyId)
    {
        if (_playerData == null || string.IsNullOrEmpty(currencyId))
        {
            return 0;
        }

        EnsurePlayerDataDefaults();
        return GetCurrencyAmountInternal(currencyId);
    }

    private long GetCurrencyAmountInternal(string currencyId)
    {
        for (int i = 0; i < _playerData.currencies.Count; i++)
        {
            CurrencyData currency = _playerData.currencies[i];
            if (currency != null && currency.currencyId == currencyId)
            {
                return currency.amount;
            }
        }

        return 0;
    }

    public void SetCurrencyAmount(string currencyId, long amount, bool saveImmediately)
    {
        if (_playerData == null || string.IsNullOrEmpty(currencyId))
        {
            return;
        }

        EnsurePlayerDataDefaults();

        bool updated = false;
        for (int i = 0; i < _playerData.currencies.Count; i++)
        {
            CurrencyData currency = _playerData.currencies[i];
            if (currency == null || currency.currencyId != currencyId)
            {
                continue;
            }

            currency.amount = amount;
            _playerData.currencies[i] = currency;
            updated = true;
            break;
        }

        if (!updated)
        {
            _playerData.currencies.Add(new CurrencyData
            {
                currencyId = currencyId,
                amount = amount
            });
        }

        SyncLegacyCurrencyFields();

        if (saveImmediately)
        {
            SavePlayerData();
        }
    }

    public void SaveCurrencyData()
    {
        SavePlayerData();
    }

    // --- CatSystem persistence helpers ---
    public CatRecord AddCat(CatRecord record, bool saveImmediately = true)
    {
        if (_playerData == null || record == null) return null;
        EnsurePlayerDataDefaults();
        if (record.id == 0) record.id = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _playerData.catRoster.Add(record);
        if (saveImmediately) SavePlayerData();
        return record;
    }

    public CatRecord GetCat(long id)
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.catRoster.Find(c => c != null && c.id == id);
    }

    public bool RemoveCat(long id, bool saveImmediately = true)
    {
        if (_playerData == null) return false;
        EnsurePlayerDataDefaults();
        var cat = _playerData.catRoster.Find(c => c != null && c.id == id);
        if (cat == null) return false;
        _playerData.catRoster.Remove(cat);
        if (saveImmediately) SavePlayerData();
        return true;
    }

    public OutingRequestRecord AddOutingRequest(OutingRequestRecord req, bool saveImmediately = true)
    {
        if (_playerData == null || req == null) return null;
        EnsurePlayerDataDefaults();
        if (req.requestId == 0) req.requestId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _playerData.outingRequests.Add(req);
        if (saveImmediately) SavePlayerData();
        return req;
    }

    public System.Collections.Generic.List<OutingRequestRecord> GetOutingRequests()
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.outingRequests;
    }

    public PlayerArtifactInstance AddArtifactInstance(PlayerArtifactInstance inst, bool saveImmediately = true)
    {
        if (_playerData == null || inst == null) return null;
        EnsurePlayerDataDefaults();
        if (inst.instanceId == 0) inst.instanceId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _playerData.playerArtifacts.Add(inst);
        if (saveImmediately) SavePlayerData();
        return inst;
    }

    public System.Collections.Generic.List<PlayerArtifactInstance> GetArtifactInstances()
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.playerArtifacts;
    }

    public RitualResultRecord AddRitualResult(RitualResultRecord record, bool saveImmediately = true)
    {
        if (_playerData == null || record == null) return null;
        EnsurePlayerDataDefaults();
        _playerData.ritualHistory.Add(record);
        if (saveImmediately) SavePlayerData();
        return record;
    }

    public BlessingRecord AddBlessing(BlessingRecord blessing, bool saveImmediately = true)
    {
        if (_playerData == null || blessing == null) return null;
        EnsurePlayerDataDefaults();
        _playerData.blessings.Add(blessing);
        if (saveImmediately) SavePlayerData();
        return blessing;
    }

    // --- TribeSystem persistence helpers ---
    public TribeSystem.TribeRecord AddTribe(TribeSystem.TribeRecord tribe, bool saveImmediately = true)
    {
        if (_playerData == null || tribe == null) return null;
        EnsurePlayerDataDefaults();
        if (tribe.tribeId < 0) tribe.tribeId = _playerData.tribes.Count;
        _playerData.tribes.Add(tribe);
        if (saveImmediately) SavePlayerData();
        return tribe;
    }

    public System.Collections.Generic.List<TribeSystem.TribeRecord> GetTribes()
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.tribes;
    }

    public TribeSystem.TribeRecord GetTribe(int tribeId)
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.tribes.Find(t => t != null && t.tribeId == tribeId);
    }

    public bool RemoveTribe(int tribeId, bool saveImmediately = true)
    {
        if (_playerData == null) return false;
        EnsurePlayerDataDefaults();
        var tribe = _playerData.tribes.Find(t => t != null && t.tribeId == tribeId);
        if (tribe == null) return false;
        _playerData.tribes.Remove(tribe);
        if (saveImmediately) SavePlayerData();
        return true;
    }

    public int GetCurrentRound()
    {
        if (_playerData == null) return 1;
        EnsurePlayerDataDefaults();
        // 确保currentRound不为0（处理旧存档或未初始化的情况）
        if (_playerData.currentRound <= 0)
        {
            _playerData.currentRound = 1;
        }
        return _playerData.currentRound;
    }

    public void SetCurrentRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.currentRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public long GetCatFood()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.catFood;
    }

    public void SetCatFood(long amount, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.catFood = amount;
        if (saveImmediately) SavePlayerData();
    }

    public void AddCatFood(long amount, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.catFood += amount;
        if (saveImmediately) SavePlayerData();
    }

    public bool TrySpendCatFood(long amount, bool saveImmediately = true)
    {
        if (_playerData == null) return false;
        EnsurePlayerDataDefaults();
        if (_playerData.catFood < amount) return false;
        _playerData.catFood -= amount;
        if (saveImmediately) SavePlayerData();
        return true;
    }

    public void UnlockAccessory(string accessoryId, bool saveImmediately = true)
    {
        if (_playerData == null || string.IsNullOrEmpty(accessoryId)) return;
        EnsurePlayerDataDefaults();
        if (!_playerData.unlockedAccessories.Contains(accessoryId))
        {
            _playerData.unlockedAccessories.Add(accessoryId);
            if (saveImmediately) SavePlayerData();
        }
    }

    public bool IsAccessoryUnlocked(string accessoryId)
    {
        if (_playerData == null || string.IsNullOrEmpty(accessoryId)) return false;
        EnsurePlayerDataDefaults();
        return _playerData.unlockedAccessories.Contains(accessoryId);
    }

    public System.Collections.Generic.List<string> GetUnlockedAccessories()
    {
        if (_playerData == null) return new System.Collections.Generic.List<string>();
        EnsurePlayerDataDefaults();
        return new System.Collections.Generic.List<string>(_playerData.unlockedAccessories);
    }

    /// <summary>
    /// 随机解锁一个未获得的饰品，返回饰品ID（若已全部获得则返回null）
    /// </summary>
    public string UnlockRandomAccessory(bool saveImmediately = true)
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();

        // 加载饰品配置获取所有饰品ID
        string configPath = UnityEngine.Application.streamingAssetsPath + "/accessory_config.json";
        if (!System.IO.File.Exists(configPath)) return null;

        var configText = System.IO.File.ReadAllText(configPath);
        var root = LitJson.JsonMapper.ToObject(configText);
        var accessoriesJson = root["accessories"];

        // 找出未解锁的饰品ID
        var unaccessedIds = new System.Collections.Generic.List<string>();
        for (int i = 0; i < accessoriesJson.Count; i++)
        {
            string accId = accessoriesJson[i]["id"].ToString();
            if (!string.IsNullOrEmpty(accId) && !_playerData.unlockedAccessories.Contains(accId))
            {
                unaccessedIds.Add(accId);
            }
        }

        if (unaccessedIds.Count == 0) return null;

        var pickedId = unaccessedIds[UnityEngine.Random.Range(0, unaccessedIds.Count)];
        UnlockAccessory(pickedId, saveImmediately);
        return pickedId;
    }

    public int GetShopRefreshCount()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.shopRefreshCount;
    }

    public void SetShopRefreshCount(int count, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.shopRefreshCount = count;
        if (saveImmediately) SavePlayerData();
    }

    public void IncrementShopRefreshCount(bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.shopRefreshCount++;
        if (saveImmediately) SavePlayerData();
    }

    // --- Consumable inventory ---

    public System.Collections.Generic.List<TribeSystem.ConsumableItem> GetConsumables()
    {
        if (_playerData == null) return new System.Collections.Generic.List<TribeSystem.ConsumableItem>();
        EnsurePlayerDataDefaults();
        return _playerData.consumables;
    }

    public void AddConsumable(TribeSystem.ConsumableItem item)
    {
        if (_playerData == null || item == null) return;
        EnsurePlayerDataDefaults();
        _playerData.consumables.Add(item);
        SavePlayerData();
    }

    public void RemoveConsumable(int id)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.consumables.RemoveAll(c => c.id == id);
        SavePlayerData();
    }

    public int GetConsumableCount()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.consumables.Count;
    }

    public int GetLastShopRound()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.lastShopRound;
    }

    public void SetLastShopRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.lastShopRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsRecruitmentCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.recruitmentCompletedRound == round;
    }

    public void SetRecruitmentCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.recruitmentCompletedRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsRitualCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.ritualCompletedRound == round;
    }

    public void SetRitualCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.ritualCompletedRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsNewTribeEventCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.newTribeEventCompletedRound == round;
    }

    public void SetNewTribeEventCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.newTribeEventCompletedRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public int GetLastStandCount()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.lastStandCount;
    }

    public void SetLastStandCount(int count, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.lastStandCount = count;
        if (saveImmediately) SavePlayerData();
    }

    public int GetLastExpandedTribeId()
    {
        if (_playerData == null) return -1;
        return _playerData.lastExpandedTribeId;
    }

    public void SetLastExpandedTribeId(int tribeId, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.lastExpandedTribeId = tribeId;
        if (saveImmediately) SavePlayerData();
    }

    private void EnsurePlayerDataDefaults()
    {
        if (_playerData == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_playerData.playerId))
        {
            _playerData.playerId = System.Guid.NewGuid().ToString();
        }

        if (_playerData.currencies == null)
        {
            _playerData.currencies = new System.Collections.Generic.List<CurrencyData>();
        }

        // Ensure TribeSystem collections exist
        if (_playerData.tribes == null)
        {
            _playerData.tribes = new System.Collections.Generic.List<TribeSystem.TribeRecord>();
        }

        // 修复旧存档：确保每个族群的cats列表不为null，并刷新族长baseSpeed
        foreach (var tribe in _playerData.tribes)
        {
            if (tribe == null) continue;

            if (tribe.cats == null)
            {
                tribe.cats = new System.Collections.Generic.List<TribeSystem.CatData>();
            }

            // 旧存档族长baseSpeed可能是默认值1000，从配置表刷新
            if (tribe.leader != null)
            {
                var config = TribeSystem.TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
                if (config != null && config.leaderBaseStats != null)
                {
                    tribe.leader.baseSpeed = config.leaderBaseStats.speed;
                }

                // 确保族长拥有天生特殊 buff（兼容旧存档）
                if (tribe.leader.permanentBuffs != null)
                {
                    tribe.leader.permanentBuffs.EnsureInnateBuffs(tribe.tribeType);
                }
            }
        }

        if (_playerData.unlockedAccessories == null)
        {
            _playerData.unlockedAccessories = new System.Collections.Generic.List<string>();
        }

        if (_playerData.consumables == null)
        {
            _playerData.consumables = new System.Collections.Generic.List<TribeSystem.ConsumableItem>();
        }

        // Ensure new persistent collections exist for CatSystem integration (legacy)
        if (_playerData.catRoster == null)
        {
            _playerData.catRoster = new System.Collections.Generic.List<CatRecord>();
        }

        if (_playerData.outingRequests == null)
        {
            _playerData.outingRequests = new System.Collections.Generic.List<OutingRequestRecord>();
        }

        if (_playerData.playerArtifacts == null)
        {
            _playerData.playerArtifacts = new System.Collections.Generic.List<PlayerArtifactInstance>();
        }

        if (_playerData.ritualHistory == null)
        {
            _playerData.ritualHistory = new System.Collections.Generic.List<RitualResultRecord>();
        }

        if (_playerData.blessings == null)
        {
            _playerData.blessings = new System.Collections.Generic.List<BlessingRecord>();
        }

        if (_playerData.shopSession == null)
        {
            _playerData.shopSession = new ShopSessionRecord();
        }

        MigrateLegacyCurrencyField(CurrencyManager.GetCurrencyKey(CurrencyType.Gold), _playerData.gold);
        MigrateLegacyCurrencyField(CurrencyManager.GetCurrencyKey(CurrencyType.Diamond), _playerData.diamond);
        SyncLegacyCurrencyFields();
    }

    private void MigrateLegacyCurrencyField(string currencyId, long legacyAmount)
    {
        bool exists = false;
        for (int i = 0; i < _playerData.currencies.Count; i++)
        {
            CurrencyData currency = _playerData.currencies[i];
            if (currency != null && currency.currencyId == currencyId)
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            _playerData.currencies.Add(new CurrencyData
            {
                currencyId = currencyId,
                amount = legacyAmount
            });
        }
    }

    private void SyncLegacyCurrencyFields()
    {
        _playerData.gold = GetCurrencyAmountInternal(CurrencyManager.GetCurrencyKey(CurrencyType.Gold));
        _playerData.diamond = GetCurrencyAmountInternal(CurrencyManager.GetCurrencyKey(CurrencyType.Diamond));
        _playerData.lastSaveTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

/// <summary>
/// 玩家数据结构
/// </summary>
[System.Serializable]
public class PlayerData
{
    public string playerId;
    public string playerName;
    public int level;
    public int currentLevel;
    public long gold;
    public long diamond;
    public long lastSaveTime;
    public System.Collections.Generic.List<CurrencyData> currencies;

    // TribeSystem persistent fields (NEW)
    public System.Collections.Generic.List<TribeSystem.TribeRecord> tribes;
    public int currentRound;
    public long catFood;
    public System.Collections.Generic.List<string> unlockedAccessories;
    public int shopRefreshCount;
    public int lastShopRound;
    public System.Collections.Generic.List<TribeSystem.ConsumableItem> consumables;

    // 本回合事件完成标记（存回合号；与currentRound相同则表示本回合已完成）
    public int recruitmentCompletedRound;
    public int ritualCompletedRound;
    public int newTribeEventCompletedRound;

    // Legacy Cat system persistent fields (kept for compatibility, marked obsolete)
    [System.Obsolete("Use TribeSystem instead")]
    public System.Collections.Generic.List<CatRecord> catRoster;
    [System.Obsolete("Use TribeSystem instead")]
    public System.Collections.Generic.List<OutingRequestRecord> outingRequests;
    [System.Obsolete("Use TribeSystem instead")]
    public System.Collections.Generic.List<PlayerArtifactInstance> playerArtifacts;
    [System.Obsolete("Use TribeSystem instead")]
    public System.Collections.Generic.List<RitualResultRecord> ritualHistory;
    [System.Obsolete("Use TribeSystem instead")]
    public System.Collections.Generic.List<BlessingRecord> blessings;
    [System.Obsolete("Use TribeSystem instead")]
    public ShopSessionRecord shopSession;
    public int lastStandCount;

    // 上次展开的族群ID（-1表示无）
    public int lastExpandedTribeId = -1;
}

[System.Serializable]
public class CurrencyData
{
    public string currencyId;
    public long amount;
}

[System.Serializable]
public class CatRecord
{
    public long id;
    public int templateId;
    public string name;
    public bool nameChanged;
    public string gender;
    public int level;
    public int attack;
    public int defense;
    public int hp;
    public float moveSpeed;
    public int energy;
    public int energyMax;
    public System.Collections.Generic.List<int> skills;
    public System.Collections.Generic.List<int> traits;
    public long accessoryInstanceId;
    public System.Collections.Generic.List<long> parents;
    public System.Collections.Generic.List<long> children;
    public CatFlags flags;
    public long createdAt;
}

[System.Serializable]
public class CatFlags
{
    public bool isOutingRequested;
    public bool isOutingActive;
    public bool isDeployed;
    public bool deadPermanently;
}

[System.Serializable]
public class OutingRequestRecord
{
    public long requestId;
    public System.Collections.Generic.List<long> pairIds;
    public int initiatedCycle;
    public int returnCycle;
    public string status;
}

[System.Serializable]
public class PlayerArtifactInstance
{
    public long instanceId;
    public int artifactId;
    public long ownerCatId;
    public int remainingDurability;
    public long acquiredAt;
}

[System.Serializable]
public class RitualResultRecord
{
    public long requestId;
    public string offerType;
    public string selectedOptionId;
    public System.Collections.Generic.List<RewardEntry> rewards;
    public long timestamp;
}

[System.Serializable]
public class RewardEntry
{
    public string type;
    public string payloadJson;
}

[System.Serializable]
public class BlessingRecord
{
    public string id;
    public string name;
    public string effectType;
    public float effectValue;
    public int durationRounds;
    public bool persistent;
}

[System.Serializable]
public class ShopSessionRecord
{
    public int timesRefreshed;
    public long lastRefreshAt;
}