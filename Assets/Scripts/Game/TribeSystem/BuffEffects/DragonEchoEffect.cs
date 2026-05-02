using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 龙语回响（gameEffectType=303）
    /// 攻击时对随机敌人喷射小型龙息（10 火伤，内置冷却 1 秒）
    /// </summary>
    public class DragonEchoEffect : IBuffEffect
    {
        public int EffectId => 303;

        private const float Cooldown = 1f;
        private float _cooldownTimer;

        public void OnTick(BuffEffectContext ctx, float deltaTime)
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= deltaTime;
        }

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (_cooldownTimer > 0f) return;
            if (ctx.Owner == null || ctx.Enemies == null) return;

            // 找随机存活敌人
            var candidates = new System.Collections.Generic.List<global::BattleFighter>();
            for (int i = 0; i < ctx.Enemies.Length; i++)
            {
                if (ctx.Enemies[i] != null && ctx.Enemies[i].IsAlive)
                    candidates.Add(ctx.Enemies[i]);
            }
            if (candidates.Count == 0) return;

            var target = candidates[Random.Range(0, candidates.Count)];
            if (target.RuntimeAttributes == null) return;

            float damage = ctx.Buff.effectParam1 > 0 ? ctx.Buff.effectParam1 : 10f;
            target.RuntimeAttributes.CurrentHp = Mathf.Max(0,
                target.RuntimeAttributes.CurrentHp - Mathf.RoundToInt(damage));

            _cooldownTimer = Cooldown;
            Debug.Log($"[DragonEchoEffect] 龙语回响触发：龙息 {Mathf.RoundToInt(damage)} 火伤");
        }
    }
}
