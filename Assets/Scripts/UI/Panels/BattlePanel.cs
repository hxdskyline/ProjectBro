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

        if (_pauseButton != null)
        {
            _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        Debug.Log("[BattlePanel] Initialized");
    }

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

        _battleManager = FindObjectOfType<BattleManager>();
        if (_battleManager == null)
        {
            GameObject battleGo = new GameObject("BattleManager");
            _battleManager = battleGo.AddComponent<BattleManager>();
        }

        _battleManager.Initialize(levelId);
        _battleManager.StartBattle();

        StartCoroutine(SimulateBattle());

        Debug.Log("[BattlePanel] Battle started for level: " + levelId);
    }

    private IEnumerator SimulateBattle()
    {
        float battleDuration = 3f;
        float elapsedTime = 0f;

        while (elapsedTime < battleDuration)
        {
            elapsedTime += Time.deltaTime;

            if (_battleProgress != null)
            {
                _battleProgress.fillAmount = elapsedTime / battleDuration;
            }

            if (_battleInfoText != null)
            {
                _battleInfoText.text = $"Battling... {(elapsedTime / battleDuration * 100):F0}%";
            }

            yield return null;
        }

        OnBattleVictory();
    }

    private void OnBattleVictory()
    {
        Debug.Log("[BattlePanel] Battle Victory!");

        GameManager.Instance.UIManager.HidePanel("ui/BattlePanel");

        VictoryPanel victoryPanel = GameManager.Instance.UIManager.ShowPanel<VictoryPanel>("ui/VictoryPanel", UIManager.UILayer.PopUp);
        if (victoryPanel != null)
        {
            victoryPanel.ShowVictoryRewards(_currentLevel);
        }
    }

    private void OnPauseButtonClicked()
    {
        Debug.Log("[BattlePanel] Pause button clicked");
    }

    public override void Close()
    {
        if (_battleManager != null)
        {
            Destroy(_battleManager.gameObject);
        }

        base.Close();
    }
}