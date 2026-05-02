namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 冰霜新星（gameEffectType=406）
    /// 龙息术改为冰霜，伤害不变但附加冻结1秒
    /// 实现：被动标记，LeaderSkillExecutor.ExecuteDragonBreath 检查后改变伤害类型
    /// </summary>
    public class FrostNovaEffect : IBuffEffect
    {
        public int EffectId => 406;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
