namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// Buff 特殊效果接口
    /// 所有 gameEffectType 对应的效果都实现此接口
    /// </summary>
    public interface IBuffEffect
    {
        int EffectId { get; }

        void OnBattleStart(BuffEffectContext ctx) { }
        void OnTick(BuffEffectContext ctx, float deltaTime) { }
        void OnAttackHit(BuffEffectContext ctx) { }
        void OnDefendHit(BuffEffectContext ctx) { }
        void OnKill(BuffEffectContext ctx) { }
        void OnDeath(BuffEffectContext ctx) { }
        void OnExpire(BuffEffectContext ctx) { }
    }

    /// <summary>
    /// 效果执行上下文
    /// </summary>
    public class BuffEffectContext
    {
        public BuffEffectContext(
            IBattleUnit owner,
            IBattleUnit target,
            UnifiedBuff buff,
            IBattleUnit[] allies,
            IBattleUnit[] enemies)
        {
            Owner = owner;
            Target = target;
            Buff = buff;
            Allies = allies;
            Enemies = enemies;
        }

        public IBattleUnit Owner { get; }
        public IBattleUnit Target { get; }
        public UnifiedBuff Buff { get; }
        public IBattleUnit[] Allies { get; }
        public IBattleUnit[] Enemies { get; }
    }
}
