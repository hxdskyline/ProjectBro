namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 双斧（gameEffectType=200）
    /// 攻击速度+30%
    /// 纯属性 buff，由 aura 系统的 stat buff 部分处理，无需额外逻辑
    /// </summary>
    public class DoubleAxeEffect : IBuffEffect
    {
        public int EffectId => 200;
        // 攻速 +30% 已通过 aura 的 statType=AttackSpeed, isPercent=true, value=0.3 应用
    }
}
