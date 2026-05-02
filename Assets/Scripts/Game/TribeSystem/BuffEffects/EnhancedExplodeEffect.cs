namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 强化自爆（gameEffectType=205）
    /// 自爆伤害+50%，范围+30%
    /// 实现：被动标记，自爆系统检查此 buff 修改参数
    /// </summary>
    public class EnhancedExplodeEffect : IBuffEffect
    {
        public int EffectId => 205;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
