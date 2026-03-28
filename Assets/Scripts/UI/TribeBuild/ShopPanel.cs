using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 商店面板 - 可选操作面板
    /// 每5回合开放一次，可买进卖出
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        private const string PanelName = "神秘商店";

        [Header("UI 组件（预制体绑定）")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _catFoodText;
        [SerializeField] private RectTransform _itemsContainer;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Text _refreshCostText;
        [SerializeField] private Button _closeButton;

        private RectTransform _externalRoot;
        private Font _cachedFont;
        private RectTransform _cachedParent;
        private bool _isRuntimeCreated;

        // 当前数据
        private List<ShopItem> _currentItems;
        private int _refreshCost;

        // 回调
        private Action<ShopItem> _onItemBuy;
        private Action _onRefresh;
        private Action _onClose;

        /// <summary>
        /// 设置外部根节点（用于弹窗场景）
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
        /// 显示商店
        /// </summary>
        public void ShowShop(List<ShopItem> items, int refreshCost, Action<ShopItem> onBuy, Action onRefresh, Action onClose)
        {
            _currentItems = items;
            _refreshCost = refreshCost;
            _onItemBuy = onBuy;
            _onRefresh = onRefresh;
            _onClose = onClose;

            // 清空并重新生成商品
            ClearShopItems();
            GenerateShopItems(items);

            // 更新刷新按钮
            UpdateRefreshButton();

            // 更新猫粮显示
            UpdateCatFoodDisplay();

            // 设置关闭按钮
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(OnCloseClicked);
            }

            Show();
        }

        /// <summary>
        /// 更新猫粮显示
        /// </summary>
        public void UpdateCatFoodDisplay()
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager != null && _catFoodText != null)
            {
                _catFoodText.text = $"猫粮: {dataManager.GetCatFood()}";
            }
        }

        private void EnsureUIComponents()
        {
            if (_titleText != null && _catFoodText != null && _itemsContainer != null &&
                _refreshButton != null && _refreshCostText != null && _closeButton != null)
            {
                return;
            }

            _titleText = transform.Find("Title")?.GetComponent<Text>();
            _catFoodText = transform.Find("CatFoodText")?.GetComponent<Text>();
            _itemsContainer = transform.Find("ItemsContainer") as RectTransform;
            _refreshButton = transform.Find("RefreshButton")?.GetComponent<Button>();
            _refreshCostText = _refreshButton?.transform.Find("CostText")?.GetComponent<Text>();
            _closeButton = transform.Find("CloseButton")?.GetComponent<Button>();

            if (_itemsContainer == null)
            {
                if (_cachedFont == null)
                    _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                RectTransform parent = transform.parent as RectTransform;
                if (parent == null) parent = transform as RectTransform;
                EnsureRuntimeUI(parent, _cachedFont);
            }
        }

        private void EnsureRuntimeUI(RectTransform parent, Font font)
        {
            if (_isRuntimeCreated) return;

            if (_itemsContainer != null) return;

            _isRuntimeCreated = true;

            RectTransform panelRect;
            if (_externalRoot != null)
            {
                panelRect = _externalRoot;
            }
            else
            {
                GameObject panelGo = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(parent, false);
                panelRect = panelGo.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.15f, 0.15f);
                panelRect.anchorMax = new Vector2(0.85f, 0.85f);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;

                Image bg = panelGo.GetComponent<Image>();
                bg.color = new Color(0.08f, 0.1f, 0.12f, 0.98f);
            }

            // 标题
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelRect, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 35f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            _titleText = titleGo.GetComponent<Text>();
            _titleText.font = font;
            _titleText.fontSize = 36;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = Color.white;

            // 猫粮显示
            GameObject catFoodGo = new GameObject("CatFoodText", typeof(RectTransform), typeof(Text));
            catFoodGo.transform.SetParent(panelRect, false);
            RectTransform catFoodRect = catFoodGo.GetComponent<RectTransform>();
            catFoodRect.anchorMin = new Vector2(0f, 0.93f);
            catFoodRect.anchorMax = new Vector2(0.4f, 0.97f);
            catFoodRect.offsetMin = Vector2.zero;
            catFoodRect.offsetMax = Vector2.zero;
            _catFoodText = catFoodGo.GetComponent<Text>();
            _catFoodText.font = font;
            _catFoodText.fontSize = 18;
            _catFoodText.alignment = TextAnchor.MiddleLeft;
            _catFoodText.color = new Color(1f, 0.9f, 0.3f, 1f);

            // 商品容器（网格布局）
            GameObject itemsGo = new GameObject("ItemsContainer", typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup));
            itemsGo.transform.SetParent(panelRect, false);
            RectTransform itemsRect = itemsGo.GetComponent<RectTransform>();
            itemsRect.anchorMin = new Vector2(0.05f, 0.20f);
            itemsRect.anchorMax = new Vector2(0.95f, 0.75f);
            itemsRect.offsetMin = Vector2.zero;
            itemsRect.offsetMax = Vector2.zero;
            _itemsContainer = itemsRect;
            Image itemsBg = itemsGo.GetComponent<Image>();
            itemsBg.color = new Color(0f, 0f, 0f, 0.15f);

            GridLayoutGroup gridLayout = itemsGo.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(150f, 180f);
            gridLayout.spacing = new Vector2(15f, 15f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 5;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.padding = new RectOffset(10, 10, 10, 10);

            // 刷新按钮
            GameObject refreshGo = new GameObject("RefreshButton", typeof(RectTransform), typeof(Image), typeof(Button));
            refreshGo.transform.SetParent(panelRect, false);
            RectTransform refreshRect = refreshGo.GetComponent<RectTransform>();
            refreshRect.anchorMin = new Vector2(0.05f, 0.08f);
            refreshRect.anchorMax = new Vector2(0.35f, 0.15f);
            refreshRect.offsetMin = Vector2.zero;
            refreshRect.offsetMax = Vector2.zero;
            _refreshButton = refreshGo.GetComponent<Button>();
            Image refreshImg = refreshGo.GetComponent<Image>();
            refreshImg.color = new Color(0.2f, 0.4f, 0.6f, 1f);
            _refreshButton.targetGraphic = refreshImg;

            // 刷新按钮文本
            GameObject refreshLabelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            refreshLabelGo.transform.SetParent(refreshRect, false);
            RectTransform refreshLabelRect = refreshLabelGo.GetComponent<RectTransform>();
            refreshLabelRect.anchorMin = new Vector2(0.3f, 0f);
            refreshLabelRect.anchorMax = new Vector2(1f, 1f);
            refreshLabelRect.offsetMin = Vector2.zero;
            refreshLabelRect.offsetMax = Vector2.zero;
            Text refreshLabel = refreshLabelGo.GetComponent<Text>();
            refreshLabel.font = font;
            refreshLabel.fontSize = 32;
            refreshLabel.alignment = TextAnchor.MiddleCenter;
            refreshLabel.color = Color.white;
            refreshLabel.text = "刷新";

            // 刷新消耗
            GameObject refreshCostGo = new GameObject("CostText", typeof(RectTransform), typeof(Text));
            refreshCostGo.transform.SetParent(refreshRect, false);
            RectTransform refreshCostRect = refreshCostGo.GetComponent<RectTransform>();
            refreshCostRect.anchorMin = new Vector2(0f, 0f);
            refreshCostRect.anchorMax = new Vector2(0.3f, 1f);
            refreshCostRect.offsetMin = Vector2.zero;
            refreshCostRect.offsetMax = Vector2.zero;
            _refreshCostText = refreshCostGo.GetComponent<Text>();
            _refreshCostText.font = font;
            _refreshCostText.fontSize = 14;
            _refreshCostText.alignment = TextAnchor.MiddleCenter;
            _refreshCostText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            // 关闭按钮
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panelRect, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.65f, 0.08f);
            closeRect.anchorMax = new Vector2(0.95f, 0.15f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            _closeButton = closeGo.GetComponent<Button>();
            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.5f, 0.25f, 0.2f, 1f);
            _closeButton.targetGraphic = closeImg;
            CreateButtonLabel(closeRect, font, "关闭");
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
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = text;
        }

        private void GenerateShopItems(List<ShopItem> items)
        {
            if (_itemsContainer == null || items == null) return;

            foreach (var item in items)
            {
                GameObject itemGo = new GameObject("ShopItem", typeof(RectTransform), typeof(Image), typeof(Button));
                itemGo.transform.SetParent(_itemsContainer, false);

                RectTransform itemRect = itemGo.GetComponent<RectTransform>();
                itemRect.sizeDelta = new Vector2(140f, 170f);

                Image itemImg = itemGo.GetComponent<Image>();
                itemImg.color = GetShopItemColor(item.itemType);

                Button itemBtn = itemGo.GetComponent<Button>();
                itemBtn.onClick.AddListener(() => OnItemClicked(item));

                // 创建商品内容
                CreateShopItemContent(itemRect, item);

                // 存储引用
                ShopItemCard cardComponent = itemGo.AddComponent<ShopItemCard>();
                cardComponent.Item = item;
                cardComponent.BackgroundImage = itemImg;
            }
        }

        private void CreateShopItemContent(RectTransform itemRect, ShopItem item)
        {
            Font font = _cachedFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 名称
            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(itemRect, false);
            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.70f);
            nameRect.anchorMax = new Vector2(0.95f, 0.85f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            Text nameText = nameGo.GetComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 36;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = item.name;

            // 价格
            GameObject priceGo = new GameObject("Price", typeof(RectTransform), typeof(Text));
            priceGo.transform.SetParent(itemRect, false);
            RectTransform priceRect = priceGo.GetComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.05f, 0.55f);
            priceRect.anchorMax = new Vector2(0.95f, 0.68f);
            priceRect.offsetMin = Vector2.zero;
            priceRect.offsetMax = Vector2.zero;
            Text priceText = priceGo.GetComponent<Text>();
            priceText.font = font;
            priceText.fontSize = 32;
            priceText.alignment = TextAnchor.MiddleCenter;
            priceText.color = new Color(1f, 0.9f, 0.3f, 1f);
            priceText.text = $"{item.GetActualPrice()} 猫粮";

            // 类型图标
            GameObject typeGo = new GameObject("Type", typeof(RectTransform), typeof(Text));
            typeGo.transform.SetParent(itemRect, false);
            RectTransform typeRect = typeGo.GetComponent<RectTransform>();
            typeRect.anchorMin = new Vector2(0.05f, 0.40f);
            typeRect.anchorMax = new Vector2(0.95f, 0.52f);
            typeRect.offsetMin = Vector2.zero;
            typeRect.offsetMax = Vector2.zero;
            Text typeText = typeGo.GetComponent<Text>();
            typeText.font = font;
            typeText.fontSize = 10;
            typeText.alignment = TextAnchor.MiddleCenter;
            typeText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            typeText.text = GetShopItemTypeName(item.itemType);

            // 描述（简短）
            GameObject descGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(itemRect, false);
            RectTransform descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.05f, 0.05f);
            descRect.anchorMax = new Vector2(0.95f, 0.35f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            Text descText = descGo.GetComponent<Text>();
            descText.font = font;
            descText.fontSize = 18;
            descText.alignment = TextAnchor.UpperCenter;
            descText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            descText.text = TruncateText(item.description, 30);
        }

        private Color GetShopItemColor(ShopItemType itemType)
        {
            switch (itemType)
            {
                case ShopItemType.Artifact:
                    return new Color(0.5f, 0.3f, 0.6f, 1f);
                case ShopItemType.Consumable:
                    return new Color(0.3f, 0.5f, 0.4f, 1f);
                case ShopItemType.Cat:
                    return new Color(0.3f, 0.4f, 0.6f, 1f);
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }

        private string GetShopItemTypeName(ShopItemType itemType)
        {
            switch (itemType)
            {
                case ShopItemType.Artifact: return "奇物";
                case ShopItemType.Consumable: return "道具";
                case ShopItemType.Cat: return "小猫";
                default: return "未知";
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }

        private void OnItemClicked(ShopItem item)
        {
            _onItemBuy?.Invoke(item);
        }

        private void UpdateRefreshButton()
        {
            if (_refreshCostText != null)
            {
                _refreshCostText.text = $"{_refreshCost}";
            }

            if (_refreshButton != null)
            {
                _refreshButton.onClick.RemoveAllListeners();
                _refreshButton.onClick.AddListener(OnRefreshClicked);
            }
        }

        private void OnRefreshClicked()
        {
            _onRefresh?.Invoke();
        }

        private void OnCloseClicked()
        {
            Hide();
            _onClose?.Invoke();
        }

        private void ClearShopItems()
        {
            if (_itemsContainer == null) return;

            for (int i = _itemsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_itemsContainer.GetChild(i).gameObject);
            }
        }

        public void Show()
        {
            if (_externalRoot != null && _itemsContainer == null && _cachedFont != null)
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
    }

    /// <summary>
    /// 商店物品卡片组件
    /// </summary>
    public class ShopItemCard : MonoBehaviour
    {
        public ShopItem Item { get; set; }
        public Image BackgroundImage { get; set; }
    }
}
