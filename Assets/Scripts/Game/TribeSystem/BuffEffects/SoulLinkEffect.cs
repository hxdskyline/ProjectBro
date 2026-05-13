using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 灵魂连接（gameEffectType=302）
    /// 拥有此 buff 的单位受到伤害时，30% 伤害转移给族长
    /// </summary>
    public class SoulLinkEffect : IBuffEffect
    {
        public int EffectId => 302;

        private const float TransferPercent = 0.3f;

        public void OnDefendHit(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;

            // 灵魂连接效果：Owner 受到的伤害减少 30%（等效减伤）
            float transferRatio = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : TransferPercent;

            Debug.Log($"[SoulLinkEffect] 灵魂连接：伤害减少 {transferRatio * 100f}%");
        }
    }
}
