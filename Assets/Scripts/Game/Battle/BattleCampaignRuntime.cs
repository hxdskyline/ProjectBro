using UnityEngine;
using System.Collections.Generic;
using System.IO;
using LitJson;

public class BattleCampaignRuntime
{
    private const string BattleLevelConfigFileName = "battle_campaign_levels.json";

    private readonly int[][] _enemyUnitIdsByBattle;
    private readonly bool[] _blessingEnabledByBattle;
    private readonly bool[] _attributeBoostEnabledByBattle;
    private readonly bool[] _hasRecruitmentByBattle;
    private readonly bool[] _hasRitualByBattle;
    private readonly bool[] _hasShopByBattle;
    private readonly bool[] _hasNewTribeEventByBattle;
    private readonly int[] _catFoodRewardByBattle;
    private readonly UnitStaticAttributes[] _enemyStatsByBattle;

    // 弹窗优先级（数字越大越先弹出）
    private readonly Dictionary<string, int> _popupPriorities = new Dictionary<string, int>();

    private int _currentBattleIndex;
    private bool _isCompleted;

    public int CurrentBattleNumber => Mathf.Clamp(_currentBattleIndex + 1, 1, MaxBattleCount);
    public int MaxBattleCount => _enemyUnitIdsByBattle.Length;
    public bool IsCompleted => _isCompleted;
    public int CurrentEnemyCount => GetEnemyCountForBattle(CurrentBattleNumber);
    public bool HasNextBattle => !_isCompleted && CurrentBattleNumber < MaxBattleCount;

    public BattleCampaignRuntime()
    {
        _enemyUnitIdsByBattle = LoadConfig(
            out _blessingEnabledByBattle,
            out _attributeBoostEnabledByBattle,
            out _hasRecruitmentByBattle,
            out _hasRitualByBattle,
            out _hasShopByBattle,
            out _hasNewTribeEventByBattle,
            out _catFoodRewardByBattle,
            out _enemyStatsByBattle);
        ResetProgress();
    }

    public void ResetProgress()
    {
        _currentBattleIndex = 0;
        _isCompleted = false;
    }

    public int GetEnemyCountForBattle(int battleNumber)
    {
        int[] enemyUnitIds = GetEnemyUnitIdsForBattle(battleNumber);
        return enemyUnitIds != null && enemyUnitIds.Length > 0
            ? enemyUnitIds.Length
            : 1;
    }

