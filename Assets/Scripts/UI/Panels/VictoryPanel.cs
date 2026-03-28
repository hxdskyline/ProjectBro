using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TribeSystem.UI;

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

        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        int catFoodReward = battleCampaignRuntime?.GetCatFoodRewardForBattle(levelId) ?? 200;
        string battleProgressText = BuildBattleProgressText(levelId, battleCampaignRuntime);

        if (_rewardText != null)
        {
            _rewardText.text = $"猫粮: +{catFoodReward}\n{battleProgressText}";
        }

        if (_starRating != null)
        {
            _starRating.fillAmount = 1f;
        }

        StartCoroutine(PlayVictoryAnimation());
        UpdatePlayerData(levelId, catFoodReward);

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

    private void UpdatePlayerData(int levelId, int catFoodReward)
    {
        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        if (battleCampaignRuntime != null)
        {
            battleCampaignRuntime.AdvanceAfterVictory(levelId);
        }

        DataManager dataManager = GameManager.Instance.DataManager;
        if (dataManager != null)
        {
            dataManager.AddCatFood(catFoodReward);
            Debug.Log($"[VictoryPanel] Added {catFoodReward} cat food for battle {levelId}");
        }
    }

    private void OnContinueButtonClicked()
    {
        Debug.Log("[VictoryPanel] Continue button clicked");

        GameManager.Instance.UIManager.ClosePanel("ui/VictoryPanel");

        // TribeBuildPanel.OnBattleEnded 已经调用了 AdvanceRound 和 SetActive(true)
        // 直接激活已有实例，不重新创建（重新创建会触发 Start→LoadPlayerData，重置回合）
        TribeBuildPanel tribeBuildPanel = GameManager.Instance.UIManager.GetPanel<TribeBuildPanel>("ui/tribebuild/tribebuildpanel");
        if (tribeBuildPanel != null)
        {
            tribeBuildPanel.gameObject.SetActive(true);
        }
    }
}