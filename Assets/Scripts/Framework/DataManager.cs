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
}

[System.Serializable]
public class CurrencyData
{
    public string currencyId;
    public long amount;
}