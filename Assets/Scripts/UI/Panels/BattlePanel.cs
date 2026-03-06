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

    public override void Initialize()
    {
        base.Initialize();

        if (_pauseButton != null)
        {
            _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        Debug.Log("[BattlePanel] Initialized");
    }

    public void StartBattle(int levelId)
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
            OnBattleEnded);

        Debug.Log("[BattlePanel] Battle started for level: " + levelId);
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