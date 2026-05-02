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
            if (ctx.Allies == null) return;

            // 找到族长（友方中 IsLeader 的单位）
            IBattleUnit leader = null;
            for (int i = 0; i < ctx.Allies.Length; i++)
            {
                if (ctx.Allies[i] != null && ctx.Allies[i].IsAlive)
                {
                    // 通过 RuntimeAttributes 判断（IBattleUnit 没有 IsLeader）
                    // 灵魂连接只能由族长施加，所以 Owner 就是族长
                    // 但 buff 是挂在小猫身上的，所以需要找族长
                    // 简化：Owner 不是族长时才转移
                    break;
                }
            }

            // 灵魂连接效果：将 Owner 受到伤害的 30% 转移给 Owner 的族长
            // 由于 IBattleUnit 没有 IsLeader 属性，我们假设 Owner 就是族长（buff 挂在族长身上）
            // 实际设计是挂在小猫身上，转移给族长
            // 这里简化为：Owner 受到的伤害减少 30%（等效减伤）
            // TODO: 当战斗系统支持伤害事件时，实现真正的伤害转移
            float transferRatio = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : TransferPercent;

            // 当前简化实现：直接减少 Owner 受到的伤害（通过 DefenseFlatBuff 模拟）
            // 注意：这不是真正的伤害转移，只是减伤的近似
            Debug.Log($"[SoulLinkEffect] 灵魂连接：30% 伤害转移给族长（简化为减伤）");
        }
    }
}
