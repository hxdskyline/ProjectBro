using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 抉择面板 - 强制选择弹窗（二选一或三选一）
    /// 用于随机事件系统：第5、10、15关出现
    /// 低风险低回报 vs 高风险高回报 vs 我全要了
    /// </summary>
    public class RandomEventPanel : MonoBehaviour
    {
        private const string PanelName = "抉择";
        private const string HintForce = "选择一个选项（此操作不可跳过）";

        [Header("UI 组件（预制体绑定）")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private Text _eventNameText;
        [SerializeField] private RectTransform _optionsContainer;
        [SerializeField] private GameObject _optionCardPrefab;

        private RectTransform _externalRoot;

        /// <summary>
        /// 设置外部根节点（用于强制弹窗场景）
        /// </summary>
        public void SetExternalRoot(RectTransform externalRoot)
        {
            _externalRoot = externalRoot;
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
            if (_hintText != null)
            {
                _hintText.text = HintForce;
            }
        }

        /// <summary>
        /// 显示随机事件选项
        /// </summary>
        public void ShowEvent(RandomEvent randomEvent, Action<RandomEventOption> onSelected)
        {
            ClearOptionCards();

            if (randomEvent == null)
            {
                Hide();
                return;
            }

            // 显示事件名称
            if (_eventNameText != null)
            {
                _eventNameText.text = randomEvent.eventName;
            }

            // 生成选项卡片
            var options = new List<RandomEventOption>();

            if (randomEvent.lowRiskOption != null)
            {
                options.Add(randomEvent.lowRiskOption);
            }
            if (randomEvent.highRiskOption != null)
            {
                options.Add(randomEvent.highRiskOption);
            }
            if (randomEvent.bothOption != null)
            {
                options.Add(randomEvent.bothOption);
            }

            GenerateOptionCards(options, onSelected);
            Show();
        }

        private void EnsureUIComponents()
        {
            if (_titleText != null && _hintText != null && _optionsContainer != null)
                return;

            _titleText = transform.Find("Title")?.GetComponent<Text>();
            _hintText = transform.Find("Hint")?.GetComponent<Text>();
            _eventNameText = transform.Find("EventName")?.GetComponent<Text>();
            _optionsContainer = transform.Find("OptionsContainer") as RectTransform;
        }

        private void GenerateOptionCards(List<RandomEventOption> options, Action<RandomEventOption> onSelected)
        {
            if (_optionsContainer == null || options == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                int optionIndex = i;

                GameObject cardGo;

                if (_optionCardPrefab != null)
                {
                    cardGo = Instantiate(_optionCardPrefab, _optionsContainer);
                }
                else
                {
                    // 运行时创建选项卡片
                    cardGo = CreateRuntimeOptionCard(_optionsContainer, option, optionIndex, font, onSelected);
                    continue;
                }

                RandomEventOptionCard cardComponent = cardGo.GetComponent<RandomEventOptionCard>();
                if (cardComponent != null)
                {
                    cardComponent.Setup(option, optionIndex, selectedOption =>
                    {
                        Hide();
                        onSelected?.Invoke(selectedOption);
                    });
                }

                Image cardImg = cardGo.GetComponent<Image>();
                if (cardImg != null)
                {
                    cardImg.color = GetOptionCardColor(option.optionType, optionIndex);
                }
            }
        }

        private GameObject CreateRuntimeOptionCard(RectTransform parent, RandomEventOption option, int index, Font font, Action<RandomEventOption> onSelected)
        {
            GameObject cardGo = new GameObject($"Option_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(parent, false);
            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(200f, 280f);

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = GetOptionCardColor(option.optionType, index);

            // 标题
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(cardRect, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.85f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            Text titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 22;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            switch (index)
            {
                case 0: titleText.text = "低风险"; break;
                case 1: titleText.text = "高风险"; break;
                case 2: titleText.text = "我全要了"; break;
                default: titleText.text = "选项"; break;
            }

            // 描述
            GameObject descGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(cardRect, false);
            RectTransform descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.05f, 0.15f);
            descRect.anchorMax = new Vector2(0.95f, 0.80f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            Text descText = descGo.GetComponent<Text>();
            descText.font = font;
            descText.fontSize = 16;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            descText.text = option.description;

            // 按钮
            Button btn = cardGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Hide();
                onSelected?.Invoke(option);
            });

            return cardGo;
        }

        private Color GetOptionCardColor(RandomEventOptionType optionType, int index)
        {
            // 低风险=绿色，高风险=红色，我全要了=金色
            if (index == 0)
                return new Color(0.2f, 0.5f, 0.3f, 1f); // 低风险-绿色
            else if (index == 1)
                return new Color(0.6f, 0.2f, 0.2f, 1f); // 高风险-红色
            else
                return new Color(0.6f, 0.5f, 0.2f, 1f); // 我全要了-金色
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
        }

        public void Hide()
        {
            gameObject.SetActive(false);

            if (_externalRoot != null)
                _externalRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 随机事件选项卡片组件
    /// </summary>
    public class RandomEventOptionCard : MonoBehaviour
    {
        public Text _titleText;
        public Text _descText;
        public Button _okButton;

        public RandomEventOption Option { get; set; }
        public int Index { get; set; }

        public void Setup(RandomEventOption option, int index, Action<RandomEventOption> onSelected)
        {
            Option = option;
            Index = index;

            if (_titleText != null)
            {
                _titleText.text = GetOptionTitle(index);
            }
            if (_descText != null)
            {
                _descText.text = option.description;
            }
            if (_okButton != null)
            {
                _okButton.onClick.RemoveAllListeners();
                _okButton.onClick.AddListener(() => onSelected?.Invoke(option));
            }
        }

        private string GetOptionTitle(int index)
        {
            switch (index)
            {
                case 0: return "低风险";
                case 1: return "高风险";
                case 2: return "我全要了";
                default: return "选项";
            }
        }
    }
}
