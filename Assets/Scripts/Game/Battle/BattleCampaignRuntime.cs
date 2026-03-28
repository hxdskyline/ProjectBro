using UnityEngine;
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
    private readonly int[] _catFoodRewardByBattle;
    private int _currentBattleIndex;
    private bool _isCompleted;

    public int CurrentBattleNumber => Mathf.Clamp(_currentBattleIndex + 1, 1, MaxBattleCount);
    public int MaxBattleCount => _enemyUnitIdsByBattle.Length;
    public bool IsCompleted => _isCompleted;
    public int CurrentEnemyCount => GetEnemyCountForBattle(CurrentBattleNumber);
    public bool HasNextBattle => !_isCompleted && CurrentBattleNumber < MaxBattleCount;

    public BattleCampaignRuntime()
    {
        _enemyUnitIdsByBattle = LoadEnemyUnitIds(
            out _blessingEnabledByBattle,
            out _attributeBoostEnabledByBattle,
            out _hasRecruitmentByBattle,
            out _hasRitualByBattle,
            out _hasShopByBattle,
            out _catFoodRewardByBattle);
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

    private static int[][] LoadEnemyUnitIds(
        out bool[] blessingEnabledByBattle,
        out bool[] attributeBoostEnabledByBattle,
        out bool[] hasRecruitmentByBattle,
        out bool[] hasRitualByBattle,
        out bool[] hasShopByBattle,
        out int[] catFoodRewardByBattle)
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, BattleLevelConfigFileName);
        if (!File.Exists(configPath))
        {
            Debug.LogError($"[BattleCampaignRuntime] Battle level config file not found: {configPath}");
            blessingEnabledByBattle = new[] { false };
            attributeBoostEnabledByBattle = new[] { false };
            hasRecruitmentByBattle = new[] { false };
            hasRitualByBattle = new[] { false };
            hasShopByBattle = new[] { false };
            catFoodRewardByBattle = new[] { 0 };
            return new[] { new[] { 1 } };
        }

        try
        {
            string jsonContent = File.ReadAllText(configPath);
            JsonData levelsJson = JsonMapper.ToObject(jsonContent);
            if (levelsJson == null || !levelsJson.IsArray || levelsJson.Count == 0)
            {
                Debug.LogError($"[BattleCampaignRuntime] Battle level config format is invalid: {configPath}");
                blessingEnabledByBattle = new[] { false };
                attributeBoostEnabledByBattle = new[] { false };
                hasRecruitmentByBattle = new[] { false };
                hasRitualByBattle = new[] { false };
                hasShopByBattle = new[] { false };
                catFoodRewardByBattle = new[] { 0 };
                return new[] { new[] { 1 } };
            }

            int count = levelsJson.Count;
            int[][] enemyUnitIdsByBattle = new int[count][];
            blessingEnabledByBattle    = new bool[count];
            attributeBoostEnabledByBattle = new bool[count];
            hasRecruitmentByBattle     = new bool[count];
            hasRitualByBattle          = new bool[count];
            hasShopByBattle            = new bool[count];
            catFoodRewardByBattle      = new int[count];

            for (int i = 0; i < count; i++)
            {
                JsonData levelJson = levelsJson[i];
                enemyUnitIdsByBattle[i] = ReadIntArray(levelJson, "enemyUnitIds");
                blessingEnabledByBattle[i]    = ReadBool(levelJson, "blessingEntry");
                attributeBoostEnabledByBattle[i] = ReadBool(levelJson, "attributeBoostEntry");
                hasRecruitmentByBattle[i]     = ReadBool(levelJson, "hasRecruitment");
                hasRitualByBattle[i]          = ReadBool(levelJson, "hasRitual");
                hasShopByBattle[i]            = ReadBool(levelJson, "hasShop");
                catFoodRewardByBattle[i]      = ReadInt(levelJson, "catFoodReward");
            }

            return enemyUnitIdsByBattle;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[BattleCampaignRuntime] Failed to load battle level config: {exception.Message}");
            blessingEnabledByBattle = new[] { false };
            attributeBoostEnabledByBattle = new[] { false };
            hasRecruitmentByBattle = new[] { false };
            hasRitualByBattle = new[] { false };
            hasShopByBattle = new[] { false };
            catFoodRewardByBattle = new[] { 0 };
            return new[] { new[] { 1 } };
        }
    }

    private static int ReadInt(JsonData json, string key)
    {
        return json.Keys.Contains(key) && int.TryParse(json[key].ToString(), out int v) ? v : 0;
    }

    private static bool ReadBool(JsonData json, string key)
    {
        return json.Keys.Contains(key)
            && bool.TryParse(json[key].ToString(), out bool v)
            && v;
    }

    public int GetCatFoodRewardForBattle(int battleNumber)
    {
        if (_catFoodRewardByBattle == null || _catFoodRewardByBattle.Length == 0) return 0;
        int index = Mathf.Clamp(battleNumber - 1, 0, _catFoodRewardByBattle.Length - 1);
        return _catFoodRewardByBattle[index];
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
}
