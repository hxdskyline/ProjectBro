using System.Collections.Generic;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// Buff 效果注册表 — 管理所有 gameEffectType → IBuffEffect 的映射
    /// </summary>
    public static class BuffEffectRegistry
    {
        private static Dictionary<int, IBuffEffect> _effects;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _effects = new Dictionary<int, IBuffEffect>();

            // 注册所有效果
            Register(new ChargeEffect());           // 203: 冲锋
            Register(new PoisonArrowEffect());      // 101: 毒箭
            Register(new PierceArrowEffect());      // 100: 穿刺箭
            Register(new GluttonyEffect());         // 300: 饕餮
            Register(new PackLeaderEffect());       // 301: 牧群领袖
            Register(new HuntMarkInnateEffect());   // 302: 狩猎印记
            Register(new DragonEchoEffect());       // 303: 龙语回响

            _initialized = true;
        }

        private static void Register(IBuffEffect effect)
        {
            _effects[effect.EffectId] = effect;
        }

        public static IBuffEffect Get(int effectId)
        {
            if (_effects == null) Initialize();
            _effects.TryGetValue(effectId, out var effect);
            return effect;
        }

        public static bool Has(int effectId)
        {
            if (_effects == null) Initialize();
            return _effects.ContainsKey(effectId);
        }
    }
}
