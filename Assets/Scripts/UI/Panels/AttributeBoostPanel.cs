using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 属性提升独立面板：负责提供 UI 根、列表容器、提示文本及确认/关闭按钮。
/// 上层负责向 `ListRoot` 填充卡片项并处理具体的属性提升逻辑。
/// </summary>
public class AttributeBoostPanel
{
    public RectTransform Root { get; private set; }
    public RectTransform ListRoot { get; private set; }
    public Text TitleText { get; private set; }
    public Text HintText { get; private set; }
    public Button ConfirmButton { get; private set; }
    public Button CloseButton { get; private set; }

    private Font _uiFont;

    public void Initialize(RectTransform parent, Font uiFont)
    {
        if (parent == null) return;
        if (Root != null) return;

        _uiFont = uiFont;

        Root = GetOrCreateChildRect(parent, "AttributeBoostPanelRoot", new Vector2(0.52f, 0.08f), new Vector2(0.96f, 0.72f));
        Image popupBg = GetOrAddComponent<Image>(Root.gameObject);
        popupBg.color = new Color(0.08f, 0.12f, 0.17f, 0.97f);

        TitleText = GetOrCreateText(Root, "Title", _uiFont, 22, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.88f), new Vector2(0.68f, 0.97f), Color.white);
        HintText = GetOrCreateText(Root, "Hint", _uiFont, 14, TextAnchor.UpperLeft, new Vector2(0.04f, 0.77f), new Vector2(0.96f, 0.86f), new Color(0.92f, 0.88f, 0.72f, 1f));
        HintText.text = "把想要强化的卡拖入此区域；确认后这些卡将获得永久提升并返回待选区。";

        ListRoot = GetOrCreateChildRect(Root, "List", new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.72f));
        Image listBg = GetOrAddComponent<Image>(ListRoot.gameObject);
        listBg.color = new Color(0f, 0f, 0f, 0.12f);

        ConfigureVerticalLayout(ListRoot);

        ConfirmButton = GetOrCreateButton(Root, "ConfirmButton", "确认强化", _uiFont, new Vector2(0.52f, 0.86f), new Vector2(0.74f, 0.96f), new Color(0.22f, 0.4f, 0.7f, 1f));
        CloseButton = GetOrCreateButton(Root, "CloseButton", "关闭", _uiFont, new Vector2(0.76f, 0.86f), new Vector2(0.96f, 0.96f), new Color(0.42f, 0.2f, 0.18f, 1f));

        Root.gameObject.SetActive(false);
    }

    public void Show()
    {
        if (Root != null) Root.gameObject.SetActive(true);
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

    public void SetConfirmCallback(UnityEngine.Events.UnityAction callback)
    {
        if (ConfirmButton == null) return;
        ConfirmButton.onClick.RemoveAllListeners();
        if (callback != null) ConfirmButton.onClick.AddListener(callback);
    }

    public void SetCloseCallback(UnityEngine.Events.UnityAction callback)
    {
        if (CloseButton == null) return;
        CloseButton.onClick.RemoveAllListeners();
        if (callback != null) CloseButton.onClick.AddListener(callback);
    }

    private void ConfigureVerticalLayout(RectTransform zoneRoot)
    {
        if (zoneRoot == null) return;
        HorizontalLayoutGroup oldHorizontal = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (oldHorizontal != null) Object.Destroy(oldHorizontal);

        VerticalLayoutGroup vert = zoneRoot.GetComponent<VerticalLayoutGroup>();
        if (vert == null) vert = zoneRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        vert.spacing = 8f;
        vert.childAlignment = TextAnchor.UpperLeft;
        vert.childControlHeight = true;
        vert.childControlWidth = true;
        vert.childForceExpandHeight = false;
        vert.childForceExpandWidth = false;
        vert.padding = new RectOffset(10, 10, 10, 10);
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
