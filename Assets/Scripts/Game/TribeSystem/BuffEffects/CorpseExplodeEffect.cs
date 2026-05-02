using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 尸爆（gameEffectType=305）
    /// 绞肉机死亡时，引爆所有储存的尸体，每具对周围造成10点法术伤害
    /// 实现：OnDeath 时对附近敌人造成基于尸体数量的伤害
    /// </summary>
    public class CorpseExplodeEffect : IBuffEffect
    {
        public int EffectId => 305;

        private const float DamagePerCorpse = 10f;
        private const float ExplodeRadius = 5f;

        public void OnDeath(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;
            if (ctx.Enemies == null) return;

            // 尸爆：对附近所有敌人造成伤害
            // 简化：固定造成 10 * 存储尸体数 的伤害
            // 实际应从 CorpseManager 获取尸体数量，这里用 buff.value 存储
            int corpseCount = Mathf.Max(1, Mathf.RoundToInt(ctx.Buff.value));
            int totalDamage = Mathf.RoundToInt(DamagePerCorpse * corpseCount);

            int hitCount = 0;
            for (int i = 0; i < ctx.Enemies.Length; i++)
            {
                var enemy = ctx.Enemies[i];
                if (enemy == null || !enemy.IsAlive || enemy.RuntimeAttributes == null) continue;

                // 简化：不做距离检查，对所有敌人造成伤害
                enemy.RuntimeAttributes.CurrentHp = Mathf.Max(0,
                    enemy.RuntimeAttributes.CurrentHp - totalDamage);
                hitCount++;

                if (enemy.RuntimeAttributes.CurrentHp <= 0)
                    enemy.FreezeTimer = 0; // 标记死亡由 BattleSimulation 处理
            }

            if (hitCount > 0)
                Debug.Log($"[CorpseExplodeEffect] 尸爆触发：{corpseCount} 具尸体爆炸，对 {hitCount} 个目标造成 {totalDamage} 伤害");
        }
    }
}
