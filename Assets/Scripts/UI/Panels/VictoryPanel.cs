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

        if (_rewardText != null)
        {
            _rewardText.text = $"Gold: +{goldReward}\nExp: +{expReward}";
        }

        if (_starRating != null)
        {
            _starRating.fillAmount = 1f;
        }

        StartCoroutine(PlayVictoryAnimation());
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
            if (playerData.currentLevel == levelId)
            {
                playerData.currentLevel = levelId + 1;
            }

            playerData.gold += goldReward;
            GameManager.Instance.DataManager.SavePlayerData();

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