using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 新部族事件面板 - 强制选择弹窗（支持2选1或3选1）
    /// 用于摇人系统：开局选择种族、第10关选择第3种族
    /// 点击卡片的"就它了"按钮直接选择，无需确认
    /// </summary>
    public class NewTribeEventPanel : MonoBehaviour
    {
        private const string PanelName = "摇人";
        private const string HintForce = "选择一个种族加入你的阵营";

        [Header("UI 组件（预制体绑定）")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
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
        /// 显示新部族事件选项
        /// </summary>
        public void ShowOptions(List<NewTribeEventOption> options, Action<NewTribeEventOption> onSelected)
        {
            ClearOptionCards();
            GenerateOptionCards(options, onSelected);
            Show();
        }

        private void EnsureUIComponents()
        {
            if (_titleText != null && _hintText != null && _optionsContainer != null)
                return;

            _titleText = transform.Find("Title")?.GetComponent<Text>();
            _hintText = transform.Find("Hint")?.GetComponent<Text>();
            _optionsContainer = transform.Find("OptionsContainer") as RectTransform;
        }

        private void GenerateOptionCards(List<NewTribeEventOption> options, Action<NewTribeEventOption> onSelected)
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
                    cardComponent.Setup(option, optionIndex, selectedOption =>
                    {
                        Hide();
                        onSelected?.Invoke(selectedOption);
                    });
                }

                Image cardImg = cardGo.GetComponent<Image>();
                if (cardImg != null)
                {
                    cardImg.color = GetOptionCardColor(option.optionType);
                }
            }
        }

        private Color GetOptionCardColor(NewTribeEventOptionType optionType)
        {
            switch (optionType)
            {
                case NewTribeEventOptionType.NewTribe:
                    return new Color(0.2f, 0.4f, 0.6f, 1f);
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 1f);
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
        }

        public void Hide()
        {
            gameObject.SetActive(false);

            if (_externalRoot != null)
                _externalRoot.gameObject.SetActive(false);
        }
    }

}
