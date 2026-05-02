using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 狩猎印记 - 族长被动（gameEffectType=302）
    /// 攻击标记目标 5 秒，被标记目标受到的伤害 +30%
    /// </summary>
    public class HuntMarkInnateEffect : IBuffEffect
    {
        public int EffectId => 302;

        private const float MarkDuration = 5f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            float damageAmp = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : 0.3f;
            var markBuff = StatusEffectFactory.CreateHuntMark(damageAmp, MarkDuration);
            ctx.Target.RuntimeAttributes.ApplyBuff(markBuff);
        }
    }
}
