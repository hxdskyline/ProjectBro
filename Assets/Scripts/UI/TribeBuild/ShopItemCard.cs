using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 商店物品卡片组件
    /// </summary>
    public class ShopItemCard : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _priceText;
        [SerializeField] private Text _typeText;
        [SerializeField] private Text _descText;
        [SerializeField] private Image _fishIconImage;

        public ShopItem Item { get; set; }
        public Image BackgroundImage { get => _backgroundImage; set => _backgroundImage = value; }

        public void Setup(ShopItem item)
        {
            Item = item;
            if (_nameText != null) _nameText.text = item.name;
            if (_priceText != null) _priceText.text = $"{item.GetActualPrice()} 猫粮";
            if (_typeText != null) _typeText.text = GetShopItemTypeName(item.itemType);
            if (_descText != null) _descText.text = TruncateText(item.description, 30);

            if (_iconImage != null)
            {
                if (!string.IsNullOrEmpty(item.iconAddress))
                    _iconImage.sprite = GameManager.Instance.ResourceManager.LoadSprite(item.iconAddress);
                else
                    _iconImage.sprite = null;
            }
        }

        private const string SelectedIcon = "ui/sprite/shop/shangren_img_daojvdi1";
        private const string UnselectedIcon = "ui/sprite/shop/shangren_img_daojvdi2";
        private static readonly Color SelectedTextColor = new Color(0.2f, 0.192f, 0.192f, 1f);    // #333131
        private static readonly Color UnselectedTextColor = new Color(0.984f, 0.965f, 0.855f, 1f); // #FBF6DA
        private static readonly Color SelectedAccentColor = new Color(0.953f, 0.745f, 0.129f, 1f); // #F3BE21

        public void SetSelected(bool selected)
        {
            if (_backgroundImage != null)
            {
                string addr = selected ? SelectedIcon : UnselectedIcon;
                _backgroundImage.sprite = GameManager.Instance.ResourceManager.LoadSprite(addr);
            }

            Color textColor = selected ? SelectedTextColor : UnselectedTextColor;
            if (_nameText != null) _nameText.color = textColor;
            if (_typeText != null) _typeText.color = textColor;
            if (_descText != null) _descText.color = textColor;

            Color accentColor = selected ? SelectedAccentColor : UnselectedTextColor;
            if (_priceText != null) _priceText.color = accentColor;
            if (_fishIconImage != null) _fishIconImage.color = accentColor;
        }

        private string GetShopItemTypeName(ShopItemType itemType)
        {
            switch (itemType)
            {
                case ShopItemType.Artifact: return "奇物";
                case ShopItemType.Consumable: return "消耗品";
                case ShopItemType.Cat: return "小猫";
                default: return "未知";
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }
    }
}
