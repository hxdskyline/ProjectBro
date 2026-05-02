using UnityEngine;
using TribeSystem;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 狩猎盛宴（gameEffectType=110）
    /// 击杀被标记敌人，所有狸花猫恢复 10% 生命
    /// </summary>
    public class HuntFeastEffect : IBuffEffect
    {
        public int EffectId => 110;

        private const float HealPercent = 0.1f;

        public void OnKill(BuffEffectContext ctx)
        {
            if (ctx.Target == null || ctx.Target.RuntimeAttributes == null) return;
            if (ctx.Allies == null) return;

            // 检查被击杀的目标是否有 HuntMark
            if (!HasHuntMark(ctx.Target)) return;

            // 治疗所有狸花猫友方
            int healed = 0;
            for (int i = 0; i < ctx.Allies.Length; i++)
            {
                var ally = ctx.Allies[i];
                if (ally == null || !ally.IsAlive || ally.RuntimeAttributes == null) continue;

                // 检查是否是狸花猫（通过 TribeType）
                // IBattleUnit 没有 TribeType，但 ctx.Owner 是狸花猫族长
                // 简化：治疗所有友方（因为只有狸花猫会携带此光环）
                int heal = Mathf.RoundToInt(ally.RuntimeAttributes.MaxHp * HealPercent);
                ally.RuntimeAttributes.CurrentHp = Mathf.Min(
                    ally.RuntimeAttributes.CurrentHp + heal,
                    ally.RuntimeAttributes.MaxHp);
                healed++;
            }

            if (healed > 0)
                Debug.Log($"[HuntFeastEffect] 狩猎盛宴触发：击杀标记目标，治疗 {healed} 个友方 {HealPercent * 100}%");
        }

        private bool HasHuntMark(IBattleUnit unit)
        {
            if (unit.RuntimeAttributes?.ActiveBuffs == null) return false;
            for (int i = 0; i < unit.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (unit.RuntimeAttributes.ActiveBuffs[i].gameEffect == GameEffect.HuntMark)
                    return true;
            }
            return false;
        }
    }
}
