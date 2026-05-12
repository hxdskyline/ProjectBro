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
            _playerData.lastSaveTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
        _playerData.currencies = new System.Collections.Generic.List<CurrencyData>();
        SetCurrencyAmount(CurrencyManager.GetCurrencyKey(CurrencyType.Gold), 0, false);
        SetCurrencyAmount(CurrencyManager.GetCurrencyKey(CurrencyType.Diamond), 0, false);

        // Initialize TribeSystem fields
        _playerData.tribes = new System.Collections.Generic.List<TribeSystem.TribeRecord>();
        _playerData.currentRound = 1;
        _playerData.catFood = 1000; // Initial cat food
        _playerData.unlockedAccessories = new System.Collections.Generic.List<string>();
        _playerData.globalCatAttackFlatBonus = 0;
        _playerData.shopRefreshCount = 0;
        _playerData.lastShopRound = 0;
        _playerData.runChoices = new System.Collections.Generic.List<TribeSystem.GameChoice>();
        _playerData.runEquipments = new System.Collections.Generic.List<TribeSystem.EquipmentRecord>();

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
    /// 清空本局获得的饰品（新游戏开始时调用）
    /// </summary>
    public void ClearRunAccessories()
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.unlockedAccessories.Clear();
        _playerData.runChoices?.Clear();
        _playerData.runEquipments?.Clear();
        SavePlayerData();
    }

    /// <summary>
    /// 随机解锁一个未获得的饰品，返回饰品ID（若已全部获得则返回null）
    /// </summary>
    public string UnlockRandomAccessory(bool saveImmediately = true)
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();

        // 加载饰品配置获取所有饰品ID
        string configPath = UnityEngine.Application.streamingAssetsPath + "/Tables/accessory_config.json";
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

    public bool IsRandomEventCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.randomEventCompletedRound == round;
    }

    public void SetRandomEventCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.randomEventCompletedRound = round;
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
        if (_playerData.ownedAffixes == null)
        {
            _playerData.ownedAffixes = new System.Collections.Generic.List<string>();
        }

        // 修复旧存档：确保每个族群的cats列表不为null，并确保族长拥有天生buff
        foreach (var tribe in _playerData.tribes)
        {
            if (tribe == null) continue;

            if (tribe.cats == null)
            {
                tribe.cats = new System.Collections.Generic.List<TribeSystem.CatData>();
            }

            if (tribe.leader != null)
            {
                // 确保族长拥有天生特殊 buff
                if (tribe.leader.permanentBuffs != null)
                {
                    tribe.leader.permanentBuffs.EnsureInnateBuffs(tribe.tribeType);
                }
            }
        }

        // 从 runChoices / runEquipments 重建 leader/cat 的 ActiveBuffs
        RebuildAuraBuffs();

        if (_playerData.unlockedAccessories == null)
        {
            _playerData.unlockedAccessories = new System.Collections.Generic.List<string>();
        }

        if (_playerData.consumables == null)
        {
            _playerData.consumables = new System.Collections.Generic.List<TribeSystem.ConsumableItem>();
        }

        if (_playerData.runChoices == null)
        {
            _playerData.runChoices = new System.Collections.Generic.List<TribeSystem.GameChoice>();
        }

        if (_playerData.runEquipments == null)
        {
            _playerData.runEquipments = new System.Collections.Generic.List<TribeSystem.EquipmentRecord>();
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
    }

    /// <summary>
    /// 从 runChoices / runEquipments 重建 leader/cat 的 ActiveBuffs
    /// ActiveBuffs 是 [NonSerialized] 的，加载存档后需要从持久化的 runChoices 重建
    /// </summary>
    private void RebuildAuraBuffs()
    {
        if (_playerData.tribes == null) return;

        Debug.Log($"[RebuildAuraBuffs] runChoices={_playerData.runChoices?.Count ?? 0}, runEquipments={_playerData.runEquipments?.Count ?? 0}, tribes={_playerData.tribes.Count}");

        // 收集所有需要应用的 aura buff（runChoices + runEquipments 中 BuffApplyType.Aura/CurrentUnit 的条目）
        var auraEntries = new System.Collections.Generic.List<TribeSystem.GameChoice>();
        if (_playerData.runChoices != null)
        {
            foreach (var choice in _playerData.runChoices)
            {
                if (choice.category != TribeSystem.ChoiceCategory.Buff) continue;
                // 重建 Aura 和 CurrentUnit 类型的 buff（CurrentUnit 也需持久化，否则存档加载后丢失）
                if (choice.buffApplyType != TribeSystem.BuffApplyType.Aura
                    && choice.buffApplyType != TribeSystem.BuffApplyType.CurrentUnit) continue;
                Debug.Log($"[RebuildAuraBuffs] runChoice: id={choice.choiceId}, name={choice.displayName}, type={choice.buffApplyType}, effects={choice.buffEffects?.Count ?? 0}");
                auraEntries.Add(choice);
            }
        }
        if (_playerData.runEquipments != null)
        {
            foreach (var equip in _playerData.runEquipments)
            {
                if (equip.buffApplyType != TribeSystem.BuffApplyType.Aura) continue;
                Debug.Log($"[RebuildAuraBuffs] runEquipment: id={equip.equipmentId}, name={equip.displayName}, type={equip.buffApplyType}, effects={equip.effects?.Count ?? 0}");
                auraEntries.Add(new TribeSystem.GameChoice
                {
                    choiceId = equip.equipmentId,
                    displayName = equip.displayName,
                    description = equip.description,
                    buffScopeFilter = equip.GetScopeFilter(),
                    buffScopeText = equip.buffScopeText,
                    buffApplyType = equip.buffApplyType,
                    buffEffects = equip.effects,
                    targetTribeType = TribeSystem.TribeType.None
                });
            }
        }

        Debug.Log($"[RebuildAuraBuffs] auraEntries count={auraEntries.Count}");
        if (auraEntries.Count == 0) return;

        foreach (var tribe in _playerData.tribes)
        {
            if (tribe == null || !tribe.isActive) continue;

            int leaderBuffCountBefore = tribe.leader?.ActiveBuffs?.Count ?? 0;
            int catBuffCountBefore = 0;
            if (tribe.cats != null) foreach (var c in tribe.cats) catBuffCountBefore += c.ActiveBuffs?.Count ?? 0;

            foreach (var entry in auraEntries)
            {
                var filter = entry.GetScopeFilter();

                if (tribe.leader != null && filter.Matches(true, tribe.tribeType, null))
                {
                    ApplyAuraEffectsGeneric(tribe.leader, entry.buffEffects, entry.displayName, entry.choiceId, entry.description);
                }

                if (tribe.cats != null)
                {
                    foreach (var cat in tribe.cats)
                    {
                        if (filter.Matches(false, tribe.tribeType, cat.tier))
                        {
                            ApplyAuraEffectsGeneric(cat, entry.buffEffects, entry.displayName, entry.choiceId, entry.description);
                        }
                    }
                }
            }

            int leaderBuffCountAfter = tribe.leader?.ActiveBuffs?.Count ?? 0;
            int catBuffCountAfter = 0;
            if (tribe.cats != null) foreach (var c in tribe.cats) catBuffCountAfter += c.ActiveBuffs?.Count ?? 0;
            Debug.Log($"[RebuildAuraBuffs] Tribe {tribe.tribeType}: leader buffs {leaderBuffCountBefore}->{leaderBuffCountAfter}, cat buffs {catBuffCountBefore}->{catBuffCountAfter}");
        }
    }

    private static void ApplyAuraEffectsGeneric(TribeSystem.IHasBuffs unit, System.Collections.Generic.List<TribeSystem.BuffEffectItem> effects, string displayName, string uniqueId, string description = null)
    {
        if (effects == null) return;
        foreach (var eff in effects)
        {
            var buff = TribeSystem.UnifiedBuff.CreateStatBuff(
                $"aura_{uniqueId}_{eff.statType}", displayName,
                TribeSystem.BuffSource.Equipment, uniqueId,
                eff.statType, eff.isPercent, eff.value,
                gameEffectType: eff.gameEffectType,
                description: description);
            Debug.Log($"[RebuildAuraBuffs] Applying buff to {(unit is TribeSystem.LeaderData ? "leader" : "cat")}: id={buff.buffId}, stat={eff.statType}, isPercent={eff.isPercent}, value={eff.value}, persistence={buff.persistence}");
            unit.AddUnifiedBuff(buff);
        }
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

    // 奇物全局加成（累计值，新小猫自动继承）
    public int globalCatAttackFlatBonus;

    // 统一 Choice / Equipment 系统（本局记录）
    public System.Collections.Generic.List<TribeSystem.GameChoice> runChoices;
    public System.Collections.Generic.List<TribeSystem.EquipmentRecord> runEquipments;

    // 本回合事件完成标记（存回合号；与currentRound相同则表示本回合已完成）
    public int recruitmentCompletedRound;
    public int ritualCompletedRound;
    public int newTribeEventCompletedRound;
    public int randomEventCompletedRound;

    // 撸铁系统：已拥有的词缀ID列表
    public System.Collections.Generic.List<string> ownedAffixes;

    // 上一关的难度（用于判断是否触发双倍撸铁）
    public int lastBattleDifficulty;

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