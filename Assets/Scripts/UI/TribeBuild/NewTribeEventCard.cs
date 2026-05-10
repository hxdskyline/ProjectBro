using System;
using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 新部族事件卡片组件
    /// </summary>
    public class NewTribeEventCard : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _descText;
        [SerializeField] private Button _okButton;

        public NewTribeEventOption Option { get; set; }
        public int Index { get; set; }
        public Image BackgroundImage
        {
            get => _backgroundImage;
            set => _backgroundImage = value;
        }

        public void Setup(NewTribeEventOption option, int index, Action<NewTribeEventOption> onSelected)
        {
            Option = option;
            Index = index;

            if (_titleText != null)
            {
                _titleText.text = GetFighterName(option.fighterId);
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

        private string GetFighterName(int fighterId)
        {
            var config = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            if (config != null && !string.IsNullOrEmpty(config.fighterName))
                return config.fighterName;
            return $"兵种{fighterId}";
        }
    }
}
