using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 鸡腿（gameEffectType=201）
    /// 攻击附带 5% 生命窃取
    /// </summary>
    public class ChickenLegEffect : IBuffEffect
    {
        public int EffectId => 201;

        private const float LifestealPercent = 0.05f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            // 计算造成的伤害（近似：攻击力 - 目标防御）
            int rawDmg = Mathf.Max(0, ctx.Owner.RuntimeAttributes.Attack - ctx.Target.RuntimeAttributes.Defense);
            float dr = Mathf.Max(0.2f, 1f - (float)ctx.Target.RuntimeAttributes.Defense / (ctx.Target.RuntimeAttributes.Defense + 100f));
            int damage = Mathf.Max(1, Mathf.RoundToInt(rawDmg * dr));

            // 吸血：回复造成伤害的 5%
            int heal = Mathf.RoundToInt(damage * LifestealPercent);
            if (heal > 0)
            {
                var attrs = ctx.Owner.RuntimeAttributes;
                attrs.CurrentHp = Mathf.Min(attrs.CurrentHp + heal, attrs.MaxHp);
                Debug.Log($"[ChickenLegEffect] 鸡腿触发：吸血 {heal} HP");
            }
        }
    }
}
