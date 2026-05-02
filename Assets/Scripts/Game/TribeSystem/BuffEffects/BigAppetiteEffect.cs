namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 胃口大开（gameEffectType=207）
    /// 每次进食额外获得1层饱食
    /// 实现：被动标记，进食系统（第二顿早餐等）检查此 buff 额外赋予饱食层
    /// </summary>
    public class BigAppetiteEffect : IBuffEffect
    {
        public int EffectId => 207;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
