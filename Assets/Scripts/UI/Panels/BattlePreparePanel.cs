using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TribeSystem;
using TribeSystem.UI;

/// <summary>
/// 战前准备界面 - 族群系统版本（含地形/天气/难度/出战消耗）
/// </summary>
public class BattlePreparePanel : UIPanel
{
    private const string RootName = "PrepareContentRoot";
    private const string TitleName = "PrepareTitleText";
    private const string SummaryName = "PrepareSummaryText";
    private const string StatusName = "PrepareStatusText";
    private const string TribesRootName = "TribesRoot";
    private const string EnemyRootName = "EnemyCardsRoot";
    private const string StartButtonName = "PrepareStartButton";
    private const string BackButtonName = "PrepareBackButton";
    private const string ScenarioTabsName = "ScenarioTabsRoot";
    private const string DifficultyTabsName = "DifficultyTabsRoot";
    private const string DeployCostName = "DeployCostText";
    private const string BuffPreviewName = "BuffPreviewText";
    private const string EnemyInfoName = "EnemyInfoText";

    private readonly List<TribeRecord> _deployedTribes = new List<TribeRecord>();
    private readonly Dictionary<int, int> _deployCatCounts = new Dictionary<int, int>();

    [SerializeField] private Text _titleText;
    [SerializeField] private Text _summaryText;
    [SerializeField] private Text _statusText;
    [SerializeField] private Button _startBattleButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private RectTransform _tribesRoot;
    [SerializeField] private RectTransform _enemyCardsRoot;
    private Font _uiFont;

    // New: scenario/difficulty/cost UI
    private RectTransform _scenarioTabsRoot;
    private RectTransform _difficultyTabsRoot;
    private Text _deployCostText;
    private Text _buffPreviewText;
    private Text _enemyInfoText;

    // State
    private int _currentLevel;
    private List<BattleScenarioOption> _scenarioOptions = new List<BattleScenarioOption>();
    private List<DifficultyLevel> _difficultyOptions = new List<DifficultyLevel>();
    private int _selectedScenarioIndex;
    private int _selectedDifficultyIndex;
    private int _freeDeployQuota;
    private int _currentCatFood;

    // Colors
    private static readonly Color TabActiveColor = new Color(0.2f, 0.6f, 0.3f, 0.95f);
    private static readonly Color TabInactiveColor = new Color(0.3f, 0.35f, 0.4f, 0.85f);

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

    /// <summary>
    /// 设置战斗准备 - 所有族群默认上阵
    /// </summary>
    public void SetupBattle(int levelId, List<TribeRecord> allTribes)
    {
        _currentLevel = levelId;

        _deployedTribes.Clear();
        _deployCatCounts.Clear();

        if (allTribes != null)
        {
            _deployedTribes.AddRange(allTribes);
            foreach (TribeRecord tribe in allTribes)
            {
                _deployCatCounts[tribe.tribeId] = Mathf.Min(GetCommandLimit(tribe), tribe.GetCatCount());
            }
        }

        // Load scenario/difficulty options from campaign config
        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;
        if (campaign != null)
        {
            _scenarioOptions = campaign.GetScenarioOptions(levelId);
            _difficultyOptions = campaign.GetDifficultyOptions(levelId);
            _freeDeployQuota = campaign.GetFreeDeployQuota(levelId);
        }

        _selectedScenarioIndex = 0;
        _selectedDifficultyIndex = 0;

        // Load current cat food
        DataManager dataManager = GameManager.Instance?.DataManager;
        _currentCatFood = dataManager != null ? (int)dataManager.GetCatFood() : 0;

        RefreshUI();
    }

    private void RefreshUI()
    {
        RefreshTexts();
        RebuildScenarioTabs();
        RebuildDifficultyTabs();
        RebuildTribeViews();
        RebuildEnemyViews();
        RefreshDeployCost();
        RefreshBuffPreview();
        RefreshEnemyInfo();
        RefreshStartButtonState();
    }

