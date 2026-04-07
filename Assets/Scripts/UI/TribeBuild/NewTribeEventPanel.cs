using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 新部族事件面板 - 强制二选一弹窗
    /// 每4回合判定，部族数≤4时触发，在招募+祭祀之后弹出
    /// </summary>
    public class NewTribeEventPanel : MonoBehaviour
    {
        private const string PanelName = "新部族事件";
        private const string HintForce = "二选一：请选择一项奖励";

        [Header("UI 组件（预制体绑定）")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private RectTransform _optionsContainer;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private GameObject _optionCardPrefab;

        private RectTransform _externalRoot;

        private List<NewTribeEventOption> _currentOptions;
        private NewTribeEventOption _selectedOption;
        private Action<NewTribeEventOption> _onOptionSelected;

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
        /// 显示新部族事件选项
        /// </summary>
        public void ShowOptions(List<NewTribeEventOption> options, Action<NewTribeEventOption> onSelected)
        {
            _currentOptions = options;
            _selectedOption = null;
            _onOptionSelected = onSelected;

            ClearOptionCards();
            GenerateOptionCards(options);

            if (_confirmButton != null)
            {
                _confirmButton.interactable = false;
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            Show();
        }

        private void EnsureUIComponents()
        {
            if (_titleText != null && _hintText != null && _optionsContainer != null && _confirmButton != null)
                return;

            _titleText = transform.Find("Title")?.GetComponent<Text>();
            _hintText = transform.Find("Hint")?.GetComponent<Text>();
            _optionsContainer = transform.Find("OptionsContainer") as RectTransform;
            _confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();
        }

        private void GenerateOptionCards(List<NewTribeEventOption> options)
        {
            if (_optionsContainer == null || options == null || _optionCardPrefab == null) return;

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                int optionIndex = i;

                GameObject cardGo = Instantiate(_optionCardPrefab, _optionsContainer);
                NewTribeEventCard cardComponent = cardGo.GetComponent<NewTribeEventCard>();
                if (cardComponent != null)
                {
                    cardComponent.Setup(option, optionIndex);
                }

                Image cardImg = cardGo.GetComponent<Image>();
                if (cardImg != null)
                {
                    cardImg.color = GetOptionCardColor(option.optionType);
                }

                Button cardBtn = cardGo.GetComponent<Button>();
                if (cardBtn != null)
                {
                    cardBtn.onClick.AddListener(() => OnOptionCardClicked(optionIndex));
                }
            }
        }


        private Color GetOptionCardColor(NewTribeEventOptionType optionType)
        {
            switch (optionType)
            {
                case NewTribeEventOptionType.NewRandomTribe:
                    return new Color(0.2f, 0.4f, 0.6f, 1f);
                case NewTribeEventOptionType.CatFoodReward:
                    return new Color(0.6f, 0.5f, 0.2f, 1f);
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }

        private void OnOptionCardClicked(int index)
        {
            if (_currentOptions == null || index < 0 || index >= _currentOptions.Count)
                return;

            _selectedOption = _currentOptions[index];

            // 更新选中状态
            UpdateCardSelection(index);

            if (_confirmButton != null)
            {
                _confirmButton.interactable = true;
            }
        }

        private void UpdateCardSelection(int selectedIndex)
        {
            if (_optionsContainer == null) return;

            for (int i = 0; i < _optionsContainer.childCount; i++)
            {
                Transform child = _optionsContainer.GetChild(i);
                NewTribeEventCard card = child.GetComponent<NewTribeEventCard>();
                if (card != null && card.BackgroundImage != null)
                {
                    if (i == selectedIndex)
                    {
                        card.BackgroundImage.color = new Color(1f, 0.9f, 0.3f, 1f); // 金色选中
                    }
                    else
                    {
                        card.BackgroundImage.color = GetOptionCardColor(card.Option.optionType);
                    }
                }
            }
        }

        private void OnConfirmClicked()
        {
            if (_selectedOption == null) return;

            Hide();
            _onOptionSelected?.Invoke(_selectedOption);
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

}
