namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 转生精通（gameEffectType=308）
    /// 转生仪式复活的单位属性提升至75%
    /// 实现：被动标记，LeaderSkillExecutor.ExecuteResurrectCorpse 检查后修改复活血量
    /// </summary>
    public class ResurrectMasteryEffect : IBuffEffect
    {
        public int EffectId => 308;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
