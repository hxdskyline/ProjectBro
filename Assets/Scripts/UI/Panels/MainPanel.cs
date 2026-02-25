using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面 - 游戏启动后的第一个界面
/// </summary>
public class MainPanel : UIPanel
{
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;

    public override void Initialize()
    {
        base.Initialize();

        // 绑定按钮事件
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }

        if (_settingsButton != null)
        {
            _settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(OnQuitButtonClicked);
        }

        Debug.Log("[MainPanel] Initialized");
    }

    private void OnStartButtonClicked()
    {
        Debug.Log("[MainPanel] Start button clicked");

        // 隐藏主界面
        GameManager.Instance.UIManager.HidePanel("MainPanel");

        // 显示卡牌构建界面
        GameManager.Instance.UIManager.ShowPanel<CardBuildPanel>("CardBuildPanel", UIManager.UILayer.Normal);
    }

    private void OnSettingsButtonClicked()
    {
        Debug.Log("[MainPanel] Settings button clicked");
        // TODO: 打开设置面板
    }

    private void OnQuitButtonClicked()
    {
        Debug.Log("[MainPanel] Quit button clicked");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}