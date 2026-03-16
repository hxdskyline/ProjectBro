using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 胜利界面 - 显示战斗胜利和奖励
/// </summary>
public class VictoryPanel : UIPanel
{
    [SerializeField] private Text _victoryText;
    [SerializeField] private Text _rewardText;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Image _starRating;
    [SerializeField] private CanvasGroup _rewardGroup;

    private int _currentLevel;

    public override void Initialize()
    {
        base.Initialize();

        if (_continueButton != null)
        {
            _continueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        Debug.Log("[VictoryPanel] Initialized");
    }

    public void ShowVictoryRewards(int levelId)
    {
        _currentLevel = levelId;

        if (_victoryText != null)
        {
            _victoryText.text = "VICTORY!";
        }

        int goldReward = 100 * levelId;
        int expReward = 50 * levelId;

        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        string battleProgressText = BuildBattleProgressText(levelId, battleCampaignRuntime);

        if (_rewardText != null)
        {
            _rewardText.text = $"Gold: +{goldReward}\nExp: +{expReward}\n{battleProgressText}";
        }

        if (_starRating != null)
        {
            _starRating.fillAmount = 1f;
        }

        StartCoroutine(PlayVictoryAnimation());
        UpdatePlayerData(levelId, goldReward);

        Debug.Log("[VictoryPanel] Victory rewards shown for level: " + levelId);
    }

    private string BuildBattleProgressText(int levelId, BattleCampaignRuntime battleCampaignRuntime)
    {
        if (battleCampaignRuntime == null)
        {
            return $"Cleared: Battle {levelId}";
        }

        if (levelId >= battleCampaignRuntime.MaxBattleCount)
        {
            return $"Cleared: Battle {levelId}\nNext: Campaign completed for this run";
        }

        int nextBattleNumber = battleCampaignRuntime.GetNextBattleNumber(levelId);
        int nextEnemyCount = battleCampaignRuntime.GetEnemyCountForBattle(nextBattleNumber);
        return $"Cleared: Battle {levelId}\nNext: Battle {nextBattleNumber}, Enemies {nextEnemyCount}";
    }

    private IEnumerator PlayVictoryAnimation()
    {
        if (_rewardGroup != null)
        {
            _rewardGroup.alpha = 0;

            float duration = 0.5f;
            float elapsedTime = 0;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                _rewardGroup.alpha = Mathf.Lerp(0, 1, elapsedTime / duration);
                yield return null;
            }

            _rewardGroup.alpha = 1;
        }
    }

    private void UpdatePlayerData(int levelId, int goldReward)
    {
        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        if (battleCampaignRuntime != null)
        {
            battleCampaignRuntime.AdvanceAfterVictory(levelId);
        }

        CurrencyManager currencyManager = GameManager.Instance.CurrencyManager;
        if (currencyManager != null)
        {
            currencyManager.AddCurrency(CurrencyType.Gold, goldReward);

            Debug.Log("[VictoryPanel] Player data updated");
        }
    }

    private void OnContinueButtonClicked()
    {
        Debug.Log("[VictoryPanel] Continue button clicked");

        GameManager.Instance.UIManager.ClosePanel("ui/VictoryPanel");
        GameManager.Instance.UIManager.ShowPanel<CardBuildPanel>("ui/CardBuildPanel", UIManager.UILayer.Normal);
    }
}