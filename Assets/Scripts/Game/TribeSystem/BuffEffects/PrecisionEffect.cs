using UnityEngine;
using TribeSystem;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 精准（gameEffectType=102）
    /// 对标记目标的伤害 +40%
    /// 实现：OnAttackHit 时检查目标是否有 HuntMark，额外造成 40% 攻击力伤害
    /// </summary>
    public class PrecisionEffect : IBuffEffect
    {
        public int EffectId => 102;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            if (HasHuntMark(ctx.Target))
            {
                float bonusPercent = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : 0.4f;
                int bonusDmg = Mathf.RoundToInt(ctx.Owner.RuntimeAttributes.Attack * bonusPercent);
                ctx.Target.RuntimeAttributes.CurrentHp = Mathf.Max(0,
                    ctx.Target.RuntimeAttributes.CurrentHp - bonusDmg);
                Debug.Log($"[PrecisionEffect] 精准触发：对标记目标额外 {bonusDmg} 伤害");
            }
        }

        private bool HasHuntMark(IBattleUnit unit)
        {
            if (unit.RuntimeAttributes?.ActiveBuffs == null) return false;
            for (int i = 0; i < unit.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (unit.RuntimeAttributes.ActiveBuffs[i].gameEffect == GameEffect.HuntMark)
                    return true;
            }
            return false;
        }
    }
}
