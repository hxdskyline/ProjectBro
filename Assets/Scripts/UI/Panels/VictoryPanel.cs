using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TribeSystem.UI;
using BattleSystem;

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
    private bool _lastVictory;

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
        _lastVictory = true;

        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        bool isCampaignComplete = battleCampaignRuntime != null && levelId >= battleCampaignRuntime.MaxBattleCount;

        if (_victoryText != null)
        {
            _victoryText.text = isCampaignComplete ? "通关！" : "VICTORY!";
        }

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

        Debug.Log("[VictoryPanel] Victory rewards shown for level: " + levelId);
    }

    public void ShowDefeatResult(int levelId, int halvedReward)
    {
        _currentLevel = levelId;
        _lastVictory = false;

        if (_victoryText != null)
        {
            _victoryText.text = "战败！";
        }

        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        int fullReward = battleCampaignRuntime?.GetCatFoodRewardForBattle(levelId) ?? 200;

        if (_rewardText != null)
        {
            _rewardText.text = $"战斗失败! 猫粮减半\n原本: +{fullReward}\n实际: +{halvedReward}";
        }

        if (_starRating != null)
        {
            _starRating.fillAmount = 0f;
        }

        StartCoroutine(PlayVictoryAnimation());

        Debug.Log("[VictoryPanel] Defeat result shown for level: " + levelId);
    }

    private string BuildBattleProgressText(int levelId, BattleCampaignRuntime battleCampaignRuntime)
    {
        if (battleCampaignRuntime == null)
        {
            return $"已通过: 第{levelId}关";
        }

        if (levelId >= battleCampaignRuntime.MaxBattleCount)
        {
            return $"已通过: 第{levelId}关\n恭喜通关！猫村重获和平！";
        }

        int nextBattleNumber = battleCampaignRuntime.GetNextBattleNumber(levelId);
        int nextEnemyCount = battleCampaignRuntime.GetEnemyCountForBattle(nextBattleNumber);
        return $"已通过: 第{levelId}关\n下一关: 第{nextBattleNumber}关, 敌人{nextEnemyCount}只";
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

    private void OnContinueButtonClicked()
    {
        Debug.Log("[VictoryPanel] Continue button clicked");

        // 隐藏战斗面板和结算面板
        GameManager.Instance.UIManager.ClosePanel("ui/VictoryPanel");
        GameManager.Instance.UIManager.HidePanel("ui/BattlePanel");

        // 检查是否通关了最后一关
        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;
        bool isCampaignComplete = campaign != null && _currentLevel >= campaign.MaxBattleCount;

        if (isCampaignComplete)
        {
            Debug.Log("[VictoryPanel] 通关！清除游戏数据，返回主界面");

            // 清除存档数据
            var dataManager = GameManager.Instance.DataManager;
            if (dataManager != null)
            {
                dataManager.DeleteSaveData();
            }

            // 重置 GameFlowController 状态
            var gfc = GameFlowController.Instance;
            if (gfc != null)
            {
                gfc.ReturnToMainMenu();
            }
        }
        else
        {
            // 回调TribeBuildPanel处理战斗结束
            // 直接激活已有实例，不重新创建（重新创建会触发 Start→LoadPlayerData，重置回合）
            TribeBuildPanel tribeBuildPanel = GameManager.Instance.UIManager.GetPanel<TribeBuildPanel>("ui/tribebuild/tribebuildpanel");
            if (tribeBuildPanel != null)
            {
                tribeBuildPanel.gameObject.SetActive(true);
                tribeBuildPanel.OnBattleEnded(_lastVictory);
            }
        }
    }
}