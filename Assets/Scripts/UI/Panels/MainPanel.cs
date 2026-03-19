using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// ������ - ��Ϸ������ĵ�һ������
/// </summary>
public class MainPanel : UIPanel
{
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;

    public override void Initialize()
    {
        base.Initialize();

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

        // 默认选中状态：Start 为选中，其他为未选中
        SetSelectedButton(_startButton);

        // 注册悬浮（PointerEnter）事件：鼠标悬浮到某按钮则该按钮为选中，其它变为未选中
        if (_startButton != null) RegisterHover(_startButton);
        if (_settingsButton != null) RegisterHover(_settingsButton);
        if (_quitButton != null) RegisterHover(_quitButton);

        Debug.Log("[MainPanel] Initialized");
    }

    private readonly Color32 _selectedTextColor = new Color32(0xFE, 0xF0, 0xD3, 0xFF); // fef0d3
    private readonly Color32 _unselectedTextColor = new Color32(0x80, 0xBD, 0x70, 0xFF); // 80bd70

    private void RegisterHover(Button btn)
    {
        var trigger = btn.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener((baseEventData) => { OnButtonHover(btn); });
        trigger.triggers.Add(entry);
    }

    private void OnButtonHover(Button btn)
    {
        SetSelectedButton(btn);
    }

    private void SetSelectedButton(Button selected)
    {
        var buttons = new[] { _startButton, _settingsButton, _quitButton };
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            bool isSelected = btn == selected;
            // 设置 Image 透明度：选中 100% (1.0)，未选中 0% (0.0)
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = isSelected ? 1f : 0f;
                img.color = c;
            }

            // 设置子 Text (Legacy) 颜色
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.color = isSelected ? (Color)_selectedTextColor : (Color)_unselectedTextColor;
            }
        }
    }

    private void OnStartButtonClicked()
    {
        Debug.Log("[MainPanel] Start button clicked");

        GameManager.Instance.UIManager.HidePanel("ui/MainPanel");
        GameManager.Instance.UIManager.ShowPanel<CardBuildPanel>("ui/CardBuildPanel", UIManager.UILayer.Normal);
    }

    private void OnSettingsButtonClicked()
    {
        Debug.Log("[MainPanel] Settings button clicked");
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