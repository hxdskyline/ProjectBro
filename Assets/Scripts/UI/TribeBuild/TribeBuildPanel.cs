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

        [Header("子面板引用")]
        [SerializeField] private RecruitmentPanel _recruitmentPanel;
        [SerializeField] private RitualPanel _ritualPanel;
        [SerializeField] private ShopPanel _shopPanel;
        [SerializeField] private NewTribeEventPanel _newTribeEventPanel;
        private AccessoryCodexPanel _codexPanel;
        private RecruitmentResultPanel _recruitmentResultPanel;

        [Header("强制弹窗根节点")]
        [SerializeField] private RectTransform _forcedPopupRoot;
        [SerializeField] private GameObject _tribeAvatarRoot;

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
        private List<ShopItem> _currentShopItems;

        // 优先级弹窗队列
        private List<string> _popupEventQueue;
        private int _popupQueueIndex;

        private TribeCard _displayedCard;
        private int _currentDisplayTribeId = -1;
        private int _selectedCatIndex = -1; // -1=选中leader, >=0=选中小猫索引
        private int _selectedCatTribeId = -1; // 选中小猫所属的 tribeId

        private System.Collections.Generic.List<AsyncOperationHandle<Sprite>> _avatarHandles = new System.Collections.Generic.List<AsyncOperationHandle<Sprite>>();

        // 头像 idle/attack 帧切换
        private GameObject _selectedAvatarGo;
        private Dictionary<GameObject, Sprite> _avatarIdleSprites = new Dictionary<GameObject, Sprite>();
        private Dictionary<GameObject, Sprite> _avatarAttackSprites = new Dictionary<GameObject, Sprite>();
        private GameObject _selectionIndicator; // ▼ 选中指示器

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
            if (_newTribeEventPanel != null && _newTribeEventPanel.gameObject.activeInHierarchy) isPopupActive = true;
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

            // 绑定出售小猫按钮
            if (_sellCatButton != null)
            {
                _sellCatButton.onClick.RemoveAllListeners();
                _sellCatButton.onClick.AddListener(OnSellCatButtonClicked);
                _sellCatButton.gameObject.SetActive(false);
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

            // 新部族事件面板（支持预制体，fallback 运行时创建）
            _newTribeEventPanel = InstantiatePanelIfNeeded(_newTribeEventPanel, "NewTribeEventPanel");
            if (_newTribeEventPanel == null)
            {
                CreateNewTribeEventPanel();
            }
            else if (_forcedPopupRoot != null)
            {
                _newTribeEventPanel.SetExternalRoot(_forcedPopupRoot);
                _newTribeEventPanel.Initialize();
                _newTribeEventPanel.Hide();
            }

            // 图鉴面板（运行时创建）
            CreateCodexPanel();

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
            // 本回合已完成过招募，跳过
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
                case RecruitmentOptionType.AddCats:
                {
                    var tribe = FindTribeById(option.targetTribeId);
                    if (tribe == null) break;
                    string tribeName = GetTribeTypeName(tribe.tribeType);
                    var beforeCats = SnapshotCats(tribe.cats);
                    _recruitmentService.ExecuteAddCats(tribe, option.cost);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    ShowCatListResult(tribeName, beforeCats, tribe.cats, tribe);
                    return;
                }
                case RecruitmentOptionType.QualityEvolution:
                {
                    var tribe = FindTribeById(option.targetTribeId);
                    if (tribe == null) break;
                    string tribeName = GetTribeTypeName(tribe.tribeType);
                    var beforeCats = SnapshotCats(tribe.cats);
                    _recruitmentService.ExecuteQualityEvolution(tribe, option.cost);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    ShowCatListResult(tribeName, beforeCats, tribe.cats, tribe);
                    return;
                }
                case RecruitmentOptionType.LeaderBoost:
                {
                    var tribe = FindTribeById(option.targetTribeId);
                    if (tribe == null) break;
                    string tribeName = GetTribeTypeName(tribe.tribeType);
                    _recruitmentService.ExecuteLeaderBoost(tribe, option.bonusAttack, option.bonusHp);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    ShowLeaderBoostResult(tribeName, tribe.leader, option.bonusAttack, option.bonusHp, tribe.tribeType);
                    return;
                }
                case RecruitmentOptionType.NewTribe:
                {
                    if (!option.targetTribeType.HasValue) break;
                    string tribeName = GetTribeTypeName(option.targetTribeType.Value);
                    var newTribe = _recruitmentService.ExecuteNewTribeRecruitment(option.targetTribeType.Value, option.cost);
                    _tribes = _dataManager.GetTribes();
                    RefreshUI();
                    if (newTribe != null)
                    {
                        ShowCatListResult(tribeName, new List<CatData>(), newTribe.cats, newTribe);
                        return;
                    }
                    break;
                }
            }

            // fallback: 无结果面板，直接继续
            RefreshUI();
            ProcessNextPopupEvent();
        }

        private void ShowCatListResult(string tribeName, List<CatData> beforeCats, List<CatData> afterCats, TribeRecord tribe)
        {
            if (_recruitmentResultPanel == null)
            {
                ProcessNextPopupEvent();
                return;
            }

            if (_forcedPopupRoot != null)
                _forcedPopupRoot.gameObject.SetActive(true);

            _recruitmentResultPanel.ShowCatListResult(tribeName, beforeCats, afterCats, tribe, () =>
            {
                if (_forcedPopupRoot != null)
                    _forcedPopupRoot.gameObject.SetActive(false);
                ProcessNextPopupEvent();
            });
        }

        private void ShowLeaderBoostResult(string tribeName, LeaderData leader, int bonusAttack, int bonusHp, TribeType tribeType)
        {
            if (_recruitmentResultPanel == null)
            {
                ProcessNextPopupEvent();
                return;
            }

            if (_forcedPopupRoot != null)
                _forcedPopupRoot.gameObject.SetActive(true);

            _recruitmentResultPanel.ShowLeaderBoostResult(tribeName, leader, bonusAttack, bonusHp, tribeType, () =>
            {
                if (_forcedPopupRoot != null)
                    _forcedPopupRoot.gameObject.SetActive(false);
                ProcessNextPopupEvent();
            });
        }

        private List<CatData> SnapshotCats(List<CatData> cats)
        {
            var snapshot = new List<CatData>();
            if (cats == null) return snapshot;
            foreach (var cat in cats)
            {
                snapshot.Add(new CatData
                {
                    catId = cat.catId,
                    quality = cat.quality,
                    attackMultiplier = cat.attackMultiplier,
                    defenseMultiplier = cat.defenseMultiplier,
                    hpMultiplier = cat.hpMultiplier,
                    speedMultiplier = cat.speedMultiplier
                });
            }
            return snapshot;
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
        /// 处理新部族事件
        /// </summary>
        private void ProcessNewTribeEvent()
        {
            // 本回合已完成过新部族事件，跳过
            if (_dataManager.IsNewTribeEventCompletedForRound(_roundManager.CurrentRound))
            {
                ProcessNextPopupEvent();
                return;
            }

            // 部族数>=6时不再触发
            int tribeCount = _tribes != null ? _tribes.Count : 0;
            if (tribeCount >= 6)
            {
                ProcessNextPopupEvent();
                return;
            }

            // 检查是否还有未拥有的部族类型
            var availableTypes = _recruitmentService.GetAvailableTribeTypes();

            // 构造二选一选项
            var options = new List<NewTribeEventOption>();
            if (availableTypes.Count > 0)
            {
                options.Add(new NewTribeEventOption
                {
                    optionType = NewTribeEventOptionType.NewRandomTribe,
                    description = "获得一个随机新部族"
                });
            }
            options.Add(new NewTribeEventOption
            {
                optionType = NewTribeEventOptionType.CatFoodReward,
                description = "获得1000猫粮"
            });

            if (_newTribeEventPanel != null)
            {
                _newTribeEventPanel.ShowOptions(options, OnNewTribeEventConfirmed);
            }
            else
            {
                ProcessNextPopupEvent();
            }
        }

        private void OnNewTribeEventConfirmed(NewTribeEventOption option)
        {
            if (option != null)
            {
                switch (option.optionType)
                {
                    case NewTribeEventOptionType.NewRandomTribe:
                        var availableTypes = _recruitmentService.GetAvailableTribeTypes();
                        if (availableTypes.Count > 0)
                        {
                            TribeType selected = availableTypes[UnityEngine.Random.Range(0, availableTypes.Count)];
                            _recruitmentService.ExecuteFreeNewTribeRecruitment(selected);
                            Debug.Log($"[TribeBuildPanel] 新部族事件：免费获得 {selected} 部族");
                        }
                        break;

                    case NewTribeEventOptionType.CatFoodReward:
                        _dataManager.AddCatFood(1000);
                        Debug.Log("[TribeBuildPanel] 新部族事件：获得1000猫粮");
                        break;
                }

                _dataManager.SetNewTribeEventCompletedForRound(_roundManager.CurrentRound);
                _tribes = _dataManager.GetTribes();
                RefreshUI();
            }

            // 继续处理下一个弹窗事件
            ProcessNextPopupEvent();
        }

        private void CreateNewTribeEventPanel()
        {
            if (_newTribeEventPanel != null) return;

            GameObject go = new GameObject("NewTribeEventPanel", typeof(RectTransform));
            go.transform.SetParent(_forcedPopupRoot != null ? _forcedPopupRoot : transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _newTribeEventPanel = go.AddComponent<NewTribeEventPanel>();
            if (_forcedPopupRoot != null)
            {
                _newTribeEventPanel.SetExternalRoot(_forcedPopupRoot);
            }
            _newTribeEventPanel.Initialize();
            _newTribeEventPanel.Hide();
        }

        /// <summary>
        /// 进入自由操作阶段
        /// </summary>
        private void EnterFreeActionPhase()
        {
            // 玩家可以自由操作：查看族群、打开商店、准备战斗
        }

        private void CreateCodexPanel()
        {
            if (_codexPanel != null) return;

            GameObject go = new GameObject("AccessoryCodexPanel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _codexPanel = go.AddComponent<AccessoryCodexPanel>();
            _codexPanel.Initialize();
            _codexPanel.Hide();
        }

        /// <summary>
        /// 打开图鉴面板（可由UI按钮调用）
        /// </summary>
        public void OpenCodex()
        {
            if (_codexPanel != null)
            {
                _codexPanel.Show();
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

        private void OnCatToSellSelected(TribeRecord tribe, CatData cat)
        {
            _shopService.SellCat(tribe, cat);
            _tribes = _dataManager.GetTribes();
            RefreshUI();
        }

        private void OnShopItemBuy(ShopItem item)
        {
            int result = _shopService.BuyItem(item);
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
            }
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
                Debug.Log("[TribeBuildPanel] 战斗失败，可以重新挑战");

                // 通知GameFlowController战斗失败
                GameFlowController.Instance?.OnBattleEnded(victory);

                // 失败不推进回合，允许重新调整阵容
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
            // 减少限时buff持续时间
            foreach (var tribe in _tribes)
            {
                if (tribe.leader?.temporaryBuff != null)
                {
                    tribe.leader.temporaryBuff.DecreaseDuration();
                }
            }

            _dataManager.SavePlayerData();
        }

        private void ShowInitialTribeSelection()
        {
            // TODO: 显示初始六选一界面
            Debug.Log("[TribeBuildPanel] 显示初始族群选择界面");

            // 暂时自动选择一个族群
            var maineTribe = CreateInitialTribe(TribeType.Maine);

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
                tribe.cats.Add(CatData.CreateWithQuality(CatQuality.White, type));
            }

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
            if (_tribeListContainer == null || _tribeCardPrefab == null) return;

            // 清空现有列表
            for (int i = _tribeListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_tribeListContainer.GetChild(i).gameObject);
            }
            _displayedCard = null;
            _currentDisplayTribeId = -1;

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
            if (_displayedCard == null || tribe == null) return;
            if (_currentDisplayTribeId == tribe.tribeId) return;

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

            // 创建选中指示器（▼）
            if (_selectionIndicator == null)
            {
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
            }
            _selectionIndicator.SetActive(false);

            if (_tribes == null || _tribes.Count == 0) return;

            int tribeCount = _tribes.Count;
            float spacing = 400f;

            for (int t = 0; t < tribeCount; t++)
            {
                var tribe = _tribes[t];
                string breed = GetTribeBreedName(tribe.tribeType);
                if (string.IsNullOrEmpty(breed)) continue;

                string idleAddr = $"avatartemp/{breed}1";
                string attackAddr = $"avatartemp/{breed}2";

                float tribeCenterX = (t - (tribeCount - 1) / 2f) * spacing;
                int clickedTribeId = tribe.tribeId;
                bool isFirstTribe = (t == 0);

                // 并行加载 idle 和 attack 两帧
                var idleHandle = Addressables.LoadAssetAsync<Sprite>(idleAddr);
                var attackHandle = Addressables.LoadAssetAsync<Sprite>(attackAddr);
                _avatarHandles.Add(idleHandle);
                _avatarHandles.Add(attackHandle);

                int pending = 2;
                Sprite idleSprite = null;
                Sprite attackSprite = null;

                System.Action onBothLoaded = () =>
                {
                    if (_tribeAvatarRoot == null) return;
                    Sprite defaultSprite = idleSprite ?? attackSprite;
                    if (defaultSprite == null) return;

                    bool tribeIsSelected = (_currentDisplayTribeId == clickedTribeId);
                    bool leaderIsSelected = tribeIsSelected && _selectedCatIndex < 0;
                    int selectedCat = tribeIsSelected ? _selectedCatIndex : -1;

                    // Leader
                    GameObject leaderGo = new GameObject($"Leader_{tribe.tribeType}", typeof(RectTransform), typeof(Image), typeof(Button));
                    leaderGo.transform.SetParent(_tribeAvatarRoot.transform, false);
                    RectTransform leaderRt = leaderGo.GetComponent<RectTransform>();
                    leaderRt.anchorMin = new Vector2(0.5f, 0.5f);
                    leaderRt.anchorMax = new Vector2(0.5f, 0.5f);
                    float leaderX = tribeCenterX + Random.Range(-30f, 30f);
                    float leaderY = Random.Range(-30f, 30f);
                    leaderRt.anchoredPosition = new Vector2(leaderX, leaderY);
                    float leaderScale = Random.Range(160f, 220f);
                    leaderRt.sizeDelta = new Vector2(leaderScale, leaderScale);
                    Image leaderImg = leaderGo.GetComponent<Image>();
                    leaderImg.sprite = leaderIsSelected && attackSprite != null ? attackSprite : defaultSprite;
                    leaderImg.color = new Color(1f, 1f, 1f, Random.Range(0.85f, 1f));
                    Button leaderBtn = leaderGo.GetComponent<Button>();
                    leaderBtn.transition = Selectable.Transition.None;
                    leaderBtn.onClick.AddListener(() => OnLeaderAvatarClicked(clickedTribeId));

                    if (idleSprite != null) _avatarIdleSprites[leaderGo] = idleSprite;
                    if (attackSprite != null) _avatarAttackSprites[leaderGo] = attackSprite;
                    if (leaderIsSelected)
                    {
                        _selectedAvatarGo = leaderGo;
                        PositionIndicatorAbove(leaderRt);
                    }

                    // Cats
                    int catCount = tribe.GetCatCount();
                    if (catCount > 0)
                    {
                        for (int i = 0; i < catCount; i++)
                        {
                            GameObject catGo = new GameObject($"Cat_{tribe.tribeType}_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                            catGo.transform.SetParent(_tribeAvatarRoot.transform, false);
                            RectTransform catRt = catGo.GetComponent<RectTransform>();
                            catRt.anchorMin = new Vector2(0.5f, 0.5f);
                            catRt.anchorMax = new Vector2(0.5f, 0.5f);

                            float baseX = tribeCenterX + (i - (catCount - 1) / 2f) * 80f;
                            float catX = baseX + Random.Range(-30f, 30f);
                            float catY = Random.Range(-200f, -120f);
                            catRt.anchoredPosition = new Vector2(catX, catY);

                            float catSize = Random.Range(50f, 80f);
                            catRt.sizeDelta = new Vector2(catSize, catSize);

                            bool catIsSelected = (i == selectedCat);
                            Image catImg = catGo.GetComponent<Image>();
                            catImg.sprite = catIsSelected && attackSprite != null ? attackSprite : defaultSprite;
                            catImg.color = new Color(1f, 1f, 1f, Random.Range(0.6f, 0.95f));
                            Button catBtn = catGo.GetComponent<Button>();
                            catBtn.transition = Selectable.Transition.None;
                            int catIdx = i;
                            catBtn.onClick.AddListener(() => OnCatAvatarClicked(clickedTribeId, catIdx));

                            if (idleSprite != null) _avatarIdleSprites[catGo] = idleSprite;
                            if (attackSprite != null) _avatarAttackSprites[catGo] = attackSprite;
                            if (catIsSelected)
                            {
                                _selectedAvatarGo = catGo;
                                PositionIndicatorAbove(catRt);
                            }
                        }
                    }
                };

                idleHandle.Completed += (op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded) idleSprite = op.Result;
                    if (--pending == 0) onBothLoaded();
                };
                attackHandle.Completed += (op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded) attackSprite = op.Result;
                    if (--pending == 0) onBothLoaded();
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

        private void OnLeaderAvatarClicked(int tribeId)
        {
            var tribe = _tribes?.Find(t => t.tribeId == tribeId);
            if (tribe == null) return;

            // 切换选中头像的 sprite
            GameObject clickedGo = FindAvatarGoByTribe(tribeId, -1);
            if (_selectedAvatarGo != null && _selectedAvatarGo != clickedGo)
                SetAvatarSelected(_selectedAvatarGo, false);
            SetAvatarSelected(clickedGo, true);
            _selectedAvatarGo = clickedGo;

            _selectedCatIndex = -1;
            _selectedCatTribeId = -1;
            if (_sellCatButton != null) _sellCatButton.gameObject.SetActive(false);
            ShowTribeOnCard(tribe);
        }

        private void OnCatAvatarClicked(int tribeId, int catIndex)
        {
            var tribe = _tribes?.Find(t => t.tribeId == tribeId);
            if (tribe == null || tribe.cats == null || catIndex >= tribe.cats.Count) return;

            // 切换选中头像的 sprite
            GameObject clickedGo = FindAvatarGoByTribe(tribeId, catIndex);
            if (_selectedAvatarGo != null && _selectedAvatarGo != clickedGo)
                SetAvatarSelected(_selectedAvatarGo, false);
            SetAvatarSelected(clickedGo, true);
            _selectedAvatarGo = clickedGo;

            _selectedCatIndex = catIndex;
            _selectedCatTribeId = tribeId;
            _currentDisplayTribeId = -1;
            if (_sellCatButton != null) _sellCatButton.gameObject.SetActive(true);
            _displayedCard?.SetupForCat(tribe.cats[catIndex], tribe);
        }

        /// <summary>
        /// 根据 tribeId 和 catIndex 查找对应的头像 GameObject
        /// catIndex == -1 表示查找 leader
        /// </summary>
        private GameObject FindAvatarGoByTribe(int tribeId, int catIndex)
        {
            var tribe = _tribes?.Find(t => t.tribeId == tribeId);
            if (tribe == null || _tribeAvatarRoot == null) return null;

            string prefix = catIndex < 0 ? $"Leader_{tribe.tribeType}" : $"Cat_{tribe.tribeType}_{catIndex}";
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
            if (_selectedCatIndex < 0) return;
            var tribe = _tribes?.Find(t => t.tribeId == _selectedCatTribeId);
            if (tribe == null || tribe.cats == null || _selectedCatIndex >= tribe.cats.Count) return;

            CatData cat = tribe.cats[_selectedCatIndex];
            int sellPrice = _shopService.SellCat(tribe, cat);
            Debug.Log($"[TribeBuildPanel] 出售小猫获得 {sellPrice} 猫粮");

            if (_catFoodText != null)
                _catFoodText.text = $"猫粮: {_dataManager.GetCatFood()}";

            // 刷新卡片属性
            _currentDisplayTribeId = -1; // 重置以强制刷新
            ShowTribeOnCard(tribe);

            // 刷新头像
            RebuildAllTribeAvatars();

            // 隐藏出售按钮（猫已卖掉）
            _selectedCatIndex = -1;
            _selectedCatTribeId = -1;
            if (_sellCatButton != null) _sellCatButton.gameObject.SetActive(false);

            _dataManager?.SavePlayerData();
        }

        private string GetTribeBreedName(TribeType tribeType)
        {
            switch (tribeType)
            {
                case TribeType.Maine: return "mianyin";
                case TribeType.Tabby: return "lihua";
                case TribeType.Orange: return "daju";
                case TribeType.Cow: return "nainiu";
                case TribeType.Siamese: return "xianluo";
                case TribeType.Ragdoll: return "buou";
                default: return null;
            }
        }

        private string GetTribePortraitAddress(TribeType tribeType)
        {
            string breed = GetTribeBreedName(tribeType);
            return breed != null ? $"avatartemp/{breed}1" : null;
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
            return tribe?.tribeType ?? TribeType.Maine;
        }
    }
}
