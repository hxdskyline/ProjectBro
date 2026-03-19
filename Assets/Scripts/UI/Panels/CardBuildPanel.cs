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
    [SerializeField] private RectTransform _reserveCardsRoot;
    [SerializeField] private Button _outingEntryButton;
    [SerializeField] private Button _blessingEntryButton;
    [SerializeField] private Button _attributeBoostEntryButton;
    [SerializeField] private Button _discardEntryButton;
    [SerializeField] private int _maxDeployedCards = 5;

    private int _currentLevel = 1;
    private readonly List<CardBuildCardData> _outingCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _blessingCandidates = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _attributeBoostCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _discardCards = new List<CardBuildCardData>();
    private readonly List<CardBuildCardData> _reserveCards = new List<CardBuildCardData>();
    private int _selectedBlessingCardId = -1;
    private bool _cardsInitialized;
    private bool _hasActiveSecondaryZone;
    private CardZoneType _activeSecondaryZone;
    private Font _uiFont;
    private OutingRewardConfig _outingRewardConfig;
    
    // 独立的强制赐福弹窗根节点，避免与其他二级界面共用导致的时序/排版冲突
    private RectTransform _forcedBlessingPopupRoot;
    private Text _forcedBlessingTitleText;
    private RectTransform _forcedBlessingListRoot;
    private Button _forcedBlessingCloseButton;
    private bool _forceBlessingSelectionActive = false;
    private bool _blessingReadonlyMode = false;
    // 新拆分的面板实例
    private OutingPanel _outingPanel;
    private BlessingPanel _blessingPanel;
    private AttributeBoostPanel _attributeBoostPanel;
    private DiscardPanel _discardPanel;

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
        EnsureReserveRootPlacement();
        HideLegacySecondaryZoneTexts();
        EnsureSecondaryEntryButtons();
        // 已拆分各二级面板为独立根，不再创建共享 SecondaryPopupRoot
        EnsureForcedBlessingPopup();

        // 初始化拆分出来的面板（Reserve 保持在 CardBuildPanel 内）
        _outingPanel = new OutingPanel();
        _outingPanel.Initialize(transform as RectTransform, _uiFont);

        // 绑定游历面板的确认与关闭回调
        _outingPanel.SetConfirmCallback(() =>
        {
            if (!HasOutingCards())
            {
                SetStatusText($"外出区域需要恰好 {RequiredOutingCardCount} 只猫咪才能确认游历。");
                return;
            }

            SetStatusText("已确认游历请求；结果将在战斗结算时处理。");
            RebuildCardViews();
            UpdateUI();
            CloseActiveSecondaryZone();
        });
        _outingPanel.SetCloseCallback(CloseActiveSecondaryZone);

        _blessingPanel = new BlessingPanel();
        _blessingPanel.Initialize(transform as RectTransform, _uiFont);
        _blessingPanel.SetCloseCallback(CloseActiveSecondaryZone);

        _attributeBoostPanel = new AttributeBoostPanel();
        _attributeBoostPanel.Initialize(transform as RectTransform, _uiFont);
        _attributeBoostPanel.SetConfirmCallback(() =>
        {
            int boosted = ApplyAttributeBoostRewards();
            RebuildCardViews();
            UpdateUI();
            if (boosted > 0) SetStatusText($"属性提升区强化了 {boosted} 张卡，已返回待选区");
            CloseActiveSecondaryZone();
        });
        _attributeBoostPanel.SetCloseCallback(CloseActiveSecondaryZone);

        _discardPanel = new DiscardPanel();
        _discardPanel.Initialize(transform as RectTransform, _uiFont);
        _discardPanel.SetConfirmCallback(OnSecondaryPopupConfirmButtonClicked);
        _discardPanel.SetCloseCallback(CloseActiveSecondaryZone);
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
        TryForceOpenBlessingForLevel();
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
        return _selectedBlessingCardId > 0;
    }

    public bool IsBlessingCandidateSelected(int cardId)
    {
        return _selectedBlessingCardId == cardId;
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
                rewardMessages.Add($"赐福效果已结束，本场祈祷区的 {blessingCardCount} 只猫恢复为未激活状态");
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

        if (fromZone == CardZoneType.Blessing || targetZone == CardZoneType.Blessing)
        {
            SetStatusText("赐福候选由系统随机提供，请直接点击赐福区中的猫咪切换激活对象。再次点击已激活的猫咪可取消选择。");
            RebuildCardViews();
            UpdateUI();
            return;
        }

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
        if (fromZone == CardZoneType.Blessing)
        {
            ToggleBlessingSelection(card.Id);
            RebuildCardViews();
            UpdateUI();
            return;
        }

        if (fromZone == CardZoneType.Reserve && _hasActiveSecondaryZone && _activeSecondaryZone == CardZoneType.Blessing)
        {
            SetStatusText("赐福区不接收拖入或点入操作，请直接点击赐福候选来决定谁进入祈祷区。");
            return;
        }

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
                $"待选: {GetVisibleReserveCardCount()}  外出: {_outingCards.Count}  赐福: {(HasBlessingCards() ? 1 : 0)}  强化: {_attributeBoostCards.Count}  弃猫: {_discardCards.Count}";
        }

        UpdateSecondaryEntryButtons();

        if (_statusText == null)
        {
            return;
        }

        if (_hasActiveSecondaryZone)
        {
            if (_activeSecondaryZone == CardZoneType.Blessing)
            {
                _statusText.text = "赐福区已打开。系统会随机展示 3 只候选猫，点击可激活或切换祈祷对象；再次点击当前激活猫可取消选择。";
            }
            else if (_activeSecondaryZone == CardZoneType.Discard)
            {
                _statusText.text = "弃猫区已打开。拖入这里只是暂存，点击确认按钮后才会永久删除；在确认前仍可拖回或点回待选区。";
            }
            else
            {
                _statusText.text = $"{GetZoneDisplayName(_activeSecondaryZone)}已打开。待选区单击会自动移入当前二级界面。";
            }

            return;
        }

        _statusText.text = _outingCards.Count == 1
            ? "外出区域需要恰好 2 只猫咪才会生效；当前数量不足，不会触发游历效果。"
            : HasBlessingCards()
                ? "祈祷区当前已激活 1 只猫；该猫在下一场战斗上阵时会获得一次性强化，并暂时从待选区隐藏。"
                : _discardCards.Count > 0
                    ? $"弃猫区里还有 {_discardCards.Count} 只待确认删除的猫；重新打开弃猫区后可继续处理，未确认前不会消失。"
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
            AvatarDefinitionAddress = GetInheritedAvatarDefinitionAddress(firstCat, secondCat),
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
        if (_reserveCardsRoot != null)
        {
            return;
        }

        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        _reserveCardsRoot = CreateRuntimeZone(panelRect, "ReserveCardsRoot", "待选区", new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.22f));
    }

    private void EnsureReserveRootPlacement()
    {
        if (_reserveCardsRoot == null)
        {
            return;
        }

        RectTransform currentParent = _reserveCardsRoot.parent as RectTransform;
        if (currentParent != null && (currentParent.name == "Left" || currentParent.name == "Right"))
        {
            RectTransform desiredParent = currentParent.parent as RectTransform;
            if (desiredParent != null)
            {
                _reserveCardsRoot.SetParent(desiredParent, false);
            }
        }

        _reserveCardsRoot.anchorMin = new Vector2(0.05f, 0.02f);
        _reserveCardsRoot.anchorMax = new Vector2(0.95f, 0.22f);
        _reserveCardsRoot.anchoredPosition = Vector2.zero;
        _reserveCardsRoot.sizeDelta = Vector2.zero;
        _reserveCardsRoot.SetAsLastSibling();
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
        SetupSecondaryButton(_outingEntryButton, CardZoneType.Outing, OutingEntryButtonName);
        SetupSecondaryButton(_blessingEntryButton, CardZoneType.Blessing, BlessingEntryButtonName);
        SetupSecondaryButton(_attributeBoostEntryButton, CardZoneType.AttributeBoost, AttributeBoostEntryButtonName);
        SetupSecondaryButton(_discardEntryButton, CardZoneType.Discard, DiscardEntryButtonName);
    }

    private void SetupSecondaryButton(Button button, CardZoneType zoneType, string buttonName)
    {
        if (button == null)
        {
            Debug.LogWarning($"[CardBuildPanel] {buttonName} 未在 Inspector 中绑定。请把预制体中的按钮拖到脚本字段上。");
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnSecondaryEntryButtonClicked(zoneType));
    }

    // 共享二级弹窗已移除，使用各自面板的根节点。

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
        VerticalLayoutGroup oldVertical = zoneRoot.GetComponent<VerticalLayoutGroup>();
        if (oldVertical != null)
        {
            Destroy(oldVertical);
        }

        HorizontalLayoutGroup layout = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = zoneRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.padding = new RectOffset(12, 12, 12, 12);
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
        _blessingCandidates.Clear();
        _attributeBoostCards.Clear();
        _discardCards.Clear();
        _reserveCards.Clear();
        _selectedBlessingCardId = -1;

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
                    AvatarDefinitionAddress = ReadString(cardJson, "avatarDefinitionAddress", string.Empty),
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
        // 防御性保证：在 Rebuild 前确保运行时根节点已创建，避免初始化顺序导致的 NullReference
        if (_reserveCardsRoot == null)
        {
            EnsureCardRoots();
        }


        if (_reserveCardsRoot != null)
        {
            ClearChildren(_reserveCardsRoot);
        }

        // 清理各面板列表根（如果存在）以便重新填充
        if (_forcedBlessingListRoot != null)
        {
            ClearChildren(_forcedBlessingListRoot);
        }

        if (_blessingPanel != null && _blessingPanel.ListRoot != null)
        {
            ClearChildren(_blessingPanel.ListRoot);
        }

        if (_outingPanel != null && _outingPanel.ListRoot != null)
        {
            ClearChildren(_outingPanel.ListRoot);
        }

        if (_attributeBoostPanel != null && _attributeBoostPanel.ListRoot != null)
        {
            ClearChildren(_attributeBoostPanel.ListRoot);
        }

        if (_discardPanel != null && _discardPanel.ListRoot != null)
        {
            ClearChildren(_discardPanel.ListRoot);
        }

        // 不再使用共享的 secondary popup root，渲染由各自面板接管

        for (int i = 0; i < _reserveCards.Count; i++)
        {
            if (ShouldHideReserveCard(_reserveCards[i].Id))
            {
                continue;
            }

            CreateCardItem(_reserveCardsRoot, _reserveCards[i], CardZoneType.Reserve);
        }

        if (_hasActiveSecondaryZone)
        {
            if (_activeSecondaryZone == CardZoneType.Blessing)
            {
                // 渲染赐福候选到 BlessingPanel 或 forced 列表
                if (_forceBlessingSelectionActive)
                {
                    // 优先把强制候选渲染到 BlessingPanel 的 ListRoot（如果已初始化并指向外部根）
                    if (_blessingPanel != null && _blessingPanel.ListRoot != null)
                    {
                        _blessingPanel.SetForceMode(true);
                        for (int i = 0; i < _blessingCandidates.Count; i++)
                        {
                            CreateCardItem(_blessingPanel.ListRoot, _blessingCandidates[i], CardZoneType.Blessing);
                        }
                    }
                    else if (_forcedBlessingListRoot != null)
                    {
                        for (int i = 0; i < _blessingCandidates.Count; i++)
                        {
                            CreateCardItem(_forcedBlessingListRoot, _blessingCandidates[i], CardZoneType.Blessing);
                        }
                    }
                }
                else
                {
                    if (_blessingPanel != null && _blessingPanel.ListRoot != null)
                    {
                        for (int i = 0; i < _blessingCandidates.Count; i++)
                        {
                            CreateCardItem(_blessingPanel.ListRoot, _blessingCandidates[i], CardZoneType.Blessing);
                        }
                    }
                    else
                    {
                        // BlessingPanel 未初始化，无法渲染赐福候选
                        SetStatusText("赐福面板未初始化，无法显示候选卡牌。");
                    }
                }
            }
            else if (_activeSecondaryZone == CardZoneType.Outing)
            {
                if (_outingPanel != null && _outingPanel.ListRoot != null)
                {
                    for (int i = 0; i < _outingCards.Count; i++)
                    {
                        CreateCardItem(_outingPanel.ListRoot, _outingCards[i], CardZoneType.Outing);
                    }
                }
                else
                {
                    // OutingPanel 未初始化，跳过渲染
                }
            }
            else if (_activeSecondaryZone == CardZoneType.AttributeBoost)
            {
                if (_attributeBoostPanel != null && _attributeBoostPanel.ListRoot != null)
                {
                    for (int i = 0; i < _attributeBoostCards.Count; i++)
                    {
                        CreateCardItem(_attributeBoostPanel.ListRoot, _attributeBoostCards[i], CardZoneType.AttributeBoost);
                    }
                }
                else
                {
                    // AttributeBoostPanel 未初始化，跳过渲染
                }
            }
            else if (_activeSecondaryZone == CardZoneType.Discard)
            {
                if (_discardPanel != null && _discardPanel.ListRoot != null)
                {
                    for (int i = 0; i < _discardCards.Count; i++)
                    {
                        CreateCardItem(_discardPanel.ListRoot, _discardCards[i], CardZoneType.Discard);
                    }
                }
                else
                {
                    // DiscardPanel 未初始化，跳过渲染
                }
            }
        }
    }

    private void CreateCardItem(RectTransform parent, CardBuildCardData cardData, CardZoneType zoneType)
    {
        // card sizing and fonts (used by both prefab and fallback paths)
        float cardWidth = zoneType == CardZoneType.Reserve ? 160f : 240f;
        float cardHeight = zoneType == CardZoneType.Reserve ? 160f : 140f;
        int nameFont = zoneType == CardZoneType.Reserve ? 18 : 24;
        int statsFont = zoneType == CardZoneType.Reserve ? 16 : 18;

        // Try to instantiate from editable prefab first (address: ui/carditem)
        GameObject cardGo = null;
        try
        {
            var rm = GameManager.Instance != null ? GameManager.Instance.ResourceManager : null;
            if (rm != null)
            {
                cardGo = rm.InstantiatePrefab("ui/carditem", parent);
            }
        }
        catch { }

        if (cardGo == null)
        {
            var prefab = Resources.Load<GameObject>("ui/carditem");
            if (prefab != null)
            {
                cardGo = Object.Instantiate(prefab, parent);
            }
        }

        if (cardGo != null)
        {
            cardGo.name = $"Card_{cardData.Id}";

            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(0f, 1f);
                cardRect.pivot = new Vector2(0f, 1f);
            }

            LayoutElement layoutElement = cardGo.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minWidth = cardWidth;
                layoutElement.minHeight = cardHeight;
                layoutElement.preferredWidth = cardWidth;
                layoutElement.preferredHeight = cardHeight;
            }

            if (cardRect != null)
            {
                cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            }

            Image cardBg = cardGo.GetComponent<Image>();
            if (cardBg != null)
            {
                cardBg.color = ResolveZoneCardColor(zoneType, cardData.Id);
            }

            // Portrait handling: try to find child named "Portrait" else create one
            Sprite portraitSprite = CardPortraitResolver.ResolvePortrait(cardData.AvatarDefinitionAddress);
            Transform portraitTf = cardGo.transform.Find("Portrait");
            if (portraitTf != null && portraitSprite != null)
            {
                var img = portraitTf.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.sprite = portraitSprite;
                    img.preserveAspect = true;
                    img.color = Color.white;
                    img.raycastTarget = false;
                }
            }
            else if (portraitSprite != null)
            {
                CreateCardPortrait(cardGo.transform, portraitSprite);
            }

            // Name / Stats text: try to find existing children
            Text nameText = null;
            Text statText = null;
            var nameTf = cardGo.transform.Find("Name");
            if (nameTf != null) nameText = nameTf.GetComponent<Text>();
            var statsTf = cardGo.transform.Find("Stats");
            if (statsTf != null) statText = statsTf.GetComponent<Text>();

            
            if (nameText == null) { nameText = CreateCardText(cardGo.transform, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), nameFont); }
            else { nameText.fontSize = nameFont; }
            if (statText == null) { statText = CreateCardText(cardGo.transform, "Stats", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), statsFont); }
            else { statText.fontSize = statsFont; }

            nameText.alignment = TextAnchor.UpperLeft;
            statText.alignment = TextAnchor.LowerLeft;

            CardDragItem dragItem = cardGo.GetComponent<CardDragItem>();
            if (dragItem != null)
            {
                dragItem.BindTextRefs(nameText, statText);
                dragItem.Initialize(this, cardData, zoneType);
            }

            return;
        }

        // Fallback to previous runtime construction if prefab not available
        GameObject fallback = new GameObject($"Card_{cardData.Id}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement),
            typeof(CanvasGroup),
            typeof(CardDragItem));
        fallback.transform.SetParent(parent, false);

        RectTransform fbRect = fallback.GetComponent<RectTransform>();
        fbRect.anchorMin = new Vector2(0f, 1f);
        fbRect.anchorMax = new Vector2(0f, 1f);
        fbRect.pivot = new Vector2(0f, 1f);

        LayoutElement fbLayout = fallback.GetComponent<LayoutElement>();
        fbLayout.minWidth = cardWidth;
        fbLayout.minHeight = cardHeight;
        fbLayout.preferredWidth = cardWidth;
        fbLayout.preferredHeight = cardHeight;
        fbRect.sizeDelta = new Vector2(cardWidth, cardHeight);

        Image fbBg = fallback.GetComponent<Image>();
        fbBg.color = ResolveZoneCardColor(zoneType, cardData.Id);

        Sprite fbPortrait = CardPortraitResolver.ResolvePortrait(cardData.AvatarDefinitionAddress);
        if (fbPortrait != null)
        {
            CreateCardPortrait(fallback.transform, fbPortrait);
        }

        Text fbNameText = CreateCardText(fallback.transform, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -8f), nameFont);
        fbNameText.alignment = TextAnchor.UpperLeft;

        Text fbStatText = CreateCardText(fallback.transform, "Stats", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), statsFont);
        fbStatText.alignment = TextAnchor.LowerLeft;

        CardDragItem fbDragItem = fallback.GetComponent<CardDragItem>();
        fbDragItem.BindTextRefs(fbNameText, fbStatText);
        fbDragItem.Initialize(this, cardData, zoneType);
    }

    private static void CreateCardPortrait(Transform parent, Sprite portraitSprite)
    {
        GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGo.transform.SetParent(parent, false);
        portraitGo.transform.SetAsFirstSibling();

        RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        // reduce padding so portrait fills more of the card
        portraitRect.offsetMin = new Vector2(4f, 4f);
        portraitRect.offsetMax = new Vector2(-4f, -4f);

        Image portraitImage = portraitGo.GetComponent<Image>();
        portraitImage.sprite = portraitSprite;
        portraitImage.preserveAspect = true;
        portraitImage.color = new Color(1f, 1f, 1f, 1f);
        portraitImage.raycastTarget = false;
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
        // increase text area height for larger cards
        textRect.sizeDelta = new Vector2(-12f, 40f);

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
            if (_hasActiveSecondaryZone && _activeSecondaryZone == CardZoneType.Blessing)
            {
                return CardZoneType.Reserve;
            }

            return _hasActiveSecondaryZone ? _activeSecondaryZone : CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.Outing)
        {
            return CardZoneType.Reserve;
        }

        if (fromZone == CardZoneType.Blessing)
        {
            return CardZoneType.Blessing;
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
            return _blessingCandidates;
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
        int blessingCardCount = HasBlessingCards() ? 1 : 0;
        if (blessingCardCount == 0 && _blessingCandidates.Count == 0)
        {
            return 0;
        }

        _selectedBlessingCardId = -1;
        _blessingCandidates.Clear();
        return blessingCardCount;
    }

    private int ConfirmDiscardCards()
    {
        int discardCardCount = _discardCards.Count;
        if (discardCardCount == 0)
        {
            return 0;
        }

        _discardCards.Clear();
        return discardCardCount;
    }

    private int ReturnDiscardCardsToReserve()
    {
        int discardCardCount = _discardCards.Count;
        if (discardCardCount == 0)
        {
            return 0;
        }

        _reserveCards.AddRange(_discardCards);
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
            _outingCards.Count + _attributeBoostCards.Count + _reserveCards.Count);

        ownedCards.AddRange(_outingCards);
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
        if (!HasBlessingCards())
        {
            return new int[0];
        }

        return new[] { _selectedBlessingCardId };
    }

    private Color ResolveZoneCardColor(CardZoneType zoneType, int cardId)
    {
        if (zoneType == CardZoneType.Outing)
        {
            return new Color(0.48f, 0.34f, 0.14f, 0.92f);
        }

        if (zoneType == CardZoneType.Blessing)
        {
            if (cardId < 0)
            {
                return new Color(0.7f, 0.56f, 0.15f, 0.94f);
            }

            return _selectedBlessingCardId == cardId
                ? new Color(0.84f, 0.67f, 0.18f, 0.96f)
                : new Color(0.29f, 0.22f, 0.08f, 0.94f);
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
        if (_forceBlessingSelectionActive)
        {
            SetStatusText("当前正在进行强制祈福选择，请先完成该操作。");
            return;
        }
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
        // 打开对应功能面板（优先使用新拆分的面板）
        _activeSecondaryZone = zoneType;
        _hasActiveSecondaryZone = true;

        if (zoneType == CardZoneType.Blessing)
        {
            EnsureBlessingCandidates();
            if (_forceBlessingSelectionActive)
            {
                if (_blessingPanel != null)
                {
                    _blessingPanel.SetExternalRoot(_forcedBlessingPopupRoot);
                    _blessingPanel.SetForceMode(true);
                    _blessingPanel.Show(false);
                }
                else if (_forcedBlessingPopupRoot != null)
                {
                    _forcedBlessingPopupRoot.pivot = new Vector2(0.5f, 0.5f);
                    _forcedBlessingPopupRoot.anchorMin = new Vector2(0.5f, 0.5f);
                    _forcedBlessingPopupRoot.anchorMax = new Vector2(0.5f, 0.5f);
                    _forcedBlessingPopupRoot.sizeDelta = new Vector2(800f, 480f);
                    _forcedBlessingPopupRoot.anchoredPosition = Vector2.zero;
                    _forcedBlessingPopupRoot.gameObject.SetActive(true);
                }
            }
            else
            {
                if (_blessingPanel != null)
                {
                    // 使用 BlessingPanel 自身根显示（不再使用共享二级弹窗）
                    _blessingPanel.SetExternalRoot(null);
                    _blessingPanel.SetForceMode(false);
                    _blessingPanel.Show(_blessingReadonlyMode);
                }
                else
                {
                    SetStatusText("赐福面板未初始化，无法打开赐福界面。");
                    _hasActiveSecondaryZone = false;
                    return;
                }
            }
        }
        else if (zoneType == CardZoneType.Outing)
        {
            if (_outingPanel != null) _outingPanel.Show();
            else { SetStatusText("游历面板未初始化，无法打开游历界面。"); _hasActiveSecondaryZone = false; return; }
        }
        else if (zoneType == CardZoneType.AttributeBoost)
        {
            if (_attributeBoostPanel != null) _attributeBoostPanel.Show();
            else { SetStatusText("属性提升面板未初始化，无法打开属性提升界面。"); _hasActiveSecondaryZone = false; return; }
        }
        else if (zoneType == CardZoneType.Discard)
        {
            if (_discardPanel != null) _discardPanel.Show();
            else { SetStatusText("弃猫面板未初始化，无法打开弃猫界面。"); _hasActiveSecondaryZone = false; return; }
        }

        RebuildCardViews();
        UpdateUI();
    }

    private void CloseActiveSecondaryZone()
    {
        if (_forceBlessingSelectionActive)
        {
            // 强制祈福选择时不允许关闭界面
            SetStatusText("请先从候选中选择一只猫作为本次祈福对象。此操作不可跳过。");
            return;
        }
        bool wasDiscardZone = _hasActiveSecondaryZone && _activeSecondaryZone == CardZoneType.Discard;
        int returnedCardCount = wasDiscardZone ? ReturnDiscardCardsToReserve() : 0;

        // 隐藏对应面板并恢复状态
        if (_hasActiveSecondaryZone)
        {
            if (_activeSecondaryZone == CardZoneType.Blessing)
            {
                if (_blessingPanel != null) _blessingPanel.Hide();
                if (_forcedBlessingPopupRoot != null) _forcedBlessingPopupRoot.gameObject.SetActive(false);
            }
            else if (_activeSecondaryZone == CardZoneType.Outing)
            {
                if (_outingPanel != null) _outingPanel.Hide();
            }
            else if (_activeSecondaryZone == CardZoneType.AttributeBoost)
            {
                if (_attributeBoostPanel != null) _attributeBoostPanel.Hide();
            }
            else if (_activeSecondaryZone == CardZoneType.Discard)
            {
                if (_discardPanel != null) _discardPanel.Hide();
            }
        }

        _hasActiveSecondaryZone = false;
        _blessingReadonlyMode = false;
        RebuildCardViews();
        UpdateUI();

        if (wasDiscardZone && returnedCardCount > 0)
        {
            SetStatusText($"弃猫区已关闭，未确认删除的 {returnedCardCount} 只猫已全部返回待选区。");
        }
    }

    private void OnSecondaryPopupConfirmButtonClicked()
    {
        if (!_hasActiveSecondaryZone || _activeSecondaryZone != CardZoneType.Discard)
        {
            return;
        }

        int removedCardCount = ConfirmDiscardCards();
        RebuildCardViews();
        UpdateUI();

        if (removedCardCount > 0)
        {
            SetStatusText($"已确认弃猫，{removedCardCount} 只猫被永久删除。");
            return;
        }

        SetStatusText("弃猫区里没有待确认删除的猫。拖入后仍可在确认前拖回待选区。");
    }

    private void UpdateSecondaryEntryButtons()
    {
        UpdateSecondaryEntryButton(_outingEntryButton, CardZoneType.Outing, _outingCards.Count, $"限 {RequiredOutingCardCount} 只，不限公母");
        UpdateSecondaryEntryButton(_blessingEntryButton, CardZoneType.Blessing, HasBlessingCards() ? 1 : 0, "随机 3 选 1，点击切换");
        UpdateSecondaryEntryButton(_attributeBoostEntryButton, CardZoneType.AttributeBoost, _attributeBoostCards.Count, "战后永久强化");
        UpdateSecondaryEntryButton(_discardEntryButton, CardZoneType.Discard, _discardCards.Count, "确认后永久删除");
    }

    private void UpdateSecondaryEntryButton(Button button, CardZoneType zoneType, int count, string detailText)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text == null)
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
                ? ResolveZoneCardColor(zoneType, -1)
                : ResolveZoneCardColor(zoneType, -1) * new Color(1f, 1f, 1f, 0.9f);
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
            return "打开时会随机展示 3 只候选猫。点击候选可切换祈祷对象，再次点击当前激活猫可取消选择；激活后该猫会暂时从待选区隐藏。";
        }

        if (zoneType == CardZoneType.AttributeBoost)
        {
            return "与待选区双向拖拽。强化区中的猫会在战斗结算后获得永久属性提升。";
        }

        if (zoneType == CardZoneType.Discard)
        {
            return "与待选区双向拖拽。拖入弃猫区后只是暂存，确认按钮点击后才会永久删除；在确认前仍可把猫拖回或点回待选区。";
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

    private List<CardBuildCardData> GetActiveDisplayList(CardZoneType zoneType)
    {
        if (zoneType == CardZoneType.Blessing)
        {
            return _blessingCandidates;
        }

        return GetZoneList(zoneType);
    }

    private void EnsureBlessingCandidates()
    {
        if (_blessingCandidates.Count > 0)
        {
            return;
        }

        List<CardBuildCardData> candidatePool = new List<CardBuildCardData>(_reserveCards);
        ShuffleCards(candidatePool);

        int candidateCount = Mathf.Min(3, candidatePool.Count);
        for (int i = 0; i < candidateCount; i++)
        {
            _blessingCandidates.Add(candidatePool[i]);
        }
    }

    private void ToggleBlessingSelection(int cardId)
    {
        if (_blessingReadonlyMode)
        {
            SetStatusText("赐福已锁定：当前为观察模式，选中的猫不可切换。");
            return;
        }

        bool candidateExists = false;
        for (int i = 0; i < _blessingCandidates.Count; i++)
        {
            if (_blessingCandidates[i].Id == cardId)
            {
                candidateExists = true;
                break;
            }
        }

        if (!candidateExists)
        {
            SetStatusText("这只猫不在当前赐福候选中。请先打开赐福区刷新候选。");
            return;
        }

        if (_selectedBlessingCardId == cardId)
        {
            if (_forceBlessingSelectionActive)
            {
                // 强制选择期间不能取消
                SetStatusText("当前为强制祈福选择，无法取消选择，请选择其他猫或确认当前选择。完成选择后界面将自动关闭。");
                return;
            }

            _selectedBlessingCardId = -1;
            SetStatusText("已取消当前祈祷对象，待选区恢复完整显示。");
            return;
        }

        _selectedBlessingCardId = cardId;
        if (TryFindCardById(_blessingCandidates, cardId, out CardBuildCardData selectedCard))
        {
            SetStatusText($"当前祈祷对象已切换为 {selectedCard.Name}。这只猫会暂时从待选区隐藏，并在下场战斗上阵时获得赐福强化。");
        }

        if (_forceBlessingSelectionActive)
        {
            // 在强制模式下，玩家选择后立即结束该流程
            _forceBlessingSelectionActive = false;
            // 关闭强制模式的 UI（优先使用 BlessingPanel）并进入只读管理
            if (_blessingPanel != null)
            {
                _blessingPanel.SetForceMode(false);
                _blessingPanel.Hide();
            }
            else
            {
                if (_forcedBlessingCloseButton != null)
                {
                    _forcedBlessingCloseButton.interactable = true;
                }
                CloseForcedBlessingPopup();
            }
            _blessingReadonlyMode = true;
            RebuildCardViews();
            UpdateUI();
        }
    }

    private void TryForceOpenBlessingForLevel()
    {
        BattleCampaignRuntime runtime = GameManager.Instance != null ? GameManager.Instance.BattleCampaignRuntime : null;
        if (runtime == null)
        {
            return;
        }

        if (!runtime.IsBlessingEnabledForBattle(_currentLevel))
        {
            return;
        }

        // 如果已经有选中的赐福，则不重复弹出
        if (HasBlessingCards())
        {
            return;
        }
        // 准备候选并强制打开独立弹窗（不使用共享的二级弹窗 root）
        _blessingCandidates.Clear();
        EnsureBlessingCandidates();
        _selectedBlessingCardId = -1;
        _forceBlessingSelectionActive = true;
        // 确保二级面板状态反映为赐福区已打开，以便 RebuildCardViews 在弹窗里填充候选卡牌
        _activeSecondaryZone = CardZoneType.Blessing;
        _hasActiveSecondaryZone = true;

        EnsureForcedBlessingPopup();

        if (_forcedBlessingPopupRoot != null)
        {
            _forcedBlessingPopupRoot.pivot = new Vector2(0.5f, 0.5f);
            _forcedBlessingPopupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _forcedBlessingPopupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _forcedBlessingPopupRoot.sizeDelta = new Vector2(800f, 480f);
            _forcedBlessingPopupRoot.anchoredPosition = Vector2.zero;

            // 禁用关闭以防止跳过
            if (_forcedBlessingCloseButton != null)
            {
                _forcedBlessingCloseButton.interactable = false;
            }

            _forcedBlessingPopupRoot.gameObject.SetActive(true);
        }

        // 优先使用 BlessingPanel 来展示强制选择
        if (_blessingPanel != null)
        {
            _blessingPanel.SetExternalRoot(_forcedBlessingPopupRoot);
            _blessingPanel.SetForceMode(true);
            _blessingPanel.Show(false);
        }

        RebuildCardViews();

        SetStatusText("本关开启了祈福：请从下面三只候选猫中选择一只进行祈福（此操作不可跳过）。");
    }

    private bool ShouldHideReserveCard(int cardId)
    {
        return _selectedBlessingCardId == cardId;
    }

    private int GetVisibleReserveCardCount()
    {
        return _reserveCards.Count - (HasBlessingCards() ? 1 : 0);
    }

    private static void ShuffleCards(List<CardBuildCardData> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            CardBuildCardData temp = cards[i];
            cards[i] = cards[swapIndex];
            cards[swapIndex] = temp;
        }
    }

    private static bool TryFindCardById(List<CardBuildCardData> cards, int cardId, out CardBuildCardData result)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Id == cardId)
            {
                result = cards[i];
                return true;
            }
        }

        result = default;
        return false;
    }

    private static string GetInheritedAvatarDefinitionAddress(CardBuildCardData firstCard, CardBuildCardData secondCard)
    {
        bool firstHasAddress = !string.IsNullOrEmpty(firstCard.AvatarDefinitionAddress);
        bool secondHasAddress = !string.IsNullOrEmpty(secondCard.AvatarDefinitionAddress);

        if (firstHasAddress && secondHasAddress)
        {
            return Random.value < 0.5f ? firstCard.AvatarDefinitionAddress : secondCard.AvatarDefinitionAddress;
        }

        if (firstHasAddress)
        {
            return firstCard.AvatarDefinitionAddress;
        }

        if (secondHasAddress)
        {
            return secondCard.AvatarDefinitionAddress;
        }

        return string.Empty;
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
        // Do not add a specific layout group here to avoid race conditions.
        // Clear existing layout groups so OpenSecondaryZone can set the appropriate one (horizontal for Blessing, vertical otherwise).
        HorizontalLayoutGroup oldHorizontal = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (oldHorizontal != null)
        {
            Object.Destroy(oldHorizontal);
        }

        VerticalLayoutGroup oldVertical = zoneRoot.GetComponent<VerticalLayoutGroup>();
        if (oldVertical != null)
        {
            Object.Destroy(oldVertical);
        }
    }

    private void EnsureForcedBlessingPopup()
    {
        if (_forcedBlessingPopupRoot != null)
        {
            return;
        }

        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        _forcedBlessingPopupRoot = GetOrCreateChildRect(panelRect, "ForcedBlessingPopupRoot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        Image popupBg = GetOrAddComponent<Image>(_forcedBlessingPopupRoot.gameObject);
        popupBg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

        _forcedBlessingTitleText = GetOrCreateText(_forcedBlessingPopupRoot, "Title", _uiFont, 28, TextAnchor.UpperCenter, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.97f), Color.white);
        Text hint = GetOrCreateText(_forcedBlessingPopupRoot, "Hint", _uiFont, 16, TextAnchor.UpperCenter, new Vector2(0.02f, 0.74f), new Vector2(0.98f, 0.84f), new Color(0.94f, 0.9f, 0.72f, 1f));
        hint.text = "请选择一只猫作为本次祈祷对象（不可跳过）";

        _forcedBlessingListRoot = GetOrCreateChildRect(_forcedBlessingPopupRoot, "List", new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.72f));
        Image listBg = GetOrAddComponent<Image>(_forcedBlessingListRoot.gameObject);
        listBg.color = new Color(0f, 0f, 0f, 0.12f);

        SecondaryZoneCardDropZone dropZone = GetOrAddComponent<SecondaryZoneCardDropZone>(_forcedBlessingListRoot.gameObject);
        dropZone.Initialize(this);

        // 强制弹窗始终使用横向布局（3 选 1）
        HorizontalLayoutGroup horiz = _forcedBlessingListRoot.GetComponent<HorizontalLayoutGroup>();
        if (horiz == null)
        {
            horiz = _forcedBlessingListRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        horiz.spacing = 24f;
        horiz.childAlignment = TextAnchor.MiddleCenter;
        horiz.childControlHeight = true;
        horiz.childControlWidth = false;
        horiz.childForceExpandHeight = false;
        horiz.childForceExpandWidth = false;
        horiz.padding = new RectOffset(10, 10, 10, 10);

        _forcedBlessingCloseButton = GetOrCreateButton(_forcedBlessingPopupRoot, "Close", "关闭", _uiFont, new Vector2(0.88f, 0.86f), new Vector2(0.98f, 0.96f), new Color(0.42f, 0.2f, 0.18f, 1f));
        _forcedBlessingCloseButton.onClick.RemoveAllListeners();
        _forcedBlessingCloseButton.onClick.AddListener(CloseForcedBlessingPopup);

        _forcedBlessingPopupRoot.gameObject.SetActive(false);
    }

    private void CloseForcedBlessingPopup()
    {
        if (_forceBlessingSelectionActive)
        {
            // 强制选择期间不允许关闭
            SetStatusText("请先从候选中选择一只猫作为本次祈福对象。此操作不可跳过。");
            return;
        }

        if (_forcedBlessingPopupRoot != null)
        {
            _forcedBlessingPopupRoot.gameObject.SetActive(false);
        }

        if (_blessingPanel != null)
        {
            _blessingPanel.SetForceMode(false);
            _blessingPanel.Hide();
            _blessingPanel.SetExternalRoot(null);
        }

        // 强制选择完成后，赐福区在后续被打开时应为只读观察模式
        _blessingReadonlyMode = true;
        RebuildCardViews();
        UpdateUI();
    }
}