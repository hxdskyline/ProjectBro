using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战前准备界面。
/// </summary>
public class BattlePreparePanel : UIPanel
{
    private const int RequiredOutingCardCount = 2;
    private const string RootName = "PrepareContentRoot";
    private const string TitleName = "PrepareTitleText";
    private const string SummaryName = "PrepareSummaryText";
    private const string StatusName = "PrepareStatusText";
    private const string OwnedRootName = "OwnedCardsRoot";
    private const string DeployedRootName = "DeployedCardsRoot";
    private const string EnemyRootName = "EnemyCardsRoot";
    private const string StartButtonName = "PrepareStartButton";
    private const string BackButtonName = "PrepareBackButton";

    private readonly List<CardBuildCardData> _ownedCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _deployedCards = new List<CardBuildCardData>();
    private readonly HashSet<int> _outingCardIds = new HashSet<int>();
    private readonly HashSet<int> _blessingCardIds = new HashSet<int>();

    private Text _titleText;
    private Text _summaryText;
    private Text _statusText;
    private Button _startBattleButton;
    private Button _backButton;
    private RectTransform _ownedCardsRoot;
    private RectTransform _deployedCardsRoot;
    private RectTransform _enemyCardsRoot;
    private Font _uiFont;

    private int _currentLevel;
    private bool _grantOutingRewardOnBattleEnd;
    private bool _consumeBlessingOnBattleEnd;
    private bool _grantAttributeBoostOnBattleEnd;
    private int _maxDeployedCount = 5;

    public override void Initialize()
    {
        base.Initialize();

        _uiFont = LoadBuiltinFont();
        EnsureRuntimeLayout();
        EnsureDropZones();
        EnsureZoneLayouts();

        if (_startBattleButton != null)
        {
            _startBattleButton.onClick.RemoveListener(OnStartBattleClicked);
            _startBattleButton.onClick.AddListener(OnStartBattleClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackClicked);
            _backButton.onClick.AddListener(OnBackClicked);
        }
    }

    public void SetupBattle(
        int levelId,
        CardBuildCardData[] allOwnedCards,
        CardBuildCardData[] defaultDeployedCards,
        int[] outingCardIds,
        int[] blessingCardIds,
        bool grantOutingRewardOnBattleEnd,
        bool consumeBlessingOnBattleEnd,
        bool grantAttributeBoostOnBattleEnd,
        int maxDeployedCount)
    {
        _currentLevel = levelId;
        _grantOutingRewardOnBattleEnd = grantOutingRewardOnBattleEnd;
        _consumeBlessingOnBattleEnd = consumeBlessingOnBattleEnd;
        _grantAttributeBoostOnBattleEnd = grantAttributeBoostOnBattleEnd;
        _maxDeployedCount = Mathf.Max(1, maxDeployedCount);

        _ownedCards.Clear();
        _deployedCards.Clear();
        _outingCardIds.Clear();
        _blessingCardIds.Clear();

        if (outingCardIds != null && outingCardIds.Length == RequiredOutingCardCount)
        {
            for (int i = 0; i < outingCardIds.Length; i++)
            {
                _outingCardIds.Add(outingCardIds[i]);
            }
        }

        if (blessingCardIds != null)
        {
            for (int i = 0; i < blessingCardIds.Length; i++)
            {
                _blessingCardIds.Add(blessingCardIds[i]);
            }
        }

        if (allOwnedCards != null)
        {
            _ownedCards.AddRange(allOwnedCards);
        }

        if (defaultDeployedCards != null)
        {
            for (int i = 0; i < defaultDeployedCards.Length; i++)
            {
                CardBuildCardData card = defaultDeployedCards[i];
                RemoveCardById(_ownedCards, card.Id);
                _deployedCards.Add(card);
            }
        }

        RefreshUI();
    }

    public void HandleCardDrop(PrepareCardDragItem dragItem, PrepareCardZoneType targetZone)
    {
        dragItem.gameObject.SendMessage("ForceCleanupDragVisual", SendMessageOptions.DontRequireReceiver);

        CardBuildCardData card = dragItem.CardData;
        PrepareCardZoneType fromZone = dragItem.ZoneType;
        if (fromZone == targetZone)
        {
            RebuildCardViews();
            return;
        }

        if (targetZone == PrepareCardZoneType.Deployed && _deployedCards.Count >= _maxDeployedCount)
        {
            SetStatusText($"上阵区已满，最多 {_maxDeployedCount} 只猫咪。", true);
            RebuildCardViews();
            return;
        }

        if (targetZone == PrepareCardZoneType.Deployed && IsOutingCard(card.Id))
        {
            SetStatusText($"{card.Name} 正在游历中，本场只能停留在待选区。", true);
            RebuildCardViews();
            return;
        }

        if (!MoveCardBetweenZones(fromZone, targetZone, card.Id))
        {
            RebuildCardViews();
            return;
        }

        RefreshUI();
    }

