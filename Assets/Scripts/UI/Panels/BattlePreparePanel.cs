using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TribeSystem;
using TribeSystem.UI;
using BattleSystem;
using BattleSystem.Fighter;

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

    // Avatar state (同 TribeAvatarRoot 功能)
    private readonly List<AsyncOperationHandle<Sprite>> _avatarHandles = new List<AsyncOperationHandle<Sprite>>();
    private readonly Dictionary<GameObject, Sprite> _avatarIdleSprites = new Dictionary<GameObject, Sprite>();
    private readonly Dictionary<GameObject, Sprite> _avatarAttackSprites = new Dictionary<GameObject, Sprite>();
    private GameObject _selectedAvatarGo;
    private GameObject _selectionIndicator;
    private int _selectedCatIndex = -1;
    private int _selectedCatTribeId = -1;

    // State
    private int _currentLevel;
    private int _currentCatFood;

    private class BattleChoice
    {
        public TerrainType terrain;
        public WeatherType weather;
        public DifficultyLevel difficulty;
        public int[] enemyUnitIds;
        public bool isBoss;
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

        // 根据关卡生成战斗选项
        _battleChoices.Clear();
        BattleCampaignRuntime campaign = GameManager.Instance.BattleCampaignRuntime;

        bool isBossLevel = (levelId == 5 || levelId == 10);
        bool hasExtremeChallenge = (levelId == 3 || levelId == 6 || levelId == 9);

        if (isBossLevel)
        {
            // Boss关：只有一个选项，Boss挑战，使用血战数值
            var choice = new BattleChoice
            {
                terrain = TerrainType.Plain,
                weather = WeatherType.Sunny,
                difficulty = DifficultyLevel.Bloodbath,
                isBoss = true,
                enemyUnitIds = campaign != null ? campaign.GetEnemyUnitIdsForBattle(_currentLevel) : new int[] { 1, 2, 3 }
            };
            _battleChoices.Add(choice);
        }
        else
        {
            // 生成不同的地形和天气组合
            TerrainType terrain1 = (TerrainType)Random.Range(0, 2);
            TerrainType terrain2 = terrain1 == TerrainType.Plain ? TerrainType.Brush : TerrainType.Plain;

            WeatherType weather1 = (WeatherType)Random.Range(0, 4);
            WeatherType weather2;
            do { weather2 = (WeatherType)Random.Range(0, 4); } while (weather1 == weather2);

            // 两个普通选项
            for (int i = 0; i < 2; i++)
            {
                var choice = new BattleChoice
                {
                    terrain = i == 0 ? terrain1 : terrain2,
                    weather = i == 0 ? weather1 : weather2,
                    difficulty = DifficultyLevel.Normal,
                    isBoss = false,
                    enemyUnitIds = campaign != null ? campaign.GetEnemyUnitIdsForBattle(_currentLevel) : new int[] { 1, 2, 3 }
                };
                _battleChoices.Add(choice);
            }

            // 第三六九关额外增加一个极难选项
            if (hasExtremeChallenge)
            {
                // 只有2种地形，随机选一个
                TerrainType terrain3 = (TerrainType)Random.Range(0, 2);
                WeatherType weather3;
                do { weather3 = (WeatherType)Random.Range(0, 4); } while (weather3 == weather1 || weather3 == weather2);

                var extremeChoice = new BattleChoice
                {
                    terrain = terrain3,
                    weather = weather3,
                    difficulty = DifficultyLevel.Bloodbath,
                    isBoss = false,
                    enemyUnitIds = campaign != null ? campaign.GetEnemyUnitIdsForBattle(_currentLevel) : new int[] { 1, 2, 3 }
                };
                _battleChoices.Add(extremeChoice);
            }
        }

        _selectedChoiceIndex = 0;

        DataManager dataManager = GameManager.Instance?.DataManager;
        _currentCatFood = dataManager != null ? (int)dataManager.GetCatFood() : 0;

        RefreshUI();
    }

    private void RefreshUI()
    {
        RefreshTexts();
        RebuildAllTribeAvatars();
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

            // 猫粮奖励
            int catFoodReward = 0;
            var campaign = GameManager.Instance?.BattleCampaignRuntime;
            if (campaign != null)
            {
                catFoodReward = campaign.GetCatFoodReward(_currentLevel, choice.difficulty);
            }

            string titleLabel = choice.isBoss ? "Boss挑战" : $"战斗选项 {i + 1} ({diffName})";
            string contentStr = $"<size=24>{titleLabel}</size>\n\n地形: {terrainName}    天气: {weatherName}\n敌人数量: {enemyCount}    猫粮: +{catFoodReward}";

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
        RefreshBattleOptionUI();
    }

    private void RefreshBattleOptionUI()
    {
        // 更新战斗选项按钮颜色
        if (_battleOptionsRoot != null)
        {
            for (int i = 0; i < _battleOptionsRoot.childCount; i++)
            {
                var child = _battleOptionsRoot.GetChild(i);
                var img = child.GetComponent<Image>();
                if (img != null)
                    img.color = (i == _selectedChoiceIndex) ? TabActiveColor : TabInactiveColor;
            }
        }

        // 刷新 buff 指示器
        RefreshBuffIndicators();

        // 刷新文本（天气/地形信息可能变化）
        RefreshTexts();
    }

    private void RefreshStartButtonState()
    {
        if (_startBattleButton == null) return;
        _startBattleButton.interactable = true;
    }

    private static int GetCommandLimit(TribeRecord tribe)
    {
        return tribe.GetCatCount();
    }

    // --- Tribe Avatar Views (同 TribeAvatarRoot 功能) ---

    private void RebuildAllTribeAvatars()
    {
        if (_tribesRoot == null) return;

        // 移除可能残留的布局组件（头像手动定位）
        var vlg = _tribesRoot.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) Object.Destroy(vlg);
        var hlg = _tribesRoot.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) Object.Destroy(hlg);

        // 清空旧头像
        for (int i = _tribesRoot.childCount - 1; i >= 0; i--)
            Destroy(_tribesRoot.GetChild(i).gameObject);

        // 释放旧句柄
        foreach (var h in _avatarHandles)
        {
            if (h.IsValid()) Addressables.Release(h);
        }
        _avatarHandles.Clear();
        _avatarIdleSprites.Clear();
        _avatarAttackSprites.Clear();
        _selectedAvatarGo = null;

        // 创建选中指示器（▼）
        _selectionIndicator = new GameObject("SelectionIndicator", typeof(RectTransform), typeof(Text));
        _selectionIndicator.transform.SetParent(_tribesRoot, false);
        RectTransform indRt = _selectionIndicator.GetComponent<RectTransform>();
        indRt.anchorMin = new Vector2(0.5f, 0.5f);
        indRt.anchorMax = new Vector2(0.5f, 0.5f);
        indRt.sizeDelta = new Vector2(65f, 65f);
        Text indText = _selectionIndicator.GetComponent<Text>();
        indText.font = _uiFont ?? LoadBuiltinFont();
        indText.fontSize = 56;
        indText.alignment = TextAnchor.MiddleCenter;
        indText.color = new Color(0.286f, 1f, 0.2f, 1f);
        indText.text = "▼";
        _selectionIndicator.SetActive(false);

        if (_deployedTribes == null || _deployedTribes.Count == 0) return;

        int tribeCount = _deployedTribes.Count;
        float spacing = 400f;

        for (int t = 0; t < tribeCount; t++)
        {
            var tribe = _deployedTribes[t];
            string breed = GetTribeBreedName(tribe.tribeType);
            if (string.IsNullOrEmpty(breed)) continue;

            float tribeCenterX = (t - (tribeCount - 1) / 2f) * spacing;
            int clickedTribeId = tribe.tribeId;

            // 族长使用 fighter_config.json 中的 avatarId
            string leaderIdleAddr = TribeConfigLoader.Instance?.GetLeaderAvatarAddress(tribe.tribeType, 1) ?? $"avatartemp/{breed}1";
            string leaderAttackAddr = TribeConfigLoader.Instance?.GetLeaderAvatarAddress(tribe.tribeType, 2) ?? $"avatartemp/{breed}2";

            var idleHandle = Addressables.LoadAssetAsync<Sprite>(leaderIdleAddr);
            var attackHandle = Addressables.LoadAssetAsync<Sprite>(leaderAttackAddr);
            _avatarHandles.Add(idleHandle);
            _avatarHandles.Add(attackHandle);

            int pending = 2;
            Sprite leaderIdleSprite = null;
            Sprite leaderAttackSprite = null;

            System.Action onLeaderLoaded = () =>
            {
                if (_tribesRoot == null) return;
                Sprite defaultSprite = leaderIdleSprite ?? leaderAttackSprite;
                if (defaultSprite == null) return;

                bool tribeIsSelected = (_selectedCatTribeId == clickedTribeId);
                bool leaderIsSelected = tribeIsSelected && _selectedCatIndex < 0;

                // Leader
                GameObject leaderGo = new GameObject($"Leader_{tribe.tribeType}", typeof(RectTransform), typeof(Image), typeof(Button));
                leaderGo.transform.SetParent(_tribesRoot, false);
                RectTransform leaderRt = leaderGo.GetComponent<RectTransform>();
                leaderRt.anchorMin = new Vector2(0.5f, 0.5f);
                leaderRt.anchorMax = new Vector2(0.5f, 0.5f);
                float leaderX = tribeCenterX + Random.Range(-30f, 30f);
                float leaderY = Random.Range(-30f, 30f);
                leaderRt.anchoredPosition = new Vector2(leaderX, leaderY);
                float leaderScale = Random.Range(160f, 220f);
                leaderRt.sizeDelta = new Vector2(leaderScale, leaderScale);
                Image leaderImg = leaderGo.GetComponent<Image>();
                leaderImg.sprite = leaderIsSelected && leaderAttackSprite != null ? leaderAttackSprite : defaultSprite;
                leaderImg.color = new Color(1f, 1f, 1f, Random.Range(0.85f, 1f));
                Button leaderBtn = leaderGo.GetComponent<Button>();
                leaderBtn.transition = Selectable.Transition.None;
                leaderBtn.onClick.AddListener(() => OnLeaderAvatarClicked(clickedTribeId));

                if (leaderIdleSprite != null) _avatarIdleSprites[leaderGo] = leaderIdleSprite;
                if (leaderAttackSprite != null) _avatarAttackSprites[leaderGo] = leaderAttackSprite;
                if (leaderIsSelected)
                {
                    _selectedAvatarGo = leaderGo;
                    PositionIndicatorAbove(leaderRt);
                }

                // Buff 指示器（地形+天气加减益）
                CreateBuffIndicator(leaderGo.transform, tribe.tribeType, new Vector2(66f, -59f));

                // 为每只猫单独加载外观（根据 fighterId → avatarId）
                int catCount = tribe.GetCatCount();
                for (int i = 0; i < catCount; i++)
                {
                    var cat = tribe.cats[i];
                    int fighterId = GetCatFighterIdForAvatar(tribe.tribeType, cat.tier);
                    GetCatAvatarAddresses(fighterId, breed, out string catIdleAddr, out string catAttackAddr);

                    int catIdx = i;
                    bool catIsSelected = (tribeIsSelected && i == _selectedCatIndex);

                    var catIdleHandle = Addressables.LoadAssetAsync<Sprite>(catIdleAddr);
                    var catAttackHandle = Addressables.LoadAssetAsync<Sprite>(catAttackAddr);
                    _avatarHandles.Add(catIdleHandle);
                    _avatarHandles.Add(catAttackHandle);

                    int catPending = 2;
                    Sprite catIdle = null;
                    Sprite catAttack = null;

                    System.Action onCatLoaded = () =>
                    {
                        if (_tribesRoot == null) return;
                        Sprite catDefault = catIdle ?? catAttack;
                        if (catDefault == null) return;

                        GameObject catGo = new GameObject($"Cat_{tribe.tribeType}_{catIdx}", typeof(RectTransform), typeof(Image), typeof(Button));
                        catGo.transform.SetParent(_tribesRoot, false);
                        RectTransform catRt = catGo.GetComponent<RectTransform>();
                        catRt.anchorMin = new Vector2(0.5f, 0.5f);
                        catRt.anchorMax = new Vector2(0.5f, 0.5f);

                        float baseX = tribeCenterX + (catIdx - (catCount - 1) / 2f) * 80f;
                        float catX = baseX + Random.Range(-30f, 30f);
                        float catY = Random.Range(-200f, -120f);
                        catRt.anchoredPosition = new Vector2(catX, catY);

                        float catSize = Random.Range(50f, 80f);
                        catRt.sizeDelta = new Vector2(catSize, catSize);

                        Image catImg = catGo.GetComponent<Image>();
                        catImg.sprite = catIsSelected && catAttack != null ? catAttack : catDefault;
                        catImg.color = new Color(1f, 1f, 1f, Random.Range(0.6f, 0.95f));
                        Button catBtn = catGo.GetComponent<Button>();
                        catBtn.transition = Selectable.Transition.None;
                        catBtn.onClick.AddListener(() => OnCatAvatarClicked(clickedTribeId, catIdx));

                        if (catIdle != null) _avatarIdleSprites[catGo] = catIdle;
                        if (catAttack != null) _avatarAttackSprites[catGo] = catAttack;

                        // 小猫也加 Buff 指示器
                        CreateBuffIndicator(catGo.transform, tribe.tribeType, new Vector2(26f, -11f));

                        if (catIsSelected)
                        {
                            _selectedAvatarGo = catGo;
                            PositionIndicatorAbove(catRt);
                        }
                    };

                    int capturedCatPending = catPending;
                    catIdleHandle.Completed += (op) =>
                    {
                        if (op.Status == AsyncOperationStatus.Succeeded) catIdle = op.Result;
                        if (--capturedCatPending == 0) onCatLoaded();
                    };
                    catAttackHandle.Completed += (op) =>
                    {
                        if (op.Status == AsyncOperationStatus.Succeeded) catAttack = op.Result;
                        if (--capturedCatPending == 0) onCatLoaded();
                    };
                }
            };

            idleHandle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded) leaderIdleSprite = op.Result;
                if (--pending == 0) onLeaderLoaded();
            };
            attackHandle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded) leaderAttackSprite = op.Result;
                if (--pending == 0) onLeaderLoaded();
            };
        }
    }

    private void SetAvatarSelected(GameObject avatarGo, bool selected)
    {
        if (avatarGo == null) return;
        var img = avatarGo.GetComponent<Image>();
        if (img == null) return;

        if (selected)
        {
            if (_avatarAttackSprites.TryGetValue(avatarGo, out var atkSprite))
                img.sprite = atkSprite;
            var rt = avatarGo.GetComponent<RectTransform>();
            if (rt != null) PositionIndicatorAbove(rt);
        }
        else
        {
            if (_avatarIdleSprites.TryGetValue(avatarGo, out var idleSprite))
                img.sprite = idleSprite;
        }
    }

    private void PositionIndicatorAbove(RectTransform target)
    {
        if (_selectionIndicator == null || target == null) return;
        _selectionIndicator.SetActive(true);
        var indRt = _selectionIndicator.GetComponent<RectTransform>();
        indRt.anchoredPosition = new Vector2(
            target.anchoredPosition.x,
            target.anchoredPosition.y + target.sizeDelta.y * 0.5f + 12f
        );
    }

    private void OnLeaderAvatarClicked(int tribeId)
    {
        var tribe = _deployedTribes?.Find(t => t.tribeId == tribeId);
        if (tribe == null) return;

        GameObject clickedGo = FindAvatarGoByTribe(tribeId, -1);
        if (_selectedAvatarGo != null && _selectedAvatarGo != clickedGo)
            SetAvatarSelected(_selectedAvatarGo, false);
        SetAvatarSelected(clickedGo, true);
        _selectedAvatarGo = clickedGo;

        _selectedCatIndex = -1;
        _selectedCatTribeId = -1;
    }

    private void OnCatAvatarClicked(int tribeId, int catIndex)
    {
        var tribe = _deployedTribes?.Find(t => t.tribeId == tribeId);
        if (tribe == null || tribe.cats == null || catIndex >= tribe.cats.Count) return;

        GameObject clickedGo = FindAvatarGoByTribe(tribeId, catIndex);
        if (_selectedAvatarGo != null && _selectedAvatarGo != clickedGo)
            SetAvatarSelected(_selectedAvatarGo, false);
        SetAvatarSelected(clickedGo, true);
        _selectedAvatarGo = clickedGo;

        _selectedCatIndex = catIndex;
        _selectedCatTribeId = tribeId;
    }

    private GameObject FindAvatarGoByTribe(int tribeId, int catIndex)
    {
        var tribe = _deployedTribes?.Find(t => t.tribeId == tribeId);
        if (tribe == null) return null;
        string name = catIndex < 0
            ? $"Leader_{tribe.tribeType}"
            : $"Cat_{tribe.tribeType}_{catIndex}";
        return _tribesRoot?.Find(name)?.gameObject;
    }

    private string GetTribeBreedName(TribeType tribeType)
    {
        switch (tribeType)
        {
            case TribeType.Tabby: return "lihua";
            case TribeType.Orange: return "daju";
            case TribeType.Cow: return "nainiu";
            case TribeType.Siamese: return "xianluo";
            default: return null;
        }
    }

    private int GetCatFighterIdForAvatar(TribeType tribeType, UnitTier tier)
    {
        var tribeConfig = TribeConfigLoader.Instance?.GetTribeConfig(tribeType);
        if (tribeConfig != null)
        {
            var unitType = tribeConfig.GetUnitType(tier);
            if (unitType != null && unitType.fighterId > 0)
                return unitType.fighterId;
        }
        return 0;
    }

    private void GetCatAvatarAddresses(int fighterId, string defaultBreed, out string idleAddr, out string attackAddr)
    {
        if (fighterId > 0)
        {
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            if (fighterConfig != null && !string.IsNullOrEmpty(fighterConfig.avatarId))
            {
                idleAddr = $"avatartemp/{fighterConfig.avatarId}1";
                attackAddr = $"avatartemp/{fighterConfig.avatarId}2";
                return;
            }
        }
        idleAddr = $"avatartemp/{defaultBreed}1";
        attackAddr = $"avatartemp/{defaultBreed}2";
    }

    private void CreateBuffIndicator(Transform parent, TribeType tribeType, Vector2 position)
    {
        GameObject buffIndGo = new GameObject("BuffIndicator", typeof(RectTransform), typeof(Text));
        buffIndGo.transform.SetParent(parent, false);
        RectTransform buffIndRt = buffIndGo.GetComponent<RectTransform>();
        buffIndRt.anchorMin = new Vector2(0.5f, 0.5f);
        buffIndRt.anchorMax = new Vector2(0.5f, 0.5f);
        buffIndRt.pivot = new Vector2(0.5f, 0.5f);
        buffIndRt.sizeDelta = new Vector2(210f, 31f);
        buffIndRt.anchoredPosition = position;
        Text buffIndText = buffIndGo.GetComponent<Text>();
        buffIndText.font = _uiFont ?? LoadBuiltinFont();
        buffIndText.fontSize = 28;
        buffIndText.alignment = TextAnchor.MiddleCenter;
        buffIndText.raycastTarget = false;
        buffIndText.supportRichText = true;
        SetBuffIndicatorText(buffIndText, tribeType);
    }

    private void SetBuffIndicatorText(Text text, TribeType tribeType)
    {
        if (text == null || _battleChoices == null || _selectedChoiceIndex < 0 || _selectedChoiceIndex >= _battleChoices.Count)
        {
            if (text != null) text.text = "";
            return;
        }

        BattleChoice choice = _battleChoices[_selectedChoiceIndex];
        int weatherBuff = TribeBattleBuffProvider.GetWeatherBuffStatus(tribeType, choice.weather);
        int terrainBuff = TribeBattleBuffProvider.GetTerrainBuffStatus(tribeType, choice.terrain);
        int totalBuff = weatherBuff + terrainBuff;

        if (totalBuff > 1) text.text = "<color=#00FF00>↑↑</color>";
        else if (totalBuff == 1) text.text = "<color=#00FF00>↑</color>";
        else if (totalBuff == -1) text.text = "<color=#FF0000>↓</color>";
        else if (totalBuff < -1) text.text = "<color=#FF0000>↓↓</color>";
        else text.text = "";
    }

    private void RefreshBuffIndicators()
    {
        if (_tribesRoot == null || _deployedTribes == null) return;

        foreach (var tribe in _deployedTribes)
        {
            // Leader
            RefreshBuffIndicatorOn(_tribesRoot.Find($"Leader_{tribe.tribeType}"), tribe.tribeType);

            // Cats
            int catCount = tribe.GetCatCount();
            for (int i = 0; i < catCount; i++)
            {
                RefreshBuffIndicatorOn(_tribesRoot.Find($"Cat_{tribe.tribeType}_{i}"), tribe.tribeType);
            }
        }
    }

    private void RefreshBuffIndicatorOn(Transform avatarTf, TribeType tribeType)
    {
        if (avatarTf == null) return;
        Transform buffIndTf = avatarTf.Find("BuffIndicator");
        if (buffIndTf == null) return;
        Text buffText = buffIndTf.GetComponent<Text>();
        SetBuffIndicatorText(buffText, tribeType);
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
        // TribesRoot 不使用布局组件，头像由代码手动定位
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
            case DifficultyLevel.Bloodbath: return "极难";
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

    private string ResolveEnemyName(int enemyUnitId)
    {
        if (GameManager.Instance == null)
            return $"敌方兵种 {enemyUnitId}";
        var campaign = GameManager.Instance.BattleCampaignRuntime;
        if (campaign != null)
            return campaign.GetEnemyName(enemyUnitId);
        return $"敌方兵种 {enemyUnitId}";
    }

    private void OnDestroy()
    {
        foreach (var h in _avatarHandles)
        {
            if (h.IsValid()) Addressables.Release(h);
        }
        _avatarHandles.Clear();
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
