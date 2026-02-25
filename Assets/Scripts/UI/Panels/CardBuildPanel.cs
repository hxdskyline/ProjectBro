using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卡牌构建界面 - 玩家选择卡牌并开始战斗
/// </summary>
public class CardBuildPanel : UIPanel
{
    [SerializeField] private Button _startBattleButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Text _levelText;
    [SerializeField] private Text _cardInfoText;

    private int _currentLevel = 1;

    public override void Initialize()
    {
        base.Initialize();

        if (_startBattleButton != null)
        {
            _startBattleButton.onClick.AddListener(OnStartBattleButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        if (GameManager.Instance.DataManager.PlayerData != null)
        {
            _currentLevel = GameManager.Instance.DataManager.PlayerData.currentLevel;
        }

        UpdateUI();
        Debug.Log("[CardBuildPanel] Initialized");
    }

    private void UpdateUI()
    {
        if (_levelText != null)
        {
            _levelText.text = $"Level: {_currentLevel}";
        }

        if (_cardInfoText != null)
        {
            _cardInfoText.text = "Select your cards...\n(Card selection UI will be added later)";
        }
    }

    private void OnStartBattleButtonClicked()
    {
        Debug.Log("[CardBuildPanel] Start Battle button clicked");

        GameManager.Instance.UIManager.HidePanel("ui/CardBuildPanel");

        BattlePanel battlePanel = GameManager.Instance.UIManager.ShowPanel<BattlePanel>("ui/BattlePanel", UIManager.UILayer.Normal);

        if (battlePanel != null)
        {
            battlePanel.StartBattle(_currentLevel);
        }
    }

    private void OnBackButtonClicked()
    {
        Debug.Log("[CardBuildPanel] Back button clicked");

        GameManager.Instance.UIManager.HidePanel("ui/CardBuildPanel");
        GameManager.Instance.UIManager.ShowPanel<MainPanel>("ui/MainPanel", UIManager.UILayer.Normal);
    }
}