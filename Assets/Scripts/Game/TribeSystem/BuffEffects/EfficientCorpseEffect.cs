namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 高效分尸（gameEffectType=304）
    /// 分尸效果提升至"算作3具"
    /// 实现：被动标记，CorpseManager.ConsumeCorpse 检查此 buff 后额外消耗 2 具
    /// </summary>
    public class EfficientCorpseEffect : IBuffEffect
    {
        public int EffectId => 304;

        public void OnBattleStart(BuffEffectContext ctx)
        {
            ctx.Buff.value = 1f;
        }
    }
}
