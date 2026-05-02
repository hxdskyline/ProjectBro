using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 虚空共鸣（gameEffectType=408）
    /// 相位转移对敌人使用时，结束时造成30法伤
    /// 实现：被动标记，相位转移技能执行时检查此 buff
    /// </summary>
    public class VoidResonanceEffect : IBuffEffect
    {
        public int EffectId => 408;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
