using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 陷阱大师（gameEffectType=106）
    /// 捕兽夹可同时存在 2 个，定身时间 +2 秒
    /// 实现：被动光环，修改捕兽夹技能参数（通过 buff.value 存储额外陷阱数量）
    /// LeaderSkillExecutor 在执行捕兽夹时检查此 buff
    /// </summary>
    public class TrapMasterEffect : IBuffEffect
    {
        public int EffectId => 106;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            // 设置 buff.value = 1 表示陷阱大师已激活
            // LeaderSkillExecutor.ExecuteBearTrap 会检查此值
            ctx.Buff.value = 1f;
            Debug.Log($"[TrapMasterEffect] 陷阱大师激活：捕兽夹可同时存在 2 个，定身 +2s");
        }
    }
}
