using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 弃猫面板 - 作为预制体加载的 MonoBehaviour 组件。
/// 用于确认永久删除猫。
/// </summary>
public class DiscardPanel : MonoBehaviour
{
    private const string PanelName = "弃猫区";
    private const string DefaultHint = "拖入弃猫区后会暂存；确认后这些猫将被永久删除。未确认前可拖回待选区。";

    [Header("UI 组件（预制体绑定）")]
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _hintText;
    [SerializeField] private RectTransform _listRoot;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _closeButton;

    private RectTransform _externalRoot;
    private Font _cachedFont;
    private bool _isRuntimeCreated;

    /// <summary>
    /// 列表容器根节点，用于放置卡片项。
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
        if (_hintText != null)
        {
            _hintText.text = DefaultHint;
        }
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
        if (_titleText != null && _hintText != null && _listRoot != null &&
            _confirmButton != null && _closeButton != null)
        {
            return;
        }

        // 尝试在当前物体下查找组件
        _titleText = transform.Find("Title")?.GetComponent<Text>();
        _hintText = transform.Find("Hint")?.GetComponent<Text>();
        _listRoot = transform.Find("List") as RectTransform;
        _confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();
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
            GameObject panelGo = new GameObject("DiscardPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.3f, 0.25f);
            panelRect.anchorMax = new Vector2(0.7f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.18f, 0.08f, 0.08f, 0.98f);
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
        _hintText.color = new Color(0.94f, 0.72f, 0.72f, 1f);

        // 列表容器
        GameObject listGo = new GameObject("List", typeof(RectTransform), typeof(Image));
        listGo.transform.SetParent(panelRect, false);
        RectTransform listRect = listGo.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.05f, 0.12f);
        listRect.anchorMax = new Vector2(0.95f, 0.82f);
        listRect.offsetMin = Vector2.zero;
        listRect.offsetMax = Vector2.zero;
        _listRoot = listRect;
        Image listBg = listGo.GetComponent<Image>();
        listBg.color = new Color(0f, 0f, 0f, 0.2f);

        // 确认按钮（红色，表示危险操作）
        GameObject confirmGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
        confirmGo.transform.SetParent(panelRect, false);
        RectTransform confirmRect = confirmGo.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.6f, 0.02f);
        confirmRect.anchorMax = new Vector2(0.85f, 0.1f);
        confirmRect.offsetMin = Vector2.zero;
        confirmRect.offsetMax = Vector2.zero;
        _confirmButton = confirmGo.GetComponent<Button>();
        Image confirmImg = confirmGo.GetComponent<Image>();
        confirmImg.color = new Color(0.7f, 0.2f, 0.2f, 1f);
        _confirmButton.targetGraphic = confirmImg;
        CreateButtonLabel(confirmRect, font, "确认删除");

        // 关闭按钮
        GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(panelRect, false);
        RectTransform closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.15f, 0.02f);
        closeRect.anchorMax = new Vector2(0.4f, 0.1f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;
        _closeButton = closeGo.GetComponent<Button>();
        Image closeImg = closeGo.GetComponent<Image>();
        closeImg.color = new Color(0.35f, 0.35f, 0.35f, 1f);
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

    public void Show()
    {
        gameObject.SetActive(true);
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

    public void SetConfirmCallback(UnityEngine.Events.UnityAction callback)
    {
        if (_confirmButton == null) return;
        _confirmButton.onClick.RemoveAllListeners();
        if (callback != null)
        {
            _confirmButton.onClick.AddListener(callback);
        }
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
}
