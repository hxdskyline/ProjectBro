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

        // Ensure new persistent collections exist for CatSystem integration
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
    // Cat system persistent fields
    public System.Collections.Generic.List<CatRecord> catRoster;
    public System.Collections.Generic.List<OutingRequestRecord> outingRequests;
    public System.Collections.Generic.List<PlayerArtifactInstance> playerArtifacts;
    public System.Collections.Generic.List<RitualResultRecord> ritualHistory;
    public System.Collections.Generic.List<BlessingRecord> blessings;
    public ShopSessionRecord shopSession;
    public int lastStandCount;
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