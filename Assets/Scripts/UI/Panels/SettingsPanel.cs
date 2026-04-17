using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板 - 音量控制等
/// </summary>
public class SettingsPanel : UIPanel
{
    [Header("关闭按钮")]
    [SerializeField] private Button _closeButton;

    [Header("音量开关")]
    [SerializeField] private Toggle _masterVolumeToggle;
    [SerializeField] private Toggle _sfxVolumeToggle;
    [SerializeField] private Toggle _bgmVolumeToggle;

    [Header("底部按钮")]
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    // 保存进入时的状态，用于取消时恢复
    private bool _origMaster;
    private bool _origSfx;
    private bool _origBgm;

    public override void Initialize()
    {
        base.Initialize();

        if (_closeButton != null)
            _closeButton.onClick.AddListener(Close);

        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirm);

        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(OnCancel);

        // 记录初始状态
        _origMaster = _masterVolumeToggle != null && _masterVolumeToggle.isOn;
        _origSfx = _sfxVolumeToggle != null && _sfxVolumeToggle.isOn;
        _origBgm = _bgmVolumeToggle != null && _bgmVolumeToggle.isOn;

        Debug.Log("[SettingsPanel] Initialized");
    }

    private void OnConfirm()
    {
        // 保存设置
        bool master = _masterVolumeToggle != null && _masterVolumeToggle.isOn;
        bool sfx = _sfxVolumeToggle != null && _sfxVolumeToggle.isOn;
        bool bgm = _bgmVolumeToggle != null && _bgmVolumeToggle.isOn;

        PlayerPrefs.SetInt("MasterVolume", master ? 1 : 0);
        PlayerPrefs.SetInt("SfxVolume", sfx ? 1 : 0);
        PlayerPrefs.SetInt("BgmVolume", bgm ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"[SettingsPanel] Settings saved: Master={master}, SFX={sfx}, BGM={bgm}");

        Close();
    }

    private void OnCancel()
    {
        // 恢复进入时的状态
        if (_masterVolumeToggle != null) _masterVolumeToggle.isOn = _origMaster;
        if (_sfxVolumeToggle != null) _sfxVolumeToggle.isOn = _origSfx;
        if (_bgmVolumeToggle != null) _bgmVolumeToggle.isOn = _origBgm;

        Close();
    }

    private void Close()
    {
        // 关闭自己
        GameManager.Instance.UIManager.ClosePanel("ui/settingspanel");
    }
}
