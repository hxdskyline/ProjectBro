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

        // 绑定按钮事件
        if (_continueButton != null)
        {
            _continueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        Debug.Log("[VictoryPanel] Initialized");
    }

    /// <summary>
    /// 显示胜利奖励
    /// </summary>
    public void ShowVictoryRewards(int levelId)
    {
        _currentLevel = levelId;

        if (_victoryText != null)
        {
            _victoryText.text = "VICTORY!";
        }

        // 计算奖励
        int goldReward = 100 * levelId;
        int expReward = 50 * levelId;

        if (_rewardText != null)
        {
            _rewardText.text = $"Gold: +{goldReward}\nExp: +{expReward}";
        }

        // 显示星级评价
        if (_starRating != null)
        {
            _starRating.fillAmount = 1f; // 满分3星
        }

        // 播放显示动画
        StartCoroutine(PlayVictoryAnimation());

        // 更新玩家数据
        UpdatePlayerData(levelId, goldReward);

        Debug.Log("[VictoryPanel] Victory rewards shown for level: " + levelId);
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
        PlayerData playerData = GameManager.Instance.DataManager.PlayerData;
        if (playerData != null)
        {
            // 更新等级进度
            if (playerData.currentLevel == levelId)
            {
                playerData.currentLevel = levelId + 1;
            }

            // ���加金币
            playerData.gold += goldReward;

            // 保存数据
            GameManager.Instance.DataManager.SavePlayerData();

            Debug.Log("[VictoryPanel] Player data updated");
        }
    }

    private void OnContinueButtonClicked()
    {
        Debug.Log("[VictoryPanel] Continue button clicked");

        // 关闭胜利界面
        GameManager.Instance.UIManager.ClosePanel("VictoryPanel");

        // 显示卡牌构建界面
        GameManager.Instance.UIManager.ShowPanel<CardBuildPanel>("CardBuildPanel", UIManager.UILayer.Normal);
    }
}