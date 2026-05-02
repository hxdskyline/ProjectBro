using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 龙语精通（gameEffectType=407）
    /// 充能上限+5，每层效果+3%
    /// 实现：修改龙语充能 buff 的 maxStacks 和 effectParam1
    /// </summary>
    public class DragonMasteryEffect : IBuffEffect
    {
        public int EffectId => 407;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;

            // 增强龙语充能 buff
            if (ctx.Owner.RuntimeAttributes.ActiveBuffs != null)
            {
                for (int i = 0; i < ctx.Owner.RuntimeAttributes.ActiveBuffs.Count; i++)
                {
                    var buff = ctx.Owner.RuntimeAttributes.ActiveBuffs[i];
                    if (buff.buffId == "dragon_charge")
                    {
                        buff.maxStacks += 5;
                        buff.effectParam1 += 3f; // 每层 +3%
                        Debug.Log($"[DragonMasteryEffect] 龙语精通：充能上限 → {buff.maxStacks}，每层 +{buff.effectParam1}%");
                        break;
                    }
                }
            }
        }
    }
}
