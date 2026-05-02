using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 冲锋效果（gameEffectType=203）
    /// 开场5秒内移动速度+100%，首次攻击晕眩敌人1秒
    /// </summary>
    public class ChargeEffect : IBuffEffect
    {
        public int EffectId => 203;

        private const float Duration = 5f;
        private const float StunDuration = 1f;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.remainingDuration = Duration;
        }

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Buff.remainingDuration <= 0f) return;

            ctx.Target.FreezeTimer = Mathf.Max(ctx.Target.FreezeTimer, StunDuration);
            ctx.Buff.remainingDuration = 0f;
        }

        public void OnExpire(BuffEffectContext ctx)
        {
        }
    }
}
