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

        // 绑定按钮事件
        if (_startBattleButton != null)
        {
            _startBattleButton.onClick.AddListener(OnStartBattleButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        // 从玩家数据获取当前关卡
        if (GameManager.Instance.DataManager.PlayerData != null)
        {
            _currentLevel = GameManager.Instance.DataManager.PlayerData.currentLevel;
        }

        UpdateUI();
        Debug.Log("[CardBuildPanel] Initialized");
    }

    private void UpdateUI()
    {
        // 更新关卡显示
        if (_levelText != null)
        {
            _levelText.text = $"Level: {_currentLevel}";
        }

        // 更新卡牌信息显示
        if (_cardInfoText != null)
        {
            _cardInfoText.text = "Select your cards...\n(Card selection UI will be added later)";
        }
    }

    private void OnStartBattleButtonClicked()
    {
        Debug.Log("[CardBuildPanel] Start Battle button clicked");

        // 隐藏卡牌构建界面
        GameManager.Instance.UIManager.HidePanel("CardBuildPanel");

        // 显示战斗界面
        BattlePanel battlePanel = GameManager.Instance.UIManager.ShowPanel<BattlePanel>("BattlePanel", UIManager.UILayer.Normal);

        // 开始战斗
        if (battlePanel != null)
        {
            battlePanel.StartBattle(_currentLevel);
        }
    }

    private void OnBackButtonClicked()
    {
        Debug.Log("[CardBuildPanel] Back button clicked");

        // 隐藏卡牌构建界面
        GameManager.Instance.UIManager.HidePanel("CardBuildPanel");

        // 显示主界面
        GameManager.Instance.UIManager.ShowPanel<MainPanel>("MainPanel", UIManager.UILayer.Normal);
    }
}