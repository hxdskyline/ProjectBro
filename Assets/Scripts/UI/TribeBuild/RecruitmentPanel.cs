using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 招募面板 - 强制三选一弹窗
    /// 用于撸铁系统：词缀选择
    /// </summary>
    public class RecruitmentPanel : MonoBehaviour
    {
        private const string PanelName = "撸铁";
        private const string HintForce = "选择一个词缀强化你的猫咪";

        [Header("UI 组件（预制体绑定）")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private RectTransform _optionsContainer;
        [SerializeField] private GameObject _optionCardPrefab;

        private RectTransform _externalRoot;
        private Font _cachedFont;
        private RectTransform _cachedParent;
        private bool _isRuntimeCreated;
        private GameObject _panelContent;

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
            UpdateHintText();
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
        /// 显示招募选项
        /// </summary>
        public void ShowOptions(List<RecruitmentOption> options, Action<RecruitmentOption> onSelected, Func<RecruitmentOption, TribeType> resolveTribeType = null)
        {
            ClearOptionCards();
            GenerateOptionCards(options, onSelected, resolveTribeType);
            Show();
        }

        private void EnsureUIComponents()
        {
            if (_titleText != null && _hintText != null && _optionsContainer != null)
            {
                return;
            }

            _titleText = transform.Find("Title")?.GetComponent<Text>();
            _hintText = transform.Find("Hint")?.GetComponent<Text>();
            _optionsContainer = transform.Find("OptionsContainer") as RectTransform;

            // 如果组件仍然缺失，尝试用运行时创建
            if (_optionsContainer == null)
            {
                if (_cachedFont == null)
                    _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                RectTransform parent = _externalRoot != null ? _externalRoot : transform.parent as RectTransform;
                if (parent == null) parent = transform as RectTransform;
                EnsureRuntimeUI(parent, _cachedFont);
            }
        }

        private void EnsureRuntimeUI(RectTransform parent, Font font)
        {
            if (_isRuntimeCreated) return;

            if (_optionsContainer != null) return;

            _isRuntimeCreated = true;

            RectTransform targetParent = _externalRoot != null ? _externalRoot : parent;
            GameObject panelGo = new GameObject("RecruitmentPanelContent", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(targetParent, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            if (_externalRoot != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }
            else
            {
                panelRect.anchorMin = new Vector2(0.2f, 0.2f);
                panelRect.anchorMax = new Vector2(0.8f, 0.8f);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }
            Image bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.08f, 0.12f, 0.98f);
            _panelContent = panelGo;

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
            _hintText.color = new Color(1f, 0.7f, 0.3f, 1f);

            // 选项容器（横向布局）
            GameObject optionsGo = new GameObject("OptionsContainer", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            optionsGo.transform.SetParent(panelRect, false);
            RectTransform optionsRect = optionsGo.GetComponent<RectTransform>();
            optionsRect.anchorMin = new Vector2(0.05f, 0.15f);
            optionsRect.anchorMax = new Vector2(0.95f, 0.80f);
            optionsRect.offsetMin = Vector2.zero;
            optionsRect.offsetMax = Vector2.zero;
            _optionsContainer = optionsRect;
            Image optionsBg = optionsGo.GetComponent<Image>();
            optionsBg.color = new Color(0f, 0f, 0f, 0.2f);

            HorizontalLayoutGroup layout = optionsGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(15, 15, 15, 15);
        }

        private void GenerateOptionCards(List<RecruitmentOption> options, Action<RecruitmentOption> onSelected, Func<RecruitmentOption, TribeType> resolveTribeType)
        {
            if (_optionsContainer == null || options == null) return;

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                int optionIndex = i;

                GameObject cardGo;
                RecruitmentOptionCard cardComponent;

                cardGo = Instantiate(_optionCardPrefab, _optionsContainer);
                cardComponent = cardGo.GetComponent<RecruitmentOptionCard>();
                if (cardComponent != null)
                {
                    cardComponent.Setup(option, optionIndex, selectedOption =>
                    {
                        Hide();
                        onSelected?.Invoke(selectedOption);
                    });
                }
            }
        }

        private void CreateOptionCardContent(RectTransform cardRect, RecruitmentOption option)
        {
            Font font = _cachedFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 标题
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(cardRect, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.85f);
            titleRect.anchorMax = new Vector2(0.9f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            Text titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 36;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = GetOptionTypeTitle(option.optionType);

            // 消耗
            GameObject costGo = new GameObject("Cost", typeof(RectTransform), typeof(Text));
            costGo.transform.SetParent(cardRect, false);
            RectTransform costRect = costGo.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.1f, 0.75f);
            costRect.anchorMax = new Vector2(0.9f, 0.83f);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;
            Text costText = costGo.GetComponent<Text>();
            costText.font = font;
            costText.fontSize = 18;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.9f, 0.3f, 1f);
            costText.text = $"消耗: {option.cost} 猫粮";

            // 描述
            GameObject descGo = new GameObject("Description", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(cardRect, false);
            RectTransform descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.15f);
            descRect.anchorMax = new Vector2(0.9f, 0.70f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            Text descText = descGo.GetComponent<Text>();
            descText.font = font;
            descText.fontSize = 32;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            descText.text = option.description;
        }

        private Color GetOptionCardColor(ChoiceCategory optionType)
        {
            switch (optionType)
            {
                case ChoiceCategory.Reinforcement:
                    return new Color(0.2f, 0.4f, 0.6f, 1f);
                case ChoiceCategory.AddCats:
                    return new Color(0.2f, 0.6f, 0.4f, 1f);
                case ChoiceCategory.QualityEvolution:
                    return new Color(0.5f, 0.3f, 0.6f, 1f);
                case ChoiceCategory.Buff:
                    return new Color(0.6f, 0.4f, 0.2f, 1f);
                case ChoiceCategory.Affix:
                    return new Color(0.6f, 0.2f, 0.4f, 1f); // 紫红色
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }

        private string GetOptionTypeTitle(ChoiceCategory optionType)
        {
            switch (optionType)
            {
                case ChoiceCategory.Reinforcement:
                    return "新增族群";
                case ChoiceCategory.AddCats:
                    return "增加小猫";
                case ChoiceCategory.QualityEvolution:
                    return "品质进化";
                case ChoiceCategory.Buff:
                    return "族长强化";
                case ChoiceCategory.Affix:
                    return "词缀";
                default:
                    return "招募选项";
            }
        }

        private void ClearOptionCards()
        {
            if (_optionsContainer == null) return;

            for (int i = _optionsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_optionsContainer.GetChild(i).gameObject);
            }
        }

        public void Show()
        {
            EnsureUIComponents();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (_externalRoot != null)
                _externalRoot.gameObject.SetActive(true);
            if (_panelContent != null)
                _panelContent.SetActive(true);

            UpdateHintText();
        }

        public void Hide()
        {
            gameObject.SetActive(false);

            if (_externalRoot != null)
                _externalRoot.gameObject.SetActive(false);
            if (_panelContent != null)
                _panelContent.SetActive(false);
        }

        private void UpdateHintText()
        {
            if (_hintText == null) return;
            _hintText.text = HintForce;
        }
    }

}
