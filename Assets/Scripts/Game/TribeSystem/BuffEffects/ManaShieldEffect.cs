using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 法力护盾（gameEffectType=402）
    /// 受到伤害时消耗法力抵消（每点法力抵2伤害）
    /// 简化实现：被动增加防御力（模拟减伤效果）
    /// </summary>
    public class ManaShieldEffect : IBuffEffect
    {
        public int EffectId => 402;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;

            // 简化：直接增加 20% 减伤（模拟法力护盾效果）
            ctx.Owner.RuntimeAttributes.DefensePercentBuff += 0.2f;
            ctx.Owner.RuntimeAttributes.Recalculate();
            Debug.Log($"[ManaShieldEffect] 法力护盾激活：减伤 20%");
        }
    }
}
