using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 瘟疫使者（gameEffectType=309）
    /// 所有毒伤害+50%，毒层数上限+5
    /// 实现：OnAttackHit 时，如果目标有中毒，额外附加一层毒（模拟 +50% 毒伤）
    /// </summary>
    public class PlaguebearerEffect : IBuffEffect
    {
        public int EffectId => 309;

        private const float BonusPoisonDps = 1.5f; // +50% of base 3dps
        private const float BonusPoisonDuration = 6f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            // 检查目标是否有中毒
            if (HasPoison(ctx.Target))
            {
                // 额外附加一层毒（模拟 +50% 毒伤）
                var poison = BattleSystem.Effects.StatusEffectFactory.CreatePoison(
                    BonusPoisonDps, BonusPoisonDuration);
                ctx.Target.RuntimeAttributes.ApplyBuff(poison);
                Debug.Log($"[PlaguebearerEffect] 瘟疫使者触发：对中毒目标附加额外毒层");
            }
        }

        private bool HasPoison(IBattleUnit unit)
        {
            if (unit.RuntimeAttributes?.ActiveBuffs == null) return false;
            for (int i = 0; i < unit.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (unit.RuntimeAttributes.ActiveBuffs[i].gameEffect == GameEffect.Poison)
                    return true;
            }
            return false;
        }
    }
}
