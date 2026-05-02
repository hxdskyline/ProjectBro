using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 亡灵统帅（gameEffectType=306）
    /// 族长每拥有10点攻击力，所有友军攻速+5%
    /// </summary>
    public class UndeadCommanderEffect : IBuffEffect
    {
        public int EffectId => 306;

        private int _lastAtkTier;
        private const float AtkSpeedPerTier = 0.05f; // +5% per 10 ATK

        public void OnBattleStart(BuffEffectContext ctx)
        {
            _lastAtkTier = -1;
            Apply(ctx);
        }

        public void OnTick(BuffEffectContext ctx, float deltaTime)
        {
            Apply(ctx);
        }

        private void Apply(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Allies == null) return;

            // 族长攻击力的"10点档位"
            int atkTier = ctx.Owner.RuntimeAttributes.Attack / 10;
            if (atkTier == _lastAtkTier) return;

            // 回退旧值，对所有友方应用新值
            float oldBonus = _lastAtkTier * AtkSpeedPerTier;
            float newBonus = atkTier * AtkSpeedPerTier;

            for (int i = 0; i < ctx.Allies.Length; i++)
            {
                var ally = ctx.Allies[i];
                if (ally == null || !ally.IsAlive || ally.RuntimeAttributes == null) continue;

                ally.RuntimeAttributes.AttackSpeedPercentBuff -= oldBonus;
                ally.RuntimeAttributes.AttackSpeedPercentBuff += newBonus;
                ally.RuntimeAttributes.Recalculate();
            }

            _lastAtkTier = atkTier;
            Debug.Log($"[UndeadCommanderEffect] 亡灵统帅：族长 {ctx.Owner.RuntimeAttributes.Attack} ATK → 友军攻速 +{newBonus * 100}%");
        }
    }
}
