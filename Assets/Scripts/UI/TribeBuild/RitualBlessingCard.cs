using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 祝福卡片组件
    /// </summary>
    public class RitualBlessingCard : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descText;

        public RitualRewardItem Blessing { get; set; }
        public Image BackgroundImage
        {
            get => _backgroundImage;
            set => _backgroundImage = value;
        }

        public void Setup(RitualRewardItem blessing)
        {
            Blessing = blessing;

            if (_nameText != null)
            {
                _nameText.text = GetRewardTypeName(blessing.rewardType);
            }
            if (_descText != null)
            {
                _descText.text = blessing.displayName;
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
}
