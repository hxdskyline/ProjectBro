using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using LitJson;

/// <summary>
/// 卡牌构筑界面，支持上阵区和待选区之间自由拖拽。
/// </summary>
public class CardBuildPanel : UIPanel
{
    private const string CardConfigFileName = "card_build_cards.json";
    private const string OutingRewardConfigFileName = "outing_reward_config.json";
    private const int AttributeBoostAttackIncrease = 2;
    private const int AttributeBoostDefenseIncrease = 1;
    private const int AttributeBoostHpIncrease = 12;
    private const float AttributeBoostMoveSpeedIncrease = 0.1f;
    private const float AttributeBoostAttackRangeIncrease = 0.1f;

    private sealed class OutingRewardConfig
    {
        public string[] NamePrefixes;
        public OutingRewardRange Attack;
        public OutingRewardRange Defense;
        public OutingRewardRange Hp;
        public OutingRewardFloatRange MoveSpeed;
        public OutingRewardFloatRange AttackRange;
    }

    private sealed class OutingRewardRange
    {
        public int Min;
        public int Max;
    }

    private sealed class OutingRewardFloatRange
    {
        public float Min;
        public float Max;
    }

    [SerializeField] private Button _startBattleButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Text _levelText;
    [SerializeField] private Text _battleProgressText;
    [SerializeField] private Text _cardInfoText;
    [SerializeField] private Text _statusText;
    [SerializeField] private RectTransform _deployedCardsRoot;
    [SerializeField] private RectTransform _outingCardsRoot;
    [SerializeField] private RectTransform _attributeBoostCardsRoot;
    [SerializeField] private RectTransform _reserveCardsRoot;
    [SerializeField] private int _maxDeployedCards = 5;

    private int _currentLevel = 1;
    private readonly List<CardBuildCardData> _deployedCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _outingCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _attributeBoostCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _reserveCards = new List<CardBuildCardData>();
    private bool _cardsInitialized;
    private Font _uiFont;
    private OutingRewardConfig _outingRewardConfig;

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

        RefreshBattleProgress();

        _uiFont = LoadBuiltinFont();
        EnsureInfoTexts();
        EnsureCardRoots();
        EnsureDeployedDropZone(_deployedCardsRoot);
        EnsureOutingDropZone(_outingCardsRoot);
        EnsureAttributeBoostDropZone(_attributeBoostCardsRoot);
        EnsureReserveDropZone(_reserveCardsRoot);
        EnsureDeployedLayout(_deployedCardsRoot);
        EnsureOutingLayout(_outingCardsRoot);
        EnsureAttributeBoostLayout(_attributeBoostCardsRoot);
        EnsureReserveLayout(_reserveCardsRoot);
        CreateDemoCardsIfNeeded();
        RebuildCardViews();
        UpdateUI();

