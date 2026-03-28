using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;
using System.Collections;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 族群构筑主界面 - 管理所有族群相关UI和回合流程
    /// </summary>
    public class TribeBuildPanel : UIPanel
    {
        private const string PanelName = "族群管理";

        [Header("UI 组件")]
        [SerializeField] private Text _roundText;
        [SerializeField] private Text _catFoodText;
        [SerializeField] private RectTransform _tribeListContainer;
        [SerializeField] private Button _startBattleButton;
        [SerializeField] private Button _shopButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _deployCostText;

        [Header("预制体引用")]
        [SerializeField] private TribeCard _tribeCardPrefab;
        [SerializeField] private TribeDetailTips _tribeDetailTipsPrefab;

        [Header("子面板引用")]
        [SerializeField] private RecruitmentPanel _recruitmentPanel;
        [SerializeField] private RitualPanel _ritualPanel;
        [SerializeField] private ShopPanel _shopPanel;

        [Header("强制弹窗根节点")]
        [SerializeField] private RectTransform _forcedPopupRoot;

        private DataManager _dataManager;
        private RecruitmentService _recruitmentService;
        private RitualService _ritualService;
        private ShopService _shopService;

        // 回合和存档管理
        private RoundManager _roundManager;
        private TribeSaveManager _saveManager;

        private List<TribeRecord> _tribes;
        // 注：族群部署选择已移至BattlePreparePanel，以下代码保留以兼容旧系统
        private List<TribeRecord> _deployedTribes;
        private Dictionary<int, bool> _tribeDeployStates;

        private bool _isProcessingRecruitment = false;
        private bool _isProcessingRitual = false;
        private bool _isShopOpen = false;

        private TribeDetailTips _currentDetailTips;

        private Font _cachedFont;

        private void Awake()
        {
            _dataManager = GameManager.Instance?.DataManager;
            _recruitmentService = new RecruitmentService();
            _ritualService = new RitualService();
            _shopService = new ShopService();

            // 初始化回合和存档管理器
            _roundManager = new RoundManager();
            _saveManager = new TribeSaveManager(_dataManager);

            _deployedTribes = new List<TribeRecord>();
            _tribeDeployStates = new Dictionary<int, bool>();
        }

        private void Start()
        {
            InitializeButtons();
            InitializePanels();
            StartCoroutine(LoadPrefabsAndInitialize());
        }

        private IEnumerator LoadPrefabsAndInitialize()
        {
            // 如果 TribeCard 预制体未绑定，通过 Addressables 加载
            if (_tribeCardPrefab == null)
            {
                var handle = Addressables.LoadAssetAsync<GameObject>("ui/tribebuild/tribecard");
                yield return handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _tribeCardPrefab = handle.Result.GetComponent<TribeCard>();
                    Debug.Log("[TribeBuildPanel] TribeCard prefab loaded via Addressables");
                }
                else
                {
                    Debug.LogWarning("[TribeBuildPanel] Failed to load TribeCard prefab, will use runtime creation");
                }
            }

            LoadPlayerData();
            RefreshUI();
        }

        private void InitializeButtons()
        {
            // 绑定开始战斗按钮
            if (_startBattleButton != null)
            {
                _startBattleButton.onClick.RemoveAllListeners();
                _startBattleButton.onClick.AddListener(OnStartBattleButtonClicked);
            }

            // 绑定商店按钮
            if (_shopButton != null)
            {
                _shopButton.onClick.RemoveAllListeners();
                _shopButton.onClick.AddListener(OpenShop);
            }

            // 绑定返回按钮
            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(OnBackButtonClicked);
            }
        }

        private void OnBackButtonClicked()
        {
            Debug.Log("[TribeBuildPanel] Back button clicked - returning to main menu");

            // 隐藏当前界面
            gameObject.SetActive(false);

            // 返回主界面（游戏暂停，数据已保存）
            GameManager.Instance.UIManager.ShowPanel<MainPanel>("ui/mainpanel", UIManager.UILayer.Normal);

            // 注：游戏状态保留在GameFlowController中，用户可从主菜单点击开始游戏继续
        }

        public override void Initialize()
        {
            base.Initialize();
            Debug.Log("[TribeBuildPanel] Initialized");
        }

        private void InitializePanels()
        {
            // 如果子面板是预制体引用，需要实例化
            _recruitmentPanel = InstantiatePanelIfNeeded(_recruitmentPanel, "RecruitmentPanel");
            _ritualPanel = InstantiatePanelIfNeeded(_ritualPanel, "RitualPanel");
            _shopPanel = InstantiatePanelIfNeeded(_shopPanel, "ShopPanel");

            // 先设置外部根节点，再初始化（确保运行时 UI 构建在正确的父节点内）
            if (_forcedPopupRoot != null)
            {
                _recruitmentPanel?.SetExternalRoot(_forcedPopupRoot);
                _ritualPanel?.SetExternalRoot(_forcedPopupRoot);
            }

            // 初始化并隐藏子面板（按需显示）
            if (_recruitmentPanel != null)
            {
                _recruitmentPanel.Initialize();
                _recruitmentPanel.Hide();
            }

            if (_ritualPanel != null)
            {
                _ritualPanel.Initialize();
                _ritualPanel.Hide();
            }

            if (_shopPanel != null)
            {
                _shopPanel.Initialize();
                _shopPanel.Hide();
            }
        }

        /// <summary>
        /// 如果面板是预制体引用而非场景实例，则实例化它
        /// </summary>
        private T InstantiatePanelIfNeeded<T>(T panel, string name) where T : Component
        {
            if (panel == null) return null;

            // 检查是否是预制体资源（不在场景中）
            if (panel.gameObject.scene.name == null)
            {
                // 这是一个预制体引用，需要实例化
                GameObject instance = Instantiate(panel.gameObject, transform, false);
                instance.name = name;
                return instance.GetComponent<T>();
            }

            return panel;
        }

        private void LoadPlayerData()
        {
            if (_dataManager == null) return;

            // 同步回合到RoundManager
            int savedRound = _dataManager.GetCurrentRound();
            _roundManager?.SetRound(savedRound);

            _tribes = _dataManager.GetTribes();
            if (_tribes == null)
            {
                _tribes = new List<TribeRecord>();
            }

            // 注：初始族群选择由GameFlowController处理（InitialTribeSelectionPanel）
            // TribeBuildPanel只负责回合流程
            StartRoundPreparation();

            // 回合开始前存档
            _saveManager?.SaveBeforeRound(_roundManager.CurrentRound);
        }

        /// <summary>
        /// 开始回合准备流程
        /// </summary>
        public void StartRoundPreparation()
        {
            _deployedTribes.Clear();
            _tribeDeployStates.Clear();

            // 开始新回合
            _roundManager?.StartRound();

            // 先处理强制招募
            ProcessRecruitment();
        }

        /// <summary>
        /// 处理招募&练兵（第2-10关强制）
        /// </summary>
        private void ProcessRecruitment()
        {
            if (!_roundManager.CanDoRecruitment())
            {
                // 第1关无招募，直接进入后续流程
                OnRecruitmentOptionSelected(null);
                return;
            }

            // 本回合已完成过招募，跳过
            if (_dataManager.IsRecruitmentCompletedForRound(_roundManager.CurrentRound))
            {
                OnRecruitmentOptionSelected(null);
                return;
            }

            _isProcessingRecruitment = true;

            var options = _recruitmentService.GenerateOptions();

            if (_recruitmentPanel != null)
            {
                _recruitmentPanel.ShowOptions(options, OnRecruitmentOptionSelected);
            }
            else
            {
                // 如果没有面板，直接执行第一个选项
                OnRecruitmentOptionSelected(options[0]);
            }
        }

        private void OnRecruitmentOptionSelected(RecruitmentOption option)
        {
            if(option != null)
            {
                ExecuteRecruitmentOption(option);
                // 玩家做出选择并拿到奖励，标记本回合招募已完成
                _dataManager.SetRecruitmentCompletedForRound(_roundManager.CurrentRound);
            }

            _isProcessingRecruitment = false;

            // 招募结束存档
            _saveManager?.SaveAfterRecruitment(_roundManager.CurrentRound);

            // 招募完成后，检查是否需要祭祀
            if (_roundManager.CanDoRitual())
            {
                ProcessRitual();
            }
            else if (_roundManager.CanOpenShop())
            {
                // 商店不是强制的，只提示
                ShowShopAvailableHint();
            }
            else
            {
                // 进入自由操作阶段
                EnterFreeActionPhase();
            }
        }

        private void ExecuteRecruitmentOption(RecruitmentOption option)
        {
            switch (option.optionType)
            {
                case RecruitmentOptionType.NewTribe:
                    if (option.targetTribeType.HasValue)
                    {
                        _recruitmentService.ExecuteNewTribeRecruitment(option.targetTribeType.Value, option.cost);
                    }
                    break;

                case RecruitmentOptionType.AddCats:
                    var tribe = FindTribeById(option.targetTribeId);
                    if (tribe != null)
                    {
                        _recruitmentService.ExecuteAddCats(tribe, option.cost);
                    }
                    break;

                case RecruitmentOptionType.QualityEvolution:
                    tribe = FindTribeById(option.targetTribeId);
                    if (tribe != null)
                    {
                        _recruitmentService.ExecuteQualityEvolution(tribe, option.cost);
                    }
                    break;

                case RecruitmentOptionType.LeaderBoost:
                    tribe = FindTribeById(option.targetTribeId);
                    if (tribe != null)
                    {
                        _recruitmentService.ExecuteLeaderBoost(tribe, option.targetStatType, option.cost);
                    }
                    break;
            }

            // 只刷新本地数据，不重新触发回合流程
            _tribes = _dataManager.GetTribes();
            RefreshUI();
        }

        /// <summary>
        /// 处理祭祀（强制）
        /// </summary>
        private void ProcessRitual()
        {
            // 本回合已完成过祭祀，跳过
            if (_dataManager.IsRitualCompletedForRound(_roundManager.CurrentRound))
            {
                CheckShopAvailability();
                return;
            }

            _isProcessingRitual = true;

            if (_ritualPanel != null)
            {
                _ritualPanel.StartRitual(
                    _ritualService.GetTiers(),
                    (tier) => _ritualService.DrawBlessings(tier),
                    OnRitualConfirmed);
            }
            else
            {
                // 如果没有面板，跳过祭祀
                _isProcessingRitual = false;
                CheckShopAvailability();
            }
        }

        private void OnRitualConfirmed(RitualTier tier, RitualRewardItem blessing)
        {
            if (tier == null) { CheckShopAvailability(); return; } // 玩家跳过祭祀
            _ritualService.ExecuteRitual(tier, blessing);

            // 玩家选择了祝福并获得奖励，标记本回合祭祀已完成
            _dataManager.SetRitualCompletedForRound(_roundManager.CurrentRound);

            _isProcessingRitual = false;

            // 祭祀结束存档
            _saveManager?.SaveAfterRitual(_roundManager.CurrentRound);

            // 只刷新本地数据，不重新触发回合流程
            _tribes = _dataManager.GetTribes();
            RefreshUI();

            // 祭祀完成后，检查是否需要商店
            CheckShopAvailability();
        }

        private void CheckShopAvailability()
        {
            if (_roundManager.CanOpenShop())
            {
                ShowShopAvailableHint();
            }
            else
            {
                EnterFreeActionPhase();
            }
        }

        private void ShowShopAvailableHint()
        {
            Debug.Log("[TribeBuildPanel] 商店已开放，可点击商店按钮进入");
            // TODO: 显示商店开放提示
            EnterFreeActionPhase();
        }

        /// <summary>
        /// 进入自由操作阶段
        /// </summary>
        private void EnterFreeActionPhase()
        {
            // 玩家可以自由操作：查看族群、打开商店、准备战斗
        }

        /// <summary>
        /// 打开商店
        /// </summary>
        public void OpenShop()
        {
            if (_shopPanel == null) return;

            var items = _shopService.GenerateShopItems();
            int refreshCost = _shopService.CalculateRefreshCost();

            _shopPanel.ShowShop(items, refreshCost, OnShopItemBuy, OnShopRefresh, OnShopClose);
            _isShopOpen = true;
        }

        private void OnShopItemBuy(ShopItem item)
        {
            _shopService.BuyItem(item);
            RefreshUI();
            _shopPanel.UpdateCatFoodDisplay();

            // 商店购买后存档
            _saveManager?.SaveAfterShopPurchase(_roundManager.CurrentRound);
        }

        private void OnShopRefresh()
        {
            var newItems = _shopService.RefreshShop();
            if (newItems != null)
            {
                int newRefreshCost = _shopService.CalculateRefreshCost();
                _shopPanel.ShowShop(newItems, newRefreshCost, OnShopItemBuy, OnShopRefresh, OnShopClose);
            }
            RefreshUI();
            _shopPanel.UpdateCatFoodDisplay();
        }

        private void OnShopClose()
        {
            _isShopOpen = false;
        }

        /// <summary>
        /// 设置族群上阵状态
        /// </summary>
        public void SetTribeDeployState(int tribeId, bool isDeployed)
        {
            var tribe = FindTribeById(tribeId);
            if (tribe == null) return;

            // 检查族长是否在休息
            if (isDeployed && tribe.IsLeaderResting())
            {
                Debug.Log($"[TribeBuildPanel] {tribe.tribeType}族长正在休息，无法上阵");
                return;
            }

            _tribeDeployStates[tribeId] = isDeployed;

            if (isDeployed)
            {
                if (!_deployedTribes.Contains(tribe))
                {
                    _deployedTribes.Add(tribe);
                }
            }
            else
            {
                _deployedTribes.Remove(tribe);
            }

            RefreshUI();
        }

        /// <summary>
        /// 切换族群上阵状态（兼容旧调用）
        /// </summary>
        public void ToggleTribeDeploy(int tribeId)
        {
            bool currentState = _tribeDeployStates.ContainsKey(tribeId) && _tribeDeployStates[tribeId];
            SetTribeDeployState(tribeId, !currentState);
        }

        /// <summary>
        /// 开始战斗 - 打开战斗准备界面
        /// </summary>
        public void OnStartBattleButtonClicked()
        {
            Debug.Log("[TribeBuildPanel] 开始战斗 - 进入战斗准备界面");

            // 打开战斗准备界面（族群部署在该界面进行）
            var uiManager = GameManager.Instance?.UIManager;
            if (uiManager == null)
            {
                Debug.LogError("[TribeBuildPanel] UIManager not found");
                return;
            }

            // 隐藏当前界面
            gameObject.SetActive(false);

            // 显示战斗准备界面
            var battlePreparePanel = uiManager.ShowPanel<BattlePreparePanel>("ui/BattlePreparePanel", UIManager.UILayer.Normal);
            if (battlePreparePanel != null)
            {
                // 获取所有族群供BattlePreparePanel进行部署选择
                List<TribeRecord> allTribes = _dataManager.GetTribes();

                // 设置战斗准备（族群部署选择在BattlePreparePanel中进行）
                battlePreparePanel.SetupBattle(
                    _dataManager.GetCurrentRound(),
                    allTribes,
                    new List<TribeRecord>(), // 初始无已选择族群
                    6 // 最多上阵6个族群
                );
            }
            else
            {
                // 如果打开失败，重新显示当前界面
                gameObject.SetActive(true);
                Debug.LogError("[TribeBuildPanel] Failed to show BattlePreparePanel");
            }
        }

        /// <summary>
        /// 从战斗准备界面返回时调用
        /// </summary>
        public void OnReturnedFromBattlePreparation()
        {
            gameObject.SetActive(true);
            RefreshUI();
        }

        /// <summary>
        /// 战斗结束后调用 - 由BattlePanel调用
        /// </summary>
        public void OnBattleEnded(bool victory)
        {
            // 显示主界面
            gameObject.SetActive(true);

            if (victory)
            {
                Debug.Log("[TribeBuildPanel] 战斗胜利！");

                // 通知GameFlowController战斗结束
                GameFlowController.Instance?.OnBattleEnded(victory);

                // 战斗胜利后推进回合
                AdvanceRound();
            }
            else
            {
                Debug.Log("[TribeBuildPanel] 战斗失败，可以重新挑战");

                // 通知GameFlowController战斗失败
                GameFlowController.Instance?.OnBattleEnded(victory);

                // 失败不推进回合，允许重新调整阵容
            }

            RefreshUI();
        }

        private int CalculateDeployCost()
        {
            // 基础消耗：每个族群50猫粮
            int baseCostPerTribe = 50;

            // 加上小猫数量影响
            int totalCatCount = 0;
            foreach (var tribe in _deployedTribes)
            {
                totalCatCount += tribe.GetCatCount();
            }

            return baseCostPerTribe * _deployedTribes.Count + totalCatCount * 5;
        }

        private void AdvanceRound()
        {
            // 结束当前回合
            _roundManager?.EndRound();

            // 检查游戏是否结束
            if (_roundManager.IsGameOver)
            {
                Debug.Log("[TribeBuildPanel] ===== 游戏通关！=====");

                // 通知GameFlowController游戏即将结束
                GameFlowController.Instance?.NotifyGameEnding();

                ShowGameClearScreen();
                return;
            }

            // 更新存档中的回合数
            _dataManager.SetCurrentRound(_roundManager.CurrentRound);

            // 通知GameFlowController回合推进
            GameFlowController.Instance?.RaiseRoundChanged(_roundManager.CurrentRound);

            // 处理限时buff和休息状态
            ProcessRoundTransition();

            // 保存存档
            _dataManager.SavePlayerData();

            // 开始下一回合
            StartRoundPreparation();
        }

        /// <summary>
        /// 显示游戏通关界面
        /// </summary>
        private void ShowGameClearScreen()
        {
            Debug.Log("[TribeBuildPanel] 恭喜！20回合全部完成！");
            // TODO: 显示通关结算界面
        }

        private void ProcessRoundTransition()
        {
            // 减少限时buff持续时间
            foreach (var tribe in _tribes)
            {
                if (tribe.leader?.temporaryBuff != null)
                {
                    tribe.leader.temporaryBuff.DecreaseDuration();
                }

                // 减少族长休息时间
                if (tribe.leader?.restTurns > 0)
                {
                    tribe.leader.restTurns--;
                }
            }

            _dataManager.SavePlayerData();
        }

        private void ShowInitialTribeSelection()
        {
            // TODO: 显示初始六选二界面
            Debug.Log("[TribeBuildPanel] 显示初始族群选择界面");

            // 暂时自动选择两个族群
            var maineTribe = CreateInitialTribe(TribeType.Maine);
            var tabbyTribe = CreateInitialTribe(TribeType.Tabby);

            _dataManager.AddTribe(maineTribe);
            _dataManager.AddTribe(tabbyTribe);

            LoadPlayerData();
            RefreshUI();
        }

        private TribeRecord CreateInitialTribe(TribeType type)
        {
            var config = TribeConfigLoader.Instance.GetTribeConfig(type);
            if (config == null) return null;

            var tribe = new TribeRecord
            {
                tribeId = _tribes.Count,
                tribeType = type,
                leader = new LeaderData
                {
                    leaderId = UnityEngine.Random.Range(1000, 9999),
                    name = $"{config.tribeName}族长",
                    baseAttack = config.leaderBaseStats.attack,
                    baseDefense = config.leaderBaseStats.defense,
                    baseHp = config.leaderBaseStats.hp,
                    baseSpeed = config.leaderBaseStats.speed,
                    command = config.leaderBaseStats.command,
                    skillIds = new List<int>(),
                    permanentBuffs = new PermanentBuffs(),
                    temporaryBuff = null,
                    restTurns = 0
                },
                cats = new List<CatData>(),
                isActive = true
            };

            // 添加初始白色小猫
            for (int i = 0; i < config.initialCatCount; i++)
            {
                tribe.cats.Add(CatData.CreateWithQuality(CatQuality.White));
            }

            return tribe;
        }

        private void RefreshUI()
        {
            // 更新回合显示（使用RoundManager获取描述）
            if (_roundText != null)
            {
                _roundText.text = _roundManager?.GetRoundDescription() ?? $"第{_dataManager.GetCurrentRound()}回合";
            }

            // 更新猫粮显示
            if (_catFoodText != null)
            {
                _catFoodText.text = $"猫粮: {_dataManager.GetCatFood()}";
            }

            // 注：上阵选择和消耗计算在BattlePreparePanel中进行，此处不需要处理

            // 开始战斗按钮始终可用
            if (_startBattleButton != null)
            {
                _startBattleButton.interactable = true;
            }

            // 刷新族群列表
            RefreshTribeList();
        }

        private void RefreshTribeList()
        {
            if (_tribeListContainer == null) return;

            // 清空现有列表
            for (int i = _tribeListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_tribeListContainer.GetChild(i).gameObject);
            }

            // 使用预制体生成族群卡片
            if (_tribeCardPrefab != null)
            {
                foreach (var tribe in _tribes)
                {
                    TribeCard card = Instantiate(_tribeCardPrefab, _tribeListContainer, false);
                    // 注：族群部署在BattlePreparePanel中进行，此处仅展示族群信息
                    card.Setup(tribe, false, null, OnShowTribeDetail);
                }
            }
            else
            {
                // 如果没有预制体，使用旧的方式创建（兼容）
                foreach (var tribe in _tribes)
                {
                    CreateTribeCardRuntime(tribe);
                }
            }
        }

        private void CreateTribeCardRuntime(TribeRecord tribe)
        {
            Font font = _cachedFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject tribeGo = new GameObject("TribeCard", typeof(RectTransform), typeof(Image), typeof(Toggle));
            tribeGo.transform.SetParent(_tribeListContainer, false);

            RectTransform tribeRect = tribeGo.GetComponent<RectTransform>();
            tribeRect.sizeDelta = new Vector2(200f, 120f);

            Image tribeImg = tribeGo.GetComponent<Image>();
            tribeImg.color = GetTribeCardColor(tribe.tribeType);

            Toggle tribeToggle = tribeGo.GetComponent<Toggle>();
            bool isDeployed = _tribeDeployStates.ContainsKey(tribe.tribeId) && _tribeDeployStates[tribe.tribeId];
            tribeToggle.SetIsOnWithoutNotify(isDeployed);
            int tribeId = tribe.tribeId;
            tribeToggle.onValueChanged.AddListener((value) => OnTribeToggleChanged(tribeId, value));

            // 创建卡片内容
            CreateTribeCardContent(tribeRect, tribe, font);
        }

        private void CreateTribeCardContent(RectTransform cardRect, TribeRecord tribe, Font font)
        {
            // 族群名称
            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(cardRect, false);
            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.75f);
            nameRect.anchorMax = new Vector2(0.95f, 0.90f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 16;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = GetTribeTypeName(tribe.tribeType);

            // 小猫数量
            GameObject countGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            countGo.transform.SetParent(cardRect, false);
            RectTransform countRect = countGo.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.05f, 0.60f);
            countRect.anchorMax = new Vector2(0.95f, 0.72f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            Text countText = countGo.GetComponent<Text>();
            countText.font = font;
            countText.fontSize = 12;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            countText.text = $"小猫: {tribe.GetCatCount()} 只";

            // 状态
            GameObject statusGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(cardRect, false);
            RectTransform statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.05f, 0.40f);
            statusRect.anchorMax = new Vector2(0.95f, 0.55f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            Text statusText = statusGo.GetComponent<Text>();
            statusText.font = font;
            statusText.fontSize = 10;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            if (tribe.IsLeaderResting())
            {
                statusText.text = $"族长休息中 ({tribe.leader.restTurns}回)";
                statusText.color = new Color(1f, 0.3f, 0.3f, 1f);
            }
            else
            {
                statusText.text = "状态: 可出战";
            }

            // 族长属性简略
            GameObject statsGo = new GameObject("Stats", typeof(RectTransform), typeof(Text));
            statsGo.transform.SetParent(cardRect, false);
            RectTransform statsRect = statsGo.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.05f, 0.05f);
            statsRect.anchorMax = new Vector2(0.95f, 0.35f);
            statsRect.offsetMin = Vector2.zero;
            statsRect.offsetMax = Vector2.zero;
            Text statsText = statsGo.GetComponent<Text>();
            statsText.font = font;
            statsText.fontSize = 9;
            statsText.alignment = TextAnchor.UpperLeft;
            statsText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            statsText.text = $"攻{tribe.leader.baseAttack} 防{tribe.leader.baseDefense}\n血{tribe.leader.baseHp} 速{tribe.leader.baseSpeed} 统{tribe.leader.command}";
        }

        private Color GetTribeCardColor(TribeType tribeType)
        {
            switch (tribeType)
            {
                case TribeType.Maine: return new Color(0.3f, 0.5f, 0.7f, 1f);
                case TribeType.Tabby: return new Color(0.6f, 0.4f, 0.3f, 1f);
                case TribeType.Orange: return new Color(0.7f, 0.5f, 0.2f, 1f);
                case TribeType.Cow: return new Color(0.4f, 0.4f, 0.5f, 1f);
                case TribeType.Siamese: return new Color(0.5f, 0.4f, 0.6f, 1f);
                case TribeType.Ragdoll: return new Color(0.7f, 0.5f, 0.6f, 1f);
                default: return new Color(0.5f, 0.5f, 0.5f, 1f);
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

        private void OnTribeToggleChanged(int tribeId, bool value)
        {
            SetTribeDeployState(tribeId, value);
        }

        private void OnShowTribeDetail(TribeRecord tribe, bool isDeployed)
        {
            if (_tribeDetailTipsPrefab == null)
            {
                Debug.LogWarning("[TribeBuildPanel] TribeDetailTips prefab not assigned");
                return;
            }

            // 如果已有详情面板，先销毁
            if (_currentDetailTips != null)
            {
                Destroy(_currentDetailTips.gameObject);
            }

            // 实例化详情面板
            _currentDetailTips = Instantiate(_tribeDetailTipsPrefab, transform, false);
            _currentDetailTips.Show(tribe, isDeployed, OnTribeRestClicked, OnTribeDeployChanged);
        }

        private void OnTribeRestClicked(TribeRecord tribe)
        {
            if (tribe?.leader != null)
            {
                tribe.leader.restTurns = 2; // 休息2回合
                Debug.Log($"[TribeBuildPanel] {tribe.tribeType}族长开始休息");
                RefreshUI();
            }
        }

        private void OnTribeDeployChanged(TribeRecord tribe, bool isDeployed)
        {
            SetTribeDeployState(tribe.tribeId, isDeployed);
        }

        private TribeRecord FindTribeById(int tribeId)
        {
            return _tribes?.Find(t => t.tribeId == tribeId);
        }
    }
}
