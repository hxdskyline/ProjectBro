using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 战斗界面 - 显示战斗进行中的UI
/// </summary>
public class BattlePanel : UIPanel
{
    [SerializeField] private Text _battleInfoText;
    [SerializeField] private Text _levelText;
    [SerializeField] private Image _battleProgress;
    [SerializeField] private Button _pauseButton;

    private BattleManager _battleManager;
    private int _currentLevel;

    public override void Initialize()
    {
        base.Initialize();

        // 绑定按钮事件
        if (_pauseButton != null)
        {
            _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        Debug.Log("[BattlePanel] Initialized");
    }

    /// <summary>
    /// 开始战斗
    /// </summary>
    public void StartBattle(int levelId)
    {
        _currentLevel = levelId;

        if (_levelText != null)
        {
            _levelText.text = $"Level: {levelId}";
        }

        if (_battleInfoText != null)
        {
            _battleInfoText.text = "Battle Start!";
        }

        // 获取或创建战斗管理器
        _battleManager = FindObjectOfType<BattleManager>();
        if (_battleManager == null)
        {
            GameObject battleGo = new GameObject("BattleManager");
            _battleManager = battleGo.AddComponent<BattleManager>();
        }

        // 初始化并开始战斗
        _battleManager.Initialize(levelId);
        _battleManager.StartBattle();

        // 模拟战斗过程
        StartCoroutine(SimulateBattle());

        Debug.Log("[BattlePanel] Battle started for level: " + levelId);
    }

    /// <summary>
    /// 模拟战斗过程（先不写实际战斗逻辑，直接胜利）
    /// </summary>
    private IEnumerator SimulateBattle()
    {
        float battleDuration = 3f; // 模拟3秒的战斗
        float elapsedTime = 0f;

        while (elapsedTime < battleDuration)
        {
            elapsedTime += Time.deltaTime;

            // 更新进度条
            if (_battleProgress != null)
            {
                _battleProgress.fillAmount = elapsedTime / battleDuration;
            }

            // 更新战斗信息
            if (_battleInfoText != null)
            {
                _battleInfoText.text = $"Battling... {(elapsedTime / battleDuration * 100):F0}%";
            }

            yield return null;
        }

        // 战斗结束，显示胜利界面
        OnBattleVictory();
    }

    /// <summary>
    /// 战斗胜利
    /// </summary>
    private void OnBattleVictory()
    {
        Debug.Log("[BattlePanel] Battle Victory!");

        // 隐藏战斗界面
        GameManager.Instance.UIManager.HidePanel("BattlePanel");

        // 显示胜利界面
        VictoryPanel victoryPanel = GameManager.Instance.UIManager.ShowPanel<VictoryPanel>("VictoryPanel", UIManager.UILayer.PopUp);
        if (victoryPanel != null)
        {
            victoryPanel.ShowVictoryRewards(_currentLevel);
        }
    }

    private void OnPauseButtonClicked()
    {
        Debug.Log("[BattlePanel] Pause button clicked");
        // TODO: 实现暂停功能
    }

    public override void Close()
    {
        // 清理战斗管理器
        if (_battleManager != null)
        {
            Destroy(_battleManager.gameObject);
        }

        base.Close();
    }
}