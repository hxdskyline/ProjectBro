using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 卡牌构筑界面，支持上阵区和待选区之间自由拖拽。
/// </summary>
public class CardBuildPanel : UIPanel
{
    [SerializeField] private Button _startBattleButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Text _levelText;
    [SerializeField] private Text _cardInfoText;
    [SerializeField] private RectTransform _deployedCardsRoot;
    [SerializeField] private RectTransform _reserveCardsRoot;
    [SerializeField] private int _maxDeployedCards = 5;

    private int _currentLevel = 1;
    private readonly List<CardBuildCardData> _deployedCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _reserveCards = new List<CardBuildCardData>();
    private bool _cardsInitialized;
    private Font _uiFont;

    public override void Initialize()
    {
        base.Initialize();

        if (_startBattleButton != null)
        {
            _startBattleButton.onClick.RemoveListener(OnStartBattleButtonClicked);
            _startBattleButton.onClick.AddListener(OnStartBattleButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        if (GameManager.Instance.DataManager.PlayerData != null)
        {
            _currentLevel = GameManager.Instance.DataManager.PlayerData.currentLevel;
        }

        _uiFont = LoadBuiltinFont();
        EnsureCardRoots();
        EnsureDeployedDropZone(_deployedCardsRoot);
        EnsureReserveDropZone(_reserveCardsRoot);
        EnsureDeployedLayout(_deployedCardsRoot);
        EnsureReserveLayout(_reserveCardsRoot);
        CreateDemoCardsIfNeeded();
        RebuildCardViews();
        UpdateUI();

        Debug.Log("[CardBuildPanel] Initialized");
    }

    public void HandleCardDrop(CardDragItem dragItem, CardZoneType targetZone)
    {
        // RebuildCardViews may destroy dragged item before OnEndDrag runs.
        dragItem.gameObject.SendMessage("ForceCleanupDragVisual", SendMessageOptions.DontRequireReceiver);

        CardBuildCardData card = dragItem.CardData;
        CardZoneType fromZone = dragItem.ZoneType;

        if (fromZone == targetZone)
        {
            RebuildCardViews();
            return;
        }

        if (targetZone == CardZoneType.Deployed && _deployedCards.Count >= _maxDeployedCards)
        {
            if (_cardInfoText != null)
            {
                _cardInfoText.text = $"Deployed is full ({_maxDeployedCards}). Move one card out first.";
            }

            RebuildCardViews();
            return;
        }

        if (fromZone == CardZoneType.Deployed)
        {
            MoveCardById(_deployedCards, _reserveCards, card.Id);
        }
        else
        {
            MoveCardById(_reserveCards, _deployedCards, card.Id);
        }

        RebuildCardViews();
        UpdateUI();
    }

    public void HandleCardClick(CardBuildCardData card, CardZoneType fromZone)
    {
        CardZoneType targetZone = fromZone == CardZoneType.Deployed
            ? CardZoneType.Reserve
            : CardZoneType.Deployed;

        if (targetZone == CardZoneType.Deployed && _deployedCards.Count >= _maxDeployedCards)
        {
            if (_cardInfoText != null)
            {
                _cardInfoText.text = $"Deployed is full ({_maxDeployedCards}). Move one card out first.";
            }
            return;
        }

        if (fromZone == CardZoneType.Deployed)
        {
            MoveCardById(_deployedCards, _reserveCards, card.Id);
        }
        else
        {
            MoveCardById(_reserveCards, _deployedCards, card.Id);
        }

        RebuildCardViews();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_levelText != null)
        {
            _levelText.text = $"Level: {_currentLevel}";
        }

        if (_cardInfoText != null)
        {
            _cardInfoText.text = $"Drag cards between zones. Deployed: {_deployedCards.Count}/{_maxDeployedCards}  Reserve: {_reserveCards.Count}";
        }
    }

    private void OnStartBattleButtonClicked()
    {
        if (_deployedCards.Count == 0)
        {
            if (_cardInfoText != null)
            {
                _cardInfoText.text = "Please deploy at least 1 card before starting battle.";
            }
            return;
        }

        Debug.Log("[CardBuildPanel] Start Battle button clicked");

        GameManager.Instance.UIManager.HidePanel("ui/CardBuildPanel");

        BattlePanel battlePanel = GameManager.Instance.UIManager.ShowPanel<BattlePanel>("ui/BattlePanel", UIManager.UILayer.Normal);

        if (battlePanel != null)
        {
            battlePanel.StartBattle(_currentLevel);
        }
    }

    private void OnBackButtonClicked()
    {
        Debug.Log("[CardBuildPanel] Back button clicked");

        GameManager.Instance.UIManager.HidePanel("ui/CardBuildPanel");
        GameManager.Instance.UIManager.ShowPanel<MainPanel>("ui/MainPanel", UIManager.UILayer.Normal);
    }

    private void EnsureCardRoots()
    {
        if (_deployedCardsRoot != null && _reserveCardsRoot != null)
        {
            return;
        }

        RectTransform panelRect = transform as RectTransform;
        _deployedCardsRoot = _deployedCardsRoot ?? CreateRuntimeZone(panelRect, "DeployedCardsRoot", "Deployed", new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.35f));
        _reserveCardsRoot = _reserveCardsRoot ?? CreateRuntimeZone(panelRect, "ReserveCardsRoot", "Reserve", new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.62f));
    }

    private RectTransform CreateRuntimeZone(RectTransform parent, string rootName, string title, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject rootGo = new GameObject(rootName, typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(parent, false);

        RectTransform rootRect = rootGo.GetComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.offsetMin = new Vector2(0, 0);
        rootRect.offsetMax = new Vector2(0, 0);

        Image bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.09f, 0.14f, 0.2f, 0.55f);

        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(rootRect, false);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 24f);
        titleRect.anchoredPosition = Vector2.zero;

        Text titleText = titleGo.GetComponent<Text>();
        titleText.font = _uiFont;
        titleText.fontSize = 18;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = Color.white;
        titleText.text = title;

        GameObject listGo = new GameObject("List", typeof(RectTransform));
        listGo.transform.SetParent(rootRect, false);

        RectTransform listRect = listGo.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.offsetMin = new Vector2(10f, 10f);
        listRect.offsetMax = new Vector2(-10f, -30f);

        return listRect;
    }

    private void EnsureDeployedDropZone(RectTransform zoneRoot)
    {
        DeployedCardDropZone dropZone = zoneRoot.GetComponent<DeployedCardDropZone>();
        if (dropZone == null)
        {
            dropZone = zoneRoot.gameObject.AddComponent<DeployedCardDropZone>();
        }

        dropZone.Initialize(this);

        if (zoneRoot.GetComponent<Image>() == null)
        {
            Image image = zoneRoot.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.08f);
        }
    }

    private void EnsureReserveDropZone(RectTransform zoneRoot)
    {
        ReserveCardDropZone dropZone = zoneRoot.GetComponent<ReserveCardDropZone>();
        if (dropZone == null)
        {
            dropZone = zoneRoot.gameObject.AddComponent<ReserveCardDropZone>();
        }

        dropZone.Initialize(this);

        if (zoneRoot.GetComponent<Image>() == null)
        {
            Image image = zoneRoot.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.08f);
        }
    }

    private void EnsureDeployedLayout(RectTransform zoneRoot)
    {
        VerticalLayoutGroup oldVertical = zoneRoot.GetComponent<VerticalLayoutGroup>();
        if (oldVertical == null)
        {
            oldVertical = zoneRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        HorizontalLayoutGroup oldHorizontal = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (oldHorizontal != null)
        {
            Destroy(oldHorizontal);
        }

        oldVertical.spacing = 8f;
        oldVertical.childAlignment = TextAnchor.UpperLeft;
        oldVertical.childControlHeight = true;
        oldVertical.childControlWidth = true;
        oldVertical.childForceExpandHeight = false;
        oldVertical.childForceExpandWidth = false;
        oldVertical.padding = new RectOffset(8, 8, 8, 8);
    }

    private void EnsureReserveLayout(RectTransform zoneRoot)
    {
        HorizontalLayoutGroup oldHorizontal = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (oldHorizontal != null)
        {
            Destroy(oldHorizontal);
        }

        VerticalLayoutGroup layout = zoneRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = zoneRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.padding = new RectOffset(8, 8, 8, 8);
    }

    private void CreateDemoCardsIfNeeded()
    {
        if (_cardsInitialized)
        {
            return;
        }

        _cardsInitialized = true;
        _deployedCards.Clear();
        _reserveCards.Clear();

        _reserveCards.Add(new CardBuildCardData { Id = 1, Name = "Guardian", Attack = 12, Hp = 95 });
        _reserveCards.Add(new CardBuildCardData { Id = 2, Name = "Ranger", Attack = 18, Hp = 70 });
        _reserveCards.Add(new CardBuildCardData { Id = 3, Name = "Assassin", Attack = 24, Hp = 50 });
        _reserveCards.Add(new CardBuildCardData { Id = 4, Name = "Priest", Attack = 8, Hp = 82 });
        _reserveCards.Add(new CardBuildCardData { Id = 5, Name = "Berserker", Attack = 22, Hp = 68 });
        _reserveCards.Add(new CardBuildCardData { Id = 6, Name = "Mage", Attack = 20, Hp = 55 });
    }

    private void RebuildCardViews()
    {
        ClearChildren(_deployedCardsRoot);
        ClearChildren(_reserveCardsRoot);

        for (int i = 0; i < _deployedCards.Count; i++)
        {
            CreateCardItem(_deployedCardsRoot, _deployedCards[i], CardZoneType.Deployed);
        }

        for (int i = 0; i < _reserveCards.Count; i++)
        {
            CreateCardItem(_reserveCardsRoot, _reserveCards[i], CardZoneType.Reserve);
        }
    }

    private void CreateCardItem(RectTransform parent, CardBuildCardData cardData, CardZoneType zoneType)
    {
        GameObject cardGo = new GameObject($"Card_{cardData.Id}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement),
            typeof(CanvasGroup),
            typeof(CardDragItem));
        cardGo.transform.SetParent(parent, false);

        RectTransform cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(0f, 1f);
        cardRect.pivot = new Vector2(0f, 1f);

        LayoutElement layoutElement = cardGo.GetComponent<LayoutElement>();
        const float cardHeight = 90f;
        const float cardWidth = cardHeight * 2f;
        layoutElement.minWidth = cardWidth;
        layoutElement.minHeight = cardHeight;
        layoutElement.preferredWidth = cardWidth;
        layoutElement.preferredHeight = cardHeight;
        cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);

        Image cardBg = cardGo.GetComponent<Image>();
        cardBg.color = new Color(0.2f, 0.48f, 0.26f, 0.9f);

        Text nameText = CreateCardText(cardGo.transform, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), 16);
        nameText.alignment = TextAnchor.UpperLeft;

        Text statText = CreateCardText(cardGo.transform, "Stats", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), 14);
        statText.alignment = TextAnchor.LowerLeft;

        CardDragItem dragItem = cardGo.GetComponent<CardDragItem>();
        dragItem.BindTextRefs(nameText, statText);
        dragItem.Initialize(this, cardData, zoneType);
    }

    private Text CreateCardText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, int fontSize)
    {
        GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.pivot = new Vector2(0f, anchorMin.y);
        textRect.anchoredPosition = anchoredPos;
        textRect.sizeDelta = new Vector2(-12f, 26f);

        Text text = textGo.GetComponent<Text>();
        text.font = _uiFont;
        text.fontSize = fontSize;
        text.color = Color.white;

        return text;
    }

    private void ClearChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void MoveCardById(List<CardBuildCardData> from, List<CardBuildCardData> to, int cardId)
    {
        for (int i = 0; i < from.Count; i++)
        {
            if (from[i].Id != cardId)
            {
                continue;
            }

            CardBuildCardData card = from[i];
            from.RemoveAt(i);
            to.Add(card);
            return;
        }
    }

    private Font LoadBuiltinFont()
    {
        // Unity 6+ renamed the default built-in font.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        // Fallback for older editor/runtime versions.
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null)
        {
            return font;
        }

        Debug.LogError("[CardBuildPanel] Failed to load built-in font (LegacyRuntime.ttf/Arial.ttf).");
        return null;
    }
}