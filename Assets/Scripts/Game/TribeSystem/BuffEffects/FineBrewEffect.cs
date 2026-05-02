namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 精酿（gameEffectType=208）
    /// 酒雾的易伤效果提升至+75%
    /// 实现：被动标记，酒雾技能执行时检查此 buff 修改参数
    /// </summary>
    public class FineBrewEffect : IBuffEffect
    {
        public int EffectId => 208;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
