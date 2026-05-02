using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 毒箭效果（gameEffectType=101）
    /// 30%概率附加1层毒（每秒3点，6秒）
    /// </summary>
    public class PoisonArrowEffect : IBuffEffect
    {
        public int EffectId => 101;

        private const float TriggerChance = 0.3f;
        private const float PoisonDps = 3f;
        private const float PoisonDuration = 6f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            if (Random.value < TriggerChance)
            {
                var poisonBuff = StatusEffectFactory.CreatePoison(PoisonDps, PoisonDuration);
                ctx.Target.RuntimeAttributes.ApplyBuff(poisonBuff);
                Debug.Log($"[PoisonArrowEffect] 毒箭触发，附加毒层");
            }
        }
    }
}
