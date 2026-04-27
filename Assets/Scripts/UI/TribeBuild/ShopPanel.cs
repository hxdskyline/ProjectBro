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

        [Header("子卡片预制体")]
        [SerializeField] private GameObject _shopItemCardPrefab;

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
        private Action<TribeRecord, CatData> _onCatSell;

        // 出售模式
        private bool _isSellMode = false;
        private Button _sellButton;
        private Button _backToShopButton;
        private RectTransform _sellContainer;

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
        public void ShowShop(List<ShopItem> items, int refreshCost, Action<ShopItem> onBuy, Action onRefresh, Action onClose, Action<TribeRecord, CatData> onCatSell = null)
        {
            _currentItems = items;
            _refreshCost = refreshCost;
            _onItemBuy = onBuy;
            _onRefresh = onRefresh;
            _onClose = onClose;
            _onCatSell = onCatSell;
            _isSellMode = false;

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

            ShowSellView(false);
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

            // 出售按钮（放在刷新和关闭之间）
            GameObject sellGo = new GameObject("SellButton", typeof(RectTransform), typeof(Image), typeof(Button));
            sellGo.transform.SetParent(panelRect, false);
            RectTransform sellRect = sellGo.GetComponent<RectTransform>();
            sellRect.anchorMin = new Vector2(0.35f, 0.08f);
            sellRect.anchorMax = new Vector2(0.65f, 0.15f);
            sellRect.offsetMin = Vector2.zero;
            sellRect.offsetMax = Vector2.zero;
            _sellButton = sellGo.GetComponent<Button>();
            Image sellImg = sellGo.GetComponent<Image>();
            sellImg.color = new Color(0.6f, 0.45f, 0.2f, 1f);
            _sellButton.targetGraphic = sellImg;
            CreateButtonLabel(sellRect, font, "出售小猫");
            _sellButton.onClick.AddListener(OnSellButtonClicked);

            // 出售容器（ScrollView样式，复用商品容器位置）
            GameObject sellGo2 = new GameObject("SellContainer", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            sellGo2.transform.SetParent(panelRect, false);
            RectTransform sellRect2 = sellGo2.GetComponent<RectTransform>();
            sellRect2.anchorMin = new Vector2(0.05f, 0.20f);
            sellRect2.anchorMax = new Vector2(0.95f, 0.75f);
            sellRect2.offsetMin = Vector2.zero;
            sellRect2.offsetMax = Vector2.zero;
            _sellContainer = sellRect2;
            Image sellBg = sellGo2.GetComponent<Image>();
            sellBg.color = new Color(0f, 0f, 0f, 0.15f);
            ScrollRect scrollRect = sellGo2.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;

            // 出售内容节点（VerticalLayoutGroup）
            GameObject sellContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            sellContent.transform.SetParent(sellRect2, false);
            RectTransform contentRect = sellContent.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            scrollRect.content = contentRect;
            VerticalLayoutGroup vlg = sellContent.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 5f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            _sellContainer.gameObject.SetActive(false);
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
                GameObject itemGo;
                ShopItemCard cardComponent;

                if (_shopItemCardPrefab != null)
                {
                    itemGo = Instantiate(_shopItemCardPrefab, _itemsContainer);
                    cardComponent = itemGo.GetComponent<ShopItemCard>();
                    if (cardComponent != null) cardComponent.Setup(item);

                    Image cardImg = itemGo.GetComponent<Image>();
                    if (cardImg != null) cardImg.color = GetShopItemColor(item.itemType);

                    Button cardBtn = itemGo.GetComponent<Button>();
                    if (cardBtn != null) cardBtn.onClick.AddListener(() => OnItemClicked(item));
                }
                else
                {
                    // Fallback: 运行时创建
                    itemGo = new GameObject("ShopItem", typeof(RectTransform), typeof(Image), typeof(Button));
                    itemGo.transform.SetParent(_itemsContainer, false);

                    RectTransform itemRect = itemGo.GetComponent<RectTransform>();
                    itemRect.sizeDelta = new Vector2(140f, 170f);

                    Image itemImg = itemGo.GetComponent<Image>();
                    itemImg.color = GetShopItemColor(item.itemType);

                    Button itemBtn = itemGo.GetComponent<Button>();
                    itemBtn.onClick.AddListener(() => OnItemClicked(item));

                    CreateShopItemContent(itemRect, item);

                    cardComponent = itemGo.AddComponent<ShopItemCard>();
                    cardComponent.Item = item;
                    cardComponent.BackgroundImage = itemImg;
                }
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

        private void OnSellButtonClicked()
        {
            _isSellMode = true;
            ShowSellView(true);
        }

        private void ShowSellView(bool showSell)
        {
            if (_itemsContainer != null) _itemsContainer.gameObject.SetActive(!showSell);
            if (_refreshButton != null) _refreshButton.gameObject.SetActive(!showSell);
            if (_sellContainer != null) _sellContainer.gameObject.SetActive(showSell);

            if (_sellButton != null)
            {
                // 按钮文字切换
                Text label = _sellButton.GetComponentInChildren<Text>();
                if (label != null) label.text = showSell ? "返回商店" : "出售小猫";
                _sellButton.onClick.RemoveAllListeners();
                if (showSell)
                    _sellButton.onClick.AddListener(OnBackToShopClicked);
                else
                    _sellButton.onClick.AddListener(OnSellButtonClicked);
            }

            if (showSell)
            {
                GenerateSellList();
            }
        }

        private void OnBackToShopClicked()
        {
            _isSellMode = false;
            ShowSellView(false);
        }

        private void GenerateSellList()
        {
            if (_sellContainer == null) return;

            Transform content = _sellContainer.Find("Content");
            if (content == null) return;

            // 清空现有列表
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }

            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var tribes = dataManager.GetTribes();
            if (tribes == null) return;

            Font font = _cachedFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shopService = new ShopService();

            bool hasAnyCats = false;
            foreach (var tribe in tribes)
            {
                if (tribe.cats == null || tribe.cats.Count == 0) continue;

                // 族群标题
                GameObject tribeHeader = new GameObject($"Tribe_{tribe.tribeType}", typeof(RectTransform), typeof(LayoutElement));
                tribeHeader.transform.SetParent(content, false);
                LayoutElement headerLayout = tribeHeader.GetComponent<LayoutElement>();
                headerLayout.preferredHeight = 25f;

                GameObject headerLabel = new GameObject("Label", typeof(RectTransform), typeof(Text));
                headerLabel.transform.SetParent(tribeHeader.transform, false);
                RectTransform hlRect = headerLabel.GetComponent<RectTransform>();
                hlRect.anchorMin = Vector2.zero;
                hlRect.anchorMax = Vector2.one;
                hlRect.offsetMin = Vector2.zero;
                hlRect.offsetMax = Vector2.zero;
                Text hlText = headerLabel.GetComponent<Text>();
                hlText.font = font;
                hlText.fontSize = 14;
                hlText.alignment = TextAnchor.MiddleLeft;
                hlText.color = new Color(1f, 0.85f, 0.4f, 1f);
                hlText.text = $"  {GetTribeTypeName(tribe.tribeType)}族 ({tribe.cats.Count}只)";

                for (int i = 0; i < tribe.cats.Count; i++)
                {
                    hasAnyCats = true;
                    var cat = tribe.cats[i];
                    int sellPrice = shopService.GetCatSellPrice(tribe.tribeType, cat.quality);

                    GameObject row = new GameObject("CatRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    row.transform.SetParent(content, false);
                    LayoutElement rowLayout = row.GetComponent<LayoutElement>();
                    rowLayout.preferredHeight = 35f;
                    Image rowBg = row.GetComponent<Image>();
                    rowBg.color = GetQualityColor(cat.quality) * 0.4f;

                    // 品质+属性文本
                    GameObject infoGo = new GameObject("Info", typeof(RectTransform), typeof(Text));
                    infoGo.transform.SetParent(row.transform, false);
                    RectTransform infoRect = infoGo.GetComponent<RectTransform>();
                    infoRect.anchorMin = new Vector2(0.02f, 0f);
                    infoRect.anchorMax = new Vector2(0.7f, 1f);
                    infoRect.offsetMin = Vector2.zero;
                    infoRect.offsetMax = Vector2.zero;
                    Text infoText = infoGo.GetComponent<Text>();
                    infoText.font = font;
                    infoText.fontSize = 12;
                    infoText.alignment = TextAnchor.MiddleLeft;
                    infoText.color = Color.white;
                    infoText.text = $"  {GetQualityName(cat.quality)}  攻{cat.attackMultiplier:P0} 防{cat.defenseMultiplier:P0} 血{cat.hpMultiplier:P0} 速{cat.speedMultiplier:P0}";

                    // 出售按钮
                    GameObject sellBtnGo = new GameObject("SellBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                    sellBtnGo.transform.SetParent(row.transform, false);
                    RectTransform sellBtnRect = sellBtnGo.GetComponent<RectTransform>();
                    sellBtnRect.anchorMin = new Vector2(0.72f, 0.1f);
                    sellBtnRect.anchorMax = new Vector2(0.98f, 0.9f);
                    sellBtnRect.offsetMin = Vector2.zero;
                    sellBtnRect.offsetMax = Vector2.zero;
                    Image sellBtnImg = sellBtnGo.GetComponent<Image>();
                    sellBtnImg.color = new Color(0.5f, 0.3f, 0.2f, 1f);
                    Button sellBtn = sellBtnGo.GetComponent<Button>();
                    sellBtn.targetGraphic = sellBtnImg;

                    // 按钮文本
                    GameObject btnLabel = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    btnLabel.transform.SetParent(sellBtnRect, false);
                    RectTransform blRect = btnLabel.GetComponent<RectTransform>();
                    blRect.anchorMin = Vector2.zero;
                    blRect.anchorMax = Vector2.one;
                    blRect.offsetMin = Vector2.zero;
                    blRect.offsetMax = Vector2.zero;
                    Text blText = btnLabel.GetComponent<Text>();
                    blText.font = font;
                    blText.fontSize = 12;
                    blText.alignment = TextAnchor.MiddleCenter;
                    blText.color = new Color(1f, 0.9f, 0.5f, 1f);
                    blText.text = $"出售 +{sellPrice}";

                    // 捕获闭包变量
                    TribeRecord capturedTribe = tribe;
                    CatData capturedCat = cat;
                    sellBtn.onClick.AddListener(() => OnSellCatClicked(capturedTribe, capturedCat));
                }
            }

            if (!hasAnyCats)
            {
                GameObject emptyLabel = new GameObject("EmptyLabel", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
                emptyLabel.transform.SetParent(content, false);
                LayoutElement el = emptyLabel.GetComponent<LayoutElement>();
                el.preferredHeight = 60f;
                Text emptyText = emptyLabel.GetComponent<Text>();
                emptyText.font = font;
                emptyText.fontSize = 18;
                emptyText.alignment = TextAnchor.MiddleCenter;
                emptyText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                emptyText.text = "没有可出售的小猫";
            }
        }

        private void OnSellCatClicked(TribeRecord tribe, CatData cat)
        {
            _onCatSell?.Invoke(tribe, cat);
            // 刷新出售列表和猫粮
            UpdateCatFoodDisplay();
            GenerateSellList();
        }

        private Color GetQualityColor(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return new Color(0.8f, 0.8f, 0.8f, 1f);
                case CatQuality.Blue: return new Color(0.3f, 0.5f, 0.9f, 1f);
                case CatQuality.Purple: return new Color(0.6f, 0.3f, 0.8f, 1f);
                case CatQuality.Gold: return new Color(0.9f, 0.75f, 0.2f, 1f);
                default: return Color.gray;
            }
        }

        private string GetQualityName(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return "菜鸟";
                case CatQuality.Blue: return "老手";
                case CatQuality.Purple: return "精英";
                case CatQuality.Gold: return "大师";
                default: return quality.ToString();
            }
        }

        private string GetTribeTypeName(TribeType type)
        {
            switch (type)
            {
                case TribeType.Tabby: return "狸花";
                case TribeType.Orange: return "大橘";
                case TribeType.Cow: return "奶牛";
                case TribeType.Siamese: return "暹罗";
                default: return type.ToString();
            }
        }

        private void ClearShopItems()
        {
            if (_itemsContainer == null) return;

            for (int i = _itemsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_itemsContainer.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// 刷新商品显示（移除售罄物品）
        /// </summary>
        public void RefreshItems(List<ShopItem> items)
        {
            _currentItems = items;
            ClearShopItems();
            GenerateShopItems(items);
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

}
