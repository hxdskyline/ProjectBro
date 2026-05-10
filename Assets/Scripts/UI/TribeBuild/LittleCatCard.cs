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

        public void Setup(CatData cat, TribeRecord tribe)
        {
            if (cat == null) return;

            // 品质名称
            if (_nameText != null)
                _nameText.text = GetQualityName(cat.quality);

            // 属性倍率（展示相对于族长基础属性的比例）
            if (_statsText != null)
            {
                if (tribe?.leader != null)
                {
                    var leader = tribe.leader;
                    int atk = Mathf.RoundToInt(leader.baseAttack * cat.attackMultiplier);
                    int def = Mathf.RoundToInt(leader.baseDefense * cat.defenseMultiplier);
                    int hp  = Mathf.RoundToInt(leader.baseHp * cat.hpMultiplier);
                    int spd = Mathf.RoundToInt(leader.baseMoveSpeed * cat.speedMultiplier * 1000);
                    _statsText.text = $"攻{atk} 防{def}\n血{hp} 速{spd}";
                }
                else
                {
                    _statsText.text = $"攻{cat.attackMultiplier:P0} 防{cat.defenseMultiplier:P0}\n血{cat.hpMultiplier:P0} 速{cat.speedMultiplier:P0}";
                }
            }

            // 品质标签（复用 Status 节点）
            if (_statusText != null)
            {
                _statusText.text = GetQualityLabel(cat.quality);
                _statusText.color = GetQualityColor(cat.quality);
            }

            // 背景色随品质变化
            if (_backgroundImage != null)
                _backgroundImage.color = GetQualityBgColor(cat.quality);
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
