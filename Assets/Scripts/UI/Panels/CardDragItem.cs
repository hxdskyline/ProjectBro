using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 卡牌拖拽组件，负责拖拽过程的表现。
/// </summary>
public class CardDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
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
            dragImage.color = sourceImage.color;
        }

        CanvasGroup dragGroup = _dragVisual.GetComponent<CanvasGroup>();
        dragGroup.alpha = 0.85f;
        dragGroup.blocksRaycasts = false;

        CreateDragText(_dragVisual.transform, _nameText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), TextAnchor.UpperLeft);
        CreateDragText(_dragVisual.transform, _statText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), TextAnchor.LowerLeft);
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
