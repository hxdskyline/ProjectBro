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
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _priceText;
        [SerializeField] private Text _typeText;
        [SerializeField] private Text _descText;

        public ShopItem Item { get; set; }
        public Image BackgroundImage { get => _backgroundImage; set => _backgroundImage = value; }

        public void Setup(ShopItem item)
        {
            Item = item;
            if (_nameText != null) _nameText.text = item.name;
            if (_priceText != null) _priceText.text = $"{item.GetActualPrice()} 猫粮";
            if (_typeText != null) _typeText.text = GetShopItemTypeName(item.itemType);
            if (_descText != null) _descText.text = TruncateText(item.description, 30);
        }

        private string GetShopItemTypeName(ShopItemType itemType)
        {
            switch (itemType)
            {
                case ShopItemType.Artifact: return "奇物";
                case ShopItemType.Consumable: return "道具";
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
