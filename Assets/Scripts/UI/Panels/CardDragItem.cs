using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 卡牌拖拽组件，负责拖拽过程的表现。
/// </summary>
public class CardDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _statText;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Transform _originalParent;
    private int _originalSiblingIndex;
    private CardBuildPanel _panel;
    private CardBuildCardData _cardData;
    private CardZoneType _zoneType;
    private Canvas _rootCanvas;
    private GameObject _dragVisual;
    private RectTransform _dragVisualRect;
    private bool _isDragging;
    private GameObject _hoverPreview;
    private RectTransform _hoverRect;

    public CardBuildCardData CardData => _cardData;
    public CardZoneType ZoneType => _zoneType;

    public void ForceCleanupDragVisual()
    {
        DestroyDragVisual();
    }

    public void BindTextRefs(Text nameText, Text statText)
    {
        _nameText = nameText;
        _statText = statText;
    }

    public void Initialize(CardBuildPanel panel, CardBuildCardData cardData, CardZoneType zoneType)
    {
        _panel = panel;
        _cardData = cardData;
        _zoneType = zoneType;

        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        RefreshView();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_panel == null)
        {
            return;
        }

        _rootCanvas = GetComponentInParent<Canvas>();
        _originalParent = transform.parent;
        _originalSiblingIndex = transform.GetSiblingIndex();

        _isDragging = true;
        CreateDragVisual(eventData.position);
        DestroyHoverPreview();
        _canvasGroup.alpha = 0.45f;
        _canvasGroup.blocksRaycasts = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isDragging) return;
        CreateHoverPreview(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        DestroyHoverPreview();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragVisualRect == null)
        {
            return;
        }

        _dragVisualRect.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        DestroyDragVisual();

        if (_originalParent != null)
        {
            transform.SetParent(_originalParent, false);
            transform.SetSiblingIndex(_originalSiblingIndex);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_panel == null || _isDragging)
        {
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        // ensure any hover tooltip is removed before panel handles the click
        DestroyHoverPreview();
        _panel.HandleCardClick(_cardData, _zoneType);
    }

    private void OnDisable()
    {
        DestroyDragVisual();
    }

    private void OnDestroy()
    {
        DestroyDragVisual();
    }

    private void CreateDragVisual(Vector2 startScreenPos)
    {
        DestroyDragVisual();

        if (_rootCanvas == null)
        {
            return;
        }

        _dragVisual = new GameObject($"{name}_DragVisual",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup));
        _dragVisual.transform.SetParent(_rootCanvas.transform, false);

        _dragVisualRect = _dragVisual.GetComponent<RectTransform>();
        _dragVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
        _dragVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
        _dragVisualRect.pivot = new Vector2(0.5f, 0.5f);
        _dragVisualRect.position = startScreenPos;
        _dragVisualRect.sizeDelta = _rectTransform.rect.size;

        Image sourceImage = GetComponent<Image>();
        Image dragImage = _dragVisual.GetComponent<Image>();
        if (sourceImage != null)
        {
            dragImage.sprite = sourceImage.sprite;
            dragImage.type = sourceImage.type;
            Color c = sourceImage.color;
            c.a = 1f; // ensure full alpha for clarity in drag preview
            dragImage.color = c;
        }

        CanvasGroup dragGroup = _dragVisual.GetComponent<CanvasGroup>();
        dragGroup.alpha = 1f; // fully opaque drag preview
        dragGroup.blocksRaycasts = false;

        CreateDragText(_dragVisual.transform, _nameText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), TextAnchor.UpperLeft);
        CreateDragText(_dragVisual.transform, _statText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), TextAnchor.LowerLeft);
    }

    private void CreateHoverPreview(Vector2 startScreenPos)
    {
        DestroyHoverPreview();

        if (_rootCanvas == null)
        {
            _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas == null) return;
        }

        _hoverPreview = new GameObject($"{name}_HoverPreview", typeof(RectTransform), typeof(Image));
        _hoverPreview.transform.SetParent(_rootCanvas.transform, false);
        _hoverRect = _hoverPreview.GetComponent<RectTransform>();
        _hoverRect.pivot = new Vector2(0f, 1f);
        _hoverRect.sizeDelta = new Vector2(320f, 420f);
        _hoverRect.position = startScreenPos + new Vector2(16f, -16f);

        Image bg = _hoverPreview.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        // Portrait
        Sprite portraitSprite = CardPortraitResolver.ResolvePortrait(_cardData.AvatarDefinitionAddress);
        if (portraitSprite != null)
        {
            GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(_hoverPreview.transform, false);
            RectTransform pr = portraitGo.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 1f);
            pr.anchorMax = new Vector2(0.5f, 1f);
            pr.pivot = new Vector2(0.5f, 1f);
            pr.anchoredPosition = new Vector2(0f, -8f);
            pr.sizeDelta = new Vector2(300f, 300f);

            Image img = portraitGo.GetComponent<Image>();
            img.sprite = portraitSprite;
            img.preserveAspect = true;
            img.color = Color.white;
        }

        // Name
        GameObject nameGo = new GameObject("HoverName", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        nameGo.transform.SetParent(_hoverPreview.transform, false);
        RectTransform nr = nameGo.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(0f, 0f);
        nr.anchorMax = new Vector2(1f, 0f);
        nr.pivot = new Vector2(0.5f, 0f);
        nr.anchoredPosition = new Vector2(0f, 12f);
        nr.sizeDelta = new Vector2(-16f, 28f);
        var nameText = nameGo.GetComponent<UnityEngine.UI.Text>();
        nameText.text = _cardData.Name;
        nameText.font = _nameText != null ? _nameText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        nameText.fontSize = 22;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleCenter;

        // Stats
        GameObject statsGo = new GameObject("HoverStats", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        statsGo.transform.SetParent(_hoverPreview.transform, false);
        RectTransform sr = statsGo.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.anchoredPosition = new Vector2(0f, 40f);
        sr.sizeDelta = new Vector2(-16f, 100f);
        var statsText = statsGo.GetComponent<UnityEngine.UI.Text>();
        statsText.text = $"BP { _cardData.GetBattlePower()}  性别 {_cardData.Gender}\nATK {_cardData.Attack}  DEF {_cardData.Defense}  HP {_cardData.Hp}  SPD {_cardData.MoveSpeed:0.0}  RNG {_cardData.AttackRange:0.0}";
        statsText.font = _statText != null ? _statText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        statsText.fontSize = 16;
        statsText.color = Color.white;
        statsText.alignment = TextAnchor.UpperLeft;
        statsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statsText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void DestroyHoverPreview()
    {
        if (_hoverPreview != null)
        {
            Destroy(_hoverPreview);
            _hoverPreview = null;
            _hoverRect = null;
        }
    }

    private void CreateDragText(Transform parent, Text source, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, TextAnchor alignment)
    {
        if (source == null)
        {
            return;
        }

        GameObject textGo = new GameObject(source.name, typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.pivot = new Vector2(0f, anchorMin.y);
        textRect.anchoredPosition = anchoredPos;
        textRect.sizeDelta = new Vector2(-12f, 44f);

        Text text = textGo.GetComponent<Text>();
        text.text = source.text;
        text.font = source.font;
        text.fontSize = source.fontSize;
        text.color = source.color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    private void DestroyDragVisual()
    {
        if (_dragVisual != null)
        {
            Destroy(_dragVisual);
            _dragVisual = null;
            _dragVisualRect = null;
        }
    }

    private void RefreshView()
    {
        bool isActiveBlessing = _zoneType == CardZoneType.Blessing && _panel != null && _panel.IsBlessingCandidateSelected(_cardData.Id);

        if (_nameText != null)
        {
            if (_zoneType == CardZoneType.Blessing)
            {
                _nameText.text = isActiveBlessing
                    ? $"{_cardData.Name}  [已激活赐福]"
                    : $"{_cardData.Name}  [赐福候选]";
            }
            else if (_zoneType == CardZoneType.Discard)
            {
                _nameText.text = $"{_cardData.Name}  [弃置]";
            }
            else
            {
                _nameText.text = _cardData.Name;
            }
        }

        if (_statText != null)
        {
            string extraText = string.Empty;
            if (_zoneType == CardZoneType.Blessing)
            {
                extraText = isActiveBlessing
                    ? "\n下场战斗上阵时获得强化"
                    : "\n点击可切换为祈祷对象";
            }
            else if (_zoneType == CardZoneType.Discard)
            {
                extraText = "\n关闭窗口后永久消失";
            }

            _statText.text = $"BP {_cardData.GetBattlePower()}  性别 {_cardData.Gender}\nATK {_cardData.Attack}  DEF {_cardData.Defense}  HP {_cardData.Hp}  SPD {_cardData.MoveSpeed:0.0}{extraText}";
        }
    }
}
