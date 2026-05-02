using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 饕餮（gameEffectType=300）
    /// 击杀单位恢复 10% 最大生命
    /// </summary>
    public class GluttonyEffect : IBuffEffect
    {
        public int EffectId => 300;

        public void OnKill(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            var attrs = ctx.Owner.RuntimeAttributes;
            float healPercent = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : 0.1f;
            int heal = Mathf.RoundToInt(attrs.MaxHp * healPercent);
            attrs.CurrentHp = Mathf.Min(attrs.CurrentHp + heal, attrs.MaxHp);
            Debug.Log($"[GluttonyEffect] 饕餮触发：击杀恢复 {heal} 生命");
        }
    }
}