    public void HandleCardClick(CardBuildCardData card, PrepareCardZoneType fromZone)
    {
        PrepareCardZoneType targetZone = fromZone == PrepareCardZoneType.Deployed
            ? PrepareCardZoneType.Owned
            : PrepareCardZoneType.Deployed;

        if (targetZone == PrepareCardZoneType.Deployed && _deployedCards.Count >= _maxDeployedCount)
        {
            SetStatusText($"上阵区已满，最多 {_maxDeployedCount} 只猫咪。", true);
            return;
        }

        if (targetZone == PrepareCardZoneType.Deployed && IsOutingCard(card.Id))
        {
            SetStatusText($"{card.Name} 正在游历中，本场不能加入出战区。", true);
            return;
        }

        if (!MoveCardBetweenZones(fromZone, targetZone, card.Id))
        {
            return;
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        RefreshTexts();
        RebuildCardViews();
        RebuildEnemyViews();
    }

    private void RefreshTexts()
    {
        if (_titleText != null)
        {
            _titleText.text = $"站前准备  Battle {_currentLevel}";
        }

        if (_summaryText != null)
        {
            _summaryText.text =
                $"持有猫咪: {_ownedCards.Count + _deployedCards.Count}    " +
                $"上阵: {_deployedCards.Count}/{_maxDeployedCount}    " +
                $"游历生效: {(_outingCardIds.Count == RequiredOutingCardCount ? "是" : "否")}    " +
                $"赐福中: {_blessingCardIds.Count}    " +
                $"敌人: {ResolveEnemyCount(_currentLevel)}    " +
                $"外出结算: {(_grantOutingRewardOnBattleEnd ? "开" : "关")}    " +
                $"赐福消耗: {(_consumeBlessingOnBattleEnd ? "开" : "关")}    " +
                $"强化结算: {(_grantAttributeBoostOnBattleEnd ? "开" : "关")}";
        }

        if (_statusText != null && string.IsNullOrEmpty(_statusText.text))
        {
            _statusText.text = _outingCardIds.Count == RequiredOutingCardCount
                ? "带有“游历中”标记的猫咪无法拖入上阵区。"
                : _blessingCardIds.Count > 0
                    ? "带有“赐福”标记的猫咪会在本场战斗获得临时强化。"
                    : "把猫咪拖入上阵区，右侧查看本场敌人列表。";
        }
    }

    private void RebuildCardViews()
    {
        ClearChildren(_ownedCardsRoot);
        ClearChildren(_deployedCardsRoot);

        for (int i = 0; i < _ownedCards.Count; i++)
        {
            CreateCardItem(_ownedCardsRoot, _ownedCards[i], PrepareCardZoneType.Owned);
        }

        for (int i = 0; i < _deployedCards.Count; i++)
        {
            CreateCardItem(_deployedCardsRoot, _deployedCards[i], PrepareCardZoneType.Deployed);
        }
    }

    private void RebuildEnemyViews()
    {
        ClearChildren(_enemyCardsRoot);

        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        int[] enemyUnitIds = battleCampaignRuntime != null
            ? battleCampaignRuntime.GetEnemyUnitIdsForBattle(_currentLevel)
            : null;

        if (enemyUnitIds == null || enemyUnitIds.Length == 0)
        {
            CreateEnemyItem(_enemyCardsRoot, 1, 1);
            return;
        }

        for (int i = 0; i < enemyUnitIds.Length; i++)
        {
            CreateEnemyItem(_enemyCardsRoot, i + 1, enemyUnitIds[i]);
        }
    }

    private void CreateCardItem(RectTransform parent, CardBuildCardData cardData, PrepareCardZoneType zoneType)
    {
        bool isOutingCard = IsOutingCard(cardData.Id);
        bool isBlessingCard = IsBlessingCard(cardData.Id);
        GameObject cardGo = new GameObject($"PrepareCard_{cardData.Id}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement),
            typeof(CanvasGroup),
            typeof(PrepareCardDragItem));
        cardGo.transform.SetParent(parent, false);

        RectTransform cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(0.5f, 1f);

        LayoutElement layoutElement = cardGo.GetComponent<LayoutElement>();
        layoutElement.minHeight = 96f;
        layoutElement.preferredHeight = 96f;
        layoutElement.flexibleWidth = 1f;

        Image cardBg = cardGo.GetComponent<Image>();
        cardBg.color = ResolveCardBackgroundColor(zoneType, isOutingCard, isBlessingCard);

        Sprite portraitSprite = CardPortraitResolver.ResolvePortrait(cardData.AvatarDefinitionAddress);
        if (portraitSprite != null)
        {
            CreateCardPortrait(cardGo.transform, portraitSprite);
        }

        Text nameText = CreateCardText(cardGo.transform, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), 18);
        nameText.alignment = TextAnchor.UpperLeft;

        Text statText = CreateCardText(cardGo.transform, "Stats", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f), 14);
        statText.alignment = TextAnchor.LowerLeft;

