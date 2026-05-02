namespace TribeSystem
{
    /// <summary>
    /// 战斗单位接口 — 供 TribeSystem.BuffEffects 使用，避免直接依赖 BattleFighter
    /// </summary>
    public interface IBattleUnit
    {
        UnitRuntimeAttributes RuntimeAttributes { get; }
        bool IsAlive { get; }
        float FreezeTimer { get; set; }
    }
}
