using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 燃烧斗志（gameEffectType=210）
    /// 每层饱食增加2%火焰伤害
    /// 实现：OnTick 时根据饱食层数叠加攻击力（模拟火伤加成）
    /// </summary>
    public class BurningFightEffect : IBuffEffect
    {
        public int EffectId => 210;

        private int _lastFullnessStacks;
        private const float AtkPerStack = 0.02f; // +2% per fullness stack

        public void OnBattleStart(BuffEffectContext ctx)
        {
            _lastFullnessStacks = -1;
            Apply(ctx);
        }

        public void OnTick(BuffEffectContext ctx, float deltaTime)
        {
            Apply(ctx);
        }

        private void Apply(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;

            // 计算饱食层数
            int fullnessStacks = 0;
            if (ctx.Owner.RuntimeAttributes.ActiveBuffs != null)
            {
                for (int i = 0; i < ctx.Owner.RuntimeAttributes.ActiveBuffs.Count; i++)
                {
                    var buff = ctx.Owner.RuntimeAttributes.ActiveBuffs[i];
                    if (buff.buffId == "fullness_stack")
                    {
                        fullnessStacks = buff.currentStacks;
                        break;
                    }
                }
            }

            if (fullnessStacks == _lastFullnessStacks) return;

            var attrs = ctx.Owner.RuntimeAttributes;
            // 回退旧值，应用新值（用 AttackPercentBuff 模拟火伤加成）
            attrs.AttackPercentBuff -= _lastFullnessStacks * AtkPerStack;
            _lastFullnessStacks = fullnessStacks;
            attrs.AttackPercentBuff += fullnessStacks * AtkPerStack;
            attrs.Recalculate();
        }
    }
}