        PrepareCardDragItem dragItem = cardGo.GetComponent<PrepareCardDragItem>();
        dragItem.BindTextRefs(nameText, statText);
        dragItem.Initialize(this, cardData, zoneType, isOutingCard, isBlessingCard);
    }

    private static void CreateCardPortrait(Transform parent, Sprite portraitSprite)
    {
        GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGo.transform.SetParent(parent, false);
        portraitGo.transform.SetAsFirstSibling();

        RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = new Vector2(8f, 8f);
        portraitRect.offsetMax = new Vector2(-8f, -8f);

        Image portraitImage = portraitGo.GetComponent<Image>();
        portraitImage.sprite = portraitSprite;
        portraitImage.preserveAspect = true;
        portraitImage.color = new Color(1f, 1f, 1f, 0.38f);
        portraitImage.raycastTarget = false;
    }

    private void CreateEnemyItem(RectTransform parent, int displayIndex, int enemyUnitId)
    {
        GameObject enemyGo = new GameObject($"Enemy_{displayIndex}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        enemyGo.transform.SetParent(parent, false);

        RectTransform enemyRect = enemyGo.GetComponent<RectTransform>();
        enemyRect.anchorMin = new Vector2(0f, 1f);
        enemyRect.anchorMax = new Vector2(1f, 1f);
        enemyRect.pivot = new Vector2(0.5f, 1f);

        LayoutElement layoutElement = enemyGo.GetComponent<LayoutElement>();
        layoutElement.minHeight = 72f;
        layoutElement.preferredHeight = 72f;
        layoutElement.flexibleWidth = 1f;

        Image enemyBg = enemyGo.GetComponent<Image>();
        enemyBg.color = new Color(0.45f, 0.2f, 0.2f, 0.92f);

        Text titleText = CreateCardText(enemyGo.transform, "EnemyName", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), 17);
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.text = $"敌人 {displayIndex}: {ResolveEnemyName(enemyUnitId)}";

        Text detailText = CreateCardText(enemyGo.transform, "EnemyDetail", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f), 13);
        detailText.alignment = TextAnchor.LowerLeft;
        detailText.text = $"兵种ID {enemyUnitId}";
    }

    private Text CreateCardText(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, int fontSize)
    {
        GameObject textGo = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.pivot = new Vector2(0f, anchorMin.y);
        textRect.anchoredPosition = anchoredPos;
        textRect.sizeDelta = new Vector2(-16f, 42f);

        Text text = textGo.GetComponent<Text>();
        text.font = _uiFont;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void OnStartBattleClicked()
    {
        if (_deployedCards.Count == 0)
        {
            SetStatusText("至少需要上阵 1 只猫咪。", true);
            return;
        }

        GameManager.Instance.UIManager.HidePanel("ui/BattlePreparePanel");

        BattlePanel battlePanel = GameManager.Instance.UIManager.ShowPanel<BattlePanel>("ui/BattlePanel", UIManager.UILayer.Normal);
        if (battlePanel != null)
        {
            battlePanel.StartBattle(
                _currentLevel,
                _deployedCards.ToArray(),
                BuildBlessingCardIdSnapshot(),
                _grantOutingRewardOnBattleEnd,
                _consumeBlessingOnBattleEnd,
                _grantAttributeBoostOnBattleEnd);
        }
    }

    private void OnBackClicked()
    {
        GameManager.Instance.UIManager.HidePanel("ui/BattlePreparePanel");
        GameManager.Instance.UIManager.ShowPanel<CardBuildPanel>("ui/CardBuildPanel", UIManager.UILayer.Normal);
    }

    private void EnsureRuntimeLayout()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        Image backgroundImage = GetOrAddComponent<Image>(gameObject);
        backgroundImage.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);

        RectTransform contentRoot = GetOrCreateChildRect(panelRect, RootName, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));
        Image contentBackground = GetOrAddComponent<Image>(contentRoot.gameObject);
        contentBackground.color = new Color(0.87f, 0.91f, 0.96f, 0.98f);

        _titleText = GetOrCreateText(contentRoot, TitleName, _uiFont, 42, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.9f), new Vector2(0.5f, 0.98f), new Color(0.1f, 0.15f, 0.25f, 1f));
        _summaryText = GetOrCreateText(contentRoot, SummaryName, _uiFont, 20, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.84f), new Vector2(0.97f, 0.9f), new Color(0.15f, 0.19f, 0.26f, 1f));
        _statusText = GetOrCreateText(contentRoot, StatusName, _uiFont, 18, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.78f), new Vector2(0.97f, 0.84f), new Color(0.55f, 0.29f, 0.08f, 1f));

        _ownedCardsRoot = CreateRuntimeZone(contentRoot, OwnedRootName, "持有猫咪", new Vector2(0.03f, 0.16f), new Vector2(0.32f, 0.75f), new Color(0.13f, 0.23f, 0.39f, 0.72f));
        _deployedCardsRoot = CreateRuntimeZone(contentRoot, DeployedRootName, "上阵区域", new Vector2(0.355f, 0.16f), new Vector2(0.645f, 0.75f), new Color(0.15f, 0.33f, 0.2f, 0.72f));
        _enemyCardsRoot = CreateRuntimeZone(contentRoot, EnemyRootName, "敌人列表", new Vector2(0.68f, 0.16f), new Vector2(0.97f, 0.75f), new Color(0.42f, 0.18f, 0.18f, 0.72f));

        _backButton = GetOrCreateButton(contentRoot, BackButtonName, "返回构筑", _uiFont, new Vector2(0.03f, 0.04f), new Vector2(0.2f, 0.11f), new Color(0.35f, 0.36f, 0.4f, 1f));
        _startBattleButton = GetOrCreateButton(contentRoot, StartButtonName, "进入战斗", _uiFont, new Vector2(0.79f, 0.04f), new Vector2(0.97f, 0.11f), new Color(0.16f, 0.43f, 0.29f, 1f));
    }

    private void EnsureDropZones()
    {
        BattlePrepareOwnedDropZone ownedDropZone = GetOrAddComponent<BattlePrepareOwnedDropZone>(_ownedCardsRoot.gameObject);
        ownedDropZone.Initialize(this);

        BattlePrepareDeployedDropZone deployedDropZone = GetOrAddComponent<BattlePrepareDeployedDropZone>(_deployedCardsRoot.gameObject);
        deployedDropZone.Initialize(this);
    }

    private void EnsureZoneLayouts()
    {
        ConfigureVerticalLayout(_ownedCardsRoot);
        ConfigureVerticalLayout(_deployedCardsRoot);
        ConfigureVerticalLayout(_enemyCardsRoot);
    }

    private static void ConfigureVerticalLayout(RectTransform zoneRoot)
    {
        HorizontalLayoutGroup oldHorizontal = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (oldHorizontal != null)
        {
            Object.Destroy(oldHorizontal);
        }

        VerticalLayoutGroup layout = GetOrAddComponent<VerticalLayoutGroup>(zoneRoot.gameObject);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(8, 8, 8, 8);
    }

    private RectTransform CreateRuntimeZone(RectTransform parent, string rootName, string title, Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor)
    {
        RectTransform rootRect = GetOrCreateChildRect(parent, rootName, anchorMin, anchorMax);
        Image bg = GetOrAddComponent<Image>(rootRect.gameObject);
        bg.color = backgroundColor;

        Text titleText = GetOrCreateText(rootRect, "Title", _uiFont, 24, TextAnchor.MiddleLeft, new Vector2(0f, 0.92f), new Vector2(1f, 1f), Color.white);
        titleText.text = title;

        RectTransform listRect = GetOrCreateChildRect(rootRect, "List", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.9f));
        Image listBg = GetOrAddComponent<Image>(listRect.gameObject);
        listBg.color = new Color(0f, 0f, 0f, 0.08f);
        return listRect;
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

    private static Text GetOrCreateText(
        RectTransform parent,
        string objectName,
        Font font,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        RectTransform rectTransform = GetOrCreateChildRect(parent, objectName, anchorMin, anchorMax);
        Text text = GetOrAddComponent<Text>(rectTransform.gameObject);
        GetOrAddComponent<CanvasRenderer>(rectTransform.gameObject);
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Button GetOrCreateButton(
        RectTransform parent,
        string objectName,
        string label,
        Font font,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color backgroundColor)
    {
        RectTransform rectTransform = GetOrCreateChildRect(parent, objectName, anchorMin, anchorMax);
        Image image = GetOrAddComponent<Image>(rectTransform.gameObject);
        GetOrAddComponent<CanvasRenderer>(rectTransform.gameObject);
        Button button = GetOrAddComponent<Button>(rectTransform.gameObject);
        image.color = backgroundColor;
        button.targetGraphic = image;

        RectTransform textRect = GetOrCreateChildRect(rectTransform, "Label", Vector2.zero, Vector2.one);
        Text text = GetOrAddComponent<Text>(textRect.gameObject);
        GetOrAddComponent<CanvasRenderer>(textRect.gameObject);
        text.font = font;
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        text.raycastTarget = false;

        return button;
    }

    private bool MoveCardBetweenZones(PrepareCardZoneType fromZone, PrepareCardZoneType targetZone, int cardId)
    {
        List<CardBuildCardData> fromList = GetZoneList(fromZone);
        List<CardBuildCardData> targetList = GetZoneList(targetZone);
        if (fromList == null || targetList == null)
        {
            return false;
        }

        return MoveCardById(fromList, targetList, cardId);
    }

    private List<CardBuildCardData> GetZoneList(PrepareCardZoneType zoneType)
    {
        if (zoneType == PrepareCardZoneType.Deployed)
        {
            return _deployedCards;
        }

        return _ownedCards;
    }

    private static bool MoveCardById(List<CardBuildCardData> from, List<CardBuildCardData> to, int cardId)
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
            return true;
        }

        return false;
    }

    private static void RemoveCardById(List<CardBuildCardData> cards, int cardId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Id != cardId)
            {
                continue;
            }

            cards.RemoveAt(i);
            return;
        }
    }

    private void ClearChildren(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(root.GetChild(i).gameObject);
        }
    }

    private int ResolveEnemyCount(int levelId)
    {
        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        if (battleCampaignRuntime == null)
        {
            return 1;
        }

        return battleCampaignRuntime.GetEnemyCountForBattle(levelId);
    }

    private static string ResolveEnemyName(int enemyUnitId)
    {
        switch (enemyUnitId)
        {
            case 1:
                return "敌方侦察猫";
            case 2:
                return "敌方突击猫";
            case 3:
                return "敌方精英猫";
            default:
                return $"敌方兵种 {enemyUnitId}";
        }
    }

    private bool IsOutingCard(int cardId)
    {
        return _outingCardIds.Contains(cardId);
    }

    private bool IsBlessingCard(int cardId)
    {
        return _blessingCardIds.Contains(cardId);
    }

    private int[] BuildBlessingCardIdSnapshot()
    {
        List<int> deployedBlessingIds = new List<int>();
        for (int i = 0; i < _deployedCards.Count; i++)
        {
            if (_blessingCardIds.Contains(_deployedCards[i].Id))
            {
                deployedBlessingIds.Add(_deployedCards[i].Id);
            }
        }

        return deployedBlessingIds.ToArray();
    }

    private static Color ResolveCardBackgroundColor(PrepareCardZoneType zoneType, bool isOutingCard, bool isBlessingCard)
    {
        if (zoneType == PrepareCardZoneType.Deployed)
        {
            return isBlessingCard
                ? new Color(0.73f, 0.59f, 0.17f, 0.95f)
                : new Color(0.22f, 0.5f, 0.3f, 0.95f);
        }

        if (isOutingCard)
        {
            return new Color(0.58f, 0.39f, 0.12f, 0.95f);
        }

        if (isBlessingCard)
        {
            return new Color(0.66f, 0.52f, 0.16f, 0.95f);
        }

        return new Color(0.19f, 0.29f, 0.47f, 0.95f);
    }

    private void SetStatusText(string message, bool overwrite)
    {
        if (_statusText == null)
        {
            return;
        }

        if (overwrite || string.IsNullOrEmpty(_statusText.text))
        {
            _statusText.text = message;
        }
    }

    private static Font LoadBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}