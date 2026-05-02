using UnityEngine;
using BattleSystem.Effects;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 剧毒蝇（gameEffectType=206）
    /// 蝇群攻击附加1层毒（每秒3点，6秒）
    /// </summary>
    public class PoisonFlyEffect : IBuffEffect
    {
        public int EffectId => 206;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            var poison = StatusEffectFactory.CreatePoison(3f, 6f);
            ctx.Target.RuntimeAttributes.ApplyBuff(poison);
            Debug.Log($"[PoisonFlyEffect] 剧毒蝇触发：附加毒层");
        }
    }
}
