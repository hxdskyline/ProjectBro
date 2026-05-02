namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 双重吐息（gameEffectType=405）
    /// 龙息术可连续喷两次（第二发伤害减半）
    /// 实现：被动标记，LeaderSkillExecutor.ExecuteDragonBreath 检查后施放两次
    /// </summary>
    public class DoubleBreathEffect : IBuffEffect
    {
        public int EffectId => 405;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
