using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TribeSystem;
using TribeSystem.UI;
using BattleSystem;
using BattleSystem.Fighter;
using BattleSystem.Avatar;

/// <summary>
/// 战斗界面 - 显示战斗进行中的UI
/// </summary>
public class BattlePanel : UIPanel
{
    [SerializeField] private Text _battleInfoText;
    [SerializeField] private Text _levelText;
    [SerializeField] private Button _pauseButton;
    [SerializeField] private GameObject _fighterPrefab;
    [SerializeField] private AvatarAnimationDefinition _playerAvatarDefinition;
    [SerializeField] private AvatarAnimationDefinition _enemyAvatarDefinition;

    private BattleFlowController _flowController;
    private int _currentLevel;
    private bool _isPaused;
    private TerrainType _currentTerrain;
    private WeatherType _currentWeather;
    private DifficultyLevel _currentDifficulty;
    private List<TribeRecord> _deployedTribes;
    private BattleHUDPanel _battleHUD;

    // Consumable UI
    private List<ConsumableItem> _consumableItems;
    private readonly List<GameObject> _consumableButtonObjects = new List<GameObject>();
    private RectTransform _consumableBarRoot;

    private readonly Dictionary<string, AvatarAnimationDefinition> _playerAvatarDefinitionsByAddress
        = new Dictionary<string, AvatarAnimationDefinition>();

    // 族群类型 → 品种名前缀（对应 avatartemp/{breed}1 和 {breed}2）
    private static readonly Dictionary<TribeType, string> s_tribeBreedNames = new Dictionary<TribeType, string>
    {
        { TribeType.Tabby,   "lihua"   },
        { TribeType.Orange,  "daju"    },
        { TribeType.Cow,     "nainiu"  },
        { TribeType.Siamese, "xianluo" },
    };

    // avatarId → AvatarDefinition 缓存（数据驱动，从 fighter_config.json 的 avatarId 读取）
    private readonly Dictionary<string, AvatarAnimationDefinition> _avatarIdCache
        = new Dictionary<string, AvatarAnimationDefinition>();

    private AvatarAnimationDefinition GetAvatarById(string avatarId)
    {
        if (string.IsNullOrEmpty(avatarId)) return null;
        if (_avatarIdCache.TryGetValue(avatarId, out var cached)) return cached;

        // 运行时创建（从 avatartemp/{avatarId}1 和 {avatarId}2 加载帧）
        var def = AvatarAnimationDefinition.CreateRuntime(avatarId, $"avatartemp/{avatarId}1", $"avatartemp/{avatarId}2");

        _avatarIdCache[avatarId] = def;
        return def;
    }

    private AvatarAnimationDefinition GetTribeAvatarDefinition(TribeType tribeType, int fighterId = 0)
    {
        // 数据驱动：从 fighter_config.json 的 avatarId 字段读取外观
        if (fighterId > 0)
        {
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            if (fighterConfig != null && !string.IsNullOrEmpty(fighterConfig.avatarId))
            {
                return GetAvatarById(fighterConfig.avatarId);
            }
        }

        // Fallback: 用族群品种名运行时创建
        if (s_tribeBreedNames.TryGetValue(tribeType, out string breed))
        {
            return GetAvatarById(breed);
        }

        Debug.LogWarning($"[BattlePanel] 未找到族群 {tribeType} 的外观配置，使用默认avatar");
        return _playerAvatarDefinition;
    }

