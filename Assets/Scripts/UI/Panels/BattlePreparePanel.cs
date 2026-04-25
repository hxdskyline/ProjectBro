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
    private const string EnemyRootName = "BattleOptionsRoot";
    private const string StartButtonName = "PrepareStartButton";
    private const string BackButtonName = "PrepareBackButton";

    private readonly List<TribeRecord> _deployedTribes = new List<TribeRecord>();

    [SerializeField] private Text _titleText;
    [SerializeField] private Text _summaryText;
    [SerializeField] private Text _statusText;
    [SerializeField] private Button _startBattleButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private RectTransform _tribesRoot;
    [SerializeField] private RectTransform _battleOptionsRoot;
    private Font _uiFont;

    // State
    private int _currentLevel;
    private int _currentCatFood;

    private class BattleChoice
    {
        public TerrainType terrain;
        public WeatherType weather;
        public DifficultyLevel difficulty;
        public int[] enemyUnitIds;
    }

    private List<BattleChoice> _battleChoices = new List<BattleChoice>();
    private int _selectedChoiceIndex = 0;

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

        if (allTribes != null)
        {
            _deployedTribes.AddRange(allTribes);
        }

        // Generate 2 random battle choices
        _battleChoices.Clear();
        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;

        // Ensure distinct terrain (currently 2 terrains: Plain=0, Brush=1)
        TerrainType terrain1 = (TerrainType)Random.Range(0, 2);
        TerrainType terrain2 = terrain1 == TerrainType.Plain ? TerrainType.Brush : TerrainType.Plain;

        // Ensure distinct weather (currently 4 weathers: Sunny=0, Rainy=1, Night=2, Windy=3)
        WeatherType weather1 = (WeatherType)Random.Range(0, 4);
        WeatherType weather2;
        do { weather2 = (WeatherType)Random.Range(0, 4); } while (weather1 == weather2);

        // Ensure distinct difficulty: one Normal (0), one Hard (1) or Bloodbath (2)
        DifficultyLevel diffEasy = DifficultyLevel.Normal;
        DifficultyLevel diffHard = Random.value < 0.5f ? DifficultyLevel.Hard : DifficultyLevel.Bloodbath;

        // Randomize which choice gets the harder difficulty
        bool swapDifficulty = Random.value < 0.5f;
        DifficultyLevel diff1 = swapDifficulty ? diffHard : diffEasy;
        DifficultyLevel diff2 = swapDifficulty ? diffEasy : diffHard;

        for (int i = 0; i < 2; i++)
        {
            var choice = new BattleChoice
            {
                terrain = i == 0 ? terrain1 : terrain2,
                weather = i == 0 ? weather1 : weather2,
                difficulty = i == 0 ? diff1 : diff2
            };

            if (campaign != null)
            {
                choice.enemyUnitIds = campaign.GetEnemyUnitIdsForBattle(_currentLevel);
            }
            else
            {
                choice.enemyUnitIds = new int[] { 1, 2, 3 };
            }

            _battleChoices.Add(choice);
        }

        _selectedChoiceIndex = 0;

        DataManager dataManager = GameManager.Instance?.DataManager;
        _currentCatFood = dataManager != null ? (int)dataManager.GetCatFood() : 0;

        RefreshUI();
    }

    private void RefreshUI()
    {
        RefreshTexts();
        RebuildTribeViews();
        RebuildBattleOptions();
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

        if (_summaryText != null)
        {
            _summaryText.text =
                $"出战族群: {_deployedTribes.Count}    " +
                $"小猫: {totalCatCount}    " +
                $"猫粮: {_currentCatFood}";
        }

        if (_statusText != null && string.IsNullOrEmpty(_statusText.text))
        {
            _statusText.text = "请在右侧选择一场战斗，然后点击进入战斗。";
        }
    }

    private int GetTotalSelectedCatCount()
    {
        int total = 0;
        foreach (var tribe in _deployedTribes)
        {
            total += Mathf.Min(GetCommandLimit(tribe), tribe.GetCatCount());
        }
        return total;
    }

    // --- Battle Options ---

    private void RebuildBattleOptions()
    {
        if (_battleOptionsRoot == null) return;
        ClearChildren(_battleOptionsRoot);

        for (int i = 0; i < _battleChoices.Count; i++)
        {
            int capturedIndex = i;
            BattleChoice choice = _battleChoices[i];

            GameObject btnGo = new GameObject($"BattleOption_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(_battleOptionsRoot, false);

            LayoutElement le = btnGo.GetComponent<LayoutElement>();
            le.preferredHeight = 120f;
            le.flexibleWidth = 1f;

            Image bg = btnGo.GetComponent<Image>();
            bg.color = (i == _selectedChoiceIndex) ? TabActiveColor : TabInactiveColor;

            Button btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => OnBattleOptionClicked(capturedIndex));

            // Content text
            string terrainName = BattleScenarioOption.GetTerrainName(choice.terrain);
            string weatherName = BattleScenarioOption.GetWeatherName(choice.weather);
            string diffName = GetDifficultyName(choice.difficulty);
            int enemyCount = choice.enemyUnitIds != null ? choice.enemyUnitIds.Length : 0;

            string contentStr = $"<size=24>战斗选项 {i + 1} ({diffName})</size>\n\n地形: {terrainName}    天气: {weatherName}\n敌人数量: {enemyCount}";

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.05f, 0.05f);
            textRect.anchorMax = new Vector2(0.95f, 0.95f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textGo.GetComponent<Text>();
            text.font = _uiFont;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = true;
            text.text = contentStr;
            text.raycastTarget = false;
        }
    }

    private void OnBattleOptionClicked(int index)
    {
        if (index == _selectedChoiceIndex) return;
        _selectedChoiceIndex = index;
        RefreshUI(); // Refresh UI to update tribe buff indicators
    }

    private void RefreshStartButtonState()
    {
        if (_startBattleButton == null) return;
        _startBattleButton.interactable = true;
    }

    private static int GetCommandLimit(TribeRecord tribe)
    {
        if (tribe.leader != null && tribe.leader.command > 0)
            return tribe.leader.command;
        return tribe.GetCatCount();
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

    private void CreateTribeCard(RectTransform parent, TribeRecord tribe)
    {
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
        cardBg.color = GetTribeTypeColor(tribe.tribeType);

        CreateTribeCardContent(cardRect, tribe);
    }

    private void CreateTribeCardContent(RectTransform cardRect, TribeRecord tribe)
    {
        Text nameText = CreateCardText(cardRect.transform, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), 18);
        nameText.alignment = TextAnchor.UpperLeft;
        nameText.text = $"{GetTribeTypeName(tribe.tribeType)}族 (ID:{tribe.tribeId})";

        string statusInfo = $"族长: 可出战  小猫: {tribe.GetCatCount()}只";
        Text statText = CreateCardText(cardRect.transform, "Stats", new Vector2(0f, 0.38f), new Vector2(1f, 0.55f), new Vector2(10f, 0f), 14);
        statText.alignment = TextAnchor.MiddleLeft;
        statText.text = statusInfo;

        if (tribe.leader != null)
        {
            Text detailText = CreateCardText(cardRect.transform, "Detail", new Vector2(0f, 0.5f), new Vector2(1f, 0.8f), new Vector2(10f, 0f), 12);
            detailText.alignment = TextAnchor.MiddleLeft;
            detailText.text = $"攻{tribe.leader.baseAttack} 防{tribe.leader.baseDefense} 血{tribe.leader.baseHp} 统{tribe.leader.command}";
        }

        // Add buff indicators
        if (_battleChoices.Count > 0 && _selectedChoiceIndex >= 0 && _selectedChoiceIndex < _battleChoices.Count)
        {
            BattleChoice choice = _battleChoices[_selectedChoiceIndex];

            int weatherBuff = TribeBattleBuffProvider.GetWeatherBuffStatus(tribe.tribeType, choice.weather);
            int terrainBuff = TribeBattleBuffProvider.GetTerrainBuffStatus(tribe.tribeType, choice.terrain);

            string weatherSymbol = weatherBuff > 0 ? "↑" : (weatherBuff < 0 ? "↓" : "-");
            string terrainSymbol = terrainBuff > 0 ? "↑" : (terrainBuff < 0 ? "↓" : "-");

            string weatherColor = weatherBuff > 0 ? "#00FF00" : (weatherBuff < 0 ? "#FF0000" : "#FFFFFF");
            string terrainColor = terrainBuff > 0 ? "#00FF00" : (terrainBuff < 0 ? "#FF0000" : "#FFFFFF");

            Text buffText = CreateCardText(cardRect.transform, "BuffIndicators", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), 20);
            buffText.alignment = TextAnchor.UpperRight;
            buffText.supportRichText = true;
            buffText.text = $"天气: <color={weatherColor}>{weatherSymbol}</color>  地形: <color={terrainColor}>{terrainSymbol}</color>";
        }

        CreateCatCountRow(cardRect, tribe);
    }

    private void CreateCatCountRow(RectTransform cardRect, TribeRecord tribe)
    {
        int commandLimit = GetCommandLimit(tribe);
        int deployed = Mathf.Min(commandLimit, tribe.GetCatCount());

        GameObject rowGo = new GameObject("CatCountRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
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

        GameObject countGo = new GameObject("CountLabel", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        countGo.transform.SetParent(rowGo.transform, false);
        LayoutElement countLe = countGo.GetComponent<LayoutElement>();
        countLe.flexibleWidth = 1f;
        countLe.minWidth = 150f;
        Text countText = countGo.GetComponent<Text>();
        countText.font = _uiFont;
        countText.fontSize = 16;
        countText.color = new Color(0.9f, 0.95f, 1f, 1f);
        countText.alignment = TextAnchor.MiddleLeft;
        countText.text = $"出战小猫: {deployed} / {tribe.GetCatCount()} (统御上限:{commandLimit})";
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

        if (_battleChoices.Count == 0 || _selectedChoiceIndex < 0 || _selectedChoiceIndex >= _battleChoices.Count)
        {
            SetStatusText("未选择战斗场景。", true);
            return;
        }

        BattleChoice selectedChoice = _battleChoices[_selectedChoiceIndex];

        TerrainType terrain = selectedChoice.terrain;
        WeatherType weather = selectedChoice.weather;
        DifficultyLevel difficulty = selectedChoice.difficulty;

        // Build filtered tribe list based on all deployed tribes
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
            var copy = new TribeRecord
            {
                tribeId = tribe.tribeId,
                tribeType = tribe.tribeType,
                leader = tribe.leader,
                moodId = tribe.moodId,
                isActive = tribe.isActive,
                cats = new List<CatData>()
            };

            if (tribe.cats != null)
            {
                int take = Mathf.Min(GetCommandLimit(tribe), tribe.cats.Count);
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
                _tribesRoot = CreateRuntimeZone(contentRoot, TribesRootName, "出战族群", new Vector2(0.03f, 0.16f), new Vector2(0.62f, 0.72f), new Color(0.13f, 0.23f, 0.39f, 0.72f));
            }
        }

        if (_battleOptionsRoot == null)
        {
            _battleOptionsRoot = contentRoot.Find(EnemyRootName) as RectTransform;
            if (_battleOptionsRoot == null)
            {
                _battleOptionsRoot = CreateRuntimeZone(contentRoot, EnemyRootName, "选择战斗场景", new Vector2(0.68f, 0.16f), new Vector2(0.97f, 0.72f), new Color(0.18f, 0.2f, 0.25f, 0.72f));
            }
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
        ConfigureVerticalLayout(_battleOptionsRoot);
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

    private DifficultyLevel GetSelectedDifficulty()
    {
        if (_battleChoices != null && _selectedChoiceIndex >= 0 && _selectedChoiceIndex < _battleChoices.Count)
            return _battleChoices[_selectedChoiceIndex].difficulty;
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
