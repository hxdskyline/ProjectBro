using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 祭祀面板 - 强制两步选择弹窗
    /// 第一步：选择种族（三选一）
    /// 第二步：选择消耗档位（三档）
    /// </summary>
    public class RitualPanel : MonoBehaviour
    {
        private const string PanelName = "祭祀祈福";
        private const string HintStep1 = "第一步：选择祭祀的族群（三选一）";
        private const string HintStep2 = "第二步：选择消耗的猫粮数量";

        [Header("UI 组件（预制体绑定）")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private RectTransform _cardsContainer;
        [SerializeField] private Button _confirmButton;

        private RectTransform _externalRoot;
        private Font _cachedFont;
        private RectTransform _cachedParent;
        private bool _isRuntimeCreated;

        // 当前状态
        private int _currentStep = 1;
        private List<TribeRecord> _availableTribes;
        private TribeRecord _selectedTribe;
        private RitualTier _selectedTier;
        private int _selectedCost;

        // 回调
        private Action<TribeRecord, int> _onRitualConfirmed;

        /// <summary>
        /// 设置外部根节点（用于强制弹窗场景）
        /// </summary>
        public void SetExternalRoot(RectTransform externalRoot)
        {
            _externalRoot = externalRoot;
            _isRuntimeCreated = false;
        }

        /// <summary>
        /// 初始化面板
        /// </summary>
        public void Initialize()
        {
            EnsureUIComponents();
            if (_titleText != null)
            {
                _titleText.text = PanelName;
            }
        }

        /// <summary>
        /// 初始化面板（兼容旧调用方式，支持运行时创建 UI）
        /// </summary>
        public void Initialize(RectTransform parent, Font font)
        {
            _cachedFont = font;
            _cachedParent = parent;
            EnsureRuntimeUI(parent, font);
            Initialize();
        }

        /// <summary>
        /// 开始祭祀流程
        /// </summary>
        public void StartRitual(List<TribeRecord> tribes, Action<TribeRecord, int> onConfirmed)
        {
            _currentStep = 1;
            _availableTribes = tribes;
            _selectedTribe = null;
            _selectedTier = null;
            _selectedCost = 0;
            _onRitualConfirmed = onConfirmed;

            ShowStep1();
            Show();
        }

        private void ShowStep1()
        {
            _currentStep = 1;
            ClearCards();
            GenerateTribeCards(_availableTribes);

            if (_confirmButton != null)
            {
                _confirmButton.interactable = false;
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(OnStep1Confirm);
            }

            UpdateHintText();
        }

        private void ShowStep2()
        {
            _currentStep = 2;
            ClearCards();
            GenerateCostTierCards();

            if (_confirmButton != null)
            {
                _confirmButton.interactable = true;
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(OnStep2Confirm);
            }

            UpdateHintText();
        }

        private void EnsureUIComponents()
        {
            if (_titleText != null && _hintText != null && _cardsContainer != null && _confirmButton != null)
            {
                return;
            }

            _titleText = transform.Find("Title")?.GetComponent<Text>();
            _hintText = transform.Find("Hint")?.GetComponent<Text>();
            _cardsContainer = transform.Find("CardsContainer") as RectTransform;
            _confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();
        }

        private void EnsureRuntimeUI(RectTransform parent, Font font)
        {
            if (_isRuntimeCreated) return;

            if (_cardsContainer != null) return;

            _isRuntimeCreated = true;

            RectTransform panelRect;
            if (_externalRoot != null)
            {
                panelRect = _externalRoot;
            }
            else
            {
                GameObject panelGo = new GameObject("RitualPanel", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(parent, false);
                panelRect = panelGo.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.2f, 0.2f);
                panelRect.anchorMax = new Vector2(0.8f, 0.8f);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                Image bg = panelGo.GetComponent<Image>();
                bg.color = new Color(0.12f, 0.08f, 0.1f, 0.98f);
            }

            // 标题
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelRect, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 40f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            _titleText = titleGo.GetComponent<Text>();
            _titleText.font = font;
            _titleText.fontSize = 28;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = Color.white;

            // 提示文本
            GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(panelRect, false);
            RectTransform hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0.88f);
            hintRect.anchorMax = new Vector2(1f, 0.93f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            _hintText = hintGo.GetComponent<Text>();
            _hintText.font = font;
            _hintText.fontSize = 16;
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.color = new Color(0.94f, 0.7f, 0.3f, 1f);

            // 卡片容器（横向布局）
            GameObject cardsGo = new GameObject("CardsContainer", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            cardsGo.transform.SetParent(panelRect, false);
            RectTransform cardsRect = cardsGo.GetComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0.05f, 0.15f);
            cardsRect.anchorMax = new Vector2(0.95f, 0.80f);
            cardsRect.offsetMin = Vector2.zero;
            cardsRect.offsetMax = Vector2.zero;
            _cardsContainer = cardsRect;
            Image cardsBg = cardsGo.GetComponent<Image>();
            cardsBg.color = new Color(0f, 0f, 0f, 0.2f);

            HorizontalLayoutGroup layout = cardsGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(15, 15, 15, 15);

            // 确认按钮
            GameObject confirmGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            confirmGo.transform.SetParent(panelRect, false);
            RectTransform confirmRect = confirmGo.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.35f, 0.03f);
            confirmRect.anchorMax = new Vector2(0.65f, 0.12f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            _confirmButton = confirmGo.GetComponent<Button>();
            Image confirmImg = confirmGo.GetComponent<Image>();
            confirmImg.color = new Color(0.5f, 0.3f, 0.2f, 1f);
            _confirmButton.targetGraphic = confirmImg;
            CreateButtonLabel(confirmRect, font, "确认祭祀");
        }

        private void CreateButtonLabel(RectTransform parent, Font font, string text)
        {
            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(parent, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = labelGo.GetComponent<Text>();
            label.font = font;
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = text;
        }

        #region Step 1: Tribe Selection

        private void GenerateTribeCards(List<TribeRecord> tribes)
        {
            if (_cardsContainer == null || tribes == null) return;

            foreach (var tribe in tribes)
            {
                GameObject cardGo = new GameObject("TribeCard", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(_cardsContainer, false);

                RectTransform cardRect = cardGo.GetComponent<RectTransform>();
                cardRect.sizeDelta = new Vector2(200f, 250f);

                Image cardImg = cardGo.GetComponent<Image>();
                cardImg.color = GetTribeCardColor(tribe.tribeType);

                Button cardBtn = cardGo.GetComponent<Button>();
                int tribeIndex = tribes.IndexOf(tribe);
                cardBtn.onClick.AddListener(() => OnTribeCardClicked(tribeIndex));

                // 创建卡片内容
                CreateTribeCardContent(cardRect, tribe);

                // 存储引用
                RitualTribeCard cardComponent = cardGo.AddComponent<RitualTribeCard>();
                cardComponent.Tribe = tribe;
                cardComponent.Index = tribeIndex;
                cardComponent.BackgroundImage = cardImg;
            }
        }

        private void CreateTribeCardContent(RectTransform cardRect, TribeRecord tribe)
        {
            Font font = _cachedFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 族群名称
            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(cardRect, false);
            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.1f, 0.80f);
            nameRect.anchorMax = new Vector2(0.9f, 0.90f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 18;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = GetTribeTypeName(tribe.tribeType);

            // 小猫数量
            GameObject countGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            countGo.transform.SetParent(cardRect, false);
            RectTransform countRect = countGo.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.1f, 0.70f);
            countRect.anchorMax = new Vector2(0.9f, 0.78f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            Text countText = countGo.GetComponent<Text>();
            countText.font = font;
            countText.fontSize = 14;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            countText.text = $"小猫: {tribe.GetCatCount()} 只";

            // 族长属性
            GameObject statsGo = new GameObject("Stats", typeof(RectTransform), typeof(Text));
            statsGo.transform.SetParent(cardRect, false);
            RectTransform statsRect = statsGo.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.1f, 0.20f);
            statsRect.anchorMax = new Vector2(0.9f, 0.65f);
            statsRect.offsetMin = Vector2.zero;
            statsRect.offsetMax = Vector2.zero;
            Text statsText = statsGo.GetComponent<Text>();
            statsText.font = font;
            statsText.fontSize = 12;
            statsText.alignment = TextAnchor.UpperLeft;
            statsText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            statsText.text = $"攻: {tribe.leader.baseAttack}\n防: {tribe.leader.baseDefense}\n血: {tribe.leader.baseHp}\n速: {tribe.leader.baseSpeed}\n统: {tribe.leader.command}";
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
                case TribeType.Maine: return "缅因猫族";
                case TribeType.Tabby: return "狸花猫族";
                case TribeType.Orange: return "大橘猫族";
                case TribeType.Cow: return "奶牛猫族";
                case TribeType.Siamese: return "暹罗猫族";
                case TribeType.Ragdoll: return "布偶猫族";
                default: return type.ToString();
            }
        }

        private void OnTribeCardClicked(int index)
        {
            if (_availableTribes == null || index < 0 || index >= _availableTribes.Count)
                return;

            _selectedTribe = _availableTribes[index];
            UpdateTribeCardSelection(index);

            if (_confirmButton != null)
            {
                _confirmButton.interactable = true;
            }
        }

        private void UpdateTribeCardSelection(int selectedIndex)
        {
            if (_cardsContainer == null) return;

            for (int i = 0; i < _cardsContainer.childCount; i++)
            {
                Transform child = _cardsContainer.GetChild(i);
                RitualTribeCard card = child.GetComponent<RitualTribeCard>();
                if (card != null && card.BackgroundImage != null)
                {
                    if (i == selectedIndex)
                    {
                        card.BackgroundImage.color = new Color(1f, 0.9f, 0.3f, 1f);
                    }
                    else
                    {
                        card.BackgroundImage.color = GetTribeCardColor(card.Tribe.tribeType);
                    }
                }
            }
        }

        private void OnStep1Confirm()
        {
            if (_selectedTribe == null) return;

            // 进入第二步
            ShowStep2();
        }

        #endregion

        #region Step 2: Cost Tier Selection

        private void GenerateCostTierCards()
        {
            if (_cardsContainer == null) return;

            var config = TribeConfigLoader.Instance.GetRitualConfig();
            if (config?.tiers == null) return;

            foreach (var tier in config.tiers)
            {
                // 随机生成消耗范围内的具体数值
                int cost = UnityEngine.Random.Range(tier.costRange[0], tier.costRange[1] + 1);

                GameObject cardGo = new GameObject("CostCard", typeof(RectTransform), typeof(Image), typeof(Button));
                cardGo.transform.SetParent(_cardsContainer, false);

                RectTransform cardRect = cardGo.GetComponent<RectTransform>();
                cardRect.sizeDelta = new Vector2(200f, 250f);

                Image cardImg = cardGo.GetComponent<Image>();
                cardImg.color = GetCostTierColor(tier.tierName);

                Button cardBtn = cardGo.GetComponent<Button>();
                cardBtn.onClick.AddListener(() => OnCostCardClicked(tier, cost));

                // 创建卡片内容
                CreateCostTierCardContent(cardRect, tier, cost);

                // 存储引用
                RitualCostCard cardComponent = cardGo.AddComponent<RitualCostCard>();
                cardComponent.Tier = tier;
                cardComponent.Cost = cost;
                cardComponent.BackgroundImage = cardImg;
            }
        }

        private void CreateCostTierCardContent(RectTransform cardRect, RitualTier tier, int cost)
        {
            Font font = _cachedFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 档位名称
            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(cardRect, false);
            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.1f, 0.75f);
            nameRect.anchorMax = new Vector2(0.9f, 0.85f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 20;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = GetTierDisplayName(tier.tierName);

            // 消耗
            GameObject costGo = new GameObject("Cost", typeof(RectTransform), typeof(Text));
            costGo.transform.SetParent(cardRect, false);
            RectTransform costRect = costGo.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.1f, 0.60f);
            costRect.anchorMax = new Vector2(0.9f, 0.70f);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;
            Text costText = costGo.GetComponent<Text>();
            costText.font = font;
            costText.fontSize = 18;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.9f, 0.3f, 1f);
            costText.text = $"{cost} 猫粮";

            // 奖励预览
            GameObject rewardGo = new GameObject("Reward", typeof(RectTransform), typeof(Text));
            rewardGo.transform.SetParent(cardRect, false);
            RectTransform rewardRect = rewardGo.GetComponent<RectTransform>();
            rewardRect.anchorMin = new Vector2(0.1f, 0.15f);
            rewardRect.anchorMax = new Vector2(0.9f, 0.55f);
            rewardRect.offsetMin = Vector2.zero;
            rewardRect.offsetMax = Vector2.zero;
            Text rewardText = rewardGo.GetComponent<Text>();
            rewardText.font = font;
            rewardText.fontSize = 12;
            rewardText.alignment = TextAnchor.UpperLeft;
            rewardText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            rewardText.text = GetRewardPreview(tier);
        }

        private Color GetCostTierColor(string tierName)
        {
            switch (tierName)
            {
                case "free": return new Color(0.4f, 0.4f, 0.5f, 1f);
                case "low": return new Color(0.3f, 0.5f, 0.4f, 1f);
                case "high": return new Color(0.6f, 0.4f, 0.3f, 1f);
                default: return new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }

        private string GetTierDisplayName(string tierName)
        {
            switch (tierName)
            {
                case "free": return "免费祭祀";
                case "low": return "普通祭祀";
                case "high": return "盛大祭祀";
                default: return tierName;
            }
        }

        private string GetRewardPreview(RitualTier tier)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("奖励预览:");

            foreach (var reward in tier.rewards)
            {
                string rewardType = GetRewardTypeDisplayName(reward.type);
                sb.AppendLine($"• {rewardType}");
            }

            return sb.ToString();
        }

        private string GetRewardTypeDisplayName(string rewardType)
        {
            switch (rewardType)
            {
                case "LeaderStatBoostTemporary": return "族长属性临时提升";
                case "LeaderStatBoostPermanent": return "族长属性永久提升";
                case "LeaderStatBoostPercent": return "族长属性百分比提升";
                case "Cats": return "获得小猫";
                case "Consumable": return "获得道具";
                case "CatFood": return "获得猫粮";
                case "Accessory": return "获得饰品";
                default: return rewardType;
            }
        }

        private void OnCostCardClicked(RitualTier tier, int cost)
        {
            _selectedTier = tier;
            _selectedCost = cost;
            UpdateCostCardSelection();

            if (_confirmButton != null)
            {
                _confirmButton.interactable = true;
            }
        }

        private void UpdateCostCardSelection()
        {
            if (_cardsContainer == null) return;

            for (int i = 0; i < _cardsContainer.childCount; i++)
            {
                Transform child = _cardsContainer.GetChild(i);
                RitualCostCard card = child.GetComponent<RitualCostCard>();
                if (card != null && card.BackgroundImage != null)
                {
                    if (card.Tier == _selectedTier)
                    {
                        card.BackgroundImage.color = new Color(1f, 0.9f, 0.3f, 1f);
                    }
                    else
                    {
                        card.BackgroundImage.color = GetCostTierColor(card.Tier.tierName);
                    }
                }
            }
        }

        private void OnStep2Confirm()
        {
            if (_selectedTribe == null || _selectedTier == null) return;

            Hide();

            _onRitualConfirmed?.Invoke(_selectedTribe, _selectedCost);
        }

        #endregion

        #region Common Methods

        private void ClearCards()
        {
            if (_cardsContainer == null) return;

            for (int i = _cardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_cardsContainer.GetChild(i).gameObject);
            }
        }

        public void Show()
        {
            if (_externalRoot != null && _cardsContainer == null && _cachedFont != null)
            {
                RectTransform parentToUse = _cachedParent != null ? _cachedParent : transform.parent as RectTransform;
                if (parentToUse != null)
                {
                    EnsureRuntimeUI(parentToUse, _cachedFont);
                }
            }

            if (_externalRoot == null)
            {
                gameObject.SetActive(true);
            }
            UpdateHintText();
        }

        public void Hide()
        {
            if (_externalRoot != null)
            {
                if (_externalRoot.gameObject != null)
                {
                    _externalRoot.gameObject.SetActive(false);
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void UpdateHintText()
        {
            if (_hintText == null) return;

            if (_currentStep == 1)
            {
                _hintText.text = HintStep1;
            }
            else
            {
                _hintText.text = HintStep2;
            }
        }

        #endregion
    }

    /// <summary>
    /// 祭祀族群卡片组件
    /// </summary>
    public class RitualTribeCard : MonoBehaviour
    {
        public TribeRecord Tribe { get; set; }
        public int Index { get; set; }
        public Image BackgroundImage { get; set; }
    }

    /// <summary>
    /// 祭祀消耗档位卡片组件
    /// </summary>
    public class RitualCostCard : MonoBehaviour
    {
        public RitualTier Tier { get; set; }
        public int Cost { get; set; }
        public Image BackgroundImage { get; set; }
    }
}
