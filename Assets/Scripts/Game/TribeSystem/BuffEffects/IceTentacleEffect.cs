using UnityEngine;
using BattleSystem.Effects;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 寒冰触须（gameEffectType=401）
    /// 普攻附加1层冰霜减速（移速-20%，可叠3层）
    /// </summary>
    public class IceTentacleEffect : IBuffEffect
    {
        public int EffectId => 401;

        private const float SlowPercent = 0.2f;
        private const float SlowDuration = 4f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            var slowBuff = StatusEffectFactory.CreateSlow(SlowPercent, SlowDuration);
            ctx.Target.RuntimeAttributes.ApplyBuff(slowBuff);
        }
    }
}
