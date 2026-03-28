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

    private BattleFlowController _flowController;
    private int _currentLevel;
    private bool _isPaused;

    private readonly Dictionary<string, AvatarAnimationDefinition> _playerAvatarDefinitionsByAddress
        = new Dictionary<string, AvatarAnimationDefinition>();

    // 族群类型 → 品种名前缀（对应 avatartemp/{breed}1 和 {breed}2）
    private static readonly Dictionary<TribeType, string> s_tribeBreedNames = new Dictionary<TribeType, string>
    {
        { TribeType.Maine,   "mianyin" },
        { TribeType.Tabby,   "lihua"   },
        { TribeType.Orange,  "daju"    },
        { TribeType.Cow,     "nainiu"  },
        { TribeType.Siamese, "xianluo" },
        { TribeType.Ragdoll, "buou"    },
    };

    // 每个族群类型的运行时 AvatarAnimationDefinition 缓存
    private readonly Dictionary<TribeType, AvatarAnimationDefinition> _tribeAvatarCache
        = new Dictionary<TribeType, AvatarAnimationDefinition>();

    private AvatarAnimationDefinition GetTribeAvatarDefinition(TribeType tribeType)
    {
        if (_tribeAvatarCache.TryGetValue(tribeType, out var cached))
            return cached;

        if (!s_tribeBreedNames.TryGetValue(tribeType, out string breed))
        {
            Debug.LogWarning($"[BattlePanel] 未找到族群 {tribeType} 的品种名，使用默认avatar");
            return _playerAvatarDefinition;
        }

        string idleAddr   = $"avatartemp/{breed}1";
        string attackAddr = $"avatartemp/{breed}2";
        var definition = AvatarAnimationDefinition.CreateRuntime(breed, idleAddr, attackAddr);
        _tribeAvatarCache[tribeType] = definition;
        return definition;
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
    /// 开始战斗 - 使用新族群系统
    /// </summary>
    public void StartBattle(int levelId, List<TribeRecord> deployedTribes)
    {
        _currentLevel = levelId;
        _isPaused = false;

        if (_flowController == null)
        {
            _flowController = new BattleFlowController();
        }

        if (_levelText != null)
        {
            _levelText.text = $"Level: {levelId}";
        }

        if (_battleInfoText != null)
        {
            _battleInfoText.text = "Battle Running (Scene Avatar)";
        }

        _flowController.StartBattle(
            levelId,
            _fighterPrefab,
            _playerAvatarDefinition,
            _enemyAvatarDefinition,
            ResolveEnemyCount(levelId),
            BuildPlayerSpawnDefinitions(deployedTribes),
            OnBattleEnded);

        Debug.Log($"[BattlePanel] Battle started for level: {levelId}, tribes: {deployedTribes?.Count ?? 0}");
    }

    private int ResolveEnemyCount(int levelId)
    {
        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        if (battleCampaignRuntime == null)
        {
            return 1;
        }

        return battleCampaignRuntime.GetEnemyCountForBattle(levelId);
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
            // 检查族长是否在休息
            if (tribe.leader != null && tribe.leader.restTurns > 0)
            {
                Debug.LogWarning($"[BattlePanel] {tribe.tribeType}族长正在休息，无法上阵");
                continue;
            }

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
    /// 创建族长战斗单位定义
    /// </summary>
    private bool CreateLeaderSpawnDefinition(TribeRecord tribe, out BattleFighterSpawnDefinition definition)
    {
        definition = default;

        if (tribe.leader == null)
            return false;

        // 计算族长最终属性（含永久buff、临时buff、心情加成）
        LeaderStats leaderStats = TribeStatsCalculator.CalculateLeaderStats(tribe.leader);

        UnitStaticAttributes staticAttributes = new UnitStaticAttributes
        {
            MaxHp = Mathf.Max(1, leaderStats.hp),
            Attack = Mathf.Max(1, leaderStats.attack),
            Defense = Mathf.Max(0, leaderStats.defense),
            MoveSpeed = TribeStatsCalculator.CalculateMovementSpeed(leaderStats.speed),
            AttackRange = Mathf.Max(0.1f, 1.5f) // 族长攻击范围略大
        };

        // 族长名称格式："[族长] 族群名称"
        string leaderName = $"[族长] {GetTribeTypeName(tribe.tribeType)}";

        definition = new BattleFighterSpawnDefinition(
            leaderName,
            staticAttributes,
            GetTribeAvatarDefinition(tribe.tribeType));

        return true;
    }

    /// <summary>
    /// 创建小猫战斗单位定义
    /// </summary>
    private bool CreateCatSpawnDefinition(TribeRecord tribe, CatData cat, out BattleFighterSpawnDefinition definition)
    {
        definition = default;

        if (tribe.leader == null)
            return false;

        // 获取小猫的基础属性配置
        var tribeConfig = TribeConfigLoader.Instance.GetTribeConfig(tribe.tribeType);
        if (tribeConfig?.catBaseStats == null)
        {
            Debug.LogError($"[BattlePanel] 无法获取 {tribe.tribeType} 的小猫基础属性配置");
            return false;
        }

        // 转换catBaseStats为LeaderStats格式（供TribeStatsCalculator使用）
        // 注：小猫的command设为0（小猫不使用统帅力）
        LeaderStats catBaseStatsAsLeader = new LeaderStats(
            tribeConfig.catBaseStats.attack,
            tribeConfig.catBaseStats.defense,
            tribeConfig.catBaseStats.hp,
            tribeConfig.catBaseStats.speed,
            0  // 小猫的command为0
        );

        // 计算小猫属性（基于小猫基础属性和品质比例）
        CatStats catStats = TribeStatsCalculator.CalculateCatStats(cat, catBaseStatsAsLeader);

        // 应用统帅惩罚（小猫数量影响速度）
        int catCount = tribe.cats?.Count ?? 0;
        int command = tribe.leader.command;
        int finalSpeed = TribeStatsCalculator.ApplyCommandPenaltyToSpeed(catStats.speed, catCount, command);

        UnitStaticAttributes staticAttributes = new UnitStaticAttributes
        {
            MaxHp = Mathf.Max(1, catStats.hp),
            Attack = Mathf.Max(1, catStats.attack),
            Defense = Mathf.Max(0, catStats.defense),
            MoveSpeed = TribeStatsCalculator.CalculateMovementSpeed(finalSpeed),
            AttackRange = Mathf.Max(0.1f, 1.0f) // 小猫攻击范围正常
        };

        // 小猫名称格式："[品质] 族群名称"
        string catName = $"[{GetQualityName(cat.quality)}] {GetTribeTypeName(tribe.tribeType)}";

        definition = new BattleFighterSpawnDefinition(
            catName,
            staticAttributes,
            GetTribeAvatarDefinition(tribe.tribeType),
            0.65f);

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
            case TribeType.Maine: return "缅因";
            case TribeType.Tabby: return "狸花";
            case TribeType.Orange: return "大橘";
            case TribeType.Cow: return "奶牛";
            case TribeType.Siamese: return "暹罗";
            case TribeType.Ragdoll: return "布偶";
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

    private void OnBattleEnded(bool victory)
    {
        _isPaused = false;
        Time.timeScale = 1f;

        if (victory)
        {
            Debug.Log("[BattlePanel] Battle Victory!");
        }
        else
        {
            Debug.Log("[BattlePanel] Battle Defeat!");
        }

        if (_battleInfoText != null)
        {
            _battleInfoText.text = victory ? "Victory!" : "Defeat!";
        }

        // 处理战斗结束后的恢复逻辑
        ProcessPostBattleRecovery(victory);

        GameManager.Instance.UIManager.HidePanel("ui/BattlePanel");

        // 回调TribeBuildPanel处理战斗结束（地址须与 ShowPanel 时完全一致）
        TribeBuildPanel tribeBuildPanel = GameManager.Instance.UIManager.GetPanel<TribeBuildPanel>("ui/tribebuild/tribebuildpanel");
        if (tribeBuildPanel != null)
        {
            tribeBuildPanel.OnBattleEnded(victory);
        }

        VictoryPanel victoryPanel = GameManager.Instance.UIManager.ShowPanel<VictoryPanel>("ui/VictoryPanel", UIManager.UILayer.PopUp);
        if (victoryPanel != null)
        {
            victoryPanel.ShowVictoryRewards(_currentLevel);
        }
    }

    /// <summary>
    /// 处理战斗后的恢复逻辑
    /// 胜利：小猫下回合自动恢复，族长不休息
    /// 失败：小猫下回合自动恢复，族长休息1回合
    /// </summary>
    private void ProcessPostBattleRecovery(bool victory)
    {
        DataManager dataManager = GameManager.Instance?.DataManager;
        if (dataManager == null) return;

        List<TribeRecord> tribes = dataManager.GetTribes();
        if (tribes == null) return;

        foreach (TribeRecord tribe in tribes)
        {
            // 失败时族长需要休息
            if (!victory && tribe.leader != null)
            {
                tribe.leader.restTurns = 1;
                Debug.Log($"[BattlePanel] {tribe.tribeType}族长需要休息1回合");
            }

            // 小猫总是下回合自动恢复（无需操作，仅作为记录）
            // 实际恢复在下一回合开始时处理
        }

        dataManager.SavePlayerData();
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
        if (_flowController != null)
        {
            _flowController.StopAndDispose(OnBattleEnded);
            _flowController = null;
        }

        base.Close();
    }
}
