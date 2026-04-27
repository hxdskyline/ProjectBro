using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TribeSystem;
using TribeSystem.UI;

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

    [Header("各品种Avatar定义")]
    [SerializeField] private AvatarAnimationDefinition _lihuaAvatarDef;
    [SerializeField] private AvatarAnimationDefinition _dajuAvatarDef;
    [SerializeField] private AvatarAnimationDefinition _nainiuAvatarDef;
    [SerializeField] private AvatarAnimationDefinition _xianluoAvatarDef;

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

    // 每个族群类型的运行时 AvatarAnimationDefinition 缓存
    private readonly Dictionary<TribeType, AvatarAnimationDefinition> _tribeAvatarCache
        = new Dictionary<TribeType, AvatarAnimationDefinition>();

    // card_build_cards.json 中品种名 → 族群类型映射
    private static readonly Dictionary<string, TribeType> s_nameToTribeType = new Dictionary<string, TribeType>
    {
        { "狸花", TribeType.Tabby   },
        { "大橘", TribeType.Orange  },
        { "奶牛", TribeType.Cow     },
        { "暹罗", TribeType.Siamese },
    };

    // 品种 → card_build_cards.json 中的 avatarDefinitionAddress
    private static Dictionary<TribeType, string> _cardBuildAvatarAddresses;

    private AvatarAnimationDefinition GetTribeAvatarDefinition(TribeType tribeType)
    {
        if (_tribeAvatarCache.TryGetValue(tribeType, out var cached))
            return cached;

        // 优先使用序列化引用的 AvatarAnimDef asset
        AvatarAnimationDefinition def = GetSerializedAvatarDef(tribeType);
        if (def != null)
        {
            _tribeAvatarCache[tribeType] = def;
            Debug.Log($"[BattlePanel] Avatar {tribeType}: 序列化引用 {def.AvatarId}");
            return def;
        }

        // 从 card_build_cards.json 配置的地址加载
        EnsureCardBuildAvatarAddresses();
        if (_cardBuildAvatarAddresses != null && _cardBuildAvatarAddresses.TryGetValue(tribeType, out string address))
        {
            var loaded = GameManager.Instance.ResourceManager.LoadResource<AvatarAnimationDefinition>(address);
            if (loaded != null)
            {
                _tribeAvatarCache[tribeType] = loaded;
                Debug.Log($"[BattlePanel] Avatar {tribeType}: JSON配置 {address} → {loaded.AvatarId}");
                return loaded;
            }
            else
            {
                Debug.LogWarning($"[BattlePanel] Avatar {tribeType}: JSON配置地址加载失败 {address}");
            }
        }

        // Fallback: 运行时创建
        if (!s_tribeBreedNames.TryGetValue(tribeType, out string breed))
        {
            Debug.LogWarning($"[BattlePanel] 未找到族群 {tribeType} 的品种名，使用默认avatar");
            return _playerAvatarDefinition;
        }

        string idleAddr   = $"avatartemp/{breed}1";
        string attackAddr = $"avatartemp/{breed}2";
        var definition = AvatarAnimationDefinition.CreateRuntime(breed, idleAddr, attackAddr);
        _tribeAvatarCache[tribeType] = definition;
        Debug.Log($"[BattlePanel] Avatar {tribeType}: CreateRuntime fallback {idleAddr}/{attackAddr}");
        return definition;
    }

    private AvatarAnimationDefinition GetSerializedAvatarDef(TribeType tribeType)
    {
        switch (tribeType)
        {
            case TribeType.Tabby:   return _lihuaAvatarDef;
            case TribeType.Orange:  return _dajuAvatarDef;
            case TribeType.Cow:     return _nainiuAvatarDef;
            case TribeType.Siamese: return _xianluoAvatarDef;
            default: return null;
        }
    }

    private static void EnsureCardBuildAvatarAddresses()
    {
        if (_cardBuildAvatarAddresses != null) return;
        _cardBuildAvatarAddresses = new Dictionary<TribeType, string>();

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "card_build_cards.json");
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[BattlePanel] card_build_cards.json 不存在: {path}");
            return;
        }

        try
        {
            string json = System.IO.File.ReadAllText(path);
            var jsonData = LitJson.JsonMapper.ToObject(json);
            if (jsonData == null || !jsonData.IsArray) return;

            foreach (LitJson.JsonData card in jsonData)
            {
                string name = card.ContainsKey("name") ? card["name"].ToString() : null;
                string address = card.ContainsKey("avatarDefinitionAddress") ? card["avatarDefinitionAddress"].ToString() : null;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(address)) continue;

                // 取品种名（"-" 前的部分）
                string breedName = name.Contains("-") ? name.Split('-')[0] : name;
                if (s_nameToTribeType.TryGetValue(breedName, out TribeType tribeType))
                {
                    _cardBuildAvatarAddresses[tribeType] = address;
                    Debug.Log($"[BattlePanel] card_build: {breedName} → {tribeType} → {address}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BattlePanel] 读取 card_build_cards.json 失败: {e.Message}");
        }
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
    /// 每个族群生成：1个族长 + N个小猫
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
            // 添加族长（1个）
            if (CreateLeaderSpawnDefinition(tribe, out BattleFighterSpawnDefinition leaderDef))
            {
                definitions.Add(leaderDef);
            }

            // 添加小猫（N个）
            int catCount = tribe.cats?.Count ?? 0;
            Debug.Log($"[BattlePanel] {tribe.tribeType} 族群小猫数量: {catCount}");
            if (tribe.cats != null && tribe.cats.Count > 0)
            {
                foreach (CatData cat in tribe.cats)
                {
                    if (CreateCatSpawnDefinition(tribe, cat, out BattleFighterSpawnDefinition catDef))
                    {
                        definitions.Add(catDef);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[BattlePanel] {tribe.tribeType} 族群没有小猫！请检查存档是否为旧版本。如果是，请删除存档重新开始。");
            }
        }

        return definitions.Count > 0 ? definitions.ToArray() : null;
    }

    /// <summary>
    /// 创建族长战斗单位定义（基础属性，BUFF 通过运行时修正体系生效）
    /// </summary>
    private bool CreateLeaderSpawnDefinition(TribeRecord tribe, out BattleFighterSpawnDefinition definition)
    {
        definition = default;

        if (tribe.leader == null)
            return false;

        // 计算族长最终属性（含永久buff、临时buff、心情加成）
        LeaderStats leaderStats = TribeStatsCalculator.CalculateLeaderStats(tribe.leader, tribe.moodId);
        float baseSpeed = TribeStatsCalculator.CalculateMovementSpeed(leaderStats.speed);

        // 狸花射程4倍
        float attackRange = tribe.tribeType == TribeType.Tabby ? 6.0f : 1.5f;

        UnitStaticAttributes staticAttributes = new UnitStaticAttributes
        {
            MaxHp = Mathf.Max(1, Mathf.RoundToInt(leaderStats.hp)),
            Attack = Mathf.Max(1, Mathf.RoundToInt(leaderStats.attack)),
            Defense = Mathf.Max(0, Mathf.RoundToInt(leaderStats.defense)),
            MoveSpeed = Mathf.Max(0.1f, baseSpeed),
            AttackRange = Mathf.Max(0.1f, attackRange)
        };

        // BUFF 预览文本（实际 BUFF 在 BattleManager 中通过运行时修正生效）
        TerrainWeatherBuff buff = TribeBattleBuffProvider.GetBuff(tribe.tribeType, _currentTerrain, _currentWeather);
        string buffTag = buff.IsNeutral ? "" : $" [{buff.GetDescription()}]";
        string leaderName = $"[族长] {GetTribeTypeName(tribe.tribeType)}{buffTag}";

        // 传递天生特殊 buff
        var innateBuffs = tribe.leader?.permanentBuffs?.specialBuffs;

        definition = new BattleFighterSpawnDefinition(
            leaderName,
            staticAttributes,
            GetTribeAvatarDefinition(tribe.tribeType),
            1.0f,
            tribe.tribeType,
            innateBuffs);

        return true;
    }

    /// <summary>
    /// 创建小猫战斗单位定义（基础属性，BUFF 通过运行时修正体系生效）
    /// </summary>
    private bool CreateCatSpawnDefinition(TribeRecord tribe, CatData cat, out BattleFighterSpawnDefinition definition)
    {
        definition = default;

        if (tribe.leader == null)
            return false;

        var tribeConfig = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
        if (tribeConfig?.catBaseStats == null)
        {
            Debug.LogError($"[BattlePanel] 无法获取 {tribe.tribeType} 的小猫基础属性配置");
            return false;
        }

        LeaderStats catBaseStatsAsLeader = new LeaderStats(
            tribeConfig.catBaseStats.attack,
            tribeConfig.catBaseStats.defense,
            tribeConfig.catBaseStats.hp,
            tribeConfig.catBaseStats.speed,
            0
        );

        CatStats catStats = TribeStatsCalculator.CalculateCatStats(cat, catBaseStatsAsLeader, tribe.leader?.permanentBuffs);

        int catCount = tribe.cats?.Count ?? 0;
        int command = tribe.leader.command;
        int penalizedSpeed = TribeStatsCalculator.ApplyCommandPenaltyToSpeed(catStats.speed, catCount, command);
        float baseSpeed = TribeStatsCalculator.CalculateMovementSpeed(penalizedSpeed);

        // 狸花小猫射程4倍
        float catAttackRange = tribe.tribeType == TribeType.Tabby ? 4.0f : 1.0f;

        UnitStaticAttributes staticAttributes = new UnitStaticAttributes
        {
            MaxHp = Mathf.Max(1, Mathf.RoundToInt(catStats.hp)),
            Attack = Mathf.Max(1, Mathf.RoundToInt(catStats.attack)),
            Defense = Mathf.Max(0, Mathf.RoundToInt(catStats.defense)),
            MoveSpeed = Mathf.Max(0.1f, baseSpeed),
            AttackRange = Mathf.Max(0.1f, catAttackRange)
        };

        string catName = $"[{GetQualityName(cat.quality)}] {GetTribeTypeName(tribe.tribeType)}";

        definition = new BattleFighterSpawnDefinition(
            catName,
            staticAttributes,
            GetTribeAvatarDefinition(tribe.tribeType),
            0.65f,
            tribe.tribeType);

        return true;
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

    private string GetTribeTypeName(TribeType type)
    {
        switch (type)
        {
            case TribeType.Tabby: return "狸花";
            case TribeType.Orange: return "大橘";
            case TribeType.Cow: return "奶牛";
            case TribeType.Siamese: return "暹罗";
            default: return type.ToString();
        }
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
