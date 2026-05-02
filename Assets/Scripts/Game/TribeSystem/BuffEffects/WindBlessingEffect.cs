using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 风之祝福（gameEffectType=108）
    /// 隐匿持续时间 +2 秒，伤害加成提升至 100%
    /// 实现：被动光环，修改隐匿技能参数
    /// LeaderSkillExecutor.ExecuteStealth 会检查此 buff
    /// </summary>
    public class WindBlessingEffect : IBuffEffect
    {
        public int EffectId => 108;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
            Debug.Log($"[WindBlessingEffect] 风之祝福激活：隐匿 +2s，伤害加成 100%");
        }
    }
}
