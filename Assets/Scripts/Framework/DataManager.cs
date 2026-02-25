using UnityEngine;
using System.IO;
using LitJson;

/// <summary>
/// 数据管理器 - 负责玩家数据的保存、加载和管理
/// </summary>
public class DataManager : MonoBehaviour
{
    private PlayerData _playerData;
    private string _savePath;

    public PlayerData PlayerData => _playerData;

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
}