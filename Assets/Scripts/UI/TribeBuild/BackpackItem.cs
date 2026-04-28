using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 饰品背包条目 - 挂在饰品预制体上
    /// </summary>
    public class BackpackItem : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descText;
        [SerializeField] private Text _statusText;

        public void Setup(string name, string description, GameEffect primaryEffect)
        {
            if (_nameText != null)
                _nameText.text = name;

            if (_descText != null)
                _descText.text = description;

            if (_statusText != null)
                _statusText.text = "已收集";

            if (_iconImage != null)
                _iconImage.color = GetEffectColor(primaryEffect);
        }

        private Color GetEffectColor(GameEffect effect)
        {
            switch (effect)
            {
                case GameEffect.AttackPercent:
                case GameEffect.AttackFlat:
                case GameEffect.DoubleHit:
                    return new Color(0.9f, 0.3f, 0.2f, 1f);
                case GameEffect.DefensePercent:
                case GameEffect.DefenseFlat:
                case GameEffect.DamageReduce:
                case GameEffect.DamageReflect:
                    return new Color(0.3f, 0.5f, 0.9f, 1f);
                case GameEffect.HpPercent:
                case GameEffect.HpFlat:
                case GameEffect.Lifesteal:
                case GameEffect.HealAll:
                    return new Color(0.2f, 0.8f, 0.3f, 1f);
                case GameEffect.SpeedPercent:
                case GameEffect.SpeedFlat:
                    return new Color(0.8f, 0.6f, 0.1f, 1f);
                case GameEffect.AllPercent:
                case GameEffect.CritChance:
                case GameEffect.CritDamage:
                    return new Color(0.7f, 0.4f, 0.9f, 1f);
                case GameEffect.ExtraCatOnRecruit:
                case GameEffect.RecruitCostReduce:
                case GameEffect.CatFoodGain:
                    return new Color(0.9f, 0.7f, 0.2f, 1f);
                case GameEffect.SummonTotem:
                    return new Color(0.4f, 0.8f, 0.8f, 1f);
                case GameEffect.Bomb:
                case GameEffect.FreezeAll:
                case GameEffect.BuffAttack:
                case GameEffect.BuffDefense:
                    return new Color(0.9f, 0.5f, 0.1f, 1f);
                default:
                    return Color.gray;
            }
        }
    }
}
