using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 战前准备界面的卡牌拖拽组件。
/// </summary>
public class PrepareCardDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _statText;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Transform _originalParent;
    private int _originalSiblingIndex;
    private BattlePreparePanel _panel;
    private CardBuildCardData _cardData;
    private PrepareCardZoneType _zoneType;
    private bool _isOutingCard;
    private bool _isBlessingCard;
    private Canvas _rootCanvas;
    private GameObject _dragVisual;
    private RectTransform _dragVisualRect;
    private bool _isDragging;

    public CardBuildCardData CardData => _cardData;
    public PrepareCardZoneType ZoneType => _zoneType;

    public void ForceCleanupDragVisual()
    {
        DestroyDragVisual();
    }

    public void BindTextRefs(Text nameText, Text statText)
    {
        _nameText = nameText;
        _statText = statText;
    }

    public void Initialize(BattlePreparePanel panel, CardBuildCardData cardData, PrepareCardZoneType zoneType, bool isOutingCard, bool isBlessingCard)
    {
        _panel = panel;
        _cardData = cardData;
        _zoneType = zoneType;
        _isOutingCard = isOutingCard;
        _isBlessingCard = isBlessingCard;

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
        _canvasGroup.alpha = 0.45f;
        _canvasGroup.blocksRaycasts = false;
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

        _dragVisual = new GameObject($"{name}_DragVisual", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
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
            dragImage.color = sourceImage.color;
        }

        CanvasGroup dragGroup = _dragVisual.GetComponent<CanvasGroup>();
        dragGroup.alpha = 0.85f;
        dragGroup.blocksRaycasts = false;

        CreateDragText(_dragVisual.transform, _nameText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), TextAnchor.UpperLeft);
        CreateDragText(_dragVisual.transform, _statText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), TextAnchor.LowerLeft);
    }

    private static void CreateDragText(Transform parent, Text source, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, TextAnchor alignment)
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
        if (_nameText != null)
        {
            string tagText = string.Empty;
            if (_isOutingCard)
            {
                tagText = "  [游历中]";
            }
            else if (_isBlessingCard)
            {
                tagText = "  [赐福]";
            }

            _nameText.text = $"{_cardData.Name}{tagText}";
        }

        if (_statText != null)
        {
            string restrictionText = _isOutingCard
                ? "\n本场不可上阵"
                : _isBlessingCard
                    ? "\n本场战斗临时强化"
                    : string.Empty;
            _statText.text = $"BP {_cardData.GetBattlePower()}  性别 {_cardData.Gender}\nATK {_cardData.Attack}  DEF {_cardData.Defense}  HP {_cardData.Hp}  SPD {_cardData.MoveSpeed:0.0}{restrictionText}";
        }
    }
}