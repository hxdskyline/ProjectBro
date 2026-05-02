using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 牧群领袖（gameEffectType=301）
    /// 场上每有一个友方单位，族长攻击力 +1
    /// </summary>
    public class PackLeaderEffect : IBuffEffect
    {
        public int EffectId => 301;

        private int _lastFriendlyCount;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            _lastFriendlyCount = -1;
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

            int friendlyCount = 0;
            for (int i = 0; i < ctx.Allies.Length; i++)
            {
                if (ctx.Allies[i] != null && ctx.Allies[i].IsAlive && ctx.Allies[i] != ctx.Owner)
                    friendlyCount++;
            }

            if (friendlyCount == _lastFriendlyCount) return;

            var attrs = ctx.Owner.RuntimeAttributes;
            float atkPerFriendly = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : 1f;

            // 回退旧值，应用新值
            attrs.AttackFlatBuff -= Mathf.RoundToInt(_lastFriendlyCount * atkPerFriendly);
            _lastFriendlyCount = friendlyCount;
            attrs.AttackFlatBuff += Mathf.RoundToInt(friendlyCount * atkPerFriendly);
            attrs.Recalculate();
        }
    }
}
