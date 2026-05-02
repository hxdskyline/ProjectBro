namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// Buff 特殊效果接口
    /// 所有 gameEffectType 对应的效果都实现此接口
    /// </summary>
    public interface IBuffEffect
    {
        /// <summary>
        /// 效果 ID（对应 gameEffectType）
        /// </summary>
        int EffectId { get; }

        /// <summary>
        /// 战斗开始时触发（一次性初始化）
        /// </summary>
        void OnBattleStart(BuffEffectContext ctx) { }

        /// <summary>
        /// 每帧 tick（限时效果用）
        /// </summary>
        void OnTick(BuffEffectContext ctx, float deltaTime) { }

        /// <summary>
        /// 攻击命中时触发
        /// </summary>
        void OnAttackHit(BuffEffectContext ctx) { }

        /// <summary>
        /// 攻击被命中时触发（防御方）
        /// </summary>
        void OnDefendHit(BuffEffectContext ctx) { }

        /// <summary>
        /// 击杀敌人时触发
        /// </summary>
        void OnKill(BuffEffectContext ctx) { }

        /// <summary>
        /// 自身死亡时触发
        /// </summary>
        void OnDeath(BuffEffectContext ctx) { }

        /// <summary>
        /// 效果过期时清理
        /// </summary>
        void OnExpire(BuffEffectContext ctx) { }
    }

    /// <summary>
    /// 效果执行上下文
    /// </summary>
    public class BuffEffectContext
    {
        public BuffEffectContext(
            global::BattleFighter owner,
            global::BattleFighter target,
            UnifiedBuff buff,
            global::BattleFighter[] allies,
            global::BattleFighter[] enemies)
        {
            Owner = owner;
            Target = target;
            Buff = buff;
            Allies = allies;
            Enemies = enemies;
        }

        public global::BattleFighter Owner { get; }
        public global::BattleFighter Target { get; }
        public UnifiedBuff Buff { get; }
        public global::BattleFighter[] Allies { get; }
        public global::BattleFighter[] Enemies { get; }
    }
}
