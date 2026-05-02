namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 元素亲和（gameEffectType=403）
    /// 每次施法回复2点法力
    /// 实现：被动标记，技能执行系统检查此 buff 后回复"法力"（简化为攻速加成）
    /// </summary>
    public class ElementalAffinityEffect : IBuffEffect
    {
        public int EffectId => 403;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            // 简化：每次施法等效为 +10% 攻速（模拟法力回复带来的收益）
            ctx.Owner.RuntimeAttributes.AttackSpeedPercentBuff += 0.1f;
            ctx.Owner.RuntimeAttributes.Recalculate();
            ctx.Buff.value = 1f;
        }
    }
}
