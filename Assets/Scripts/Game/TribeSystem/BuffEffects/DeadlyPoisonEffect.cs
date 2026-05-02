using UnityEngine;
using TribeSystem;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 致命毒药（gameEffectType=104）
    /// 对标记目标的毒伤 +100%
    /// 实现：OnAttackHit 时，如果目标有 HuntMark，额外附加一层毒
    /// </summary>
    public class DeadlyPoisonEffect : IBuffEffect
    {
        public int EffectId => 104;

        private const float BonusPoisonDps = 3f;
        private const float BonusPoisonDuration = 6f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            if (HasHuntMark(ctx.Target))
            {
                // 对标记目标额外附加一层毒（模拟 +100% 毒伤）
                var poison = BattleSystem.Effects.StatusEffectFactory.CreatePoison(
                    BonusPoisonDps, BonusPoisonDuration);
                ctx.Target.RuntimeAttributes.ApplyBuff(poison);
                Debug.Log($"[DeadlyPoisonEffect] 致命毒药触发：对标记目标附加额外毒层");
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
