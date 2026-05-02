using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 丛林之友（gameEffectType=109）
    /// 狩猎大师召唤的猎豹数量 +1
    /// 实现：被动光环，修改召唤参数
    /// LeaderSkillExecutor 在执行召唤时检查此 buff
    /// </summary>
    public class JungleFriendEffect : IBuffEffect
    {
        public int EffectId => 109;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
            Debug.Log($"[JungleFriendEffect] 丛林之友激活：召唤猎豹 +1");
        }
    }
}
