using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 赐福面板 - 作为预制体加载的 MonoBehaviour 组件。
/// 提供横排候选区（适合三选一）、只读与强制选择控制。
/// </summary>
public class BlessingPanel : MonoBehaviour
{
    private const string PanelName = "赐福区";
    private const string HintNormal = "请选择一只猫作为本次祈祷对象。";
    private const string HintForce = "强制选择：请选择一只猫（此操作不可跳过）。";
    private const string HintReadonly = "观察模式：赐福已锁定，不可切换。";

    [Header("UI 组件（预制体绑定）")]
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _hintText;
    [SerializeField] private RectTransform _listRoot;
    [SerializeField] private Button _closeButton;

    private RectTransform _externalRoot;
    private Font _cachedFont;
    private bool _isRuntimeCreated;
    private bool _readonlyMode = false;
    private bool _forceMode = false;

    /// <summary>
    /// 列表容器根节点，用于放置候选卡片项。
    /// </summary>
    public RectTransform ListRoot => _listRoot;

    private void Awake()
    {
        // 确保初始状态为隐藏
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 设置外部根节点（用于强制弹窗场景）。
    /// </summary>
    public void SetExternalRoot(RectTransform externalRoot)
    {
        _externalRoot = externalRoot;
    }

    /// <summary>
    /// 初始化面板（设置默认文本等）。
    /// </summary>
    public void Initialize()
    {
        EnsureUIComponents();
        if (_titleText != null)
        {
            _titleText.text = PanelName;
        }
        UpdateHintText();
    }

    /// <summary>
    /// 初始化面板（兼容旧调用方式，支持运行时创建 UI）。
    /// </summary>
    public void Initialize(RectTransform parent, Font font)
    {
        _cachedFont = font;
        EnsureRuntimeUI(parent, font);
        Initialize();
    }

    private void EnsureUIComponents()
    {
        // 如果预制体已绑定组件，直接返回
        if (_titleText != null && _hintText != null && _listRoot != null && _closeButton != null)
        {
            return;
        }

        // 尝试在当前物体下查找组件
        _titleText = transform.Find("Title")?.GetComponent<Text>();
        _hintText = transform.Find("Hint")?.GetComponent<Text>();
        _listRoot = transform.Find("List") as RectTransform;
        _closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
    }

    private void EnsureRuntimeUI(RectTransform parent, Font font)
    {
        if (_isRuntimeCreated) return;

        // 如果已有 ListRoot，说明是预制体实例化，不需要运行时创建
        if (_listRoot != null) return;

        _isRuntimeCreated = true;

        // 创建根节点
        RectTransform panelRect;
        if (_externalRoot != null)
        {
            panelRect = _externalRoot;
        }
        else
        {
            GameObject panelGo = new GameObject("BlessingPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.3f, 0.25f);
            panelRect.anchorMax = new Vector2(0.7f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.1f, 0.08f, 0.98f);
        }

        // 标题
        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(panelRect, false);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 32f);
        titleRect.anchoredPosition = Vector2.zero;
        _titleText = titleGo.GetComponent<Text>();
        _titleText.font = font;
        _titleText.fontSize = 24;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = Color.white;

        // 提示文本
        GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
        hintGo.transform.SetParent(panelRect, false);
        RectTransform hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0.85f);
        hintRect.anchorMax = new Vector2(1f, 0.92f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;
        _hintText = hintGo.GetComponent<Text>();
        _hintText.font = font;
        _hintText.fontSize = 16;
        _hintText.alignment = TextAnchor.MiddleCenter;
        _hintText.color = new Color(0.94f, 0.9f, 0.72f, 1f);

        // 列表容器（横向布局，适合三选一）
        GameObject listGo = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        listGo.transform.SetParent(panelRect, false);
        RectTransform listRect = listGo.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.05f, 0.12f);
        listRect.anchorMax = new Vector2(0.95f, 0.82f);
        listRect.offsetMin = Vector2.zero;
        listRect.offsetMax = Vector2.zero;
        _listRoot = listRect;
        Image listBg = listGo.GetComponent<Image>();
        listBg.color = new Color(0f, 0f, 0f, 0.2f);

        HorizontalLayoutGroup layout = listGo.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.padding = new RectOffset(10, 10, 10, 10);

        // 关闭按钮
        GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(panelRect, false);
        RectTransform closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.4f, 0.02f);
        closeRect.anchorMax = new Vector2(0.6f, 0.1f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;
        _closeButton = closeGo.GetComponent<Button>();
        Image closeImg = closeGo.GetComponent<Image>();
        closeImg.color = new Color(0.5f, 0.4f, 0.2f, 1f);
        _closeButton.targetGraphic = closeImg;
        CreateButtonLabel(closeRect, font, "关闭");
    }

    private void CreateButtonLabel(RectTransform parent, Font font, string text)
    {
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(parent, false);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelGo.GetComponent<Text>();
        label.font = font;
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = text;
    }

    public void Show(bool readonlyMode = false)
    {
        _readonlyMode = readonlyMode;
        gameObject.SetActive(true);
        UpdateHintText();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 清理列表中的所有卡片项。
    /// </summary>
    public void ClearList()
    {
        if (_listRoot == null) return;
        for (int i = _listRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_listRoot.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 设置强制选择模式（不可跳过）。
    /// </summary>
    public void SetForceMode(bool force)
    {
        _forceMode = force;
        // 强制模式下关闭按钮应被禁用，避免跳过
        if (_closeButton != null)
        {
            _closeButton.interactable = !force;
        }
        UpdateHintText();
    }

    public void SetCloseCallback(UnityEngine.Events.UnityAction callback)
    {
        if (_closeButton == null) return;
        _closeButton.onClick.RemoveAllListeners();
        if (callback != null)
        {
            _closeButton.onClick.AddListener(callback);
        }
    }

    private void UpdateHintText()
    {
        if (_hintText == null) return;
        if (_forceMode)
        {
            _hintText.text = HintForce;
        }
        else if (_readonlyMode)
        {
            _hintText.text = HintReadonly;
        }
        else
        {
            _hintText.text = HintNormal;
        }
    }
}
