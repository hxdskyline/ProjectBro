using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ս������ - ��ʾս�������е�UI
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
    private bool _grantOutingRewardOnBattleEnd;
    private bool _grantAttributeBoostOnBattleEnd;

    public override void Initialize()
    {
        base.Initialize();

        if (_pauseButton != null)
        {
            _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        Debug.Log("[BattlePanel] Initialized");
    }

    public void StartBattle(
        int levelId,
        CardBuildCardData[] deployedCards,
        bool grantOutingRewardOnBattleEnd,
        bool grantAttributeBoostOnBattleEnd)
    {
        _currentLevel = levelId;
        _isPaused = false;
        _grantOutingRewardOnBattleEnd = grantOutingRewardOnBattleEnd;
        _grantAttributeBoostOnBattleEnd = grantAttributeBoostOnBattleEnd;

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
            BuildPlayerSpawnDefinitions(deployedCards),
            OnBattleEnded);

        Debug.Log("[BattlePanel] Battle started for level: " + levelId);
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

    private BattleFighterSpawnDefinition[] BuildPlayerSpawnDefinitions(CardBuildCardData[] deployedCards)
    {
        if (deployedCards == null || deployedCards.Length == 0)
        {
            return null;
        }

        BattleFighterSpawnDefinition[] definitions = new BattleFighterSpawnDefinition[deployedCards.Length];
        for (int i = 0; i < deployedCards.Length; i++)
        {
            CardBuildCardData card = deployedCards[i];
            definitions[i] = new BattleFighterSpawnDefinition(
                card.Name,
                new UnitStaticAttributes
                {
                    MaxHp = Mathf.Max(1, card.Hp),
                    Attack = Mathf.Max(1, card.Attack),
                    Defense = Mathf.Max(0, card.Defense),
                    MoveSpeed = Mathf.Max(0.1f, card.MoveSpeed),
                    AttackRange = Mathf.Max(0.1f, card.AttackRange)
                });
        }

        return definitions;
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

        if (_grantOutingRewardOnBattleEnd || _grantAttributeBoostOnBattleEnd)
        {
            CardBuildPanel cardBuildPanel = GameManager.Instance.UIManager.GetPanel<CardBuildPanel>("ui/CardBuildPanel");
            cardBuildPanel?.ResolveBattleEndZoneRewards(_grantOutingRewardOnBattleEnd, _grantAttributeBoostOnBattleEnd);
        }

        GameManager.Instance.UIManager.HidePanel("ui/BattlePanel");

        VictoryPanel victoryPanel = GameManager.Instance.UIManager.ShowPanel<VictoryPanel>("ui/VictoryPanel", UIManager.UILayer.PopUp);
        if (victoryPanel != null)
        {
            victoryPanel.ShowVictoryRewards(_currentLevel);
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
        if (_flowController != null)
        {
            _flowController.StopAndDispose(OnBattleEnded);
            _flowController = null;
        }

        base.Close();
    }
}