    public int[] GetEnemyUnitIdsForBattle(int battleNumber)
    {
        if (_enemyUnitIdsByBattle == null || _enemyUnitIdsByBattle.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(battleNumber - 1, 0, _enemyUnitIdsByBattle.Length - 1);
        return _enemyUnitIdsByBattle[index];
    }

    public int GetNextBattleNumber(int currentBattleNumber)
    {
        return Mathf.Clamp(currentBattleNumber + 1, 1, MaxBattleCount);
    }

    public void AdvanceAfterVictory(int battleNumber)
    {
        int resolvedBattleNumber = Mathf.Clamp(battleNumber, 1, MaxBattleCount);
        int resolvedIndex = resolvedBattleNumber - 1;
        if (resolvedIndex != _currentBattleIndex)
        {
            return;
        }

        if (_currentBattleIndex >= MaxBattleCount - 1)
        {
            _isCompleted = true;
            return;
        }

        _currentBattleIndex++;
    }

    public int GetCatFoodRewardForBattle(int battleNumber)
    {
        if (_catFoodRewardByBattle == null || _catFoodRewardByBattle.Length == 0) return 0;
        int index = Mathf.Clamp(battleNumber - 1, 0, _catFoodRewardByBattle.Length - 1);
        return _catFoodRewardByBattle[index];
    }

    public UnitStaticAttributes GetEnemyStatsForBattle(int battleNumber)
    {
        if (_enemyStatsByBattle == null || _enemyStatsByBattle.Length == 0)
        {
            return UnitStaticAttributes.Default;
        }
        int index = Mathf.Clamp(battleNumber - 1, 0, _enemyStatsByBattle.Length - 1);
        return _enemyStatsByBattle[index];
    }

    public bool HasRecruitmentForBattle(int battleNumber)
    {
        if (_hasRecruitmentByBattle == null || _hasRecruitmentByBattle.Length == 0) return false;
        int index = Mathf.Clamp(battleNumber - 1, 0, _hasRecruitmentByBattle.Length - 1);
        return _hasRecruitmentByBattle[index];
    }

    public bool HasRitualForBattle(int battleNumber)
    {
        if (_hasRitualByBattle == null || _hasRitualByBattle.Length == 0) return false;
        int index = Mathf.Clamp(battleNumber - 1, 0, _hasRitualByBattle.Length - 1);
        return _hasRitualByBattle[index];
    }

    public bool HasShopForBattle(int battleNumber)
    {
        if (_hasShopByBattle == null || _hasShopByBattle.Length == 0) return false;
        int index = Mathf.Clamp(battleNumber - 1, 0, _hasShopByBattle.Length - 1);
        return _hasShopByBattle[index];
    }

    public bool HasNewTribeEventForBattle(int battleNumber)
    {
        if (_hasNewTribeEventByBattle == null || _hasNewTribeEventByBattle.Length == 0) return false;
        int index = Mathf.Clamp(battleNumber - 1, 0, _hasNewTribeEventByBattle.Length - 1);
        return _hasNewTribeEventByBattle[index];
    }

    public bool IsBlessingEnabledForBattle(int battleNumber)
    {
        if (_blessingEnabledByBattle == null || _blessingEnabledByBattle.Length == 0)
            return false;

        int index = Mathf.Clamp(battleNumber - 1, 0, _blessingEnabledByBattle.Length - 1);
        return _blessingEnabledByBattle[index];
    }

    public bool IsAttributeBoostEnabledForBattle(int battleNumber)
    {
        if (_attributeBoostEnabledByBattle == null || _attributeBoostEnabledByBattle.Length == 0)
            return false;

        int index = Mathf.Clamp(battleNumber - 1, 0, _attributeBoostEnabledByBattle.Length - 1);
        return _attributeBoostEnabledByBattle[index];
    }

    /// <summary>
    /// 获取弹窗事件类型的优先级（数字越大越先弹出）
    /// </summary>
    public int GetPopupPriority(string eventType)
    {
        if (_popupPriorities.TryGetValue(eventType, out int priority))
            return priority;
        return 0;
    }

    /// <summary>
    /// 获取当前回合所有需要弹出的事件，按优先级从高到低排序
    /// </summary>
    public List<string> GetSortedPopupEvents(int battleNumber)
    {
        var events = new List<System.Tuple<string, int>>();

        if (HasNewTribeEventForBattle(battleNumber))
            events.Add(new System.Tuple<string, int>("newTribeEvent", GetPopupPriority("newTribeEvent")));
        if (HasRecruitmentForBattle(battleNumber))
            events.Add(new System.Tuple<string, int>("recruitment", GetPopupPriority("recruitment")));
        if (HasRitualForBattle(battleNumber))
            events.Add(new System.Tuple<string, int>("ritual", GetPopupPriority("ritual")));
        if (HasShopForBattle(battleNumber))
            events.Add(new System.Tuple<string, int>("shop", GetPopupPriority("shop")));

        // 按优先级降序排列
        events.Sort((a, b) => b.Item2.CompareTo(a.Item2));

        var result = new List<string>();
        foreach (var e in events)
            result.Add(e.Item1);
        return result;
    }

    private int[][] LoadConfig(
        out bool[] blessingEnabledByBattle,
        out bool[] attributeBoostEnabledByBattle,
        out bool[] hasRecruitmentByBattle,
        out bool[] hasRitualByBattle,
        out bool[] hasShopByBattle,
        out bool[] hasNewTribeEventByBattle,
        out int[] catFoodRewardByBattle,
        out UnitStaticAttributes[] enemyStatsByBattle)
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, BattleLevelConfigFileName);
        if (!File.Exists(configPath))
        {
            Debug.LogError($"[BattleCampaignRuntime] Battle level config file not found: {configPath}");
            return LoadFallback(
                out blessingEnabledByBattle,
                out attributeBoostEnabledByBattle,
                out hasRecruitmentByBattle,
                out hasRitualByBattle,
                out hasShopByBattle,
                out hasNewTribeEventByBattle,
                out catFoodRewardByBattle,
                out enemyStatsByBattle);
        }

