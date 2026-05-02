using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 集群（gameEffectType=204）
    /// 每只蝇为周围友军提供+2%攻击速度
    /// 实现：OnTick 时根据附近友方蝇群数量叠加攻速
    /// </summary>
    public class SwarmEffect : IBuffEffect
    {
        public int EffectId => 204;

        private int _lastFlyCount;
        private const float AtkSpeedPerFly = 0.02f; // +2% per fly

        public void OnBattleStart(BuffEffectContext ctx)
        {
            _lastFlyCount = -1;
            Apply(ctx);
        }

        public void OnTick(BuffEffectContext ctx, float deltaTime)
        {
            Apply(ctx);
        }

        private void Apply(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Allies == null) return;

            // 计算附近友方蝇群数量（简化：计算所有存活友方）
            int flyCount = 0;
            for (int i = 0; i < ctx.Allies.Length; i++)
            {
                if (ctx.Allies[i] != null && ctx.Allies[i].IsAlive && ctx.Allies[i] != ctx.Owner)
                    flyCount++;
            }

            if (flyCount == _lastFlyCount) return;

            var attrs = ctx.Owner.RuntimeAttributes;
            // 回退旧值，应用新值
            attrs.AttackSpeedPercentBuff -= _lastFlyCount * AtkSpeedPerFly;
            _lastFlyCount = flyCount;
            attrs.AttackSpeedPercentBuff += flyCount * AtkSpeedPerFly;
            attrs.Recalculate();
        }
    }
}
