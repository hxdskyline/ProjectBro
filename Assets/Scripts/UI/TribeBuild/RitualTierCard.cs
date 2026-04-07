using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 祭祀档位卡片组件
    /// </summary>
    public class RitualTierCard : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _hintText;
        [SerializeField] private Text _lockText;

        public RitualTier Tier { get; set; }
        public Image BackgroundImage
        {
            get => _backgroundImage;
            set => _backgroundImage = value;
        }

        public void Setup(RitualTier tier, bool canAfford)
        {
            Tier = tier;

            if (_nameText != null)
            {
                _nameText.text = tier.displayName ?? tier.tierName;
            }
            if (_costText != null)
            {
                string costText = tier.cost == 0 ? "免费" : $"{tier.cost} 猫粮";
                _costText.text = costText;
                _costText.color = canAfford ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.4f);
            }
            if (_hintText != null)
            {
                _hintText.text = $"抽取 {tier.drawCount} 条祝福\n三选一";
            }
            if (_lockText != null)
            {
                _lockText.gameObject.SetActive(!canAfford);
            }
        }
    }
}
