namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 战神意志（gameEffectType=209）
    /// 战神降临冷却-20秒，持续时间+5秒
    /// 实现：被动标记，战神降临技能执行时检查此 buff 修改参数
    /// </summary>
    public class WarriorWillEffect : IBuffEffect
    {
        public int EffectId => 209;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
