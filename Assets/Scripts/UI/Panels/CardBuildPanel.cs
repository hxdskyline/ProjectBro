using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using LitJson;

/// <summary>
/// 卡牌构筑界面，负责待选、外出、赐福和属性提升区域的管理。
/// </summary>
public class CardBuildPanel : UIPanel
{
    private const string CardConfigFileName = "card_build_cards.json";
    private const string OutingRewardConfigFileName = "outing_reward_config.json";
    private const string SecondaryPopupRootName = "SecondaryPopupRoot";
    private const string SecondaryPopupTitleName = "SecondaryPopupTitle";
    private const string SecondaryPopupHintName = "SecondaryPopupHint";
    private const string SecondaryPopupListName = "SecondaryPopupList";
    private const string SecondaryPopupCloseButtonName = "SecondaryPopupCloseButton";
    private const string OutingEntryButtonName = "OutingEntryButton";
    private const string BlessingEntryButtonName = "BlessingEntryButton";
    private const string AttributeBoostEntryButtonName = "AttributeBoostEntryButton";
    private const string DiscardEntryButtonName = "DiscardEntryButton";
    private const int RequiredOutingCardCount = 2;
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
    [SerializeField] private RectTransform _outingCardsRoot;
    [SerializeField] private RectTransform _blessingCardsRoot;
    [SerializeField] private RectTransform _attributeBoostCardsRoot;
    [SerializeField] private RectTransform _discardCardsRoot;
    [SerializeField] private RectTransform _reserveCardsRoot;
    [SerializeField] private int _maxDeployedCards = 5;

