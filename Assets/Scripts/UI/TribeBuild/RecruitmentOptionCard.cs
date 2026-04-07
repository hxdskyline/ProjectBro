using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 招募选项卡片组件
    /// </summary>
    public class RecruitmentOptionCard : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _descText;

        public RecruitmentOption Option { get; set; }
        public int Index { get; set; }
        public Image BackgroundImage
        {
            get => _backgroundImage;
            set => _backgroundImage = value;
        }

        public void Setup(RecruitmentOption option, int index)
        {
            Option = option;
            Index = index;

            if (_titleText != null)
            {
                _titleText.text = GetOptionTypeTitle(option.optionType);
            }
            if (_costText != null)
            {
                _costText.text = $"消耗: {option.cost} 猫粮";
            }
            if (_descText != null)
            {
                _descText.text = option.description;
            }
        }

        private string GetOptionTypeTitle(RecruitmentOptionType optionType)
        {
            switch (optionType)
            {
                case RecruitmentOptionType.NewTribe:
                    return "新增族群";
                case RecruitmentOptionType.AddCats:
                    return "增加小猫";
                case RecruitmentOptionType.QualityEvolution:
                    return "品质进化";
                case RecruitmentOptionType.LeaderBoost:
                    return "族长强化";
                default:
                    return "招募选项";
            }
        }
    }
}
