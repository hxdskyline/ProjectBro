using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 穿刺箭效果（gameEffectType=100）
    /// 20%概率穿透一个敌人
    /// </summary>
    public class PierceArrowEffect : IBuffEffect
    {
        public int EffectId => 100;

        private const float TriggerChance = 0.2f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            if (Random.value < TriggerChance)
            {
                // 对目标额外造成一次50%攻击力的伤害
                int pierceDmg = Mathf.RoundToInt(ctx.Owner.RuntimeAttributes.Attack * 0.5f);
                ctx.Target.RuntimeAttributes.CurrentHp -= pierceDmg;
                Debug.Log($"[PierceArrowEffect] 穿刺箭触发，穿透伤害 {pierceDmg}");
            }
        }
    }
}
