using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 单只小猫卡片 - 显示在族群详情弹窗的小猫列表中
    /// 通过 transform.Find 查找预制体节点，不依赖序列化绑定
    /// </summary>
    public class LittleCatCard : MonoBehaviour
    {
        public Text _nameText;
        public Text _statsText;
        public Text _statusText;
        public Image _backgroundImage;

        private void Awake()
        {

        }

        public void Setup(FighterData unit, TribeRecord tribe)
        {
            if (unit == null) return;

            // 品质名称
            if (_nameText != null)
                _nameText.text = GetQualityName(unit.quality);

            // 属性值（直接使用 FighterData 的静态属性）
            if (_statsText != null)
            {
                int atk = Mathf.RoundToInt(unit.staticAttack);
                int def = Mathf.RoundToInt(unit.staticDefense);
                int hp  = Mathf.RoundToInt(unit.staticHp);
                int spd = Mathf.RoundToInt(unit.staticMoveSpeed * 1000);
                _statsText.text = $"攻{atk} 防{def}\n血{hp} 速{spd}";
            }

            // 品质标签（复用 Status 节点）
            if (_statusText != null)
            {
                _statusText.text = GetQualityLabel(unit.quality);
                _statusText.color = GetQualityColor(unit.quality);
            }

            // 背景色随品质变化
            if (_backgroundImage != null)
                _backgroundImage.color = GetQualityBgColor(unit.quality);
        }

        private string GetQualityName(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White:  return "白色兵种";
                case CatQuality.Blue:   return "蓝色兵种";
                case CatQuality.Purple: return "紫色兵种";
                case CatQuality.Gold:   return "金色兵种";
                default: return "兵种";
            }
        }

        private string GetQualityLabel(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White:  return "菜鸟";
                case CatQuality.Blue:   return "老手";
                case CatQuality.Purple: return "精英";
                case CatQuality.Gold:   return "大师";
                default: return "";
            }
        }

        private Color GetQualityColor(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White:  return new Color(0.85f, 0.85f, 0.85f);
                case CatQuality.Blue:   return new Color(0.4f, 0.6f, 1f);
                case CatQuality.Purple: return new Color(0.7f, 0.4f, 1f);
                case CatQuality.Gold:   return new Color(1f, 0.75f, 0.2f);
                default: return Color.white;
            }
        }

        private Color GetQualityBgColor(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White:  return new Color(0.25f, 0.25f, 0.28f);
                case CatQuality.Blue:   return new Color(0.15f, 0.25f, 0.45f);
                case CatQuality.Purple: return new Color(0.28f, 0.15f, 0.42f);
                case CatQuality.Gold:   return new Color(0.42f, 0.3f, 0.08f);
                default: return new Color(0.2f, 0.2f, 0.2f);
            }
        }
    }
}