        Debug.Log("[CardBuildPanel] Initialized");
    }

    public override void Show()
    {
        base.Show();
        RefreshBattleProgress();
        RebuildCardViews();
        UpdateUI();
    }

    public bool HasOutingCards()
    {
        return _outingCards.Count > 0;
    }

    public bool HasAttributeBoostCards()
    {
        return _attributeBoostCards.Count > 0;
    }

    public void ResolveBattleEndZoneRewards(bool grantOutingReward, bool grantAttributeBoostReward)
    {
        List<string> rewardMessages = new List<string>();

        if (grantOutingReward)
        {
            CardBuildCardData randomCard = CreateRandomOutingRewardCard();
            _outingCards.Add(randomCard);
            rewardMessages.Add($"外出区域带回了新卡：{randomCard.Name}");
        }

        if (grantAttributeBoostReward)
        {
            int boostedCardCount = ApplyAttributeBoostRewards();
            if (boostedCardCount > 0)
            {
                rewardMessages.Add($"属性提升区域强化了 {boostedCardCount} 张卡，已返回待选区");
            }
        }

        if (gameObject.activeInHierarchy)
        {
            RebuildCardViews();
            UpdateUI();
        }

        if (rewardMessages.Count > 0)
        {
            SetStatusText(string.Join("  ", rewardMessages));
        }
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
            SetStatusText($"Deployed is full ({_maxDeployedCards}). Move one card out first.");

            RebuildCardViews();
            return;
        }

        if (!MoveCardBetweenZones(fromZone, targetZone, card.Id))
        {
            RebuildCardViews();
            return;
        }

        RebuildCardViews();
        UpdateUI();
    }

    public void HandleCardClick(CardBuildCardData card, CardZoneType fromZone)
    {
        CardZoneType targetZone = GetClickTargetZone(fromZone);

        if (targetZone == CardZoneType.Deployed && _deployedCards.Count >= _maxDeployedCards)
        {
            SetStatusText($"Deployed is full ({_maxDeployedCards}). Move one card out first.");
            return;
        }

        if (!MoveCardBetweenZones(fromZone, targetZone, card.Id))
        {
            return;
        }

        RebuildCardViews();
        UpdateUI();
    }

    private void UpdateUI()
    {
        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;

        if (_levelText != null)
        {
            int maxBattleCount = battleCampaignRuntime != null ? battleCampaignRuntime.MaxBattleCount : 10;
            _levelText.text = $"Battle: {_currentLevel}/{maxBattleCount}";
        }

        if (_battleProgressText != null)
        {
            int currentEnemyCount = battleCampaignRuntime != null
                ? battleCampaignRuntime.GetEnemyCountForBattle(_currentLevel)
                : 1;

            string nextBattleText;
            if (battleCampaignRuntime != null && battleCampaignRuntime.HasNextBattle)
            {
                int nextBattleNumber = battleCampaignRuntime.GetNextBattleNumber(_currentLevel);
                int nextEnemyCount = battleCampaignRuntime.GetEnemyCountForBattle(nextBattleNumber);
                nextBattleText = $"Next: Battle {nextBattleNumber}, Enemies {nextEnemyCount}";
            }
            else if (battleCampaignRuntime != null && battleCampaignRuntime.IsCompleted)
            {
                nextBattleText = "Next: Campaign completed for this run";
            }
            else
            {
                nextBattleText = "Next: Final battle ahead";
            }

            _battleProgressText.text =
                $"Current: Battle {_currentLevel}, Enemies {currentEnemyCount}\n" +
                nextBattleText;
        }

        if (_cardInfoText != null)
        {
            _cardInfoText.text =
                $"Deployed: {_deployedCards.Count}/{_maxDeployedCards}  " +
                $"外出: {_outingCards.Count}  强化: {_attributeBoostCards.Count}  Reserve: {_reserveCards.Count}";
        }

        SetStatusText("Drag cards between deployed, outing, boost, and reserve zones to adjust your lineup.");
    }

    private void SetStatusText(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
        }
    }

    private void OnStartBattleButtonClicked()
    {
        if (_deployedCards.Count == 0)
        {
            SetStatusText("Please deploy at least 1 card before starting battle.");
            return;
        }

        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        if (battleCampaignRuntime != null && battleCampaignRuntime.IsCompleted)
        {
            SetStatusText("All 10 battles are completed for this run. Restart the game to begin again.");
            return;
        }

        Debug.Log("[CardBuildPanel] Start Battle button clicked");

        GameManager.Instance.UIManager.HidePanel("ui/CardBuildPanel");

        BattlePanel battlePanel = GameManager.Instance.UIManager.ShowPanel<BattlePanel>("ui/BattlePanel", UIManager.UILayer.Normal);

        if (battlePanel != null)
        {
            battlePanel.StartBattle(_currentLevel, _deployedCards.ToArray(), HasOutingCards(), HasAttributeBoostCards());
        }
    }

    private void OnBackButtonClicked()
    {
        Debug.Log("[CardBuildPanel] Back button clicked");

        GameManager.Instance.UIManager.HidePanel("ui/CardBuildPanel");
        GameManager.Instance.UIManager.ShowPanel<MainPanel>("ui/MainPanel", UIManager.UILayer.Normal);
    }

    private void RefreshBattleProgress()
    {
        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        if (battleCampaignRuntime != null)
        {
            _currentLevel = battleCampaignRuntime.CurrentBattleNumber;
        }
    }

    private CardBuildCardData CreateRandomOutingRewardCard()
    {
        OutingRewardConfig config = GetOutingRewardConfig();
        int nextId = GetNextCardId();
        int attack = Random.Range(config.Attack.Min, config.Attack.Max + 1);
        int defense = Random.Range(config.Defense.Min, config.Defense.Max + 1);
        int hp = Random.Range(config.Hp.Min, config.Hp.Max + 1);
        float moveSpeed = Random.Range(config.MoveSpeed.Min, config.MoveSpeed.Max + 0.01f);
        float attackRange = Random.Range(config.AttackRange.Min, config.AttackRange.Max + 0.01f);

        return new CardBuildCardData
        {
            Id = nextId,
            Name = $"{GetRandomOutingRewardNamePrefix(config)}{nextId}",
            Gender = "未知",
            Attack = attack,
            Defense = defense,
            Hp = hp,
            MoveSpeed = moveSpeed,
            AttackRange = attackRange
        };
    }

    private OutingRewardConfig GetOutingRewardConfig()
    {
        if (_outingRewardConfig != null)
        {
            return _outingRewardConfig;
        }

        _outingRewardConfig = LoadOutingRewardConfig();
        return _outingRewardConfig;
    }

    private OutingRewardConfig LoadOutingRewardConfig()
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, OutingRewardConfigFileName);
        if (!File.Exists(configPath))
        {
            Debug.LogWarning($"[CardBuildPanel] Outing reward config file not found: {configPath}");
            return CreateFallbackOutingRewardConfig();
        }

        try
        {
            string jsonContent = File.ReadAllText(configPath);
            JsonData json = JsonMapper.ToObject(jsonContent);
            OutingRewardConfig config = new OutingRewardConfig
            {
                NamePrefixes = ReadStringArray(json, "namePrefixes"),
                Attack = ReadIntRange(json, "attack"),
                Defense = ReadIntRange(json, "defense"),
                Hp = ReadIntRange(json, "hp"),
                MoveSpeed = ReadFloatRange(json, "moveSpeed"),
                AttackRange = ReadFloatRange(json, "attackRange")
            };

            if (!IsValidOutingRewardConfig(config))
            {
                Debug.LogWarning($"[CardBuildPanel] Outing reward config format is invalid: {configPath}");
                return CreateFallbackOutingRewardConfig();
            }

            NormalizeOutingRewardConfig(config);
            return config;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[CardBuildPanel] Failed to load outing reward config: {exception.Message}");
            return CreateFallbackOutingRewardConfig();
        }
    }

    private static string GetRandomOutingRewardNamePrefix(OutingRewardConfig config)
    {
        if (config == null || config.NamePrefixes == null || config.NamePrefixes.Length == 0)
        {
            return "外出援军";
        }

        int index = Random.Range(0, config.NamePrefixes.Length);
        string prefix = config.NamePrefixes[index];
        return string.IsNullOrEmpty(prefix) ? "外出援军" : prefix;
    }

    private static bool IsValidOutingRewardConfig(OutingRewardConfig config)
    {
        return config != null &&
            config.NamePrefixes != null && config.NamePrefixes.Length > 0 &&
            config.Attack != null &&
            config.Defense != null &&
            config.Hp != null &&
            config.MoveSpeed != null &&
            config.AttackRange != null;
    }

    private static void NormalizeOutingRewardConfig(OutingRewardConfig config)
    {
        NormalizeIntRange(config.Attack);
        NormalizeIntRange(config.Defense);
        NormalizeIntRange(config.Hp);
        NormalizeFloatRange(config.MoveSpeed);
        NormalizeFloatRange(config.AttackRange);
    }

    private static void NormalizeIntRange(OutingRewardRange range)
    {
        if (range == null)
        {
            return;
        }

        range.Min = Mathf.Max(1, range.Min);
        range.Max = Mathf.Max(range.Min, range.Max);
    }

    private static void NormalizeFloatRange(OutingRewardFloatRange range)
    {
        if (range == null)
        {
            return;
        }

        range.Min = Mathf.Max(0.1f, range.Min);
        range.Max = Mathf.Max(range.Min, range.Max);
    }

    private static OutingRewardConfig CreateFallbackOutingRewardConfig()
    {
        OutingRewardConfig config = new OutingRewardConfig
        {
            NamePrefixes = new[] { "外出援军" },
            Attack = new OutingRewardRange { Min = 10, Max = 28 },
            Defense = new OutingRewardRange { Min = 2, Max = 8 },
            Hp = new OutingRewardRange { Min = 52, Max = 100 },
            MoveSpeed = new OutingRewardFloatRange { Min = 1.7f, Max = 3.0f },
            AttackRange = new OutingRewardFloatRange { Min = 0.9f, Max = 2.2f }
        };

        return config;
    }

    private int GetNextCardId()
    {
        int maxId = 0;
        maxId = Mathf.Max(maxId, GetMaxCardId(_deployedCards));
        maxId = Mathf.Max(maxId, GetMaxCardId(_outingCards));
        maxId = Mathf.Max(maxId, GetMaxCardId(_attributeBoostCards));
        maxId = Mathf.Max(maxId, GetMaxCardId(_reserveCards));
        return maxId + 1;
    }

    private static int GetMaxCardId(List<CardBuildCardData> cards)
    {
        int maxId = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            maxId = Mathf.Max(maxId, cards[i].Id);
        }

        return maxId;
    }

    private void EnsureInfoTexts()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        _battleProgressText = _battleProgressText ?? CreateRuntimeInfoText(
            panelRect,
            "BattleProgressText",
            new Vector2(0.05f, 0.86f),
            new Vector2(0.95f, 0.95f),
            16,
            TextAnchor.UpperLeft,
            new Color(0.88f, 0.94f, 1f, 1f));

        _cardInfoText = _cardInfoText ?? CreateRuntimeInfoText(
            panelRect,
            "LineupInfoText",
            new Vector2(0.05f, 0.80f),
            new Vector2(0.95f, 0.85f),
            15,
            TextAnchor.MiddleLeft,
            Color.white);

        _statusText = _statusText ?? CreateRuntimeInfoText(
            panelRect,
            "StatusText",
            new Vector2(0.05f, 0.74f),
            new Vector2(0.95f, 0.79f),
            14,
            TextAnchor.MiddleLeft,
            new Color(1f, 0.88f, 0.62f, 1f));
    }

    private Text CreateRuntimeInfoText(
        RectTransform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        GameObject textGo = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGo.GetComponent<Text>();
        text.font = _uiFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = string.Empty;

        return text;
    }

    private void EnsureCardRoots()
    {
        if (_deployedCardsRoot != null && _outingCardsRoot != null && _attributeBoostCardsRoot != null && _reserveCardsRoot != null)
        {
            return;
        }

        RectTransform panelRect = transform as RectTransform;
        _deployedCardsRoot = _deployedCardsRoot ?? CreateRuntimeZone(panelRect, "DeployedCardsRoot", "Deployed", new Vector2(0.05f, 0.11f), new Vector2(0.95f, 0.23f));
        _outingCardsRoot = _outingCardsRoot ?? CreateRuntimeZone(panelRect, "OutingCardsRoot", "外出区域", new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.38f));
        _attributeBoostCardsRoot = _attributeBoostCardsRoot ?? CreateRuntimeZone(panelRect, "AttributeBoostCardsRoot", "属性提升区域", new Vector2(0.05f, 0.41f), new Vector2(0.95f, 0.53f));
        _reserveCardsRoot = _reserveCardsRoot ?? CreateRuntimeZone(panelRect, "ReserveCardsRoot", "Reserve", new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.68f));
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

    private void EnsureOutingDropZone(RectTransform zoneRoot)
    {
        OutingCardDropZone dropZone = zoneRoot.GetComponent<OutingCardDropZone>();
        if (dropZone == null)
        {
            dropZone = zoneRoot.gameObject.AddComponent<OutingCardDropZone>();
        }

        dropZone.Initialize(this);

        if (zoneRoot.GetComponent<Image>() == null)
        {
            Image image = zoneRoot.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.08f);
        }
    }

    private void EnsureAttributeBoostDropZone(RectTransform zoneRoot)
    {
        AttributeBoostCardDropZone dropZone = zoneRoot.GetComponent<AttributeBoostCardDropZone>();
        if (dropZone == null)
        {
            dropZone = zoneRoot.gameObject.AddComponent<AttributeBoostCardDropZone>();
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

    private void EnsureOutingLayout(RectTransform zoneRoot)
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

    private void EnsureAttributeBoostLayout(RectTransform zoneRoot)
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
        _outingCards.Clear();
        _attributeBoostCards.Clear();
        _reserveCards.Clear();

        string cardConfigPath = Path.Combine(Application.streamingAssetsPath, CardConfigFileName);
        if (!File.Exists(cardConfigPath))
        {
            Debug.LogError($"[CardBuildPanel] Card config file not found: {cardConfigPath}");
            SetStatusText("Card config file is missing. Please check StreamingAssets.");
            return;
        }

        try
        {
            string jsonContent = File.ReadAllText(cardConfigPath);
            JsonData cardsJson = JsonMapper.ToObject(jsonContent);
            if (cardsJson == null || !cardsJson.IsArray)
            {
                Debug.LogError($"[CardBuildPanel] Card config file format is invalid: {cardConfigPath}");
                SetStatusText("Card config format is invalid.");
                return;
            }

            for (int i = 0; i < cardsJson.Count; i++)
            {
                JsonData cardJson = cardsJson[i];
                _reserveCards.Add(new CardBuildCardData
                {
                    Id = ReadInt(cardJson, "id", i + 1),
                    Name = ReadString(cardJson, "name", $"Card_{i + 1}"),
                    Gender = ReadString(cardJson, "gender", "未知"),
                    Attack = ReadInt(cardJson, "attack", 1),
                    Defense = ReadInt(cardJson, "defense", 0),
                    Hp = ReadInt(cardJson, "hp", 1),
                    MoveSpeed = ReadFloat(cardJson, "moveSpeed", 1f),
                    AttackRange = ReadFloat(cardJson, "attackRange", 1f)
                });
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[CardBuildPanel] Failed to load card config: {exception.Message}");
            SetStatusText("Failed to load card config.");
        }
    }

    private static int ReadInt(JsonData json, string key, int defaultValue)
    {
        if (json == null || !json.Keys.Contains(key))
        {
            return defaultValue;
        }

        return int.TryParse(json[key].ToString(), out int value) ? value : defaultValue;
    }

    private static float ReadFloat(JsonData json, string key, float defaultValue)
    {
        if (json == null || !json.Keys.Contains(key))
        {
            return defaultValue;
        }

        return float.TryParse(json[key].ToString(), out float value) ? value : defaultValue;
    }

    private static string ReadString(JsonData json, string key, string defaultValue)
    {
        if (json == null || !json.Keys.Contains(key))
        {
            return defaultValue;
        }

        string value = json[key].ToString();
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    private static string[] ReadStringArray(JsonData json, string key)
    {
        if (json == null || !json.Keys.Contains(key))
        {
            return null;
        }

        JsonData arrayJson = json[key];
        if (arrayJson == null || !arrayJson.IsArray)
        {
            return null;
        }

        List<string> values = new List<string>(arrayJson.Count);
        for (int i = 0; i < arrayJson.Count; i++)
        {
            string value = arrayJson[i]?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static OutingRewardRange ReadIntRange(JsonData json, string key)
    {
        if (json == null || !json.Keys.Contains(key))
        {
            return null;
        }

        JsonData rangeJson = json[key];
        if (rangeJson == null || !rangeJson.IsObject)
        {
            return null;
        }

        return new OutingRewardRange
        {
            Min = ReadInt(rangeJson, "min", 0),
            Max = ReadInt(rangeJson, "max", 0)
        };
    }

    private static OutingRewardFloatRange ReadFloatRange(JsonData json, string key)
    {
        if (json == null || !json.Keys.Contains(key))
        {
            return null;
        }

        JsonData rangeJson = json[key];
        if (rangeJson == null || !rangeJson.IsObject)
        {
            return null;
        }

        return new OutingRewardFloatRange
        {
            Min = ReadFloat(rangeJson, "min", 0f),
            Max = ReadFloat(rangeJson, "max", 0f)
        };
    }

    private void RebuildCardViews()
    {
        ClearChildren(_deployedCardsRoot);
        ClearChildren(_outingCardsRoot);
        ClearChildren(_attributeBoostCardsRoot);
        ClearChildren(_reserveCardsRoot);

        for (int i = 0; i < _deployedCards.Count; i++)
        {
            CreateCardItem(_deployedCardsRoot, _deployedCards[i], CardZoneType.Deployed);
        }

        for (int i = 0; i < _outingCards.Count; i++)
        {
            CreateCardItem(_outingCardsRoot, _outingCards[i], CardZoneType.Outing);
        }

        for (int i = 0; i < _attributeBoostCards.Count; i++)
        {
            CreateCardItem(_attributeBoostCardsRoot, _attributeBoostCards[i], CardZoneType.AttributeBoost);
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
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private CardZoneType GetClickTargetZone(CardZoneType fromZone)
    {
        if (fromZone == CardZoneType.Deployed)
        {
            return CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.Outing)
        {
            return CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.AttributeBoost)
        {
            return CardZoneType.Reserve;
        }

        return CardZoneType.Deployed;
    }

    private bool MoveCardBetweenZones(CardZoneType fromZone, CardZoneType targetZone, int cardId)
    {
        List<CardBuildCardData> fromList = GetZoneList(fromZone);
        List<CardBuildCardData> targetList = GetZoneList(targetZone);
        if (fromList == null || targetList == null)
        {
            return false;
        }

        return MoveCardById(fromList, targetList, cardId);
    }

    private List<CardBuildCardData> GetZoneList(CardZoneType zoneType)
    {
        if (zoneType == CardZoneType.Deployed)
        {
            return _deployedCards;
        }

        if (zoneType == CardZoneType.Outing)
        {
            return _outingCards;
        }

        if (zoneType == CardZoneType.AttributeBoost)
        {
            return _attributeBoostCards;
        }

        return _reserveCards;
    }

    private int ApplyAttributeBoostRewards()
    {
        int boostedCardCount = _attributeBoostCards.Count;
        if (boostedCardCount == 0)
        {
            return 0;
        }

        for (int i = 0; i < _attributeBoostCards.Count; i++)
        {
            CardBuildCardData card = _attributeBoostCards[i];
            card.Attack += AttributeBoostAttackIncrease;
            card.Defense += AttributeBoostDefenseIncrease;
            card.Hp += AttributeBoostHpIncrease;
            card.MoveSpeed = RoundCardFloat(card.MoveSpeed + AttributeBoostMoveSpeedIncrease);
            card.AttackRange = RoundCardFloat(card.AttackRange + AttributeBoostAttackRangeIncrease);
            _reserveCards.Add(card);
        }

        _attributeBoostCards.Clear();
        return boostedCardCount;
    }

    private static float RoundCardFloat(float value)
    {
        return Mathf.Round(value * 10f) / 10f;
    }

    private bool MoveCardById(List<CardBuildCardData> from, List<CardBuildCardData> to, int cardId)
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