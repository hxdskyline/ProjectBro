using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 龙族血脉（gameEffectType=404）
    /// 生命值+10，法术吸血10%
    /// 实现：OnAttackHit 时吸血 10%
    /// </summary>
    public class DragonBloodEffect : IBuffEffect
    {
        public int EffectId => 404;

        private const float SpellLifestealPercent = 0.1f;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            // 生命值+10
            ctx.Owner.RuntimeAttributes.MaxHp += 10;
            ctx.Owner.RuntimeAttributes.CurrentHp += 10;
        }

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            // 法术吸血：回复造成伤害的 10%
            int rawDmg = Mathf.Max(0, ctx.Owner.RuntimeAttributes.Attack - ctx.Target.RuntimeAttributes.Defense);
            float dr = Mathf.Max(0.2f, 1f - (float)ctx.Target.RuntimeAttributes.Defense / (ctx.Target.RuntimeAttributes.Defense + 100f));
            int damage = Mathf.Max(1, Mathf.RoundToInt(rawDmg * dr));
            int heal = Mathf.RoundToInt(damage * SpellLifestealPercent);

            if (heal > 0)
            {
                ctx.Owner.RuntimeAttributes.CurrentHp = Mathf.Min(
                    ctx.Owner.RuntimeAttributes.CurrentHp + heal,
                    ctx.Owner.RuntimeAttributes.MaxHp);
            }
        }
    }
}
