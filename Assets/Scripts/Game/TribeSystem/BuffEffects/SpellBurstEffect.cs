using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 法术迸发（gameEffectType=400）
    /// 普攻变为小范围溅射（50%伤害）
    /// </summary>
    public class SpellBurstEffect : IBuffEffect
    {
        public int EffectId => 400;

        private const float SplashRadius = 2f;
        private const float SplashDamagePercent = 0.5f;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;
            if (ctx.Enemies == null) return;

            // 溅射伤害 = 50% 攻击力
            int splashDmg = Mathf.RoundToInt(ctx.Owner.RuntimeAttributes.Attack * SplashDamagePercent);

            int hitCount = 0;
            for (int i = 0; i < ctx.Enemies.Length; i++)
            {
                var e = ctx.Enemies[i];
                if (e == null || !e.IsAlive || e == ctx.Target) continue;
                if (e.RuntimeAttributes == null) continue;

                // 简化：对所有其他敌人造成溅射伤害（不做距离检查）
                e.RuntimeAttributes.CurrentHp = Mathf.Max(0,
                    e.RuntimeAttributes.CurrentHp - splashDmg);
                hitCount++;
            }

            if (hitCount > 0)
                Debug.Log($"[SpellBurstEffect] 法术迸发触发：溅射 {hitCount} 个目标，{splashDmg} 伤害");
        }
    }
}
