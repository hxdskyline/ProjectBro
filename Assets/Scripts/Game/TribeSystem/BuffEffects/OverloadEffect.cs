using UnityEngine;
using TribeSystem;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 灵能过载（gameEffectType=410）
    /// 满充能时，下一次法术双倍伤害，然后清零
    /// 实现：OnTick 检查龙语充能层数，满层时标记 buff
    /// </summary>
    public class OverloadEffect : IBuffEffect
    {
        public int EffectId => 410;

        private bool _isReady;
        private int _lastStacks;

        public void OnTick(BuffEffectContext ctx, float deltaTime)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;

            // 检查龙语充能层数
            int stacks = GetDragonChargeStacks(ctx.Owner);
            int maxStacks = GetDragonChargeMaxStacks(ctx.Owner);

            if (stacks >= maxStacks && maxStacks > 0 && !_isReady)
            {
                _isReady = true;
                Debug.Log($"[OverloadEffect] 灵能过载就绪：满充能 {stacks}/{maxStacks}");
            }

            _lastStacks = stacks;
        }

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (!_isReady) return;

            // 下一次攻击双倍伤害
            if (ctx.Target != null && ctx.Target.IsAlive && ctx.Target.RuntimeAttributes != null)
            {
                int bonusDmg = ctx.Owner.RuntimeAttributes.Attack;
                ctx.Target.RuntimeAttributes.CurrentHp = Mathf.Max(0,
                    ctx.Target.RuntimeAttributes.CurrentHp - bonusDmg);
                Debug.Log($"[OverloadEffect] 灵能过载触发：双倍伤害 {bonusDmg}");
            }

            // 清零龙语充能
            ClearDragonChargeStacks(ctx.Owner);
            _isReady = false;
        }

        private int GetDragonChargeStacks(IBattleUnit unit)
        {
            if (unit.RuntimeAttributes?.ActiveBuffs == null) return 0;
            for (int i = 0; i < unit.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (unit.RuntimeAttributes.ActiveBuffs[i].buffId == "dragon_charge")
                    return unit.RuntimeAttributes.ActiveBuffs[i].currentStacks;
            }
            return 0;
        }

        private int GetDragonChargeMaxStacks(IBattleUnit unit)
        {
            if (unit.RuntimeAttributes?.ActiveBuffs == null) return 0;
            for (int i = 0; i < unit.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (unit.RuntimeAttributes.ActiveBuffs[i].buffId == "dragon_charge")
                    return unit.RuntimeAttributes.ActiveBuffs[i].maxStacks;
            }
            return 0;
        }

        private void ClearDragonChargeStacks(IBattleUnit unit)
        {
            if (unit.RuntimeAttributes?.ActiveBuffs == null) return;
            for (int i = 0; i < unit.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (unit.RuntimeAttributes.ActiveBuffs[i].buffId == "dragon_charge")
                {
                    unit.RuntimeAttributes.ActiveBuffs[i].currentStacks = 0;
                    break;
                }
            }
        }
    }
}
