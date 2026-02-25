using UnityEngine;
using System.Collections.Generic;
using LitJson;

/// <summary>
/// 关卡管理器 - 负责关卡的加载、管理和切换
/// </summary>
public class LevelManager : MonoBehaviour
{
    private Level _currentLevel;
    private Dictionary<int, Level> _levels = new Dictionary<int, Level>();

    public Level CurrentLevel => _currentLevel;

    public void Initialize()
    {
        Debug.Log("[LevelManager] Initialized");
    }

    /// <summary>
    /// 加载关卡
    /// </summary>
    public void LoadLevel(int levelId)
    {
        if (_levels.ContainsKey(levelId))
        {
            _currentLevel = _levels[levelId];
            Debug.Log($"[LevelManager] Level {levelId} loaded from cache");
            return;
        }

        // 从数据表读取关卡配置
        JsonData levelData = TableReader.Instance.GetRecord("Levels", levelId.ToString());
        if (levelData == null)
        {
            Debug.LogError($"[LevelManager] Level not found: {levelId}");
            return;
        }

        _currentLevel = new Level();
        _currentLevel.levelId = int.Parse(levelData["id"].ToString());
        _currentLevel.levelName = levelData["name"].ToString();
        _currentLevel.difficulty = int.Parse(levelData["difficulty"].ToString());
        _currentLevel.targetScore = int.Parse(levelData["targetScore"].ToString());
        
        _levels[levelId] = _currentLevel;
        Debug.Log($"[LevelManager] Level {levelId} loaded: {_currentLevel.levelName}");
    }

    /// <summary>
    /// 开始关卡
    /// </summary>
    public void StartLevel(int levelId)
    {
        LoadLevel(levelId);
        if (_currentLevel != null)
        {
            Debug.Log($"[LevelManager] Starting level: {_currentLevel.levelName}");
            // 这里可以触发关卡开始事件
        }
    }

    /// <summary>
    /// 获取关卡信息
    /// </summary>
    public Level GetLevel(int levelId)
    {
        if (!_levels.ContainsKey(levelId))
        {
            LoadLevel(levelId);
        }
        return _levels.ContainsKey(levelId) ? _levels[levelId] : null;
    }
}

/// <summary>
/// 关卡数据类
/// </summary>
public class Level
{
    public int levelId;
    public string levelName;
    public int difficulty;
    public int targetScore;
    public List<Unit> enemyUnits = new List<Unit>();
}