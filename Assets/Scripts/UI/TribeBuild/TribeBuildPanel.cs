using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

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
        [SerializeField] private GameObject _shopEntranceObj; // 用于在商店未开放回合隐藏的入口节点

        [Header("预制体引用")]
        [SerializeField] private TribeCard _tribeCardPrefab;
        [SerializeField] private TribeDetailTips _tribeDetailTipsPrefab;
        [SerializeField] private Button _sellCatButton;
        [SerializeField] private Button _backpackButton;

        [Header("子面板引用")]
        [SerializeField] private RecruitmentPanel _recruitmentPanel;
        [SerializeField] private RitualPanel _ritualPanel;
        [SerializeField] private ShopPanel _shopPanel;
        [SerializeField] private RandomEventPanel _randomEventPanel;
        [SerializeField] private BackpackPanel _backpackPanelPrefab;
        private BackpackPanel _backpackPanelInstance;
        private RecruitmentResultPanel _recruitmentResultPanel;
        [SerializeField] private TribeAuraChoicePanel _auraChoicePanel;

        [Header("强制弹窗根节点")]
        [SerializeField] private RectTransform _forcedPopupRoot;
        [SerializeField] private GameObject _tribeAvatarRoot;

        private DataManager _dataManager;
        private AuraService _auraService;
        private TribeAuraService _tribeAuraService;
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
        private List<ShopItem> _currentShopItems;

        // 优先级弹窗队列
        private List<string> _popupEventQueue;
        private int _popupQueueIndex;

        private TribeCard _displayedCard;
        private int _currentDisplayTribeId = -1;
        private int _selectedUnitIndex = 0; // 选中的单位索引（0=首个单位）
        private int _selectedUnitTribeId = -1; // 选中单位所属的 tribeId

        private System.Collections.Generic.List<AsyncOperationHandle<Sprite>> _avatarHandles = new System.Collections.Generic.List<AsyncOperationHandle<Sprite>>();

        // 首次加载完成标记
        private bool _isReady = false;

        // 头像 idle/attack 帧切换
        private GameObject _selectedAvatarGo;
        private Dictionary<GameObject, Sprite> _avatarIdleSprites = new Dictionary<GameObject, Sprite>();
        private Dictionary<GameObject, Sprite> _avatarAttackSprites = new Dictionary<GameObject, Sprite>();
        private GameObject _selectionIndicator; // ▼ 选中指示器

        private void Awake()
        {
            _dataManager = GameManager.Instance?.DataManager;
            _auraService = new AuraService();
            _tribeAuraService = new TribeAuraService(_auraService);
            _tribeAuraService.LoadConfig();
            _recruitmentService = new RecruitmentService();
            _recruitmentService.SetTribeAuraService(_tribeAuraService);
            _ritualService = new RitualService();
            _shopService = new ShopService();
            _recruitmentService.SetAuraService(_auraService);
            _ritualService.SetAuraService(_auraService);
            _shopService.SetAuraService(_auraService);

            // 初始化回合和存档管理器
            _roundManager = new RoundManager();
            _saveManager = new TribeSaveManager(_dataManager);

            _deployedTribes = new List<TribeRecord>();
            _tribeDeployStates = new Dictionary<int, bool>();

            // Create root for avatars
            RectTransform avatarRt = _tribeAvatarRoot.GetComponent<RectTransform>();
            avatarRt.anchorMin = new Vector2(0.5f, 0.5f);
            avatarRt.anchorMax = new Vector2(0.5f, 0.5f);
            avatarRt.pivot = new Vector2(0.5f, 0.5f);
            avatarRt.anchoredPosition = Vector2.zero; // Center of the screen
            avatarRt.SetSiblingIndex(1); // Place it below the background but above the UI layout
        }

        private void Update()
        {
            bool isPopupActive = false;

            if (_forcedPopupRoot != null && _forcedPopupRoot.gameObject.activeSelf)
            {
                // 如果 _forcedPopupRoot 本身处于激活状态并且包含激活子物体
                for (int i = 0; i < _forcedPopupRoot.childCount; i++)
                {
                    if (_forcedPopupRoot.GetChild(i).gameObject.activeSelf)
                    {
                        isPopupActive = true;
                        break;
                    }
                }
            }

            // 直接检查具体面板的激活状态，避免它们没有挂在 _forcedPopupRoot 下时漏判
            if (_recruitmentPanel != null && _recruitmentPanel.gameObject.activeInHierarchy) isPopupActive = true;
            if (_ritualPanel != null && _ritualPanel.gameObject.activeInHierarchy) isPopupActive = true;
            if (_randomEventPanel != null && _randomEventPanel.gameObject.activeInHierarchy) isPopupActive = true;
            if (_recruitmentResultPanel != null && _recruitmentResultPanel.gameObject.activeInHierarchy) isPopupActive = true;

            if (_tribeAvatarRoot != null && _tribeAvatarRoot.activeSelf == isPopupActive)
            {
                _tribeAvatarRoot.SetActive(!isPopupActive);
            }
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
            _isReady = true;
        }

        private void InitializeButtons()
        {
            // 绑定开始战斗按钮
            _startBattleButton.onClick.RemoveAllListeners();
            _startBattleButton.onClick.AddListener(OnStartBattleButtonClicked);

            // 绑定商店按钮
            _shopButton.onClick.RemoveAllListeners();
            _shopButton.onClick.AddListener(OpenShop);

            // 绑定返回按钮
            _backButton.onClick.RemoveAllListeners();
            _backButton.onClick.AddListener(OnBackButtonClicked);

            // 绑定出售小猫按钮
            _sellCatButton.onClick.RemoveAllListeners();
            _sellCatButton.onClick.AddListener(OnSellCatButtonClicked);
            _sellCatButton.gameObject.SetActive(false);

            // 绑定背包按钮
            _backpackButton.onClick.RemoveAllListeners();
            _backpackButton.onClick.AddListener(OpenBackpack);

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

        public override void Show()
        {
            base.Show();

            // 首次加载尚未完成时（Start 中的协程还没跑完），不处理
            if (!_isReady) return;

            // 检测 DataManager 中的族群列表是否与缓存不同（存档删除后重建等情况）
            if (_dataManager != null)
            {
                var latest = _dataManager.GetTribes();
                if (!ReferenceEquals(_tribes, latest))
                {
                    Debug.Log("[TribeBuildPanel] Show() 检测到族群数据已变更，重新加载");
                    LoadPlayerData();
                    RefreshUI();
                }
            }
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

            // 随机事件面板（抉择系统）
            _randomEventPanel = InstantiatePanelIfNeeded(_randomEventPanel, "RandomEventPanel");
            if (_randomEventPanel == null)
            {
                CreateRandomEventPanel();
            }
            else if (_forcedPopupRoot != null)
            {
                _randomEventPanel.SetExternalRoot(_forcedPopupRoot);
                _randomEventPanel.Initialize();
                _randomEventPanel.Hide();
            }

            // 背包面板（运行时创建）
            CreateBackpackPanel();

            // 招募结果面板（运行时创建）
            CreateRecruitmentResultPanel();
        }

        private void CreateRecruitmentResultPanel()
        {
            var root = _forcedPopupRoot != null ? _forcedPopupRoot : transform as RectTransform;
            if (root == null) return;
            var go = new GameObject("RecruitmentResultPanel", typeof(RectTransform), typeof(RecruitmentResultPanel));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _recruitmentResultPanel = go.GetComponent<RecruitmentResultPanel>();
            _recruitmentResultPanel.Hide();
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
        /// 开始回合准备流程 - 按配置优先级依次弹出事件
        /// </summary>
        public void StartRoundPreparation()
        {
            _deployedTribes.Clear();
            _tribeDeployStates.Clear();

            // 新回合：清空商店商品缓存和刷新次数，下次打开商店时重新生成
            _currentShopItems = null;
            _dataManager.SetShopRefreshCount(0);

            // 开始新回合
            _roundManager?.StartRound();

            // 获取按优先级排序的弹窗事件列表
            _popupEventQueue = _roundManager.GetSortedPopupEvents();
            _popupQueueIndex = 0;

            // 依次处理弹窗事件
            ProcessNextPopupEvent();
        }

        /// <summary>
        /// 处理弹窗队列中的下一个事件
        /// </summary>
        private void ProcessNextPopupEvent()
        {
            while (_popupEventQueue != null && _popupQueueIndex < _popupEventQueue.Count)
            {
                string eventType = _popupEventQueue[_popupQueueIndex];
                _popupQueueIndex++;

                switch (eventType)
                {
                    case "newTribeEvent":
                        ProcessNewTribeEvent();
                        return;
                    case "recruitment":
                        ProcessRecruitment();
                        return;
                    case "ritual":
                        ProcessRitual();
                        return;
                    case "randomEvent":
                        ProcessRandomEvent();
                        return;
                    case "shop":
                        ShowShopAvailableHint();
                        return;
                    default:
                        continue;
                }
            }

            // 所有弹窗事件处理完毕，进入自由阶段
            EnterFreeActionPhase();
        }

        /// <summary>
        /// 处理招募&练兵
        /// </summary>
        private void ProcessRecruitment()
        {
            if (_dataManager.IsRecruitmentCompletedForRound(_roundManager.CurrentRound))
            {
                ProcessNextPopupEvent();
                return;
            }

            _isProcessingRecruitment = true;

            var options = _recruitmentService.GenerateOptions();

            if (_recruitmentPanel != null)
            {
                _recruitmentPanel.ShowOptions(options, OnRecruitmentOptionSelected, ResolveOptionTribeType);
            }
            else
            {
                OnRecruitmentOptionSelected(options.Count > 0 ? options[0] : null);
            }
        }

        private void OnRecruitmentOptionSelected(RecruitmentOption option)
        {
            if (option == null)
            {
                _isProcessingRecruitment = false;
                ProcessNextPopupEvent();
                return;
            }

            ExecuteRecruitmentOptionWithResult(option);
            _dataManager.SetRecruitmentCompletedForRound(_roundManager.CurrentRound);
            _isProcessingRecruitment = false;
            _saveManager?.SaveAfterRecruitment(_roundManager.CurrentRound);
        }

        private void ExecuteRecruitmentOptionWithResult(RecruitmentOption option)
        {
            switch (option.optionType)
            {
                case ChoiceCategory.Affix:
                {
                    // 撸铁系统 - 词缀选择
                    _recruitmentService.ExecuteAffixSelection(option);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    Debug.Log($"[TribeBuildPanel] 获得词缀: {option.affixData?.displayName}（兵种{option.affixData?.fighterId}）");
                    ProcessNextPopupEvent();
                    return;
                }
                case ChoiceCategory.AddCats:
                {
                    var tribe = FindTribeById(option.targetTribeId);
                    if (tribe == null) break;

                    // 使用 fighter 表中的名称
                    var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
                    string tribeName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";
                    var beforeUnits = SnapshotUnits(tribe.units);
                    _recruitmentService.ExecuteAddCats(tribe, option.cost, option.targetTier);
                    _tribes = _dataManager.GetTribes();

                    // 招募后检查是否有光环选择
                    if (option.targetTier.HasValue && _auraChoicePanel != null)
                    {
                        var auraChoice = _tribeAuraService?.GetAuraChoice(tribe.tribeType, option.targetTier.Value);
                        if (auraChoice != null && auraChoice.options.Count > 0)
                        {
                            _auraChoicePanel.Show(auraChoice, (chosenIds) =>
                            {
                                _tribeAuraService?.ApplyChosenAuras(tribe.tribeType, option.targetTier.Value, chosenIds);
                                RefreshUI();
                                ShowUnitListResult(tribeName, beforeUnits, tribe.units, tribe);
                            });
                            return;
                        }
                    }

                    RefreshUI();
                    ShowUnitListResult(tribeName, beforeUnits, tribe.units, tribe);
                    return;
                }
                case ChoiceCategory.QualityEvolution:
                {
                    var tribe = FindTribeById(option.targetTribeId);
                    if (tribe == null) break;

                    // 使用 fighter 表中的名称
                    var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
                    string tribeName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";

                    var beforeUnits = SnapshotUnits(tribe.units);
                    _recruitmentService.ExecuteQualityEvolution(tribe, option.cost);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    ShowUnitListResult(tribeName, beforeUnits, tribe.units, tribe);
                    return;
                }
                case ChoiceCategory.Buff:
                {
                    var tribe = FindTribeById(option.targetTribeId);
                    if (tribe == null) break;

                    // 使用 fighter 表中的名称
                    var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(tribe.fighterId);
                    string tribeName = fighterConfig?.fighterName ?? $"兵种{tribe.fighterId}";
                    // 优先使用 gameChoice（从 choice_config 生成）
                    if (option.gameChoice != null)
                    {
                        _auraService?.RegisterChoice(option.gameChoice);
                    }
                    else
                    {
                        _recruitmentService.ExecuteLeaderBoost(tribe, option.bonusAttack, option.bonusHp);
                    }
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    ShowLeaderBoostResult(tribeName, tribe.units[0], option.bonusAttack, option.bonusHp, tribe.fighterId);
                    return;
                }
                case ChoiceCategory.Reinforcement:
                {
                    if (!option.targetTribeType.HasValue) break;

                    var newTribe = _recruitmentService.ExecuteNewTribeRecruitment(option.targetTribeType.Value, option.cost);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();

                    if (newTribe != null)
                    {
                        // 使用 fighter 表中的名称
                        var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(newTribe.fighterId);
                        string tribeName = fighterConfig?.fighterName ?? $"兵种{newTribe.fighterId}";

                        ShowUnitListResult(tribeName, new List<FighterData>(), newTribe.units, newTribe);
                        return;
                    }
                    break;
                }
            }

            // fallback: 无结果面板，直接继续
            RefreshUI();
            ProcessNextPopupEvent();
        }

        private void ShowUnitListResult(string tribeName, List<FighterData> beforeUnits, List<FighterData> afterUnits, TribeRecord tribe)
        {
            if (_recruitmentResultPanel == null)
            {
                ProcessNextPopupEvent();
                return;
            }

            if (_forcedPopupRoot != null)
                _forcedPopupRoot.gameObject.SetActive(true);

            _recruitmentResultPanel.ShowCatListResult(tribeName, beforeUnits, afterUnits, tribe, () =>
            {
                if (_forcedPopupRoot != null)
                    _forcedPopupRoot.gameObject.SetActive(false);
                ProcessNextPopupEvent();
            });
        }

        private void ShowLeaderBoostResult(string tribeName, FighterData unit, int bonusAttack, int bonusHp, int fighterId)
        {
            if (_recruitmentResultPanel == null)
            {
                ProcessNextPopupEvent();
                return;
            }

            if (_forcedPopupRoot != null)
                _forcedPopupRoot.gameObject.SetActive(true);

            _recruitmentResultPanel.ShowLeaderBoostResult(tribeName, unit, bonusAttack, bonusHp, fighterId, () =>
            {
                if (_forcedPopupRoot != null)
                    _forcedPopupRoot.gameObject.SetActive(false);
                ProcessNextPopupEvent();
            });
        }

        private List<FighterData> SnapshotUnits(List<FighterData> units)
        {
            var snapshot = new List<FighterData>();
            if (units == null) return snapshot;
            foreach (var unit in units)
            {
                snapshot.Add(new FighterData
                {
                    id = unit.id,
                    fighterId = unit.fighterId,
                    quality = unit.quality,
                    tier = unit.tier,
                    staticAttack = unit.staticAttack,
                    staticDefense = unit.staticDefense,
                    staticHp = unit.staticHp,
                    staticMoveSpeed = unit.staticMoveSpeed,
                    staticAttackSpeed = unit.staticAttackSpeed
                });
            }
            return snapshot;
        }

        /// <summary>
        /// 处理祭祀
        /// </summary>
        private void ProcessRitual()
        {
            // 本回合已完成过祭祀，跳过
            if (_dataManager.IsRitualCompletedForRound(_roundManager.CurrentRound))
            {
                ProcessNextPopupEvent();
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
                _isProcessingRitual = false;
                ProcessNextPopupEvent();
            }
        }

        private void OnRitualConfirmed(RitualTier tier, RitualRewardItem blessing)
        {
            if (tier == null) { ProcessNextPopupEvent(); return; } // 玩家跳过祭祀
            _ritualService.ExecuteRitual(tier, blessing);

            // 展示获得的技能
            if (blessing.rewardType == RitualRewardType.LeaderSkill)
            {
                Debug.Log($"[TribeBuildPanel] 获得族长技能: {blessing.displayName}");
            }

            _dataManager.SetRitualCompletedForRound(_roundManager.CurrentRound);
            _isProcessingRitual = false;
            _saveManager?.SaveAfterRitual(_roundManager.CurrentRound);

            _tribes = _dataManager.GetTribes();

            // 立刻刷新猫粮显示
            UpdateCatFoodDisplay();
            RefreshUI();

            // 继续处理下一个弹窗事件
            ProcessNextPopupEvent();
        }

        private void ShowShopAvailableHint()
        {
            Debug.Log("[TribeBuildPanel] 商店已开放，可点击商店按钮进入");
            ProcessNextPopupEvent();
        }

        /// <summary>
        /// 处理随机事件（抉择）
        /// </summary>
        private void ProcessRandomEvent()
        {
            // 本回合已完成过随机事件，跳过
            if (_dataManager.IsRandomEventCompletedForRound(_roundManager.CurrentRound))
            {
                ProcessNextPopupEvent();
                return;
            }

            var randomEventService = new RandomEventService();
            var randomEvent = randomEventService.GenerateRandomEvent(_roundManager.CurrentRound);

            if (randomEvent == null)
            {
                ProcessNextPopupEvent();
                return;
            }

            if (_randomEventPanel != null)
            {
                _randomEventPanel.ShowEvent(randomEvent, OnRandomEventSelected);
            }
            else
            {
                ProcessNextPopupEvent();
            }
        }

        private void OnRandomEventSelected(RandomEventOption option)
        {
            if (option != null)
            {
                var randomEventService = new RandomEventService();
                randomEventService.ExecuteRandomEvent(option);

                _dataManager.SetRandomEventCompletedForRound(_roundManager.CurrentRound);
                _tribes = _dataManager.GetTribes();
                RefreshUI();
            }

            // 继续处理下一个弹窗事件
            ProcessNextPopupEvent();
        }

        /// <summary>
        /// 处理新部族事件 - 强制三选一
        /// </summary>
        private void ProcessNewTribeEvent()
        {
            // 本回合已完成过新部族事件，跳过
            if (_dataManager.IsNewTribeEventCompletedForRound(_roundManager.CurrentRound))
            {
                ProcessNextPopupEvent();
                return;
            }

            // 根据当前关卡判断摇人类型
            RecruitType recruitType = _recruitmentService.GetRecruitType(_roundManager.CurrentRound, false);

            if (recruitType == RecruitType.NewTribe)
            {
                // 第10关：加新兵种（使用 InitialTribeSelectionPanel，一轮三选一）
                int tribeCount = _tribes != null ? _tribes.Count : 0;
                if (tribeCount >= 6)
                {
                    ProcessNextPopupEvent();
                    return;
                }

                ShowFighterSelectionForNewTribe();
                return;
            }
            else if (recruitType == RecruitType.AddCats)
            {
                // 第3/5/7/9/11/13/15/17/19关：撸铁（词缀选择）
                ProcessRecruitment();
            }
            else
            {
                ProcessNextPopupEvent();
            }
        }

        private void ShowFighterSelectionForNewTribe()
        {
            // 通过 Addressables 加载 InitialTribeSelectionPanel 预制体
            var handle = Addressables.LoadAssetAsync<GameObject>("ui/initialtribeselectionpanel");
            handle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    CreateAndShowFighterSelection(op.Result);
                }
                else
                {
                    Debug.LogError("[TribeBuildPanel] 无法加载 InitialTribeSelectionPanel 预制体");
                    ProcessNextPopupEvent();
                }
            };
        }

        private void CreateAndShowFighterSelection(GameObject prefab)
        {
            var root = _forcedPopupRoot != null ? _forcedPopupRoot : transform as RectTransform;
            var go = Instantiate(prefab, root, false);
            go.name = "FighterSelectionForNewTribe";

            var panel = go.GetComponent<InitialTribeSelectionPanel>();
            if (panel != null)
            {
                panel.TotalRounds = 1; // 第十回合只选1轮
                panel.Initialize();
                panel.OnSelectionComplete += () =>
                {
                    // 选择完成，刷新UI
                    _dataManager.SetNewTribeEventCompletedForRound(_roundManager.CurrentRound);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    ProcessNextPopupEvent();
                };
            }
            else
            {
                Debug.LogError("[TribeBuildPanel] InitialTribeSelectionPanel 组件不存在");
                ProcessNextPopupEvent();
            }
        }

        private void CreateRandomEventPanel()
        {
            if (_randomEventPanel != null) return;

            GameObject go = new GameObject("RandomEventPanel", typeof(RectTransform));
            go.transform.SetParent(_forcedPopupRoot != null ? _forcedPopupRoot : transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _randomEventPanel = go.AddComponent<RandomEventPanel>();
            if (_forcedPopupRoot != null)
            {
                _randomEventPanel.SetExternalRoot(_forcedPopupRoot);
            }
            _randomEventPanel.Initialize();
            _randomEventPanel.Hide();
        }

        /// <summary>
        /// 进入自由操作阶段
        /// </summary>
        private void EnterFreeActionPhase()
        {
            // 玩家可以自由操作：查看族群、打开商店、准备战斗
        }

        private void CreateBackpackPanel()
        {
            if (_backpackPanelInstance != null) return;
            if (_backpackPanelPrefab == null) return;

            _backpackPanelInstance = Instantiate(_backpackPanelPrefab, transform);
            _backpackPanelInstance.gameObject.name = "BackpackPanel";
            _backpackPanelInstance.Hide();
        }

        /// <summary>
        /// 打开背包面板（可由UI按钮调用）
        /// </summary>
        public void OpenBackpack()
        {
            if (_backpackPanelInstance != null)
            {
                _backpackPanelInstance.Show();
            }
        }

        private void ShowShopNotOpenHint()
        {
            Debug.Log("[TribeBuildPanel] 商店未开放");
            // 创建一个临时的提示UI
            GameObject hintGo = new GameObject("ShopNotOpenHint", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            hintGo.transform.SetParent(transform, false);

            RectTransform hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0.5f);
            hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.sizeDelta = new Vector2(300f, 60f);
            hintRect.anchoredPosition = new Vector2(0, 100f);

            Image bg = hintGo.GetComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(hintGo.transform, false);

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textGo.GetComponent<Text>();
            text.text = "商店本回合未开放";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            // 1.5秒后自动销毁
            Destroy(hintGo, 1.5f);
        }

        /// <summary>
        /// 打开商店
        /// </summary>
        public void OpenShop()
        {
            if (_shopPanel == null) return;

            if (_roundManager != null && !_roundManager.CanOpenShop())
            {
                ShowShopNotOpenHint();
                return;
            }

            // 本回合首次打开时才生成商品，关闭再打开不会刷新
            if (_currentShopItems == null || _currentShopItems.Count == 0)
            {
                _currentShopItems = _shopService.GenerateShopItems();
            }
            int refreshCost = _shopService.CalculateRefreshCost();

            _shopPanel.ShowShop(_currentShopItems, refreshCost, OnShopItemBuy, OnShopRefresh, OnShopClose, OnCatToSellSelected);
            _isShopOpen = true;
        }

        private void OnCatToSellSelected(TribeRecord tribe, FighterData unit)
        {
            _shopService.SellCat(tribe, unit);
            _tribes = _dataManager.GetTribes();
            RefreshUI();
        }

        private void OnShopItemBuy(ShopItem item)
        {
            Debug.Log($"[TribeBuildPanel][Debug] OnShopItemBuy: type={item.itemType}, name={item.name}, artifactEffectType={item.artifactEffectType}");
            int result = _shopService.BuyItem(item);
            Debug.Log($"[TribeBuildPanel][Debug] BuyItem result={result}");
            if (result == 1)
            {
                RefreshUI();
                _shopPanel.UpdateCatFoodDisplay();

                // 售罄物品从列表移除
                if (item.stock <= 0)
                {
                    _currentShopItems.Remove(item);
                }
                _shopPanel.RefreshItems(_currentShopItems);

                // 商店购买后存档
                _saveManager?.SaveAfterShopPurchase(_roundManager.CurrentRound);

                // 奇物购买提示
                if (item.itemType == ShopItemType.Artifact)
                {
                    ShowPurchaseHint($"获得了奇物：{item.name}");
                }
            }
        }

        private void ShowPurchaseHint(string message)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = _forcedPopupRoot != null ? _forcedPopupRoot : transform as RectTransform;

            GameObject hintGo = new GameObject("PurchaseHint", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            hintGo.transform.SetParent(root, false);
            RectTransform hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.3f, 0.4f);
            hintRect.anchorMax = new Vector2(0.7f, 0.6f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            Image hintBg = hintGo.GetComponent<Image>();
            hintBg.color = new Color(0.15f, 0.12f, 0.2f, 0.95f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(hintRect, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            Text text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.9f, 0.4f, 1f);
            text.text = message;

            Destroy(hintGo, 2f);
        }

        private void OnShopRefresh()
        {
            var newItems = _shopService.RefreshShop();
            if (newItems != null)
            {
                _currentShopItems = newItems;
                int newRefreshCost = _shopService.CalculateRefreshCost();
                _shopPanel.ShowShop(_currentShopItems, newRefreshCost, OnShopItemBuy, OnShopRefresh, OnShopClose, OnCatToSellSelected);
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

                // 设置战斗准备（所有族群默认上阵）
                battlePreparePanel.SetupBattle(
                    _dataManager.GetCurrentRound(),
                    allTribes
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
                Debug.Log("[TribeBuildPanel] 战斗失败，奖励减半，继续推进");

                // 通知GameFlowController战斗结束
                GameFlowController.Instance?.OnBattleEnded(victory);

                // 失败也推进回合
                AdvanceRound();
            }

            RefreshUI();
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
            foreach (var tribe in _tribes)
            {
                // 减少回合制统一 buff 的持续回合
                DecreaseRoundBasedBuffs(tribe);
            }

            _dataManager.SavePlayerData();
        }

        private void DecreaseRoundBasedBuffs(TribeRecord tribe)
        {
            if (tribe.units == null) return;
            foreach (var unit in tribe.units)
            {
                if (unit?.ActiveBuffs != null)
                    DecreaseBuffs(unit.ActiveBuffs);
            }
        }

        private void DecreaseBuffs(System.Collections.Generic.List<UnifiedBuff> buffs)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].IsRoundBased)
                {
                    buffs[i].remainingRounds--;
                    if (buffs[i].IsExpired)
                        buffs.RemoveAt(i);
                }
            }
        }

        private void ShowInitialTribeSelection()
        {
            // TODO: 显示初始六选一界面
            Debug.Log("[TribeBuildPanel] 显示初始族群选择界面");

            // 暂时自动选择一个族群
            var maineTribe = CreateInitialTribe(TribeType.Tabby);

            _dataManager.AddTribe(maineTribe);

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
                fighterId = config.leaderFighterId,
                tribeType = type,
                units = new List<FighterData>(),
                isActive = true
            };

            // 添加初始白色单位
            var unit = FighterData.CreateWithQuality(CatQuality.White, type);
            tribe.units.Add(unit);

            return tribe;
        }

        private void UpdateCatFoodDisplay()
        {
            if (_catFoodText == null)
            {
                var t = transform.Find("CatFoodText");
                if (t != null) _catFoodText = t.GetComponent<Text>();
            }
            if (_catFoodText != null)
            {
                _catFoodText.text = $"猫粮: {_dataManager.GetCatFood()}";
            }
        }

        private void RefreshUI()
        {
            // 更新回合显示（使用RoundManager获取描述）
            if (_roundText != null)
            {
                _roundText.text = _roundManager?.GetRoundDescription() ?? $"第{_dataManager.GetCurrentRound()}回合";
            }

            // 更新猫粮显示
            UpdateCatFoodDisplay();

            // 商店入口的显示/隐藏（根据当前回合商店是否营业）
            if (_roundManager != null)
            {
                bool canOpenShop = _roundManager.CanOpenShop();
                if (_shopEntranceObj != null)
                {
                    _shopEntranceObj.SetActive(canOpenShop);
                }
                else if (_shopButton != null)
                {
                    // Fallback：如果没有拖拽指定的入口节点，默认隐藏商店按钮
                    _shopButton.gameObject.SetActive(canOpenShop);
                }
            }

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
            if (_tribeListContainer == null || _tribeCardPrefab == null) { Debug.Log($"[TribeBuildPanel][Debug] RefreshTribeList: container={_tribeListContainer != null}, prefab={_tribeCardPrefab != null}"); return; }

            // 清空现有列表
            for (int i = _tribeListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_tribeListContainer.GetChild(i).gameObject);
            }
            _displayedCard = null;
            _currentDisplayTribeId = -1;

            Debug.Log($"[TribeBuildPanel][Debug] RefreshTribeList: _tribes={_tribes != null}, count={_tribes?.Count}");
            if (_tribes != null && _tribes.Count > 0)
            {
                var t = _tribes[0];
                Debug.Log($"[TribeBuildPanel][Debug] RefreshTribeList: tribes[0]={t.tribeType}, units={t.units?.Count}");
            }

            if (_tribes == null || _tribes.Count == 0) return;

            // 创建唯一的一张 TribeCard，始终展开
            TribeCard card = Instantiate(_tribeCardPrefab, _tribeListContainer, false);
            _displayedCard = card;

            // 默认显示第一个族群
            ShowTribeOnCard(_tribes[0]);

            // 为所有部族生成头像
            RebuildAllTribeAvatars();
        }

        /// <summary>
        /// 在唯一的 TribeCard 上显示指定族群的属性
        /// </summary>
        private void ShowTribeOnCard(TribeRecord tribe)
        {
            if (_displayedCard == null || tribe == null) { Debug.Log($"[TribeBuildPanel][Debug] ShowTribeOnCard: card={_displayedCard != null}, tribe={tribe != null}"); return; }
            if (_currentDisplayTribeId == tribe.tribeId) { Debug.Log($"[TribeBuildPanel][Debug] ShowTribeOnCard: same tribe {_currentDisplayTribeId}, skipping"); return; }

            Debug.Log($"[TribeBuildPanel][Debug] ShowTribeOnCard: tribe={tribe.tribeType}, units={tribe.units?.Count}");
            _currentDisplayTribeId = tribe.tribeId;
            _displayedCard.Setup(tribe, false, null, null);
        }

        private void RebuildAllTribeAvatars()
        {
            if (_tribeAvatarRoot == null) return;

            // Clear old avatars
            for (int i = _tribeAvatarRoot.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(_tribeAvatarRoot.transform.GetChild(i).gameObject);
            }

            // Release all old handles
            foreach (var h in _avatarHandles)
            {
                if (h.IsValid()) Addressables.Release(h);
            }
            _avatarHandles.Clear();
            _avatarIdleSprites.Clear();
            _avatarAttackSprites.Clear();
            _selectedAvatarGo = null;

            // 创建选中指示器（▼）— 每次重建都重新创建，因为旧的已被销毁
            _selectionIndicator = new GameObject("SelectionIndicator", typeof(RectTransform), typeof(Text));
            _selectionIndicator.transform.SetParent(_tribeAvatarRoot.transform, false);
            RectTransform indRt = _selectionIndicator.GetComponent<RectTransform>();
            indRt.anchorMin = new Vector2(0.5f, 0.5f);
            indRt.anchorMax = new Vector2(0.5f, 0.5f);
            indRt.sizeDelta = new Vector2(65f, 65f);
            Text indText = _selectionIndicator.GetComponent<Text>();
            indText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            indText.fontSize = 56;
            indText.alignment = TextAnchor.MiddleCenter;
            indText.color = new Color(0.286f, 1f, 0.2f, 1f); // #49FF33
            indText.text = "▼";
            _selectionIndicator.SetActive(false);

            if (_tribes == null || _tribes.Count == 0) return;

            int tribeCount = _tribes.Count;
            float spacing = 400f;

            for (int t = 0; t < tribeCount; t++)
            {
                var tribe = _tribes[t];
                if (tribe.units == null || tribe.units.Count == 0) continue;

                float tribeCenterX = (t - (tribeCount - 1) / 2f) * spacing;
                int clickedTribeId = tribe.tribeId;

                // 使用 units[0] 的 fighterId 加载头像
                var firstUnit = tribe.units[0];
                int firstFighterId = firstUnit.fighterId > 0 ? firstUnit.fighterId : tribe.fighterId;
                string firstIdleAddr = TribeConfigLoader.Instance?.GetFighterAvatarAddress(firstFighterId, 1);
                string firstAttackAddr = TribeConfigLoader.Instance?.GetFighterAvatarAddress(firstFighterId, 2);
                if (string.IsNullOrEmpty(firstIdleAddr) && string.IsNullOrEmpty(firstAttackAddr)) continue;

                // 并行加载首个单位 idle 和 attack 两帧
                var idleHandle = Addressables.LoadAssetAsync<Sprite>(firstIdleAddr);
                var attackHandle = Addressables.LoadAssetAsync<Sprite>(firstAttackAddr);
                _avatarHandles.Add(idleHandle);
                _avatarHandles.Add(attackHandle);

                int pending = 2;
                Sprite firstIdleSprite = null;
                Sprite firstAttackSprite = null;

                System.Action onFirstUnitLoaded = () =>
                {
                    if (_tribeAvatarRoot == null) return;
                    Sprite defaultSprite = firstIdleSprite ?? firstAttackSprite;
                    if (defaultSprite == null) return;

                    bool tribeIsSelected = (_currentDisplayTribeId == clickedTribeId);
                    bool firstUnitIsSelected = tribeIsSelected && _selectedUnitIndex == 0;

                    // 第一个单位（大头像）
                    GameObject firstGo = new GameObject($"Leader_{tribe.tribeType}", typeof(RectTransform), typeof(Image), typeof(Button));
                    firstGo.transform.SetParent(_tribeAvatarRoot.transform, false);
                    RectTransform firstRt = firstGo.GetComponent<RectTransform>();
                    firstRt.anchorMin = new Vector2(0.5f, 0.5f);
                    firstRt.anchorMax = new Vector2(0.5f, 0.5f);
                    float firstX = tribeCenterX + Random.Range(-30f, 30f);
                    float firstY = Random.Range(-30f, 30f);
                    firstRt.anchoredPosition = new Vector2(firstX, firstY);
                    float firstScale = 180f;
                    firstRt.sizeDelta = new Vector2(firstScale, firstScale);
                    Image firstImg = firstGo.GetComponent<Image>();
                    firstImg.sprite = firstUnitIsSelected && firstAttackSprite != null ? firstAttackSprite : defaultSprite;
                    firstImg.color = new Color(1f, 1f, 1f, Random.Range(0.85f, 1f));
                    Button firstBtn = firstGo.GetComponent<Button>();
                    firstBtn.transition = Selectable.Transition.None;
                    firstBtn.onClick.AddListener(() => OnUnitAvatarClicked(clickedTribeId, 0));

                    if (firstIdleSprite != null) _avatarIdleSprites[firstGo] = firstIdleSprite;
                    if (firstAttackSprite != null) _avatarAttackSprites[firstGo] = firstAttackSprite;
                    if (firstUnitIsSelected)
                    {
                        _selectedAvatarGo = firstGo;
                        PositionIndicatorAbove(firstRt);
                    }

                    // 为后续单位加载外观
                    int unitCount = tribe.GetUnitCount();
                    for (int i = 1; i < unitCount; i++)
                    {
                        var unit = tribe.units[i];
                        int unitFighterId = GetFighterIdForAvatar(tribe.tribeType, unit.tier);
                        GetCatAvatarAddresses(unitFighterId, out string unitIdleAddr, out string unitAttackAddr);
                        if (string.IsNullOrEmpty(unitIdleAddr) && string.IsNullOrEmpty(unitAttackAddr)) continue;

                        int unitIdx = i;
                        bool unitIsSelected = (tribeIsSelected && i == _selectedUnitIndex);

                        var unitIdleHandle = Addressables.LoadAssetAsync<Sprite>(unitIdleAddr);
                        var unitAttackHandle = Addressables.LoadAssetAsync<Sprite>(unitAttackAddr);
                        _avatarHandles.Add(unitIdleHandle);
                        _avatarHandles.Add(unitAttackHandle);

                        int unitPending = 2;
                        Sprite unitIdle = null;
                        Sprite unitAttack = null;

                        System.Action onUnitLoaded = () =>
                        {
                            if (_tribeAvatarRoot == null) return;
                            Sprite unitDefault = unitIdle ?? unitAttack;
                            if (unitDefault == null) return;

                            GameObject unitGo = new GameObject($"Cat_{tribe.tribeType}_{unitIdx - 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                            unitGo.transform.SetParent(_tribeAvatarRoot.transform, false);
                            RectTransform unitRt = unitGo.GetComponent<RectTransform>();
                            unitRt.anchorMin = new Vector2(0.5f, 0.5f);
                            unitRt.anchorMax = new Vector2(0.5f, 0.5f);

                            float baseX = tribeCenterX + ((unitIdx - 1) - (unitCount - 2) / 2f) * 80f;
                            float unitX = baseX + Random.Range(-30f, 30f);
                            float unitY = Random.Range(-200f, -120f);
                            unitRt.anchoredPosition = new Vector2(unitX, unitY);

                            float unitSize = 65f;
                            unitRt.sizeDelta = new Vector2(unitSize, unitSize);

                            Image unitImg = unitGo.GetComponent<Image>();
                            unitImg.sprite = unitIsSelected && unitAttack != null ? unitAttack : unitDefault;
                            unitImg.color = new Color(1f, 1f, 1f, Random.Range(0.6f, 0.95f));
                            Button unitBtn = unitGo.GetComponent<Button>();
                            unitBtn.transition = Selectable.Transition.None;
                            unitBtn.onClick.AddListener(() => OnUnitAvatarClicked(clickedTribeId, unitIdx));

                            if (unitIdle != null) _avatarIdleSprites[unitGo] = unitIdle;
                            if (unitAttack != null) _avatarAttackSprites[unitGo] = unitAttack;
                            if (unitIsSelected)
                            {
                                _selectedAvatarGo = unitGo;
                                PositionIndicatorAbove(unitRt);
                            }
                        };

                        int capturedUnitPending = unitPending;
                        unitIdleHandle.Completed += (op) =>
                        {
                            if (op.Status == AsyncOperationStatus.Succeeded) unitIdle = op.Result;
                            if (--capturedUnitPending == 0) onUnitLoaded();
                        };
                        unitAttackHandle.Completed += (op) =>
                        {
                            if (op.Status == AsyncOperationStatus.Succeeded) unitAttack = op.Result;
                            if (--capturedUnitPending == 0) onUnitLoaded();
                        };
                    }
                };

                idleHandle.Completed += (op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded) firstIdleSprite = op.Result;
                    if (--pending == 0) onFirstUnitLoaded();
                };
                attackHandle.Completed += (op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded) firstAttackSprite = op.Result;
                    if (--pending == 0) onFirstUnitLoaded();
                };
            }
        }

        /// <summary>
        /// 设置头像选中状态：选中切 attack 帧，取消切 idle 帧
        /// </summary>
        private void SetAvatarSelected(GameObject avatarGo, bool selected)
        {
            if (avatarGo == null) return;
            var img = avatarGo.GetComponent<Image>();
            if (img == null) return;

            if (selected)
            {
                if (_avatarAttackSprites.TryGetValue(avatarGo, out var atkSprite))
                    img.sprite = atkSprite;
                // 移动指示器到选中头像上方
                var rt = avatarGo.GetComponent<RectTransform>();
                if (rt != null) PositionIndicatorAbove(rt);
            }
            else
            {
                if (_avatarIdleSprites.TryGetValue(avatarGo, out var idleSprite))
                    img.sprite = idleSprite;
            }
        }

        /// <summary>
        /// 将 ▼ 指示器定位到目标 RectTransform 上方
        /// </summary>
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

        private void OnUnitAvatarClicked(int tribeId, int unitIndex)
        {
            var tribe = _tribes?.Find(t => t.tribeId == tribeId);
            if (tribe == null || tribe.units == null || unitIndex < 0 || unitIndex >= tribe.units.Count) return;

            // 切换选中头像的 sprite
            GameObject clickedGo = FindAvatarGoByTribe(tribeId, unitIndex);
            if (_selectedAvatarGo != null && _selectedAvatarGo != clickedGo)
                SetAvatarSelected(_selectedAvatarGo, false);
            SetAvatarSelected(clickedGo, true);
            _selectedAvatarGo = clickedGo;

            _selectedUnitIndex = unitIndex;
            _selectedUnitTribeId = tribeId;
            _currentDisplayTribeId = -1;
            // 第一个单位不可出售，其他单位可以出售
            if (_sellCatButton != null) _sellCatButton.gameObject.SetActive(unitIndex > 0);
            if (unitIndex > 0)
                _displayedCard?.SetupForUnit(tribe.units[unitIndex], tribe);
            else
                ShowTribeOnCard(tribe);
        }

        /// <summary>
        /// 根据 tribeId 和 unitIndex 查找对应的头像 GameObject
        /// unitIndex == 0 表示首个单位（大头像），>0 表示后续单位
        /// </summary>
        private GameObject FindAvatarGoByTribe(int tribeId, int unitIndex)
        {
            var tribe = _tribes?.Find(t => t.tribeId == tribeId);
            if (tribe == null || _tribeAvatarRoot == null) return null;

            string prefix = unitIndex == 0 ? $"Leader_{tribe.tribeType}" : $"Cat_{tribe.tribeType}_{unitIndex - 1}";
            for (int i = 0; i < _tribeAvatarRoot.transform.childCount; i++)
            {
                var child = _tribeAvatarRoot.transform.GetChild(i);
                if (child.gameObject.name == prefix)
                    return child.gameObject;
            }
            return null;
        }

        private void OnSellCatButtonClicked()
        {
            if (_selectedUnitIndex <= 0) return;
            var tribe = _tribes?.Find(t => t.tribeId == _selectedUnitTribeId);
            if (tribe == null || tribe.units == null || _selectedUnitIndex >= tribe.units.Count) return;

            FighterData unit = tribe.units[_selectedUnitIndex];
            int sellPrice = _shopService.SellCat(tribe, unit);
            Debug.Log($"[TribeBuildPanel] 出售单位获得 {sellPrice} 猫粮");

            if (_catFoodText != null)
                _catFoodText.text = $"猫粮: {_dataManager.GetCatFood()}";

            // 刷新卡片属性
            _currentDisplayTribeId = -1; // 重置以强制刷新
            ShowTribeOnCard(tribe);

            // 刷新头像
            RebuildAllTribeAvatars();

            // 隐藏出售按钮（已卖掉）
            _selectedUnitIndex = 0;
            _selectedUnitTribeId = -1;
            if (_sellCatButton != null) _sellCatButton.gameObject.SetActive(false);

            _dataManager?.SavePlayerData();
        }

        /// <summary>
        /// 从 tribe_config + fighter_config 获取单位的 fighterId
        /// </summary>
        private int GetFighterIdForAvatar(TribeType tribeType, UnitTier tier)
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

        /// <summary>
        /// 根据 fighterId 的 avatarId 返回 idle/attack sprite 地址
        /// </summary>
        private void GetCatAvatarAddresses(int fighterId, out string idleAddr, out string attackAddr)
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
            idleAddr = null;
            attackAddr = null;
        }

        private TribeRecord FindTribeById(int tribeId)
        {
            return _tribes?.Find(t => t.tribeId == tribeId);
        }

        private TribeType ResolveOptionTribeType(RecruitmentOption option)
        {
            if (option.targetTribeType.HasValue)
                return option.targetTribeType.Value;
            var tribe = FindTribeById(option.targetTribeId);
            return tribe?.tribeType ?? TribeType.Tabby;
        }
    }
}
