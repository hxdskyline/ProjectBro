using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 骨墙强化（gameEffectType=307）
    /// 骨墙生命值+50，且受到攻击时反弹5点物理伤害
    /// 实现：OnDefendHit 时反弹伤害
    /// </summary>
    public class BoneWallEnhanceEffect : IBuffEffect
    {
        public int EffectId => 307;

        private const float ReflectDamage = 5f;

        public void OnDefendHit(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            // 反弹 5 点伤害给攻击者
            ctx.Target.RuntimeAttributes.CurrentHp = Mathf.Max(0,
                ctx.Target.RuntimeAttributes.CurrentHp - Mathf.RoundToInt(ReflectDamage));
            Debug.Log($"[BoneWallEnhanceEffect] 骨墙强化触发：反弹 {ReflectDamage} 伤害");
        }
    }
}
