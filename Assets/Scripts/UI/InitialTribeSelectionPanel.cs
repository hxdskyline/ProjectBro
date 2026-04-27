using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TribeSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 初始族群选择面板 - 六选一
    /// 玩家选择1个族群作为初始战力
    /// </summary>
    public class InitialTribeSelectionPanel : UIPanel
    {
        private const string PanelName = "初始族群选择";

        [Header("UI 组件")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private RectTransform _tribesContainer;
        [SerializeField] private Button _confirmButton;

        // 事件
        public event Action OnSelectionComplete;

        // 状态
        private List<TribeType> _selectedTribes = new List<TribeType>();
        private Font _cachedFont;

        public override void Initialize()
        {
            base.Initialize();
            Debug.Log("[InitialTribeSelectionPanel] 初始化");

            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureUIComponents();
            InitializeTribeButtons();
            UpdateHintText();
        }

        private void EnsureUIComponents()
        {
            // 查找或创建UI组件
            if (_titleText == null)
                _titleText = transform.Find("Title")?.GetComponent<Text>();
            if (_hintText == null)
                _hintText = transform.Find("Hint")?.GetComponent<Text>();
            if (_tribesContainer == null)
                _tribesContainer = transform.Find("TribesContainer") as RectTransform;
            if (_confirmButton == null)
                _confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();

            // 如果没找到，则创建运行时UI
            if (_tribesContainer == null)
            {
                CreateRuntimeUI();
            }

            // 绑定确认按钮
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(OnConfirmClicked);
                _confirmButton.interactable = false;
            }

            // 设置标题
            if (_titleText != null)
            {
                _titleText.text = PanelName;
            }
        }

        private void CreateRuntimeUI()
        {
            Debug.Log("[InitialTribeSelectionPanel] 运行时创建UI");

            RectTransform panelRect = transform as RectTransform;

            // 标题
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelRect, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.95f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = new Vector2(0, -10);
            _titleText = titleGo.GetComponent<Text>();
            _titleText.font = _cachedFont;
            _titleText.fontSize = 32;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = Color.white;
            _titleText.text = PanelName;

            // 提示文本
            GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(panelRect, false);
            RectTransform hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0, 0.85f);
            hintRect.anchorMax = new Vector2(1, 0.92f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            _hintText = hintGo.GetComponent<Text>();
            _hintText.font = _cachedFont;
            _hintText.fontSize = 18;
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.color = new Color(1, 0.9f, 0.3f, 1);

            // 族群容器
            GameObject containerGo = new GameObject("TribesContainer", typeof(RectTransform), typeof(GridLayoutGroup));
            containerGo.transform.SetParent(panelRect, false);
            _tribesContainer = containerGo.GetComponent<RectTransform>();
            _tribesContainer.anchorMin = new Vector2(0.1f, 0.15f);
            _tribesContainer.anchorMax = new Vector2(0.9f, 0.80f);
            _tribesContainer.offsetMin = Vector2.zero;
            _tribesContainer.offsetMax = Vector2.zero;

            GridLayoutGroup grid = containerGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180, 200);
            grid.spacing = new Vector2(20, 20);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // 确认按钮
            GameObject confirmGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Button), typeof(Image));
            confirmGo.transform.SetParent(panelRect, false);
            RectTransform confirmRect = confirmGo.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.35f, 0.05f);
            confirmRect.anchorMax = new Vector2(0.65f, 0.12f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            _confirmButton = confirmGo.GetComponent<Button>();
            Image confirmImg = confirmGo.GetComponent<Image>();
            confirmImg.color = new Color(0.2f, 0.5f, 0.3f, 1);
            _confirmButton.targetGraphic = confirmImg;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(confirmRect, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = labelGo.GetComponent<Text>();
            label.font = _cachedFont;
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "确认选择";
        }

        private void InitializeTribeButtons()
        {
            if (_tribesContainer == null) return;

            // 清空容器
            foreach (Transform child in _tribesContainer)
            {
                Destroy(child.gameObject);
            }

            // 为每个族群创建按钮
            foreach (TribeType type in System.Enum.GetValues(typeof(TribeType)))
            {
                if (type == TribeType.None) continue;
                CreateTribeButton(type);
            }
        }

        private void CreateTribeButton(TribeType tribeType)
        {
            GameObject btnGo = new GameObject($"Tribe_{tribeType}", typeof(RectTransform), typeof(Button), typeof(Image));
            btnGo.transform.SetParent(_tribesContainer, false);

            RectTransform btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(180f, 200f);

            Image btnImg = btnGo.GetComponent<Image>();
            btnImg.color = GetTribeColor(tribeType);

            Button btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(() => OnTribeButtonClicked(tribeType));

            // 创建按钮内容
            CreateTribeButtonContent(btnRect, tribeType);
        }

        private void CreateTribeButtonContent(RectTransform btnRect, TribeType tribeType)
        {
            // 族群名称
            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(btnRect, false);
            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.1f, 0.70f);
            nameRect.anchorMax = new Vector2(0.9f, 0.85f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = _cachedFont;
            nameText.fontSize = 36;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = GetTribeTypeName(tribeType);

            // 族群描述
            GameObject descGo = new GameObject("Description", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(btnRect, false);
            RectTransform descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.35f);
            descRect.anchorMax = new Vector2(0.9f, 0.65f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            Text descText = descGo.GetComponent<Text>();
            descText.font = _cachedFont;
            descText.fontSize = 18;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            descText.text = GetTribeDescription(tribeType);

            // 初始小猫数
            var config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
            GameObject countGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            countGo.transform.SetParent(btnRect, false);
            RectTransform countRect = countGo.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.1f, 0.10f);
            countRect.anchorMax = new Vector2(0.9f, 0.30f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            Text countText = countGo.GetComponent<Text>();
            countText.font = _cachedFont;
            countText.fontSize = 32;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = new Color(1f, 0.9f, 0.3f, 1f);
            countText.text = config != null ? $"小猫:{config.initialCatCount}只" : "小猫:?只";
        }

        private void OnTribeButtonClicked(TribeType tribeType)
        {
            bool isCurrentlySelected = _selectedTribes.Contains(tribeType);

            if (isCurrentlySelected)
            {
                // 取消选择
                _selectedTribes.Remove(tribeType);
            }
            else
            {
                // 选择（最多1个）
                if (_selectedTribes.Count < 1)
                {
                    _selectedTribes.Add(tribeType);
                }
                else
                {
                    // 如果已经选了1个，直接替换
                    _selectedTribes.Clear();
                    _selectedTribes.Add(tribeType);
                }
            }

            // 更新UI
            UpdateTribeButtonStates();
            UpdateHintText();
            UpdateConfirmButton();
        }

        private void UpdateTribeButtonStates()
        {
            for (int i = 0; i < _tribesContainer.childCount; i++)
            {
                Transform child = _tribesContainer.GetChild(i);
                Image childImg = child.GetComponent<Image>();

                string btnName = child.gameObject.name;
                if (btnName.StartsWith("Tribe_"))
                {
                    string typeStr = btnName.Substring(6);
                    if (System.Enum.TryParse<TribeType>(typeStr, out TribeType type))
                    {
                        if (_selectedTribes.Contains(type))
                        {
                            childImg.color = new Color(1f, 0.9f, 0.3f, 1f);
                        }
                        else
                        {
                            childImg.color = GetTribeColor(type);
                        }
                    }
                }
            }
        }

        private void UpdateHintText()
        {
            if (_hintText != null)
            {
                _hintText.text = $"选择1个族群作为初始战力（已选：{_selectedTribes.Count}/1）";
            }
        }

        private void UpdateConfirmButton()
        {
            if (_confirmButton != null)
            {
                _confirmButton.interactable = _selectedTribes.Count == 1;
            }
        }

        private void OnConfirmClicked()
        {
            if (_selectedTribes.Count != 1)
            {
                Debug.LogWarning("[InitialTribeSelectionPanel] 请选择1个族群");
                return;
            }

            Debug.Log($"[InitialTribeSelectionPanel] 选择族群: {_selectedTribes[0]}");

            // 创建初始族群并保存
            CreateAndSaveInitialTribes();

            // 触发完成事件
            OnSelectionComplete?.Invoke();

            // 隐藏面板
            gameObject.SetActive(false);
        }

        private void CreateAndSaveInitialTribes()
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            foreach (var tribeType in _selectedTribes)
            {
                var config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
                if (config == null) continue;

                var tribe = new TribeRecord
                {
                    tribeId = dataManager.GetTribes()?.Count ?? 0,
                    tribeType = tribeType,
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
                        permanentBuffs = new PermanentBuffs()
                    },
                    cats = new List<CatData>(),
                    isActive = true
                };

                // 添加初始小猫（白色品质）
                for (int i = 0; i < config.initialCatCount; i++)
                {
                    tribe.cats.Add(CatData.CreateWithQuality(CatQuality.White, tribeType));
                }

                dataManager.AddTribe(tribe);
            }

            dataManager.SavePlayerData();
            Debug.Log("[InitialTribeSelectionPanel] 初始族群已保存");
        }

        #region Helper Methods

        private Color GetTribeColor(TribeType type)
        {
            return type switch
            {
                TribeType.Tabby => new Color(0.6f, 0.4f, 0.3f, 1f),
                TribeType.Orange => new Color(0.7f, 0.5f, 0.2f, 1f),
                TribeType.Cow => new Color(0.4f, 0.4f, 0.5f, 1f),
                TribeType.Siamese => new Color(0.5f, 0.4f, 0.6f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
        }

        private string GetTribeTypeName(TribeType type)
        {
            return type switch
            {
                TribeType.Tabby => "狸花猫族",
                TribeType.Orange => "大橘猫族",
                TribeType.Cow => "奶牛猫族",
                TribeType.Siamese => "暹罗猫族",
                _ => type.ToString()
            };
        }

        private string GetTribeDescription(TribeType type)
        {
            var config = TribeConfigLoader.Instance?.GetTribeConfig(type);
            if (config != null && !string.IsNullOrEmpty(config.description))
                return config.description;
            return "???";
        }

        #endregion
    }
}