    private int _currentLevel = 1;
    private readonly List<CardBuildCardData> _outingCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _blessingCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _attributeBoostCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _discardCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _reserveCards = new List<CardBuildCardData>();
    private bool _cardsInitialized;
    private bool _hasActiveSecondaryZone;
    private CardZoneType _activeSecondaryZone;
    private Font _uiFont;
    private OutingRewardConfig _outingRewardConfig;
    private Button _outingEntryButton;
    private Button _blessingEntryButton;
    private Button _attributeBoostEntryButton;
    private Button _discardEntryButton;
    private Text _outingEntryButtonText;
    private Text _blessingEntryButtonText;
    private Text _attributeBoostEntryButtonText;
    private Text _discardEntryButtonText;
    private RectTransform _secondaryPopupRoot;
    private Text _secondaryPopupTitleText;
    private Text _secondaryPopupHintText;
    private RectTransform _secondaryPopupListRoot;
    private Button _secondaryPopupCloseButton;

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
        HideLegacySecondaryZoneTexts();
        EnsureSecondaryEntryButtons();
        EnsureSecondaryPopup();
        EnsureReserveDropZone(_reserveCardsRoot);
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
        return _outingCards.Count == RequiredOutingCardCount;
    }

    public bool HasAttributeBoostCards()
    {
        return _attributeBoostCards.Count > 0;
    }

    public bool HasBlessingCards()
    {
        return _blessingCards.Count > 0;
    }

    public void ResolveBattleEndZoneRewards(bool grantOutingReward, bool grantAttributeBoostReward, bool consumeBlessingBuff)
    {
        List<string> rewardMessages = new List<string>();

        if (grantOutingReward)
        {
            CardBuildCardData rewardCard = CreateOutingRewardCardFromActivePair();
            _outingCards.Add(rewardCard);
            rewardMessages.Add($"外出区域带回了新卡：{rewardCard.Name}（{rewardCard.Gender}）");
        }

        if (grantAttributeBoostReward)
        {
            int boostedCardCount = ApplyAttributeBoostRewards();
            if (boostedCardCount > 0)
            {
                rewardMessages.Add($"属性提升区域强化了 {boostedCardCount} 张卡，已返回待选区");
            }
        }

        if (consumeBlessingBuff)
        {
            int blessingCardCount = ClearBlessingCards();
            if (blessingCardCount > 0)
            {
                rewardMessages.Add($"赐福区域的 {blessingCardCount} 张卡结束了临时强化，已返回待选区");
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

        if (IsSecondaryZone(targetZone) && (!TryGetActiveSecondaryZone(out CardZoneType activeZone) || activeZone != targetZone))
        {
            SetStatusText("请先打开对应的二级界面，再把猫咪拖进去。");
            RebuildCardViews();
            return;
        }

        if (targetZone == CardZoneType.Outing && _outingCards.Count >= RequiredOutingCardCount)
        {
            SetStatusText($"外出区域最多只能派出 {RequiredOutingCardCount} 只猫咪。");

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

        if (targetZone == fromZone)
        {
            return;
        }

        if (targetZone == CardZoneType.Outing && _outingCards.Count >= RequiredOutingCardCount)
        {
            SetStatusText($"外出区域最多只能派出 {RequiredOutingCardCount} 只猫咪。");
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
                $"待选: {_reserveCards.Count}  外出: {_outingCards.Count}  赐福: {_blessingCards.Count}  强化: {_attributeBoostCards.Count}  弃猫: {_discardCards.Count}";
        }

        UpdateSecondaryEntryButtons();
        UpdateSecondaryPopupState();

        if (_statusText == null)
        {
            return;
        }

        if (_hasActiveSecondaryZone)
        {
            _statusText.text = $"{GetZoneDisplayName(_activeSecondaryZone)}已打开。待选区单击会自动移入当前二级界面。";
            return;
        }

        _statusText.text = _outingCards.Count == 1
            ? "外出区域需要恰好 2 只猫咪才会生效；当前数量不足，不会触发游历效果。"
            : HasBlessingCards()
                ? "赐福区的猫会在下一场战斗获得一次性强化，战斗结束后返回待选区。"
                : _discardCards.Count > 0
                    ? "弃猫区中的猫会在窗口关闭时立刻消失。"
                    : "主界面默认只显示待选区，点击入口按钮打开游历、赐福、强化或弃猫二级界面。";
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
        BattleCampaignRuntime battleCampaignRuntime = GameManager.Instance.BattleCampaignRuntime;
        if (battleCampaignRuntime != null && battleCampaignRuntime.IsCompleted)
        {
            SetStatusText("All 10 battles are completed for this run. Restart the game to begin again.");
            return;
        }

        Debug.Log("[CardBuildPanel] Start Battle button clicked");

        CloseActiveSecondaryZone();

        GameManager.Instance.UIManager.HidePanel("ui/CardBuildPanel");

        BattlePreparePanel battlePreparePanel = GameManager.Instance.UIManager.ShowPanel<BattlePreparePanel>("ui/BattlePreparePanel", UIManager.UILayer.Normal);

        if (battlePreparePanel != null)
        {
            battlePreparePanel.SetupBattle(
                _currentLevel,
                BuildOwnedCardsSnapshot(),
                new CardBuildCardData[0],
                BuildOutingCardIdSnapshot(),
                BuildBlessingCardIdSnapshot(),
                HasOutingCards(),
                HasBlessingCards(),
                HasAttributeBoostCards(),
                _maxDeployedCards);
        }
    }

    private void OnBackButtonClicked()
    {
        Debug.Log("[CardBuildPanel] Back button clicked");

        CloseActiveSecondaryZone();

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

    private CardBuildCardData CreateOutingRewardCardFromActivePair()
    {
        OutingRewardConfig config = GetOutingRewardConfig();
        int nextId = GetNextCardId();
        CardBuildCardData firstCat = _outingCards[0];
        CardBuildCardData secondCat = _outingCards[1];

        float averageAttack = (firstCat.Attack + secondCat.Attack) * 0.5f;
        float averageDefense = (firstCat.Defense + secondCat.Defense) * 0.5f;
        float averageHp = (firstCat.Hp + secondCat.Hp) * 0.5f;
        float averageMoveSpeed = (firstCat.MoveSpeed + secondCat.MoveSpeed) * 0.5f;
        float averageAttackRange = (firstCat.AttackRange + secondCat.AttackRange) * 0.5f;

        string gender = GetRandomGender();
        float multiplier = gender == "母"
            ? 1.2f
            : Random.Range(0.8f, 1.6001f);

        return new CardBuildCardData
        {
            Id = nextId,
            Name = $"{GetRandomOutingRewardNamePrefix(config)}{nextId}",
            Gender = gender,
            Attack = Mathf.Max(1, Mathf.RoundToInt(averageAttack * multiplier)),
            Defense = Mathf.Max(0, Mathf.RoundToInt(averageDefense * multiplier)),
            Hp = Mathf.Max(1, Mathf.RoundToInt(averageHp * multiplier)),
            MoveSpeed = RoundCardFloat(Mathf.Max(0.1f, averageMoveSpeed * multiplier)),
            AttackRange = RoundCardFloat(Mathf.Max(0.1f, averageAttackRange * multiplier))
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
        maxId = Mathf.Max(maxId, GetMaxCardId(_outingCards));
        maxId = Mathf.Max(maxId, GetMaxCardId(_blessingCards));
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
        if (_outingCardsRoot != null && _blessingCardsRoot != null && _attributeBoostCardsRoot != null && _discardCardsRoot != null && _reserveCardsRoot != null)
        {
            return;
        }

        RectTransform panelRect = transform as RectTransform;
        _outingCardsRoot = _outingCardsRoot ?? CreateRuntimeZone(panelRect, "OutingCardsRoot", "外出区域", new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.32f));
        _blessingCardsRoot = _blessingCardsRoot ?? CreateRuntimeZone(panelRect, "BlessingCardsRoot", "赐福区域", new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.32f));
        _attributeBoostCardsRoot = _attributeBoostCardsRoot ?? CreateRuntimeZone(panelRect, "AttributeBoostCardsRoot", "属性提升区域", new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.52f));
        _discardCardsRoot = _discardCardsRoot ?? CreateRuntimeZone(panelRect, "DiscardCardsRoot", "弃猫区域", new Vector2(0.05f, 0.24f), new Vector2(0.28f, 0.34f));
        _reserveCardsRoot = _reserveCardsRoot ?? CreateRuntimeZone(panelRect, "ReserveCardsRoot", "待选区", new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.76f));
    }

    private void HideLegacySecondaryZoneTexts()
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            string value = texts[i].text;
            if (value == "游历区" || value == "外出区域" || value == "祈福区" || value == "赐福区" || value == "祈福区域" || value == "赐福区域" || value == "练功区" || value == "属性提升区域" || value == "弃猫区" || value == "弃猫区域")
            {
                texts[i].gameObject.SetActive(false);
            }
        }
    }

    private void EnsureSecondaryEntryButtons()
    {
        _outingEntryButton = EnsureSecondaryEntryButton(_outingCardsRoot, OutingEntryButtonName, out _outingEntryButtonText, new Color(0.45f, 0.31f, 0.12f, 0.96f));
        _blessingEntryButton = EnsureSecondaryEntryButton(_blessingCardsRoot, BlessingEntryButtonName, out _blessingEntryButtonText, new Color(0.64f, 0.52f, 0.14f, 0.96f));
        _attributeBoostEntryButton = EnsureSecondaryEntryButton(_attributeBoostCardsRoot, AttributeBoostEntryButtonName, out _attributeBoostEntryButtonText, new Color(0.2f, 0.35f, 0.6f, 0.96f));
        _discardEntryButton = EnsureSecondaryEntryButton(_discardCardsRoot, DiscardEntryButtonName, out _discardEntryButtonText, new Color(0.45f, 0.18f, 0.18f, 0.96f));

        if (_outingEntryButton != null)
        {
            _outingEntryButton.onClick.RemoveAllListeners();
            _outingEntryButton.onClick.AddListener(() => OnSecondaryEntryButtonClicked(CardZoneType.Outing));
        }

        if (_blessingEntryButton != null)
        {
            _blessingEntryButton.onClick.RemoveAllListeners();
            _blessingEntryButton.onClick.AddListener(() => OnSecondaryEntryButtonClicked(CardZoneType.Blessing));
        }

        if (_attributeBoostEntryButton != null)
        {
            _attributeBoostEntryButton.onClick.RemoveAllListeners();
            _attributeBoostEntryButton.onClick.AddListener(() => OnSecondaryEntryButtonClicked(CardZoneType.AttributeBoost));
        }

        if (_discardEntryButton != null)
        {
            _discardEntryButton.onClick.RemoveAllListeners();
            _discardEntryButton.onClick.AddListener(() => OnSecondaryEntryButtonClicked(CardZoneType.Discard));
        }
    }

    private Button EnsureSecondaryEntryButton(RectTransform root, string buttonName, out Text labelText, Color backgroundColor)
    {
        labelText = null;
        if (root == null)
        {
            return null;
        }

        Transform child = root.Find(buttonName);
        RectTransform buttonRect;
        if (child == null)
        {
            GameObject buttonGo = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(root, false);
            buttonRect = buttonGo.GetComponent<RectTransform>();
        }
        else
        {
            buttonRect = child as RectTransform;
        }

        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(220f, 84f);

        Image image = GetOrAddComponent<Image>(buttonRect.gameObject);
        image.color = backgroundColor;

        Button button = GetOrAddComponent<Button>(buttonRect.gameObject);
        button.targetGraphic = image;

        labelText = GetOrCreateButtonLabel(buttonRect, _uiFont, 22);
        return button;
    }

    private void EnsureSecondaryPopup()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        _secondaryPopupRoot = GetOrCreateChildRect(panelRect, SecondaryPopupRootName, new Vector2(0.53f, 0.08f), new Vector2(0.96f, 0.72f));
        Image popupBg = GetOrAddComponent<Image>(_secondaryPopupRoot.gameObject);
        popupBg.color = new Color(0.08f, 0.12f, 0.17f, 0.97f);

        _secondaryPopupTitleText = GetOrCreateText(_secondaryPopupRoot, SecondaryPopupTitleName, _uiFont, 26, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.88f), new Vector2(0.68f, 0.97f), Color.white);
        _secondaryPopupHintText = GetOrCreateText(_secondaryPopupRoot, SecondaryPopupHintName, _uiFont, 16, TextAnchor.UpperLeft, new Vector2(0.04f, 0.77f), new Vector2(0.96f, 0.87f), new Color(0.92f, 0.88f, 0.72f, 1f));
        _secondaryPopupListRoot = GetOrCreateChildRect(_secondaryPopupRoot, SecondaryPopupListName, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.72f));

        Image listBg = GetOrAddComponent<Image>(_secondaryPopupListRoot.gameObject);
        listBg.color = new Color(0f, 0f, 0f, 0.12f);

        SecondaryZoneCardDropZone dropZone = GetOrAddComponent<SecondaryZoneCardDropZone>(_secondaryPopupListRoot.gameObject);
        dropZone.Initialize(this);

        ConfigurePopupLayout(_secondaryPopupListRoot);

        _secondaryPopupCloseButton = GetOrCreateButton(_secondaryPopupRoot, SecondaryPopupCloseButtonName, "关闭", _uiFont, new Vector2(0.76f, 0.86f), new Vector2(0.96f, 0.96f), new Color(0.42f, 0.2f, 0.18f, 1f));
        _secondaryPopupCloseButton.onClick.RemoveAllListeners();
        _secondaryPopupCloseButton.onClick.AddListener(CloseActiveSecondaryZone);

        _secondaryPopupRoot.gameObject.SetActive(false);
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

    private void EnsureBlessingDropZone(RectTransform zoneRoot)
    {
        BlessingCardDropZone dropZone = zoneRoot.GetComponent<BlessingCardDropZone>();
        if (dropZone == null)
        {
            dropZone = zoneRoot.gameObject.AddComponent<BlessingCardDropZone>();
        }

        dropZone.Initialize(this);

        Image image = zoneRoot.GetComponent<Image>();
        if (image == null)
        {
            image = zoneRoot.gameObject.AddComponent<Image>();
        }

        image.color = new Color(0.62f, 0.5f, 0.12f, 0.18f);
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

    private void EnsureBlessingLayout(RectTransform zoneRoot)
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
        _outingCards.Clear();
        _blessingCards.Clear();
        _attributeBoostCards.Clear();
        _discardCards.Clear();
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
                    Gender = NormalizeGender(ReadString(cardJson, "gender", string.Empty)),
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

    private static string NormalizeGender(string gender)
    {
        if (gender == "公" || gender == "母")
        {
            return gender;
        }

        return GetRandomGender();
    }

    private static string GetRandomGender()
    {
        return Random.value < 0.5f ? "公" : "母";
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
        ClearChildren(_reserveCardsRoot);
        ClearChildren(_secondaryPopupListRoot);

        if (_secondaryPopupRoot != null)
        {
            _secondaryPopupRoot.gameObject.SetActive(_hasActiveSecondaryZone);
        }

        for (int i = 0; i < _reserveCards.Count; i++)
        {
            CreateCardItem(_reserveCardsRoot, _reserveCards[i], CardZoneType.Reserve);
        }

        if (_hasActiveSecondaryZone)
        {
            List<CardBuildCardData> activeList = GetZoneList(_activeSecondaryZone);
            for (int i = 0; i < activeList.Count; i++)
            {
                CreateCardItem(_secondaryPopupListRoot, activeList[i], _activeSecondaryZone);
            }
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
        cardBg.color = ResolveZoneCardColor(zoneType);

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
        if (fromZone == CardZoneType.Reserve)
        {
            return _hasActiveSecondaryZone ? _activeSecondaryZone : CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.Outing)
        {
            return CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.Blessing)
        {
            return CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.Discard)
        {
            return CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.AttributeBoost)
        {
            return CardZoneType.Reserve;
        }

        return CardZoneType.Reserve;
    }

    public bool TryGetActiveSecondaryZone(out CardZoneType zoneType)
    {
        zoneType = _activeSecondaryZone;
        return _hasActiveSecondaryZone && IsSecondaryZone(_activeSecondaryZone);
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
        if (zoneType == CardZoneType.Outing)
        {
            return _outingCards;
        }

        if (zoneType == CardZoneType.Blessing)
        {
            return _blessingCards;
        }

        if (zoneType == CardZoneType.Discard)
        {
            return _discardCards;
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

    private int ClearBlessingCards()
    {
        int blessingCardCount = _blessingCards.Count;
        if (blessingCardCount == 0)
        {
            return 0;
        }

        _reserveCards.AddRange(_blessingCards);
        _blessingCards.Clear();
        return blessingCardCount;
    }

    private int ClearDiscardCards()
    {
        int discardCardCount = _discardCards.Count;
        if (discardCardCount == 0)
        {
            return 0;
        }

        _discardCards.Clear();
        return discardCardCount;
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

    private CardBuildCardData[] BuildOwnedCardsSnapshot()
    {
        List<CardBuildCardData> ownedCards = new List<CardBuildCardData>(
            _outingCards.Count + _blessingCards.Count + _attributeBoostCards.Count + _reserveCards.Count);

        ownedCards.AddRange(_outingCards);
        ownedCards.AddRange(_blessingCards);
        ownedCards.AddRange(_attributeBoostCards);
        ownedCards.AddRange(_reserveCards);

        return ownedCards.ToArray();
    }

    private int[] BuildOutingCardIdSnapshot()
    {
        int[] outingCardIds = new int[_outingCards.Count];
        for (int i = 0; i < _outingCards.Count; i++)
        {
            outingCardIds[i] = _outingCards[i].Id;
        }

        return outingCardIds;
    }

    private int[] BuildBlessingCardIdSnapshot()
    {
        int[] blessingCardIds = new int[_blessingCards.Count];
        for (int i = 0; i < _blessingCards.Count; i++)
        {
            blessingCardIds[i] = _blessingCards[i].Id;
        }

        return blessingCardIds;
    }

    private static Color ResolveZoneCardColor(CardZoneType zoneType)
    {
        if (zoneType == CardZoneType.Outing)
        {
            return new Color(0.48f, 0.34f, 0.14f, 0.92f);
        }

        if (zoneType == CardZoneType.Blessing)
        {
            return new Color(0.7f, 0.56f, 0.15f, 0.94f);
        }

        if (zoneType == CardZoneType.AttributeBoost)
        {
            return new Color(0.22f, 0.39f, 0.68f, 0.92f);
        }

        if (zoneType == CardZoneType.Discard)
        {
            return new Color(0.62f, 0.21f, 0.21f, 0.94f);
        }

        return new Color(0.2f, 0.48f, 0.26f, 0.9f);
    }

    private void OnSecondaryEntryButtonClicked(CardZoneType zoneType)
    {
        if (_hasActiveSecondaryZone)
        {
            if (_activeSecondaryZone == zoneType)
            {
                CloseActiveSecondaryZone();
                return;
            }

            SetStatusText($"请先关闭当前打开的{GetZoneDisplayName(_activeSecondaryZone)}，再进入其他二级界面。");
            return;
        }

        OpenSecondaryZone(zoneType);
    }

    private void OpenSecondaryZone(CardZoneType zoneType)
    {
        _activeSecondaryZone = zoneType;
        _hasActiveSecondaryZone = true;
        RebuildCardViews();
        UpdateUI();
    }

    private void CloseActiveSecondaryZone()
    {
        int removedCardCount = 0;
        bool wasDiscardZone = _hasActiveSecondaryZone && _activeSecondaryZone == CardZoneType.Discard;
        if (wasDiscardZone)
        {
            removedCardCount = ClearDiscardCards();
        }

        _hasActiveSecondaryZone = false;
        RebuildCardViews();
        UpdateUI();

        if (wasDiscardZone && removedCardCount > 0)
        {
            SetStatusText($"弃猫区已关闭，{removedCardCount} 只猫已消失。");
        }
    }

    private void UpdateSecondaryEntryButtons()
    {
        UpdateSecondaryEntryButton(_outingEntryButton, _outingEntryButtonText, CardZoneType.Outing, _outingCards.Count, $"{RequiredOutingCardCount} 只生效");
        UpdateSecondaryEntryButton(_blessingEntryButton, _blessingEntryButtonText, CardZoneType.Blessing, _blessingCards.Count, "本场临时强化");
        UpdateSecondaryEntryButton(_attributeBoostEntryButton, _attributeBoostEntryButtonText, CardZoneType.AttributeBoost, _attributeBoostCards.Count, "战后永久强化");
        UpdateSecondaryEntryButton(_discardEntryButton, _discardEntryButtonText, CardZoneType.Discard, _discardCards.Count, "关闭时立刻消失");
    }

    private void UpdateSecondaryEntryButton(Button button, Text text, CardZoneType zoneType, int count, string detailText)
    {
        if (button == null || text == null)
        {
            return;
        }

        bool isActive = _hasActiveSecondaryZone && _activeSecondaryZone == zoneType;
        text.text = isActive
            ? $"关闭{GetZoneDisplayName(zoneType)}\n当前 {count} 只"
            : $"打开{GetZoneDisplayName(zoneType)}\n{detailText}  当前 {count}";

        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.color = isActive
                ? ResolveZoneCardColor(zoneType)
                : ResolveZoneCardColor(zoneType) * new Color(1f, 1f, 1f, 0.9f);
        }
    }

    private void UpdateSecondaryPopupState()
    {
        if (_secondaryPopupRoot == null)
        {
            return;
        }

        _secondaryPopupRoot.gameObject.SetActive(_hasActiveSecondaryZone);
        if (!_hasActiveSecondaryZone)
        {
            return;
        }

        if (_secondaryPopupTitleText != null)
        {
            _secondaryPopupTitleText.text = $"{GetZoneDisplayName(_activeSecondaryZone)}管理";
        }

        if (_secondaryPopupHintText != null)
        {
            _secondaryPopupHintText.text = GetZoneHintText(_activeSecondaryZone);
        }
    }

    private static string GetZoneDisplayName(CardZoneType zoneType)
    {
        if (zoneType == CardZoneType.Outing)
        {
            return "游历区";
        }

        if (zoneType == CardZoneType.Blessing)
        {
            return "赐福区";
        }

        if (zoneType == CardZoneType.AttributeBoost)
        {
            return "属性提升区";
        }

        if (zoneType == CardZoneType.Discard)
        {
            return "弃猫区";
        }

        return "待选区";
    }

    private static string GetZoneHintText(CardZoneType zoneType)
    {
        if (zoneType == CardZoneType.Outing)
        {
            return "与待选区双向拖拽。只有恰好 2 只猫时，游历效果才会在战斗结算时生效。";
        }

        if (zoneType == CardZoneType.Blessing)
        {
            return "与待选区双向拖拽。赐福区中的猫如果本场被上阵，会获得一次性强力 buff。";
        }

        if (zoneType == CardZoneType.AttributeBoost)
        {
            return "与待选区双向拖拽。强化区中的猫会在战斗结算后获得永久属性提升。";
        }

        if (zoneType == CardZoneType.Discard)
        {
            return "与待选区双向拖拽。拖入弃猫区的猫会在关闭这个窗口时立即永久消失。";
        }

        return string.Empty;
    }

    private static bool IsSecondaryZone(CardZoneType zoneType)
    {
        return zoneType == CardZoneType.Outing ||
            zoneType == CardZoneType.Blessing ||
            zoneType == CardZoneType.AttributeBoost ||
            zoneType == CardZoneType.Discard;
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

        Text text = GetOrCreateButtonLabel(rectTransform, font, 20);
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

    private static void ConfigurePopupLayout(RectTransform zoneRoot)
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
        layout.childForceExpandWidth = false;
        layout.padding = new RectOffset(10, 10, 10, 10);
    }
}