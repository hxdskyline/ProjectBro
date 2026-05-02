using UnityEngine;
using BattleSystem.Effects;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 淬毒之爪（gameEffectType=300）
    /// 攻击命中时附带中毒效果（30% 概率，每秒 3 点，持续 6 秒）
    /// </summary>
    public class PoisonClawEffect : IBuffEffect
    {
        public int EffectId => 300;

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
                Debug.Log($"[PoisonClawEffect] 淬毒之爪触发，附加毒层");
            }
        }
    }
}
