using UnityEngine;
using TribeSystem;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 暗杀艺术（gameEffectType=105）
    /// 隐匿期间，对标记目标的伤害 +100%
    /// 实现：OnAttackHit 时，检查 Owner 是否有隐匿 buff + 目标是否有 HuntMark
    /// </summary>
    public class AssassinationEffect : IBuffEffect
    {
        public int EffectId => 105;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            if (HasStealth(ctx.Owner) && HasHuntMark(ctx.Target))
            {
                // 额外造成 100% 攻击力伤害
                int bonusDmg = ctx.Owner.RuntimeAttributes.Attack;
                ctx.Target.RuntimeAttributes.CurrentHp = Mathf.Max(0,
                    ctx.Target.RuntimeAttributes.CurrentHp - bonusDmg);
                Debug.Log($"[AssassinationEffect] 暗杀艺术触发：隐匿+标记，额外 {bonusDmg} 伤害");
            }
        }

        private bool HasStealth(IBattleUnit unit)
        {
            if (unit.RuntimeAttributes?.ActiveBuffs == null) return false;
            for (int i = 0; i < unit.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (unit.RuntimeAttributes.ActiveBuffs[i].buffId == "stealth_atk" ||
                    unit.RuntimeAttributes.ActiveBuffs[i].buffId == "stealth")
                    return true;
            }
            return false;
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
