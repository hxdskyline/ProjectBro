using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 祭祀面板 - 两步选择弹窗
    /// Step1：选择祭祀档次（免费/普通/盛大）
    /// Step2：从该档次池中抽取若干祝福，玩家选一条
    /// </summary>
    public class RitualPanel : MonoBehaviour
    {
        [Header("UI 组件（预制体绑定）")]
        [SerializeField] private Text _titleText;

        [SerializeField] private GameObject _step1ContainerObj;
        [SerializeField] private GameObject _step2ContainerObj;

        // Step1：档次选择容器（复用 TribesContainer 节点）
        [SerializeField] private RectTransform _tribesContainer;
        [SerializeField] private Button _step1ConfirmButton;

        // Step2：祝福选择容器（复用 CostOptionsContainer 节点）
        [SerializeField] private RectTransform _costOptionsContainer;
        [SerializeField] private Button _step2ConfirmButton;
        [SerializeField] private Button _closeButton;

        private RectTransform _externalRoot;
        private Font _cachedFont;
        private bool _isRuntimeCreated;
        private GameObject _panelContent;

        // 状态
        private int _currentStep = 1;
        private List<RitualTier> _availableTiers;
        private RitualTier _selectedTier;
        private List<RitualRewardItem> _drawnBlessings;
        private RitualRewardItem _selectedBlessing;

        // 服务引用（由调用方注入）
        private Func<RitualTier, List<RitualRewardItem>> _drawBlessings;

        // 回调：(tier=null 表示跳过)
        private Action<RitualTier, RitualRewardItem> _onRitualConfirmed;

        // ─── 公共接口 ───────────────────────────────────────────────────────────

        public void SetExternalRoot(RectTransform externalRoot)
        {
            _externalRoot = externalRoot;
            _isRuntimeCreated = false;
        }

        public void Initialize()
        {
            EnsureUIComponents();
            if (_titleText != null) _titleText.text = "族群祭祀";
        }

        public void StartRitual(
            List<RitualTier> tiers,
            Func<RitualTier, List<RitualRewardItem>> drawBlessings,
            Action<RitualTier, RitualRewardItem> onConfirmed)
        {
            _availableTiers    = tiers;
            _drawBlessings     = drawBlessings;
            _onRitualConfirmed = onConfirmed;
            _selectedTier      = null;
            _selectedBlessing  = null;
            _drawnBlessings    = null;

            EnsureUIComponents();
            ShowStep1();
            Show();
        }

        // ─── Step1：选档次 ─────────────────────────────────────────────────────

        private void ShowStep1()
        {
            _currentStep = 1;
            if (_step1ContainerObj != null) _step1ContainerObj.SetActive(true);
            if (_step2ContainerObj != null) _step2ContainerObj.SetActive(false);

            ClearContainer(_tribesContainer);
            GenerateTierCards();

            if (_step1ConfirmButton != null)
            {
                _step1ConfirmButton.interactable = false;
                _step1ConfirmButton.onClick.RemoveAllListeners();
                _step1ConfirmButton.onClick.AddListener(OnStep1Confirm);
            }
        }

        private void GenerateTierCards()
        {
            if (_tribesContainer == null || _availableTiers == null) return;
            Font font = GetFont();
            long catFood = GameManager.Instance?.DataManager?.GetCatFood() ?? 0;

            foreach (var tier in _availableTiers)
            {
                var go = new GameObject("TierCard", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_tribesContainer, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 250f);

                var img = go.GetComponent<Image>();
                img.color = GetTierColor(tier.tierName);

                bool canAfford = catFood >= tier.cost;
                var btn = go.GetComponent<Button>();
                btn.interactable = canAfford;

                var captured = tier;
                btn.onClick.AddListener(() => OnTierCardClicked(captured));

                CreateTierCardContent(go.GetComponent<RectTransform>(), tier, canAfford, font);

                var comp = go.AddComponent<RitualTierCard>();
                comp.Tier = tier;
                comp.Img  = img;
            }
        }

        private void CreateTierCardContent(RectTransform rect, RitualTier tier, bool canAfford, Font font)
        {
            string costText = tier.cost == 0 ? "免费" : $"{tier.cost} 猫粮";
            AddText(rect, "Name",  font, 20, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.88f),
                tier.displayName ?? tier.tierName, Color.white, TextAnchor.MiddleCenter);
            AddText(rect, "Cost",  font, 16, new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.75f),
                costText, canAfford ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
            AddText(rect, "Hint",  font, 12, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.60f),
                $"抽取 {tier.drawCount} 条祝福\n三选一", new Color(0.85f, 0.85f, 0.85f), TextAnchor.UpperCenter);
            if (!canAfford)
                AddText(rect, "Lock", font, 13, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.10f),
                    "猫粮不足", new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
        }

        private void OnTierCardClicked(RitualTier tier)
        {
            _selectedTier = tier;
            // 高亮选中
            foreach (Transform child in _tribesContainer)
            {
                var card = child.GetComponent<RitualTierCard>();
                if (card?.Img != null)
                    card.Img.color = card.Tier == tier
                        ? new Color(1f, 0.9f, 0.3f)
                        : GetTierColor(card.Tier.tierName);
            }
            if (_step1ConfirmButton != null) _step1ConfirmButton.interactable = true;
        }

        private void OnStep1Confirm()
        {
            if (_selectedTier == null) return;
            _drawnBlessings = _drawBlessings?.Invoke(_selectedTier) ?? new List<RitualRewardItem>();
            ShowStep2();
        }

        // ─── Step2：选祝福 ─────────────────────────────────────────────────────

        private void ShowStep2()
        {
            _currentStep = 2;
            if (_step1ContainerObj != null) _step1ContainerObj.SetActive(false);
            if (_step2ContainerObj != null) _step2ContainerObj.SetActive(true);

            ClearContainer(_costOptionsContainer);
            GenerateBlessingCards();

            if (_step2ConfirmButton != null)
            {
                _step2ConfirmButton.interactable = false;
                _step2ConfirmButton.onClick.RemoveAllListeners();
                _step2ConfirmButton.onClick.AddListener(OnStep2Confirm);
            }
        }

        private void GenerateBlessingCards()
        {
            if (_costOptionsContainer == null || _drawnBlessings == null) return;
            Font font = GetFont();

            foreach (var blessing in _drawnBlessings)
            {
                var go = new GameObject("BlessingCard", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_costOptionsContainer, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 250f);

                var img = go.GetComponent<Image>();
                img.color = GetBlessingColor(blessing.rewardType);

                var captured = blessing;
                go.GetComponent<Button>().onClick.AddListener(() => OnBlessingCardClicked(captured));

                AddText(go.GetComponent<RectTransform>(), "Name", font, 16,
                    new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.88f),
                    GetRewardTypeName(blessing.rewardType), Color.white, TextAnchor.MiddleCenter);
                AddText(go.GetComponent<RectTransform>(), "Desc", font, 14,
                    new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.65f),
                    blessing.displayName, new Color(0.9f, 0.9f, 0.9f), TextAnchor.UpperCenter);

                var comp = go.AddComponent<RitualBlessingCard>();
                comp.Blessing = blessing;
                comp.Img      = img;
            }
        }

        private void OnBlessingCardClicked(RitualRewardItem blessing)
        {
            _selectedBlessing = blessing;
            foreach (Transform child in _costOptionsContainer)
            {
                var card = child.GetComponent<RitualBlessingCard>();
                if (card?.Img != null)
                    card.Img.color = card.Blessing == blessing
                        ? new Color(1f, 0.9f, 0.3f)
                        : GetBlessingColor(card.Blessing.rewardType);
            }
            if (_step2ConfirmButton != null) _step2ConfirmButton.interactable = true;
        }

        private void OnStep2Confirm()
        {
            if (_selectedBlessing == null) return;
            Hide();
            _onRitualConfirmed?.Invoke(_selectedTier, _selectedBlessing);
        }

        private void OnCloseClicked()
        {
            Hide();
            _onRitualConfirmed?.Invoke(null, null); // 跳过祭祀
        }

        // ─── 显示/隐藏 ─────────────────────────────────────────────────────────

        public void Show()
        {
            EnsureUIComponents();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_externalRoot != null) _externalRoot.gameObject.SetActive(true);
            if (_panelContent  != null) _panelContent.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (_externalRoot != null) _externalRoot.gameObject.SetActive(false);
            if (_panelContent  != null) _panelContent.SetActive(false);
        }

        // ─── 初始化组件 ────────────────────────────────────────────────────────

        private void EnsureUIComponents()
        {
            if (_titleText == null)
                _titleText = transform.Find("Title")?.GetComponent<Text>();

            if (_step1ContainerObj == null)
            { var t = transform.Find("Step1Container"); if (t != null) _step1ContainerObj = t.gameObject; }
            if (_step2ContainerObj == null)
            { var t = transform.Find("Step2Container"); if (t != null) _step2ContainerObj = t.gameObject; }

            if (_step1ContainerObj != null)
            {
                if (_tribesContainer == null)
                    _tribesContainer = _step1ContainerObj.transform.Find("TribesContainer") as RectTransform;
                if (_step1ConfirmButton == null)
                    _step1ConfirmButton = _step1ContainerObj.transform.Find("Step1ConfirmButton")?.GetComponent<Button>();
            }
            if (_step2ContainerObj != null)
            {
                if (_costOptionsContainer == null)
                    _costOptionsContainer = _step2ContainerObj.transform.Find("CostOptionsContainer") as RectTransform;
                if (_step2ConfirmButton == null)
                    _step2ConfirmButton = _step2ContainerObj.transform.Find("Step2ConfirmButton")?.GetComponent<Button>();
            }
            if (_closeButton == null)
                _closeButton = transform.Find("CloseButton")?.GetComponent<Button>();

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(OnCloseClicked);
            }

            bool prefabOk = _step1ContainerObj != null && _step2ContainerObj != null
                         && _tribesContainer != null && _costOptionsContainer != null;
            if (!prefabOk) EnsureRuntimeUI();
        }

        private void EnsureRuntimeUI()
        {
            if (_isRuntimeCreated) return;
            if (_tribesContainer != null && _costOptionsContainer != null) return;
            _isRuntimeCreated = true;

            Font font = GetFont();
            var targetParent = _externalRoot != null ? _externalRoot : (transform.parent as RectTransform ?? transform as RectTransform);

            var panelGo = new GameObject("RitualPanelContent", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(targetParent, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.1f, 0.98f);
            _panelContent = panelGo;

            if (_titleText == null)
            {
                var tGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
                tGo.transform.SetParent(panelRect, false);
                var tRect = tGo.GetComponent<RectTransform>();
                tRect.anchorMin = new Vector2(0, 1); tRect.anchorMax = new Vector2(1, 1);
                tRect.pivot = new Vector2(0.5f, 1); tRect.sizeDelta = new Vector2(0, 50); tRect.anchoredPosition = new Vector2(0, -10);
                _titleText = tGo.GetComponent<Text>();
                _titleText.font = font; _titleText.fontSize = 28;
                _titleText.alignment = TextAnchor.MiddleCenter; _titleText.color = Color.white;
            }

            // Step1Container
            var s1 = new GameObject("Step1Container", typeof(RectTransform));
            s1.transform.SetParent(panelRect, false);
            SetStretch(s1.GetComponent<RectTransform>(), new Vector2(0, 60), Vector2.zero);
            _step1ContainerObj = s1;

            _tribesContainer = CreateHLayout(s1.GetComponent<RectTransform>(), "TribesContainer");
            CreateConfirmBtn(s1.GetComponent<RectTransform>(), font, "确认档次", out _step1ConfirmButton);

            // Step2Container
            var s2 = new GameObject("Step2Container", typeof(RectTransform));
            s2.transform.SetParent(panelRect, false);
            SetStretch(s2.GetComponent<RectTransform>(), new Vector2(0, 60), Vector2.zero);
            s2.SetActive(false);
            _step2ContainerObj = s2;

            _costOptionsContainer = CreateHLayout(s2.GetComponent<RectTransform>(), "CostOptionsContainer");
            CreateConfirmBtn(s2.GetComponent<RectTransform>(), font, "选择祝福", out _step2ConfirmButton);

            // CloseButton
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panelRect, false);
            var closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.68f, 0.02f); closeRect.anchorMax = new Vector2(0.95f, 0.10f);
            closeRect.offsetMin = Vector2.zero; closeRect.offsetMax = Vector2.zero;
            closeGo.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f);
            _closeButton = closeGo.GetComponent<Button>();
            _closeButton.onClick.AddListener(OnCloseClicked);
            AddLabel(closeRect, font, "先等等");
        }

        // ─── 工具方法 ──────────────────────────────────────────────────────────

        private void ClearContainer(RectTransform ct)
        {
            if (ct == null) return;
            for (int i = ct.childCount - 1; i >= 0; i--)
                Destroy(ct.GetChild(i).gameObject);
        }

        private Font GetFont()
        {
            if (_cachedFont == null) _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _cachedFont;
        }

        private void AddText(RectTransform parent, string name, Font font, int size,
            Vector2 aMin, Vector2 aMax, string text, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = align; t.color = color; t.text = text;
        }

        private void AddLabel(RectTransform parent, Font font, string text)
            => AddText(parent, "Label", font, 18, Vector2.zero, Vector2.one, text, Color.white, TextAnchor.MiddleCenter);

        private void SetStretch(RectTransform r, Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = offsetMin;   r.offsetMax = offsetMax;
        }

        private RectTransform CreateHLayout(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.05f, 0.15f); r.anchorMax = new Vector2(0.95f, 0.88f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.2f);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(15, 15, 15, 15);
            return r;
        }

        private void CreateConfirmBtn(RectTransform parent, Font font, string label, out Button btn)
        {
            var go = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.35f, 0.02f); r.anchorMax = new Vector2(0.65f, 0.12f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.5f, 0.3f, 0.2f);
            btn = go.GetComponent<Button>();
            AddLabel(r, font, label);
        }

        private Color GetTierColor(string tierName)
        {
            switch (tierName)
            {
                case "free": return new Color(0.4f, 0.4f, 0.5f);
                case "low":  return new Color(0.3f, 0.5f, 0.4f);
                case "high": return new Color(0.6f, 0.4f, 0.3f);
                default:     return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        private Color GetBlessingColor(RitualRewardType type)
        {
            switch (type)
            {
                case RitualRewardType.LeaderStatBoostTemporary:
                case RitualRewardType.LeaderStatBoostPermanent:
                case RitualRewardType.LeaderStatBoostPercent: return new Color(0.3f, 0.4f, 0.6f);
                case RitualRewardType.Cats:     return new Color(0.3f, 0.55f, 0.3f);
                case RitualRewardType.CatFood:  return new Color(0.5f, 0.4f, 0.2f);
                case RitualRewardType.Accessory:return new Color(0.55f, 0.3f, 0.5f);
                default:                        return new Color(0.45f, 0.45f, 0.45f);
            }
        }

        private string GetRewardTypeName(RitualRewardType type)
        {
            switch (type)
            {
                case RitualRewardType.LeaderStatBoostTemporary: return "族长临时强化";
                case RitualRewardType.LeaderStatBoostPermanent: return "族长永久强化";
                case RitualRewardType.LeaderStatBoostPercent:   return "族长百分比强化";
                case RitualRewardType.Cats:      return "获得小猫";
                case RitualRewardType.CatFood:   return "获得猫粮";
                case RitualRewardType.Consumable:return "获得道具";
                case RitualRewardType.Accessory: return "获得饰品";
                default: return "祝福";
            }
        }
    }

    // ─── 辅助组件 ───────────────────────────────────────────────────────────────

    public class RitualTierCard : MonoBehaviour
    {
        public RitualTier Tier { get; set; }
        public Image Img { get; set; }
    }

    public class RitualBlessingCard : MonoBehaviour
    {
        public RitualRewardItem Blessing { get; set; }
        public Image Img { get; set; }
    }
}
