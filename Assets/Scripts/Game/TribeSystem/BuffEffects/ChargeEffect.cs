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
            // MoveSpeed buff 已由 AuraService 自动应用，这里只需记录状态
            Debug.Log($"[ChargeEffect] 冲锋激活，{Duration}秒加速+首次攻击眩晕");
        }

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Buff.remainingDuration <= 0f) return; // 已过期

            // 首次攻击眩晕目标
            ctx.Target.FreezeTimer = Mathf.Max(ctx.Target.FreezeTimer, StunDuration);
            Debug.Log($"[ChargeEffect] 冲锋眩晕触发，目标冻结{StunDuration}秒");

            // 移除 buff（首次攻击后失效）
            ctx.Buff.remainingDuration = 0f;
        }

        public void OnExpire(BuffEffectContext ctx)
        {
            Debug.Log($"[ChargeEffect] 冲锋加速结束");
        }
    }
}
