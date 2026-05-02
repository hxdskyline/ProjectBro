using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 血肉磨坊（gameEffectType=303）
    /// 场上每具尸体，攻速 +1%（通过 AttackSpeedPercentBuff 实现）
    /// </summary>
    public class MeatMillEffect : IBuffEffect
    {
        public int EffectId => 303;

        private int _lastCorpseCount;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            _lastCorpseCount = -1;
            Apply(ctx);
        }

        public void OnTick(BuffEffectContext ctx, float deltaTime)
        {
            Apply(ctx);
        }

        private void Apply(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;

            // 血肉磨坊需要 CorpseManager 来获取尸体数量
            // 但 IBuffEffect 接口没有 CorpseManager 引用
            // 简化实现：通过 buff.effectParam1 存储当前层数，由外部系统更新
            // TODO: 当 CorpseManager 集成到 BuffEffectContext 后，实现真正的尸体计数
            // 当前回退为纯属性 buff（由 aura 系统的 stat buff 部分处理）

            int corpseCount = Mathf.RoundToInt(ctx.Buff.value);
            if (corpseCount == _lastCorpseCount) return;

            var attrs = ctx.Owner.RuntimeAttributes;
            float atkSpeedPerCorpse = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : 1f;

            // 回退旧值，应用新值
            attrs.AttackSpeedPercentBuff -= _lastCorpseCount * atkSpeedPerCorpse * 0.01f;
            _lastCorpseCount = corpseCount;
            attrs.AttackSpeedPercentBuff += corpseCount * atkSpeedPerCorpse * 0.01f;
            attrs.Recalculate();
        }
    }
}