    public override void Initialize()
    {
        base.Initialize();

        if (_pauseButton != null)
        {
            _pauseButton.onClick.RemoveListener(OnPauseButtonClicked);
            _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        Debug.Log("[BattlePanel] Initialized");
    }

    /// <summary>
    /// 开始战斗 - 使用新族群系统（含地形/天气/难度）
    /// </summary>
    public void StartBattle(int levelId, List<TribeRecord> deployedTribes,
        TerrainType terrain = TerrainType.Plain, WeatherType weather = WeatherType.Sunny,
        DifficultyLevel difficulty = DifficultyLevel.Normal)
    {
        _currentLevel = levelId;
        _currentTerrain = terrain;
        _currentWeather = weather;
        _currentDifficulty = difficulty;
        _deployedTribes = deployedTribes;
        _isPaused = false;

        if (_flowController == null)
        {
            _flowController = new BattleFlowController();
        }

        if (_levelText != null)
        {
            _levelText.text = $"Level: {levelId}  {BattleScenarioOption.GetTerrainName(terrain)}/{BattleScenarioOption.GetWeatherName(weather)}  {GetDifficultyName(difficulty)}";
        }

        if (_battleInfoText != null)
        {
            _battleInfoText.text = "Battle Running (Scene Avatar)";
        }

        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;
        UnitStaticAttributes? enemyStats = campaign != null
            ? (UnitStaticAttributes?)campaign.GetEnemyStats(levelId, difficulty)
            : null;

        int enemyCount = ResolveEnemyCount(levelId);

        _flowController.StartBattle(
            levelId,
            _fighterPrefab,
            _playerAvatarDefinition,
            _enemyAvatarDefinition,
            enemyCount,
            BuildPlayerSpawnDefinitions(deployedTribes),
            OnBattleEnded,
            enemyStats,
            terrain,
            weather);

        // Create battle HUD (vertical HP bars in top-left)
        CreateBattleHUD(deployedTribes);

        // Create consumable button bar at bottom
        CreateConsumableBar();

        if (enemyStats.HasValue)
        {
            var s = enemyStats.Value;
            Debug.Log($"[BattlePanel] Battle Lv{levelId} {GetDifficultyName(difficulty)}: " +
                $"{deployedTribes?.Count ?? 0} tribes | " +
                $"Terrain={BattleScenarioOption.GetTerrainName(terrain)} " +
                $"Weather={BattleScenarioOption.GetWeatherName(weather)} | " +
                $"Enemy stats ATK={s.Attack} DEF={s.Defense} HP={s.MaxHp} SPD={s.MoveSpeed} | " +
                $"Enemy count={enemyCount}");
        }
    }

    private int ResolveEnemyCount(int levelId)
    {
        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;
        if (campaign == null)
        {
            return 1;
        }

        // Try to get formation-specific count from scenario options
        var scenarios = campaign.GetScenarioOptions(levelId);
        if (scenarios != null && scenarios.Count > 0)
        {
            // Use first scenario's formation type (will be overridden by selected scenario later)
            EnemyFormationType formation = scenarios[0].formationType;
            int[] ids = campaign.GetEnemyUnitIds(levelId, formation);
            if (ids != null && ids.Length > 0)
                return ids.Length;
        }

        return campaign.GetEnemyCountForBattle(levelId);
    }

    /// <summary>
    /// 从上阵的族群构建战斗单位生成定义
    /// 每个族群的所有 units 统一生成战斗单位
    /// </summary>
    private BattleFighterSpawnDefinition[] BuildPlayerSpawnDefinitions(List<TribeRecord> deployedTribes)
    {
        if (deployedTribes == null || deployedTribes.Count == 0)
        {
            return null;
        }

        List<BattleFighterSpawnDefinition> definitions = new List<BattleFighterSpawnDefinition>();

        foreach (TribeRecord tribe in deployedTribes)
        {
            int unitCount = tribe.units?.Count ?? 0;
            Debug.Log($"[BattlePanel] {tribe.tribeType} 族群单位数量: {unitCount}");
            if (tribe.units != null && tribe.units.Count > 0)
            {
                for (int i = 0; i < tribe.units.Count; i++)
                {
                    FighterData unit = tribe.units[i];
                    if (CreateSpawnDefinition(tribe, unit, i, out BattleFighterSpawnDefinition unitDef))
                    {
                        definitions.Add(unitDef);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[BattlePanel] {tribe.tribeType} 族群没有单位！请检查存档是否为旧版本。如果是，请删除存档重新开始。");
            }
        }

        return definitions.Count > 0 ? definitions.ToArray() : null;
    }

    /// <summary>
    /// 创建战斗单位定义（统一处理所有单位，基础属性/BUFF 通过运行时修正体系生效）
    /// unitIndex == 0 时作为族长（缩放 1.0），其余按 tier 缩放
    /// </summary>
    private bool CreateSpawnDefinition(TribeRecord tribe, FighterData unit, int unitIndex, out BattleFighterSpawnDefinition definition)
    {
        definition = default;

        if (unit == null)
            return false;

        // 获取 fighterId
        int unitFighterId = unit.fighterId;
        if (unitFighterId <= 0)
        {
            unitFighterId = GetFighterId(tribe.tribeType, unit.tier);
        }
        var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(unitFighterId);

        // 计算战斗属性
        UnitStaticAttributes staticAttributes;
        string unitName;
        float scaleMultiplier;

        if (fighterConfig != null)
        {
            // 使用 fighter_config.json 的属性
            staticAttributes = new UnitStaticAttributes
            {
                MaxHp = Mathf.Max(1, fighterConfig.hp),
                Attack = Mathf.Max(1, fighterConfig.attack),
                Defense = Mathf.Max(0, fighterConfig.defense),
                MoveSpeed = Mathf.Max(1, Mathf.RoundToInt(fighterConfig.moveSpeed * 1000)),
                AttackSpeed = Mathf.Max(1, Mathf.RoundToInt(fighterConfig.attackSpeed * 1000)),
                AttackRange = Mathf.Max(0.1f, fighterConfig.attackRange)
            };
            unitName = fighterConfig.fighterName;
            scaleMultiplier = unitIndex == 0 ? 1.0f : unit.tier == UnitTier.Tier3 ? 0.85f : unit.tier == UnitTier.Tier2 ? 0.75f : 0.65f;
        }
        else
        {
            // 回退：使用品质计算的属性
            FighterStats unitStats = TribeStatsCalculator.CalculateFighterStats(unit, tribe.moodId);
            float unitAttackRange = tribe.tribeType == TribeType.Tabby ? 4.0f : 1.0f;

            staticAttributes = new UnitStaticAttributes
            {
                MaxHp = Mathf.Max(1, Mathf.RoundToInt(unitStats.hp)),
                Attack = Mathf.Max(1, Mathf.RoundToInt(unitStats.attack)),
                Defense = Mathf.Max(0, Mathf.RoundToInt(unitStats.defense)),
                MoveSpeed = Mathf.Max(1, Mathf.RoundToInt(unitStats.moveSpeed * 1000)),
                AttackSpeed = Mathf.Max(1, Mathf.RoundToInt(unitStats.attackSpeed * 1000)),
                AttackRange = Mathf.Max(0.1f, unitAttackRange)
            };
            scaleMultiplier = unitIndex == 0 ? 1.0f : unit.tier == UnitTier.Tier3 ? 0.85f : unit.tier == UnitTier.Tier2 ? 0.75f : 0.65f;

            unitName = fighterConfig?.fighterName ?? $"兵种{unitFighterId}";
        }

        // BUFF 预览文本（实际 BUFF 在 BattleManager 中通过运行时修正生效）
        TerrainWeatherBuff buff = TribeBattleBuffProvider.GetBuff(tribe.tribeType, _currentTerrain, _currentWeather);
        string buffTag = buff.IsNeutral ? "" : $" [{buff.GetDescription()}]";

        string displayName = $"{unitName}{buffTag}";

        definition = new BattleFighterSpawnDefinition(
            displayName,
            staticAttributes,
            GetTribeAvatarDefinition(tribe.tribeType, unitFighterId),
            scaleMultiplier,
            tribe.tribeType,
            unitFighterId);
        definition.AuraBuffs = unit.ActiveBuffs;

        Debug.Log($"[BattlePanel] Unit {tribe.tribeType} index={unitIndex} fighterId={unitFighterId} auraBuffs count={unit.ActiveBuffs?.Count ?? 0}");
        if (unit.ActiveBuffs != null)
        {
            foreach (var b in unit.ActiveBuffs)
                Debug.Log($"  [BattlePanel] Unit buff: id={b.buffId}, stat={b.statType}, isPercent={b.isPercent}, value={b.value}, persistence={b.persistence}");
        }

        return true;
    }

    /// <summary>
    /// 从 tribe_config.json 获取指定 tier 的 fighterId
    /// </summary>
    private int GetFighterId(TribeType tribeType, UnitTier tier)
    {
        // 优先从 tribe_config.json 的 unitTypes 配置中读取
        var tribeConfig = TribeConfigLoader.Instance?.GetTribeConfig(tribeType);
        if (tribeConfig != null)
        {
            var unitType = tribeConfig.GetUnitType(tier);
            if (unitType != null && unitType.fighterId > 0)
                return unitType.fighterId;
        }

        // 回退：按公式计算
        int tribeInt = (int)tribeType;
        int expectedId = tribeInt * 1000 + (int)tier;
        var config = TribeConfigLoader.Instance?.GetFighterConfig(expectedId);
        if (config != null) return expectedId;

        return 0;
    }

    private AvatarAnimationDefinition ResolveAvatarDefinition(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return _playerAvatarDefinition;
        }

        if (_playerAvatarDefinitionsByAddress.TryGetValue(address, out AvatarAnimationDefinition cachedDefinition))
        {
            return cachedDefinition != null ? cachedDefinition : _playerAvatarDefinition;
        }

        AvatarAnimationDefinition loadedDefinition = GameManager.Instance.ResourceManager.LoadResource<AvatarAnimationDefinition>(address);
        if (loadedDefinition == null)
        {
            Debug.LogWarning($"[BattlePanel] Failed to load avatar definition: {address}");
            return _playerAvatarDefinition;
        }

        _playerAvatarDefinitionsByAddress[address] = loadedDefinition;
        return loadedDefinition;
    }

    private string GetQualityName(CatQuality quality)
    {
        switch (quality)
        {
            case CatQuality.White: return "白";
            case CatQuality.Blue: return "蓝";
            case CatQuality.Purple: return "紫";
            case CatQuality.Gold: return "金";
            default: return quality.ToString();
        }
    }

    private static string GetDifficultyName(DifficultyLevel diff)
    {
        switch (diff)
        {
            case DifficultyLevel.Normal: return "普通";
            case DifficultyLevel.Hard: return "困难";
            case DifficultyLevel.Bloodbath: return "血战";
            default: return diff.ToString();
        }
    }

    private void CreateBattleHUD(List<TribeRecord> deployedTribes)
    {
        CleanupBattleHUD();

        BattleManager bm = _flowController?.BattleManager;
        if (bm == null) return;

        BattleFighter[] playerFighters = bm.PlayerFighters;
        BattleFighter[] enemyFighters = bm.EnemyFighters;
        if (playerFighters == null || enemyFighters == null) return;

        GameObject hudGo = new GameObject("BattleHUD", typeof(RectTransform));
        hudGo.transform.SetParent(transform, false);

        RectTransform hudRect = hudGo.GetComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        _battleHUD = hudGo.AddComponent<BattleHUDPanel>();
        _battleHUD.Initialize(playerFighters, enemyFighters, deployedTribes);
    }

    private void CleanupBattleHUD()
    {
        if (_battleHUD != null)
        {
            _battleHUD.Cleanup();
            _battleHUD = null;
        }
    }

    private void CreateConsumableBar()
    {
        CleanupConsumableButtons();

        DataManager dataManager = GameManager.Instance?.DataManager;
        if (dataManager == null) return;

        var allConsumables = dataManager.GetConsumables();
        if (allConsumables == null || allConsumables.Count == 0) return;

        // Copy items so we can modify the list during battle
        _consumableItems = new List<ConsumableItem>(allConsumables);

        // Root container at bottom-center
        GameObject barGo = new GameObject("ConsumableBar", typeof(RectTransform));
        barGo.transform.SetParent(transform, false);

        _consumableBarRoot = barGo.GetComponent<RectTransform>();
        _consumableBarRoot.anchorMin = new Vector2(0.5f, 0f);
        _consumableBarRoot.anchorMax = new Vector2(0.5f, 0f);
        _consumableBarRoot.pivot = new Vector2(0.5f, 0f);
        _consumableBarRoot.anchoredPosition = new Vector2(0f, 12f);

        HorizontalLayoutGroup layout = barGo.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        for (int i = 0; i < _consumableItems.Count; i++)
        {
            ConsumableItem item = _consumableItems[i];
            CreateConsumableButton(item, font);
        }
    }

    private void CreateConsumableButton(ConsumableItem item, Font font)
    {
        GameObject btnGo = new GameObject($"Btn_{item.name}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(_consumableBarRoot, false);

        RectTransform btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(80f, 36f);

        Image bgImg = btnGo.GetComponent<Image>();
        bgImg.color = GetConsumableColor(item.effectType);

        // Label
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(btnGo.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelGo.GetComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 14;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.text = item.name;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;

        Button btn = btnGo.GetComponent<Button>();
        int itemId = item.id;
        btn.onClick.AddListener(() => OnConsumableButtonClicked(itemId));

        _consumableButtonObjects.Add(btnGo);
    }

    private void OnConsumableButtonClicked(int itemId)
    {
        if (_consumableItems == null) return;

        // Find the item
        ConsumableItem item = null;
        int index = -1;
        for (int i = 0; i < _consumableItems.Count; i++)
        {
            if (_consumableItems[i].id == itemId)
            {
                item = _consumableItems[i];
                index = i;
                break;
            }
        }

        if (item == null || index < 0) return;

        // Apply effect
        BattleManager bm = _flowController?.BattleManager;
        if (bm == null || !bm.IsInBattle) return;

        bm.TryUseConsumable(item.effectType);

        // Remove from inventory
        GameManager.Instance?.DataManager?.RemoveConsumable(item.id);

        // Remove from local list
        _consumableItems.RemoveAt(index);

        // Destroy button
        if (index < _consumableButtonObjects.Count)
        {
            GameObject btnObj = _consumableButtonObjects[index];
            _consumableButtonObjects.RemoveAt(index);
            Object.Destroy(btnObj);
        }

        Debug.Log($"[BattlePanel] Used consumable: {item.name}");
    }

    private void CleanupConsumableButtons()
    {
        for (int i = _consumableButtonObjects.Count - 1; i >= 0; i--)
        {
            if (_consumableButtonObjects[i] != null)
                Object.Destroy(_consumableButtonObjects[i]);
        }
        _consumableButtonObjects.Clear();
        _consumableItems = null;

        if (_consumableBarRoot != null)
        {
            Object.Destroy(_consumableBarRoot.gameObject);
            _consumableBarRoot = null;
        }
    }

    private static Color GetConsumableColor(ConsumableEffectType type)
    {
        switch (type)
        {
            case ConsumableEffectType.Bomb: return new Color(0.8f, 0.3f, 0.2f, 0.85f);
            case ConsumableEffectType.FreezeTrap: return new Color(0.3f, 0.5f, 0.9f, 0.85f);
            case ConsumableEffectType.HealPotion: return new Color(0.2f, 0.7f, 0.3f, 0.85f);
            case ConsumableEffectType.AttackBuff: return new Color(0.9f, 0.6f, 0.1f, 0.85f);
            case ConsumableEffectType.DefenseBuff: return new Color(0.4f, 0.4f, 0.8f, 0.85f);
            default: return new Color(0.5f, 0.5f, 0.5f, 0.85f);
        }
    }

    private void OnBattleEnded(bool victory)
    {
        _isPaused = false;
        Time.timeScale = 1f;

        CleanupBattleHUD();
        CleanupConsumableButtons();

        if (_battleInfoText != null)
        {
            _battleInfoText.text = victory ? "Victory!" : "Defeat!";
        }

        // 胜利或失败都推进到下一关
        BattleCampaignRuntime campaign = GameManager.Instance?.BattleCampaignRuntime;
        DataManager dataManager = GameManager.Instance?.DataManager;
        int catFoodReward = campaign?.GetCatFoodRewardForBattle(_currentLevel) ?? 200;
        int defeatedReward = catFoodReward / 2;

        if (victory)
        {
            if (dataManager != null)
            {
                dataManager.AddCatFood(catFoodReward);
                dataManager.SavePlayerData();
            }
            Debug.Log($"[BattlePanel] Victory! 获得 {catFoodReward} 猫粮");
        }
        else
        {
            if (dataManager != null)
            {
                dataManager.AddCatFood(defeatedReward);
                dataManager.SavePlayerData();
            }
            Debug.Log($"[BattlePanel] Defeat! 猫粮减半，获得 {defeatedReward} 猫粮");
        }

        // 无论胜负都推进关卡
        if (campaign != null)
            campaign.AdvanceAfterVictory(_currentLevel);

        // 显示结算面板（在战斗场景中，点击后再返回）
        VictoryPanel victoryPanel = GameManager.Instance.UIManager.ShowPanel<VictoryPanel>("ui/VictoryPanel", UIManager.UILayer.PopUp);
        if (victoryPanel != null)
        {
            if (victory)
            {
                victoryPanel.ShowVictoryRewards(_currentLevel);
            }
            else
            {
                victoryPanel.ShowDefeatResult(_currentLevel, defeatedReward);
            }
        }
    }

    private void OnPauseButtonClicked()
    {
        if (_flowController == null)
        {
            return;
        }

        _isPaused = _flowController.TogglePause();
        if (_isPaused)
        {
            if (_battleInfoText != null)
            {
                _battleInfoText.text = "Paused";
            }
        }
        else
        {
            if (_battleInfoText != null)
            {
                _battleInfoText.text = "Battle Running (Scene Avatar)";
            }
        }
    }

    public override void Close()
    {
        CleanupBattleHUD();
        CleanupConsumableButtons();

        if (_flowController != null)
        {
            _flowController.StopAndDispose(OnBattleEnded);
            _flowController = null;
        }

        base.Close();
    }
}
