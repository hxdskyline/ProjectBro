using UnityEngine;
using TribeSystem;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 致命印记（gameEffectType=107）
    /// 狩猎印记的易伤效果 +20%（变为 +50%）
    /// 实现：OnAttackHit 时，如果目标有 HuntMark，增强其易伤效果
    /// </summary>
    public class DeadlyMarkEffect : IBuffEffect
    {
        public int EffectId => 107;

        private const float BonusAmp = 0.2f; // +20% 易伤

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Target == null || !ctx.Target.IsAlive) return;
            if (ctx.Target.RuntimeAttributes == null) return;

            // 增强目标身上的 HuntMark 效果
            if (ctx.Target.RuntimeAttributes.ActiveBuffs != null)
            {
                for (int i = 0; i < ctx.Target.RuntimeAttributes.ActiveBuffs.Count; i++)
                {
                    var buff = ctx.Target.RuntimeAttributes.ActiveBuffs[i];
                    if (buff.gameEffect == GameEffect.HuntMark)
                    {
                        // 增加易伤参数（effectParam1 存储易伤百分比）
                        buff.effectParam1 += BonusAmp;
                        Debug.Log($"[DeadlyMarkEffect] 致命印记触发：HuntMark 易伤 +{BonusAmp * 100}%");
                        break;
                    }
                }
            }
        }
    }
}
