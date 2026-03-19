using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 赐福管理面板：提供横排候选区（适合三选一）、只读与强制选择控制的 UI 根与基本 API。
/// 上层负责向 `ListRoot` 填充卡片项并注册按钮回调。
/// </summary>
public class BlessingPanel
{
    public RectTransform Root { get; private set; }
    public RectTransform ListRoot { get; private set; }
    public Text TitleText { get; private set; }
    public Text HintText { get; private set; }
    public Button CloseButton { get; private set; }

    private Font _uiFont;
    private bool _readonlyMode = false;
    private bool _forceMode = false;
    private RectTransform _ownRoot;
    private RectTransform _externalRoot;

    public void Initialize(RectTransform parent, Font uiFont)
    {
        if (parent == null) return;
        if (Root != null) return;

        _uiFont = uiFont;

        _ownRoot = GetOrCreateChildRect(parent, "BlessingPanelRoot", new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.78f));
        Root = _ownRoot;
        Image popupBg = GetOrAddComponent<Image>(Root.gameObject);
        popupBg.color = new Color(0.07f, 0.1f, 0.14f, 0.98f);

        TitleText = GetOrCreateText(Root, "Title", _uiFont, 26, TextAnchor.UpperCenter, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.97f), Color.white);
        HintText = GetOrCreateText(Root, "Hint", _uiFont, 16, TextAnchor.UpperCenter, new Vector2(0.02f, 0.74f), new Vector2(0.98f, 0.84f), new Color(0.94f, 0.9f, 0.72f, 1f));
        HintText.text = "请选择一只猫作为本次祈祷对象。";

        ListRoot = GetOrCreateChildRect(Root, "List", new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.72f));
        Image listBg = GetOrAddComponent<Image>(ListRoot.gameObject);
        listBg.color = new Color(0f, 0f, 0f, 0.12f);

        ConfigureHorizontalLayout(ListRoot);

        CloseButton = GetOrCreateButton(Root, "CloseButton", "关闭", _uiFont, new Vector2(0.88f, 0.86f), new Vector2(0.98f, 0.96f), new Color(0.42f, 0.2f, 0.18f, 1f));
        CloseButton.onClick.RemoveAllListeners();

        // 默认隐藏自身根节点；如需在外部根展示，可通过 SetExternalRoot 指定
        if (_ownRoot != null) _ownRoot.gameObject.SetActive(false);
    }

    public void SetExternalRoot(RectTransform externalRoot)
    {
        _externalRoot = externalRoot;

        if (_externalRoot != null)
        {
            // 禁用自身根（如果存在），并取/创建外部根下的控件
            if (_ownRoot != null && _ownRoot.gameObject != null)
            {
                _ownRoot.gameObject.SetActive(false);
            }

            Root = _externalRoot;
            TitleText = GetOrCreateText(Root, "Title", _uiFont, 26, TextAnchor.UpperCenter, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.97f), Color.white);
            HintText = GetOrCreateText(Root, "Hint", _uiFont, 16, TextAnchor.UpperCenter, new Vector2(0.02f, 0.74f), new Vector2(0.98f, 0.84f), new Color(0.94f, 0.9f, 0.72f, 1f));
            ListRoot = GetOrCreateChildRect(Root, "List", new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.72f));
            Image listBg = GetOrAddComponent<Image>(ListRoot.gameObject);
            listBg.color = new Color(0f, 0f, 0f, 0.12f);
            ConfigureHorizontalLayout(ListRoot);

            CloseButton = GetOrCreateButton(Root, "CloseButton", "关闭", _uiFont, new Vector2(0.88f, 0.86f), new Vector2(0.98f, 0.96f), new Color(0.42f, 0.2f, 0.18f, 1f));
        }
        else
        {
            // 切回自身根并确保自身根下的控件存在且激活
            Root = _ownRoot ?? Root;
            if (_ownRoot != null)
            {
                // 不改变激活状态：由上层通过 Show/Hide 控制何时显示自身根
                TitleText = GetOrCreateText(Root, "Title", _uiFont, 26, TextAnchor.UpperCenter, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.97f), Color.white);
                HintText = GetOrCreateText(Root, "Hint", _uiFont, 16, TextAnchor.UpperCenter, new Vector2(0.02f, 0.74f), new Vector2(0.98f, 0.84f), new Color(0.94f, 0.9f, 0.72f, 1f));

                ListRoot = GetOrCreateChildRect(Root, "List", new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.72f));
                Image listBg = GetOrAddComponent<Image>(ListRoot.gameObject);
                listBg.color = new Color(0f, 0f, 0f, 0.12f);
                ConfigureHorizontalLayout(ListRoot);

                CloseButton = GetOrCreateButton(Root, "CloseButton", "关闭", _uiFont, new Vector2(0.88f, 0.86f), new Vector2(0.98f, 0.96f), new Color(0.42f, 0.2f, 0.18f, 1f));
            }
            else
            {
                // fallback: keep existing refs
            }
        }
    }

    public void Show(bool readonlyMode = false)
    {
        _readonlyMode = readonlyMode;
        if (Root != null) Root.gameObject.SetActive(true);
        UpdateHintText();
    }

    public void Hide()
    {
        if (Root != null) Root.gameObject.SetActive(false);
    }

    public void ClearList()
    {
        if (ListRoot == null) return;
        for (int i = ListRoot.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(ListRoot.GetChild(i).gameObject);
        }
    }

    public void SetForceMode(bool force)
    {
        _forceMode = force;
        // 强制模式下关闭按钮应被禁用，避免跳过
        if (CloseButton != null)
        {
            CloseButton.interactable = !force;
        }
        UpdateHintText();
    }

    public void SetCloseCallback(UnityEngine.Events.UnityAction callback)
    {
        if (CloseButton == null) return;
        CloseButton.onClick.RemoveAllListeners();
        if (callback != null)
        {
            CloseButton.onClick.AddListener(callback);
        }
    }

    private void UpdateHintText()
    {
        if (HintText == null) return;
        if (_forceMode)
        {
            HintText.text = "强制选择：请选择一只猫（此操作不可跳过）。";
        }
        else if (_readonlyMode)
        {
            HintText.text = "观察模式：赐福已锁定，不可切换。";
        }
        else
        {
            HintText.text = "请选择一只猫作为本次祈祷对象。";
        }
    }

    private void ConfigureHorizontalLayout(RectTransform zoneRoot)
    {
        if (zoneRoot == null) return;
        VerticalLayoutGroup oldVertical = zoneRoot.GetComponent<VerticalLayoutGroup>();
        if (oldVertical != null) Object.Destroy(oldVertical);

        HorizontalLayoutGroup horiz = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (horiz == null) horiz = zoneRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        horiz.spacing = 24f;
        horiz.childAlignment = TextAnchor.MiddleCenter;
        horiz.childControlHeight = true;
        horiz.childControlWidth = false;
        horiz.childForceExpandHeight = false;
        horiz.childForceExpandWidth = false;
        horiz.padding = new RectOffset(10, 10, 10, 10);
    }

    // Helper utilities (copied minimal set)
    private static RectTransform GetOrCreateChildRect(RectTransform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform child = parent.Find(objectName);
        RectTransform rectTransform;
        if (child != null)
        {
            rectTransform = child as RectTransform;
        }
        else
        {
            GameObject childObject = new GameObject(objectName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            rectTransform = childObject.GetComponent<RectTransform>();
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return rectTransform;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    private static Text GetOrCreateText(RectTransform parent, string objectName, Font font, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        RectTransform rectTransform = GetOrCreateChildRect(parent, objectName, anchorMin, anchorMax);
        Text text = GetOrAddComponent<Text>(rectTransform.gameObject);
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Button GetOrCreateButton(RectTransform parent, string objectName, string label, Font font, Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor)
    {
        RectTransform rectTransform = GetOrCreateChildRect(parent, objectName, anchorMin, anchorMax);
        Image image = GetOrAddComponent<Image>(rectTransform.gameObject);
        Button button = GetOrAddComponent<Button>(rectTransform.gameObject);
        image.color = backgroundColor;
        button.targetGraphic = image;

        Text text = GetOrCreateButtonLabel(rectTransform, font, 18);
        text.text = label;
        return button;
    }

    private static Text GetOrCreateButtonLabel(RectTransform parent, Font font, int fontSize)
    {
        RectTransform textRect = GetOrCreateChildRect(parent, "Label", Vector2.zero, Vector2.one);
        Text text = GetOrAddComponent<Text>(textRect.gameObject);
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