        try
        {
            string jsonContent = File.ReadAllText(configPath);
            JsonData rootJson = JsonMapper.ToObject(jsonContent);

            // 加载弹窗优先级
            if (rootJson != null && rootJson.Keys.Contains("popupPriorities"))
            {
                JsonData prioritiesJson = rootJson["popupPriorities"];
                foreach (string key in prioritiesJson.Keys)
                {
                    int val = int.TryParse(prioritiesJson[key].ToString(), out int v) ? v : 0;
                    _popupPriorities[key] = val;
                }
            }

            // 获取 levels 数组
            JsonData levelsJson = rootJson != null && rootJson.Keys.Contains("levels")
                ? rootJson["levels"]
                : rootJson; // 兼容旧格式（直接是数组）

            if (levelsJson == null || !levelsJson.IsArray || levelsJson.Count == 0)
            {
                Debug.LogError($"[BattleCampaignRuntime] Battle level config format is invalid: {configPath}");
                return LoadFallback(
                    out blessingEnabledByBattle,
                    out attributeBoostEnabledByBattle,
                    out hasRecruitmentByBattle,
                    out hasRitualByBattle,
                    out hasShopByBattle,
                    out hasNewTribeEventByBattle,
                    out catFoodRewardByBattle,
                    out enemyStatsByBattle);
            }

            int count = levelsJson.Count;
            int[][] enemyUnitIdsByBattle = new int[count][];
            blessingEnabledByBattle    = new bool[count];
            attributeBoostEnabledByBattle = new bool[count];
            hasRecruitmentByBattle     = new bool[count];
            hasRitualByBattle          = new bool[count];
            hasShopByBattle            = new bool[count];
            hasNewTribeEventByBattle   = new bool[count];
            catFoodRewardByBattle      = new int[count];
            enemyStatsByBattle         = new UnitStaticAttributes[count];

            for (int i = 0; i < count; i++)
            {
                JsonData levelJson = levelsJson[i];
                enemyUnitIdsByBattle[i] = ReadIntArray(levelJson, "enemyUnitIds");
                blessingEnabledByBattle[i]    = ReadBool(levelJson, "blessingEntry");
                attributeBoostEnabledByBattle[i] = ReadBool(levelJson, "attributeBoostEntry");
                hasRecruitmentByBattle[i]     = ReadBool(levelJson, "hasRecruitment");
                hasRitualByBattle[i]          = ReadBool(levelJson, "hasRitual");
                hasShopByBattle[i]            = ReadBool(levelJson, "hasShop");
                hasNewTribeEventByBattle[i]   = ReadBool(levelJson, "hasNewTribeEvent");
                catFoodRewardByBattle[i]      = ReadInt(levelJson, "catFoodReward");
                enemyStatsByBattle[i]         = ReadEnemyStats(levelJson);
            }

            return enemyUnitIdsByBattle;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[BattleCampaignRuntime] Failed to load battle level config: {exception.Message}");
            return LoadFallback(
                out blessingEnabledByBattle,
                out attributeBoostEnabledByBattle,
                out hasRecruitmentByBattle,
                out hasRitualByBattle,
                out hasShopByBattle,
                out hasNewTribeEventByBattle,
                out catFoodRewardByBattle,
                out enemyStatsByBattle);
        }
    }

    private int[][] LoadFallback(
        out bool[] blessingEnabledByBattle,
        out bool[] attributeBoostEnabledByBattle,
        out bool[] hasRecruitmentByBattle,
        out bool[] hasRitualByBattle,
        out bool[] hasShopByBattle,
        out bool[] hasNewTribeEventByBattle,
        out int[] catFoodRewardByBattle,
        out UnitStaticAttributes[] enemyStatsByBattle)
    {
        blessingEnabledByBattle = new[] { false };
        attributeBoostEnabledByBattle = new[] { false };
        hasRecruitmentByBattle = new[] { false };
        hasRitualByBattle = new[] { false };
        hasShopByBattle = new[] { false };
        hasNewTribeEventByBattle = new[] { false };
        catFoodRewardByBattle = new[] { 0 };
        enemyStatsByBattle = new[] { UnitStaticAttributes.Default };
        return new[] { new[] { 1 } };
    }

    private static int ReadInt(JsonData json, string key)
    {
        return ReadInt(json, key, 0);
    }

    private static int ReadInt(JsonData json, string key, int defaultValue)
    {
        return json.Keys.Contains(key) && int.TryParse(json[key].ToString(), out int v) ? v : defaultValue;
    }

    private static float ReadFloat(JsonData json, string key, float defaultValue)
    {
        return json.Keys.Contains(key) && float.TryParse(json[key].ToString(), out float v) ? v : defaultValue;
    }

    private static bool ReadBool(JsonData json, string key)
    {
        return json.Keys.Contains(key)
            && bool.TryParse(json[key].ToString(), out bool v)
            && v;
    }

    private static int[] ReadIntArray(JsonData json, string key)
    {
        if (json == null || !json.Keys.Contains(key))
        {
            return new[] { 1 };
        }

        JsonData valuesJson = json[key];
        if (valuesJson == null || !valuesJson.IsArray || valuesJson.Count == 0)
        {
            return new[] { 1 };
        }

        int[] values = new int[valuesJson.Count];
        for (int i = 0; i < valuesJson.Count; i++)
        {
            values[i] = int.TryParse(valuesJson[i].ToString(), out int value)
                ? Mathf.Max(1, value)
                : 1;
        }

        return values;
    }

    private static UnitStaticAttributes ReadEnemyStats(JsonData json)
    {
        var stats = UnitStaticAttributes.Default;

        if (json == null || !json.Keys.Contains("enemyStats"))
        {
            return stats;
        }

        JsonData statsJson = json["enemyStats"];
        if (statsJson == null)
        {
            return stats;
        }

        var defaults = UnitStaticAttributes.Default;
        stats.Attack = ReadInt(statsJson, "attack", defaults.Attack);
        stats.Defense = ReadInt(statsJson, "defense", defaults.Defense);
        stats.MaxHp = ReadInt(statsJson, "hp", defaults.MaxHp);
        stats.MoveSpeed = ReadFloat(statsJson, "moveSpeed", defaults.MoveSpeed);
        stats.AttackRange = ReadFloat(statsJson, "attackRange", defaults.AttackRange);

        return stats;
    }
}