    private void RefreshTexts()
    {
        if (_titleText != null)
        {
            string levelTag = GetLevelTypeTag();
            _titleText.text = $"战前准备{levelTag} Battle {_currentLevel}";
        }

        int totalCatCount = GetTotalSelectedCatCount();
        int restingLeaderCount = 0;
        foreach (TribeRecord tribe in _deployedTribes)
        {
            if (tribe.leader?.restTurns > 0)
                restingLeaderCount++;
        }

        if (_summaryText != null)
        {
            _summaryText.text =
                $"出战族群: {_deployedTribes.Count}    " +
                $"小猫: {totalCatCount}    " +
                $"猫粮: {_currentCatFood}";
        }

        if (_statusText != null && string.IsNullOrEmpty(_statusText.text))
        {
            _statusText.text = "点击族群卡片进行上阵/下阵操作。族长休息中的族群无法上阵。";
        }
    }

    // --- Scenario Tabs ---

    private void RebuildScenarioTabs()
    {
        if (_scenarioTabsRoot == null) return;
        ClearChildren(_scenarioTabsRoot);

        if (_scenarioOptions == null || _scenarioOptions.Count == 0)
            return;

        // Configure horizontal layout
        HorizontalLayoutGroup hLayout = _scenarioTabsRoot.GetComponent<HorizontalLayoutGroup>();
        if (hLayout == null)
        {
            hLayout = GetOrAddComponent<HorizontalLayoutGroup>(_scenarioTabsRoot.gameObject);
            hLayout.spacing = 6f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlHeight = true;
            hLayout.childControlWidth = false;
            hLayout.childForceExpandHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.padding = new RectOffset(4, 4, 4, 4);
        }

        // Label
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        labelGo.transform.SetParent(_scenarioTabsRoot, false);
        Text label = labelGo.GetComponent<Text>();
        label.font = _uiFont;
        label.fontSize = 16;
        label.color = Color.white;
        label.text = "敌人情况:";
        label.alignment = TextAnchor.MiddleLeft;
        LayoutElement labelLe = labelGo.GetComponent<LayoutElement>();
        labelLe.preferredWidth = 80f;
        labelLe.minHeight = 30f;

        for (int i = 0; i < _scenarioOptions.Count; i++)
        {
            int capturedIndex = i;
            BattleScenarioOption option = _scenarioOptions[i];

            GameObject tabGo = new GameObject($"Scenario_{i}", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            tabGo.transform.SetParent(_scenarioTabsRoot, false);

            LayoutElement le = tabGo.GetComponent<LayoutElement>();
            le.preferredWidth = 140f;
            le.minHeight = 32f;

            Image bg = tabGo.GetComponent<Image>();
            bg.color = (i == _selectedScenarioIndex) ? TabActiveColor : TabInactiveColor;

            Button btn = tabGo.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => OnScenarioTabClicked(capturedIndex));

            // Tab text
            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(tabGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text tabText = textGo.GetComponent<Text>();
            tabText.font = _uiFont;
            tabText.fontSize = 15;
            tabText.color = Color.white;
            tabText.alignment = TextAnchor.MiddleCenter;
            tabText.text = $"{option.GetDisplayName()}\n{BattleScenarioOption.GetFormationName(option.formationType)}";
            tabText.raycastTarget = false;
        }
    }

    private void OnScenarioTabClicked(int index)
    {
        if (index == _selectedScenarioIndex) return;
        _selectedScenarioIndex = index;
        RefreshUI();
    }

    // --- Difficulty Tabs ---

    private void RebuildDifficultyTabs()
    {
        if (_difficultyTabsRoot == null) return;
        ClearChildren(_difficultyTabsRoot);

        if (_difficultyOptions == null || _difficultyOptions.Count == 0)
            return;

        HorizontalLayoutGroup hLayout = _difficultyTabsRoot.GetComponent<HorizontalLayoutGroup>();
        if (hLayout == null)
        {
            hLayout = GetOrAddComponent<HorizontalLayoutGroup>(_difficultyTabsRoot.gameObject);
            hLayout.spacing = 6f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlHeight = true;
            hLayout.childControlWidth = false;
            hLayout.childForceExpandHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.padding = new RectOffset(4, 4, 4, 4);
        }

        // Label
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        labelGo.transform.SetParent(_difficultyTabsRoot, false);
        Text label = labelGo.GetComponent<Text>();
        label.font = _uiFont;
        label.fontSize = 16;
        label.color = Color.white;
        label.text = "难度:";
        label.alignment = TextAnchor.MiddleLeft;
        LayoutElement labelLe = labelGo.GetComponent<LayoutElement>();
        labelLe.preferredWidth = 50f;
        labelLe.minHeight = 30f;

        for (int i = 0; i < _difficultyOptions.Count; i++)
        {
            int capturedIndex = i;
            DifficultyLevel diff = _difficultyOptions[i];

            GameObject tabGo = new GameObject($"Difficulty_{i}", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            tabGo.transform.SetParent(_difficultyTabsRoot, false);

            LayoutElement le = tabGo.GetComponent<LayoutElement>();
            le.preferredWidth = 80f;
            le.minHeight = 32f;

            Image bg = tabGo.GetComponent<Image>();
            bg.color = (i == _selectedDifficultyIndex) ? GetDifficultyActiveColor(diff) : TabInactiveColor;

            Button btn = tabGo.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => OnDifficultyTabClicked(capturedIndex));

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(tabGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text tabText = textGo.GetComponent<Text>();
            tabText.font = _uiFont;
            tabText.fontSize = 16;
            tabText.color = Color.white;
            tabText.alignment = TextAnchor.MiddleCenter;
            tabText.text = GetDifficultyName(diff);
            tabText.raycastTarget = false;
        }
    }

    private void OnDifficultyTabClicked(int index)
    {
        if (index == _selectedDifficultyIndex) return;
        _selectedDifficultyIndex = index;
        RefreshUI();
    }

    // --- Deploy Cost ---

    private void RefreshDeployCost()
    {
        if (_deployCostText == null) return;

        int totalCost = CalculateSliderTotalCost();
        int actualCost = DeployCostCalculator.CalculateActualCost(totalCost, _freeDeployQuota);
        bool canAfford = _currentCatFood >= actualCost;

        string text = $"出战消耗: {totalCost}猫粮  免费额度: {_freeDeployQuota}  " +
                       $"实际消耗: {actualCost}猫粮  (持有: {_currentCatFood})";

        if (!canAfford)
        {
            text += "  <color=#ff4444>猫粮不足!</color>";
        }

        _deployCostText.text = text;
    }

    private void RefreshStartButtonState()
    {
        if (_startBattleButton == null) return;

        int totalCost = CalculateSliderTotalCost();
        int actualCost = DeployCostCalculator.CalculateActualCost(totalCost, _freeDeployQuota);
        bool canAfford = _currentCatFood >= actualCost;

        _startBattleButton.interactable = canAfford;
    }

    private int CalculateSliderTotalCost()
    {
        int total = 0;
        foreach (var tribe in _deployedTribes)
        {
            int count = _deployCatCounts.TryGetValue(tribe.tribeId, out int c) ? c : 0;
            total += GetDeployCostPerCat(tribe.tribeType) * count;
        }
        return total;
    }

    private int GetTotalSelectedCatCount()
    {
        int total = 0;
        foreach (var c in _deployCatCounts.Values)
            total += c;
        return total;
    }

    private static int GetDeployCostPerCat(TribeType tribeType)
    {
        TribeConfig config = TribeConfigLoader.Instance?.GetTribeConfig(tribeType);
        if (config != null && config.deployCostPerCat > 0)
            return config.deployCostPerCat;

        switch (tribeType)
        {
            case TribeType.Siamese: return 12;
            case TribeType.Cow: return 8;
            default: return 10;
        }
    }

    private static int GetCommandLimit(TribeRecord tribe)
    {
        if (tribe.leader != null && tribe.leader.command > 0)
            return tribe.leader.command;
        return tribe.GetCatCount();
    }

    // --- Buff Preview ---

    private void RefreshBuffPreview()
    {
        if (_buffPreviewText == null) return;

        if (_scenarioOptions == null || _scenarioOptions.Count == 0 ||
            _selectedScenarioIndex >= _scenarioOptions.Count)
        {
            _buffPreviewText.text = "";
            return;
        }

        BattleScenarioOption selectedScenario = _scenarioOptions[_selectedScenarioIndex];

        string text = $"战场: {BattleScenarioOption.GetTerrainName(selectedScenario.terrain)} / " +
                       $"{BattleScenarioOption.GetWeatherName(selectedScenario.weather)}\n";

        if (_deployedTribes.Count == 0)
        {
            text += "上阵族群后显示 buff 预览";
        }
        else
        {
            foreach (TribeRecord tribe in _deployedTribes)
            {
                TerrainWeatherBuff buff = TribeBattleBuffProvider.GetBuff(
                    tribe.tribeType, selectedScenario.terrain, selectedScenario.weather);

                text += $"{GetTribeTypeName(tribe.tribeType)}: {buff.GetDescription()}\n";
            }
        }

        _buffPreviewText.text = text.TrimEnd('\n');
    }

    // --- Enemy Info ---

    private void RefreshEnemyInfo()
    {
        if (_enemyInfoText == null) return;

        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;
        if (campaign == null) return;

        DifficultyLevel selectedDifficulty = GetSelectedDifficulty();
        UnitStaticAttributes enemyStats = campaign.GetEnemyStats(_currentLevel, selectedDifficulty);
        int reward = campaign.GetCatFoodReward(_currentLevel, selectedDifficulty);

        string text = $"难度: {GetDifficultyName(selectedDifficulty)}  " +
                       $"敌人 ATK={enemyStats.Attack} DEF={enemyStats.Defense} HP={enemyStats.MaxHp}  " +
                       $"奖励: {reward}猫粮";

        _enemyInfoText.text = text;
    }

    // --- Tribe Views ---

    private void RebuildTribeViews()
    {
        ClearChildren(_tribesRoot);

        for (int i = 0; i < _deployedTribes.Count; i++)
        {
            CreateTribeCard(_tribesRoot, _deployedTribes[i]);
        }
    }

    private void RebuildEnemyViews()
    {
        ClearChildren(_enemyCardsRoot);

        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;
        if (campaign == null) return;

        // Determine formation type from selected scenario
        EnemyFormationType formation = EnemyFormationType.Single;
        if (_scenarioOptions != null && _selectedScenarioIndex < _scenarioOptions.Count)
        {
            formation = _scenarioOptions[_selectedScenarioIndex].formationType;
        }

        int[] enemyUnitIds = campaign.GetEnemyUnitIds(_currentLevel, formation);
        if (enemyUnitIds == null || enemyUnitIds.Length == 0)
        {
            enemyUnitIds = campaign.GetEnemyUnitIdsForBattle(_currentLevel);
        }

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

    private void CreateTribeCard(RectTransform parent, TribeRecord tribe)
    {
        bool isLeaderResting = IsLeaderResting(tribe);
        GameObject cardGo = new GameObject($"PrepareTribe_{tribe.tribeId}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        cardGo.transform.SetParent(parent, false);

        RectTransform cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0f, 1f);
        cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(0.5f, 1f);

        LayoutElement layoutElement = cardGo.GetComponent<LayoutElement>();
        layoutElement.minHeight = 155f;
        layoutElement.preferredHeight = 155f;
        layoutElement.flexibleWidth = 1f;

        Image cardBg = cardGo.GetComponent<Image>();
        cardBg.color = isLeaderResting
            ? new Color(0.4f, 0.25f, 0.25f, 0.95f)
            : GetTribeTypeColor(tribe.tribeType);

        CreateTribeCardContent(cardRect, tribe);
    }

    private void CreateTribeCardContent(RectTransform cardRect, TribeRecord tribe)
    {
        Text nameText = CreateCardText(cardRect.transform, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), 18);
        nameText.alignment = TextAnchor.UpperLeft;
        nameText.text = $"{GetTribeTypeName(tribe.tribeType)}族 (ID:{tribe.tribeId})";

        string statusInfo = $"族长: {(tribe.leader?.restTurns > 0 ? $"休息中({tribe.leader.restTurns}回)" : "可出战")}  小猫: {tribe.GetCatCount()}只";
        Text statText = CreateCardText(cardRect.transform, "Stats", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f), 14);
        statText.alignment = TextAnchor.LowerLeft;
        statText.text = statusInfo;

        if (tribe.leader != null)
        {
            Text detailText = CreateCardText(cardRect.transform, "Detail", new Vector2(0f, 0.5f), new Vector2(1f, 0.8f), new Vector2(10f, 0f), 12);
            detailText.alignment = TextAnchor.MiddleLeft;
            int costPerCat = 0;
            TribeConfig config = TribeConfigLoader.Instance?.GetTribeConfig(tribe.tribeType);
            if (config != null) costPerCat = config.deployCostPerCat;
            detailText.text = $"攻{tribe.leader.baseAttack} 防{tribe.leader.baseDefense} 血{tribe.leader.baseHp} 统{tribe.leader.command}  消耗{costPerCat}/猫";
        }

        // Slider row for controlling deploy count
        CreateSliderRow(cardRect, tribe);
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

    private void CreateSliderRow(RectTransform cardRect, TribeRecord tribe)
    {
        int commandLimit = GetCommandLimit(tribe);
        int maxSlider = Mathf.Min(commandLimit, tribe.GetCatCount());
        int current = _deployCatCounts.TryGetValue(tribe.tribeId, out int c) ? c : maxSlider;
        int costPerCat = GetDeployCostPerCat(tribe.tribeType);
        int tribeId = tribe.tribeId;

        // Slider row container
        GameObject rowGo = new GameObject("SliderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(cardRect, false);
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0.38f);
        rowRect.offsetMin = new Vector2(8f, 2f);
        rowRect.offsetMax = new Vector2(-8f, -2f);

        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // Count label
        GameObject countGo = new GameObject("CountLabel", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        countGo.transform.SetParent(rowGo.transform, false);
        LayoutElement countLe = countGo.GetComponent<LayoutElement>();
        countLe.preferredWidth = 80f;
        countLe.minWidth = 80f;
        Text countText = countGo.GetComponent<Text>();
        countText.font = _uiFont;
        countText.fontSize = 14;
        countText.color = new Color(0.9f, 0.95f, 1f, 1f);
        countText.alignment = TextAnchor.MiddleLeft;
        countText.text = $"猫:{current}/{maxSlider}";

        // Slider
        GameObject sliderGo = CreateSliderGo(rowGo.transform, maxSlider, current);
        LayoutElement sliderLe = sliderGo.GetComponent<LayoutElement>();
        if (sliderLe == null) sliderLe = sliderGo.AddComponent<LayoutElement>();
        sliderLe.preferredWidth = 140f;
        sliderLe.minHeight = 24f;
        sliderLe.flexibleWidth = 1f;

        Slider slider = sliderGo.GetComponent<Slider>();

        // Cost label
        GameObject costGo = new GameObject("SliderCost", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        costGo.transform.SetParent(rowGo.transform, false);
        LayoutElement costLe = costGo.GetComponent<LayoutElement>();
        costLe.preferredWidth = 90f;
        costLe.minWidth = 90f;
        Text costText = costGo.GetComponent<Text>();
        costText.font = _uiFont;
        costText.fontSize = 13;
        costText.color = new Color(0.85f, 0.75f, 0.2f, 1f);
        costText.alignment = TextAnchor.MiddleRight;
        costText.text = $"{costPerCat}粮/猫×{current}";

        // Slider callback
        slider.onValueChanged.AddListener((value) =>
        {
            int count = Mathf.RoundToInt(value);
            _deployCatCounts[tribeId] = count;
            countText.text = $"猫:{count}/{commandLimit}";
            costText.text = $"{costPerCat}粮/猫×{count}";
            RefreshDeployCost();
            RefreshTexts();
            RefreshStartButtonState();
        });
    }

    private GameObject CreateSliderGo(Transform parent, int max, int current)
    {
        // Root — anchor at center, sizeDelta set by LayoutElement via HLG
        GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(140f, 24f);
        Image sliderBg = sliderGo.GetComponent<Image>();
        sliderBg.color = new Color(0.15f, 0.15f, 0.15f, 0.5f); // visible for raycast

        // Background (track)
        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(sliderGo.transform, false);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Fill Area
        GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Fill
        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = new Color(0.25f, 0.65f, 0.35f, 0.9f);

        // Handle Slide Area
        GameObject handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform handleAreaRect = handleAreaGo.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        // Handle
        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        RectTransform handleRect = handleGo.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.sizeDelta = new Vector2(24f, 0f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        // Configure slider
        Slider slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = current;

        return sliderGo;
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

    // --- Battle Start ---

    private void OnStartBattleClicked()
    {
        if (_deployedTribes.Count == 0)
        {
            SetStatusText("至少需要上阵 1 个族群。", true);
            return;
        }


        foreach (TribeRecord tribe in _deployedTribes)
        {
            if (IsLeaderResting(tribe))
            {
                SetStatusText($"{GetTribeTypeName(tribe.tribeType)}族长正在休息，无法出战。", true);
                return;
            }
        }

        // Check deploy cost
        int totalCost = CalculateSliderTotalCost();
        int actualCost = DeployCostCalculator.CalculateActualCost(totalCost, _freeDeployQuota);
        if (_currentCatFood < actualCost)
        {
            SetStatusText($"猫粮不足! 需要 {actualCost} 猫粮，当前持有 {_currentCatFood}。", true);
            return;
        }

        // Deduct cat food
        if (actualCost > 0)
        {
            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager != null)
            {
                dataManager.TrySpendCatFood(actualCost);
                dataManager.SavePlayerData();
            }
        }

        // Get selected scenario and difficulty
        TerrainType terrain = TerrainType.Plain;
        WeatherType weather = WeatherType.Sunny;
        if (_scenarioOptions != null && _selectedScenarioIndex < _scenarioOptions.Count)
        {
            terrain = _scenarioOptions[_selectedScenarioIndex].terrain;
            weather = _scenarioOptions[_selectedScenarioIndex].weather;
        }

        DifficultyLevel difficulty = GetSelectedDifficulty();

        // Build filtered tribe list based on slider counts
        List<TribeRecord> filteredTribes = BuildFilteredTribeList();

        GameManager.Instance.UIManager.HidePanel("ui/BattlePreparePanel");

        BattlePanel battlePanel = GameManager.Instance.UIManager.ShowPanel<BattlePanel>("ui/BattlePanel", UIManager.UILayer.Normal);
        if (battlePanel != null)
        {
            battlePanel.StartBattle(_currentLevel, filteredTribes, terrain, weather, difficulty);
        }
    }

    private List<TribeRecord> BuildFilteredTribeList()
    {
        var result = new List<TribeRecord>();
        foreach (var tribe in _deployedTribes)
        {
            int selectedCount = _deployCatCounts.TryGetValue(tribe.tribeId, out int c) ? c : 0;

            var copy = new TribeRecord
            {
                tribeId = tribe.tribeId,
                tribeType = tribe.tribeType,
                leader = tribe.leader,
                moodId = tribe.moodId,
                isActive = tribe.isActive,
                cats = new List<CatData>()
            };

            if (tribe.cats != null && selectedCount > 0)
            {
                int take = Mathf.Min(selectedCount, tribe.cats.Count);
                for (int i = 0; i < take; i++)
                {
                    copy.cats.Add(tribe.cats[i]);
                }
            }

            result.Add(copy);
        }
        return result;
    }

    private void OnBackClicked()
    {
        GameManager.Instance.UIManager.HidePanel("ui/BattlePreparePanel");
        TribeBuildPanel tribeBuildPanel = GameManager.Instance.UIManager.GetPanel<TribeBuildPanel>("ui/TribeBuildPanel");
        if (tribeBuildPanel != null)
        {
            tribeBuildPanel.gameObject.SetActive(true);
        }
        else
        {
            GameManager.Instance.UIManager.ShowPanel<TribeBuildPanel>("ui/tribebuild/tribebuildpanel", UIManager.UILayer.Normal);
        }
    }

    // --- Layout ---

    private void EnsureRuntimeLayout()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null) return;

        Image backgroundImage = GetOrAddComponent<Image>(gameObject);
        backgroundImage.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);

        RectTransform contentRoot = panelRect.Find(RootName) as RectTransform;
        if (contentRoot == null)
        {
            contentRoot = GetOrCreateChildRect(panelRect, RootName, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));
            Image contentBackgroundFallback = GetOrAddComponent<Image>(contentRoot.gameObject);
            contentBackgroundFallback.color = new Color(0.87f, 0.91f, 0.96f, 0.98f);
            Debug.LogWarning("[BattlePreparePanel] Content root not found on prefab; created runtime fallback.");
        }

        if (_titleText == null)
        {
            var tf = contentRoot.Find(TitleName);
            if (tf != null) _titleText = tf.GetComponent<Text>();
        }
        if (_titleText == null)
        {
            _titleText = GetOrCreateText(contentRoot, TitleName, _uiFont, 42, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.9f), new Vector2(0.5f, 0.98f), new Color(0.1f, 0.15f, 0.25f, 1f));
        }

        if (_summaryText == null)
        {
            var tf = contentRoot.Find(SummaryName);
            if (tf != null) _summaryText = tf.GetComponent<Text>();
        }
        if (_summaryText == null)
        {
            _summaryText = GetOrCreateText(contentRoot, SummaryName, _uiFont, 20, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.84f), new Vector2(0.97f, 0.9f), new Color(0.15f, 0.19f, 0.26f, 1f));
        }

