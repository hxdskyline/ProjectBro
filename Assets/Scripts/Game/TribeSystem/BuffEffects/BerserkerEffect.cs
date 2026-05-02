using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 狂战士效果（gameEffectType=202）
    /// 生命低于30%时，攻击力+50%
    /// </summary>
    public class BerserkerEffect : IBuffEffect
    {
        public int EffectId => 202;

        private const float HpThreshold = 0.3f;
        private bool _isActive;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            // 初始检查：如果已经低于30%，立即激活
            UpdateActivation(ctx);
        }

        public void OnTick(BuffEffectContext ctx, float deltaTime)
        {
            UpdateActivation(ctx);
        }

        private void UpdateActivation(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || !ctx.Owner.IsAlive) return;
            var attrs = ctx.Owner.RuntimeAttributes;
            if (attrs == null || attrs.MaxHp <= 0) return;

            float hpPercent = (float)attrs.CurrentHp / attrs.MaxHp;
            bool shouldActivate = hpPercent < HpThreshold;

            if (shouldActivate == _isActive) return;

            _isActive = shouldActivate;
            ctx.Buff.value = _isActive ? 0.5f : 0f;
            attrs.Recalculate();

            Debug.Log($"[BerserkerEffect] {ctx.Owner} 狂战士{(_isActive ? "激活" : "关闭")} (HP={attrs.CurrentHp}/{attrs.MaxHp}, {hpPercent:P0})");
        }
    }
}
