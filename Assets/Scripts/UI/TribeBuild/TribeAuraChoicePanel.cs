using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 种族光环选择面板 — 显示二选一/四选二光环 buff 供玩家选择
    /// </summary>
    public class TribeAuraChoicePanel : MonoBehaviour
    {
        [Header("标题")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _subtitleText;

        [Header("固定 buff 显示")]
        [SerializeField] private GameObject _fixedAuraGroup;
        [SerializeField] private Text _fixedAuraName;
        [SerializeField] private Text _fixedAuraDesc;

        [Header("选项容器")]
        [SerializeField] private Transform _optionsContainer;

        [Header("按钮")]
        [SerializeField] private Button _confirmButton;

        private AuraChoiceResult _choiceResult;
        private List<TribeAuraOption> _selectedOptions = new List<TribeAuraOption>();
        private Action<List<string>> _onConfirmed;
        private List<GameObject> _createdItems = new List<GameObject>();

        /// <summary>
        /// 显示光环选择
        /// </summary>
        public void Show(AuraChoiceResult choice, Action<List<string>> onConfirmed)
        {
            _choiceResult = choice;
            _onConfirmed = onConfirmed;
            _selectedOptions.Clear();

            gameObject.SetActive(true);

            // 标题
            string tierName = choice.tier == UnitTier.Tier1 ? "一级兵" :
                              choice.tier == UnitTier.Tier2 ? "二级兵" : "三级兵";
            if (_titleText != null)
                _titleText.text = $"{GetTribeName(choice.tribeType)} - {choice.unitName}";
            if (_subtitleText != null)
                _subtitleText.text = $"选择{tierName}光环 buff";

            // 固定 buff
            if (_fixedAuraGroup != null)
            {
                bool hasFixed = choice.fixedAura != null;
                _fixedAuraGroup.SetActive(hasFixed);
                if (hasFixed)
                {
                    if (_fixedAuraName != null) _fixedAuraName.text = choice.fixedAura.auraName;
                    if (_fixedAuraDesc != null) _fixedAuraDesc.text = choice.fixedAura.description;
                }
            }

            // 选项
            ClearOptions();
            int pickCount = choice.selectionRule == "pick2of4" ? 2 : 1;
            foreach (var option in choice.options)
            {
                CreateOptionItem(option, pickCount);
            }

            // 确认按钮
            if (_confirmButton != null)
            {
                _confirmButton.interactable = false;
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(OnConfirm);
            }
        }

        private void CreateOptionItem(TribeAuraOption option, int maxPick)
        {
            // 动态创建选项条目（基于简单的 UI 结构）
            var item = new GameObject($"Option_{option.auraId}", typeof(RectTransform));
            item.transform.SetParent(_optionsContainer, false);

            var layout = item.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 5, 5);

            var le = item.AddComponent<LayoutElement>();
            le.minHeight = 50;
            le.preferredHeight = 60;

            // 选中框
            var toggleGo = new GameObject("Toggle", typeof(RectTransform));
            toggleGo.transform.SetParent(item.transform, false);
            var toggleLe = toggleGo.AddComponent<LayoutElement>();
            toggleLe.minWidth = 30;
            toggleLe.preferredWidth = 30;
            var toggleImg = toggleGo.AddComponent<Image>();
            toggleImg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            // 文字容器
            var textGroup = new GameObject("TextGroup", typeof(RectTransform));
            textGroup.transform.SetParent(item.transform, false);
            var textLe = textGroup.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1;

            var textLayout = textGroup.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 2;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;
            textLayout.padding = new RectOffset(5, 5, 5, 5);

            // 名称
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(textGroup.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = option.auraName;
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.preferredHeight = 22;

            // 描述
            var descGo = new GameObject("Desc", typeof(RectTransform));
            descGo.transform.SetParent(textGroup.transform, false);
            var descText = descGo.AddComponent<Text>();
            descText.text = option.description;
            descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            descText.fontSize = 13;
            descText.color = new Color(0.8f, 0.8f, 0.8f);
            var descLe = descGo.AddComponent<LayoutElement>();
            descLe.preferredHeight = 20;

            _createdItems.Add(item);

            // 点击事件
            var button = item.AddComponent<Button>();
            button.targetGraphic = toggleImg;
            button.onClick.AddListener(() =>
            {
                ToggleOption(option, toggleImg, maxPick);
            });

            // 存储引用
            item.name = option.auraId;
        }

        private void ToggleOption(TribeAuraOption option, Image toggleImg, int maxPick)
        {
            bool isSelected = _selectedOptions.Contains(option);
            if (isSelected)
            {
                _selectedOptions.Remove(option);
                toggleImg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            }
            else
            {
                if (_selectedOptions.Count >= maxPick)
                {
                    // 替换第一个已选的
                    var removed = _selectedOptions[0];
                    _selectedOptions.RemoveAt(0);
                    // 恢复被替换项的视觉状态
                    var removedItem = _createdItems.Find(i => i.name == removed.auraId);
                    if (removedItem != null)
                    {
                        var removedImg = removedItem.GetComponentInChildren<Image>();
                        if (removedImg != null)
                            removedImg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    }
                }
                _selectedOptions.Add(option);
                toggleImg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            }

            if (_confirmButton != null)
                _confirmButton.interactable = _selectedOptions.Count > 0;
        }

        private void OnConfirm()
        {
            var chosenIds = new List<string>();
            foreach (var opt in _selectedOptions)
            {
                chosenIds.Add(opt.auraId);
            }

            _onConfirmed?.Invoke(chosenIds);
            Hide();
        }

        public void Hide()
        {
            ClearOptions();
            gameObject.SetActive(false);
        }

        private void ClearOptions()
        {
            foreach (var item in _createdItems)
            {
                if (item != null) Destroy(item);
            }
            _createdItems.Clear();
        }

        private string GetTribeName(TribeType type)
        {
            switch (type)
            {
                case TribeType.Tabby: return "狸花猫族";
                case TribeType.Orange: return "大橘猫族";
                case TribeType.Cow: return "奶牛猫族";
                case TribeType.Siamese: return "暹罗猫族";
                default: return type.ToString();
            }
        }
    }
}