        // Scenario tabs row (between summary and status)
        if (_scenarioTabsRoot == null)
        {
            var tf = contentRoot.Find(ScenarioTabsName);
            if (tf != null) _scenarioTabsRoot = tf as RectTransform;
        }
        if (_scenarioTabsRoot == null)
        {
            _scenarioTabsRoot = GetOrCreateChildRect(contentRoot, ScenarioTabsName, new Vector2(0.03f, 0.78f), new Vector2(0.5f, 0.84f));
        }

        // Difficulty tabs row (right of scenario tabs)
        if (_difficultyTabsRoot == null)
        {
            var tf = contentRoot.Find(DifficultyTabsName);
            if (tf != null) _difficultyTabsRoot = tf as RectTransform;
        }
        if (_difficultyTabsRoot == null)
        {
            _difficultyTabsRoot = GetOrCreateChildRect(contentRoot, DifficultyTabsName, new Vector2(0.5f, 0.78f), new Vector2(0.97f, 0.84f));
        }

        // Status text
        if (_statusText == null)
        {
            var tf = contentRoot.Find(StatusName);
            if (tf != null) _statusText = tf.GetComponent<Text>();
        }
        if (_statusText == null)
        {
            _statusText = GetOrCreateText(contentRoot, StatusName, _uiFont, 18, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.78f), new Color(0.55f, 0.29f, 0.08f, 1f));
        }

        if (_tribesRoot == null)
        {
            _tribesRoot = contentRoot.Find(TribesRootName) as RectTransform;
            if (_tribesRoot == null)
            {
                _tribesRoot = CreateRuntimeZone(contentRoot, TribesRootName, "出战族群", new Vector2(0.03f, 0.3f), new Vector2(0.62f, 0.72f), new Color(0.13f, 0.23f, 0.39f, 0.72f));
            }
        }

        if (_enemyCardsRoot == null)
        {
            _enemyCardsRoot = contentRoot.Find(EnemyRootName) as RectTransform;
            if (_enemyCardsRoot == null)
            {
                _enemyCardsRoot = CreateRuntimeZone(contentRoot, EnemyRootName, "敌人列表", new Vector2(0.68f, 0.3f), new Vector2(0.97f, 0.72f), new Color(0.42f, 0.18f, 0.18f, 0.72f));
            }
        }

        // Deploy cost text
        if (_deployCostText == null)
        {
            var tf = contentRoot.Find(DeployCostName);
            if (tf != null) _deployCostText = tf.GetComponent<Text>();
        }
        if (_deployCostText == null)
        {
            _deployCostText = GetOrCreateText(contentRoot, DeployCostName, _uiFont, 16, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.24f), new Vector2(0.65f, 0.3f), new Color(0.85f, 0.75f, 0.2f, 1f));
        }

        // Buff preview text
        if (_buffPreviewText == null)
        {
            var tf = contentRoot.Find(BuffPreviewName);
            if (tf != null) _buffPreviewText = tf.GetComponent<Text>();
        }
        if (_buffPreviewText == null)
        {
            _buffPreviewText = GetOrCreateText(contentRoot, BuffPreviewName, _uiFont, 14, TextAnchor.UpperLeft, new Vector2(0.68f, 0.08f), new Vector2(0.97f, 0.3f), new Color(0.7f, 0.85f, 0.7f, 1f));
        }

        // Enemy info text
        if (_enemyInfoText == null)
        {
            var tf = contentRoot.Find(EnemyInfoName);
            if (tf != null) _enemyInfoText = tf.GetComponent<Text>();
        }
        if (_enemyInfoText == null)
        {
            _enemyInfoText = GetOrCreateText(contentRoot, EnemyInfoName, _uiFont, 15, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.16f), new Vector2(0.97f, 0.24f), new Color(0.8f, 0.5f, 0.5f, 1f));
        }

        if (_backButton == null)
        {
            var btTf = contentRoot.Find(BackButtonName);
            if (btTf != null) _backButton = btTf.GetComponent<Button>();
        }
        if (_backButton == null)
        {
            _backButton = GetOrCreateButton(contentRoot, BackButtonName, "返回构筑", _uiFont, new Vector2(0.03f, 0.04f), new Vector2(0.2f, 0.11f), new Color(0.35f, 0.36f, 0.4f, 1f));
        }

        if (_startBattleButton == null)
        {
            var btTf = contentRoot.Find(StartButtonName);
            if (btTf != null) _startBattleButton = btTf.GetComponent<Button>();
        }
        if (_startBattleButton == null)
        {
            _startBattleButton = GetOrCreateButton(contentRoot, StartButtonName, "进入战斗", _uiFont, new Vector2(0.79f, 0.04f), new Vector2(0.97f, 0.11f), new Color(0.16f, 0.43f, 0.29f, 1f));
        }
    }

    private void EnsureDropZones()
    {
    }

    private void EnsureZoneLayouts()
    {
        ConfigureVerticalLayout(_tribesRoot);
        ConfigureVerticalLayout(_enemyCardsRoot);
    }

    private static void ConfigureVerticalLayout(RectTransform zoneRoot)
    {
        if (zoneRoot == null) return;

        HorizontalLayoutGroup oldHorizontal = zoneRoot.GetComponent<HorizontalLayoutGroup>();
        if (oldHorizontal != null)
            Object.Destroy(oldHorizontal);

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

    // --- Utility ---

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
            component = gameObject.AddComponent<T>();
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
        RectTransform parent, string objectName, Font font, int fontSize,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Color color)
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
        RectTransform parent, string objectName, string label, Font font,
        Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor)
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

    private void ClearChildren(RectTransform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(root.GetChild(i).gameObject);
        }
    }

    private bool IsLeaderResting(TribeRecord tribe)
    {
        return tribe.leader != null && tribe.leader.restTurns > 0;
    }

    private DifficultyLevel GetSelectedDifficulty()
    {
        if (_difficultyOptions != null && _selectedDifficultyIndex < _difficultyOptions.Count)
            return _difficultyOptions[_selectedDifficultyIndex];
        return DifficultyLevel.Normal;
    }

    private string GetLevelTypeTag()
    {
        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;
        if (campaign == null) return "";

        switch (campaign.GetLevelType(_currentLevel))
        {
            case LevelType.Elite: return " [精英]";
            case LevelType.Boss: return " [Boss]";
            default: return "";
        }
    }

    private static string GetDifficultyName(DifficultyLevel diff)
    {
        switch (diff)
        {
            case DifficultyLevel.Normal: return "普通";
            case DifficultyLevel.Hard: return "困难";
            case DifficultyLevel.Bloodbath: return "血战";
            default: return diff.ToString();
        }
    }

    private static Color GetDifficultyActiveColor(DifficultyLevel diff)
    {
        switch (diff)
        {
            case DifficultyLevel.Normal: return new Color(0.2f, 0.6f, 0.3f, 0.95f);
            case DifficultyLevel.Hard: return new Color(0.7f, 0.5f, 0.1f, 0.95f);
            case DifficultyLevel.Bloodbath: return new Color(0.7f, 0.15f, 0.15f, 0.95f);
            default: return TabActiveColor;
        }
    }

    private Color GetTribeTypeColor(TribeType type)
    {
        switch (type)
        {
            case TribeType.Maine: return new Color(0.3f, 0.5f, 0.7f, 0.95f);
            case TribeType.Tabby: return new Color(0.6f, 0.4f, 0.3f, 0.95f);
            case TribeType.Orange: return new Color(0.7f, 0.5f, 0.2f, 0.95f);
            case TribeType.Cow: return new Color(0.4f, 0.4f, 0.5f, 0.95f);
            case TribeType.Siamese: return new Color(0.5f, 0.4f, 0.6f, 0.95f);
            case TribeType.Ragdoll: return new Color(0.7f, 0.5f, 0.6f, 0.95f);
            default: return new Color(0.5f, 0.5f, 0.5f, 0.95f);
        }
    }

    private string GetTribeTypeName(TribeType type)
    {
        switch (type)
        {
            case TribeType.Maine: return "缅因";
            case TribeType.Tabby: return "狸花";
            case TribeType.Orange: return "大橘";
            case TribeType.Cow: return "奶牛";
            case TribeType.Siamese: return "暹罗";
            case TribeType.Ragdoll: return "布偶";
            default: return type.ToString();
        }
    }

    private string ResolveEnemyName(int enemyUnitId)
    {
        switch (enemyUnitId)
        {
            case 1: return "敌方侦察猫";
            case 2: return "敌方突击猫";
            case 3: return "敌方精英猫";
            default: return $"敌方兵种 {enemyUnitId}";
        }
    }

    private void SetStatusText(string message, bool overwrite)
    {
        if (_statusText == null) return;
        if (overwrite || string.IsNullOrEmpty(_statusText.text))
            _statusText.text = message;
    }

    private static Font LoadBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
