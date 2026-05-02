namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 风暴之眼（gameEffectType=409）
    /// 烈焰风暴范围+30%，燃烧时间+4秒
    /// 实现：被动标记，LeaderSkillExecutor.ExecuteFlameStorm 检查后修改参数
    /// </summary>
    public class StormEyeEffect : IBuffEffect
    {
        public int EffectId => 409;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
