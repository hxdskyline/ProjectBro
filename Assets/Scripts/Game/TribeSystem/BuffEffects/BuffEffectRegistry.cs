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

            // ── 狸花猫光环 ──
            Register(new PierceArrowEffect());      // 100: 穿刺箭
            Register(new PoisonArrowEffect());      // 101: 毒箭
            Register(new PrecisionEffect());        // 102: 精准
            Register(new GuerrillaEffect());        // 103: 游击
            Register(new DeadlyPoisonEffect());     // 104: 致命毒药
            Register(new AssassinationEffect());    // 105: 暗杀艺术
            Register(new TrapMasterEffect());       // 106: 陷阱大师
            Register(new DeadlyMarkEffect());       // 107: 致命印记
            Register(new WindBlessingEffect());     // 108: 风之祝福
            Register(new JungleFriendEffect());     // 109: 丛林之友
            Register(new HuntFeastEffect());        // 110: 狩猎盛宴

            // ── 橘猫光环 ──
            Register(new DoubleAxeEffect());        // 200: 双斧
            Register(new ChickenLegEffect());       // 201: 鸡腿
            Register(new BerserkerEffect());        // 202: 狂战士
            Register(new ChargeEffect());           // 203: 冲锋
            Register(new SwarmEffect());            // 204: 集群
            Register(new EnhancedExplodeEffect());  // 205: 强化自爆
            Register(new PoisonFlyEffect());        // 206: 剧毒蝇
            Register(new BigAppetiteEffect());      // 207: 胃口大开
            Register(new FineBrewEffect());         // 208: 精酿
            Register(new WarriorWillEffect());      // 209: 战神意志
            Register(new BurningFightEffect());     // 210: 燃烧斗志

            // ── 奶牛猫光环 ──
            Register(new PoisonClawEffect());       // 300: 淬毒之爪
            Register(new DeadFeastEffect());        // 301: 亡者供养
            Register(new SoulLinkEffect());         // 302: 灵魂连接
            Register(new MeatMillEffect());         // 303: 血肉磨坊
            Register(new EfficientCorpseEffect());  // 304: 高效分尸
            Register(new CorpseExplodeEffect());    // 305: 尸爆
            Register(new UndeadCommanderEffect());  // 306: 亡灵统帅
            Register(new BoneWallEnhanceEffect());  // 307: 骨墙强化
            Register(new ResurrectMasteryEffect()); // 308: 转生精通
            Register(new PlaguebearerEffect());     // 309: 瘟疫使者

            // ── 无毛猫光环 ──
            Register(new SpellBurstEffect());       // 400: 法术迸发
            Register(new IceTentacleEffect());      // 401: 寒冰触须
            Register(new ManaShieldEffect());       // 402: 法力护盾
            Register(new ElementalAffinityEffect()); // 403: 元素亲和
            Register(new DragonBloodEffect());      // 404: 龙族血脉
            Register(new DoubleBreathEffect());     // 405: 双重吐息
            Register(new FrostNovaEffect());        // 406: 冰霜新星
            Register(new DragonMasteryEffect());    // 407: 龙语精通
            Register(new VoidResonanceEffect());    // 408: 虚空共鸣
            Register(new StormEyeEffect());         // 409: 风暴之眼
            Register(new OverloadEffect());         // 410: 灵能过载

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
