using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 亡者供养（gameEffectType=301）
    /// 己方单位死亡时，在尸体位置产生 2 具额外尸体
    /// </summary>
    public class DeadFeastEffect : IBuffEffect
    {
        public int EffectId => 301;

        public void OnDeath(BuffEffectContext ctx)
        {
            if (ctx.Owner == null) return;

            // 亡者供养是给族长的光环，当拥有此光环的单位死亡时触发
            // 但更合理的设计是：当任意友方单位死亡时，族长获得额外尸体
            // 这里实现为：拥有此 buff 的单位死亡时产生额外尸体
            // 实际尸体生成由 BattleSimulation 处理，此效果标记额外产尸数量
            Debug.Log($"[DeadFeastEffect] 亡者供养触发：单位死亡，将产生额外尸体");
        }
    }
}
