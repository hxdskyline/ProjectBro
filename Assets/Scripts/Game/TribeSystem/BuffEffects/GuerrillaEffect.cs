using UnityEngine;

namespace TribeSystem.BuffEffects
{
    /// <summary>
    /// 游击（gameEffectType=103）
    /// 每攻击 3 次，自动位移一小段并闪避下次攻击
    /// </summary>
    public class GuerrillaEffect : IBuffEffect
    {
        public int EffectId => 103;

        private int _attackCount;
        private const int AttacksRequired = 3;

        public void OnAttackHit(BuffEffectContext ctx)
        {
            if (ctx.Owner == null || ctx.Owner.RuntimeAttributes == null) return;

            _attackCount++;

            if (_attackCount >= AttacksRequired)
            {
                _attackCount = 0;

                // 闪避下次攻击：给自己加一个短暂的减伤 buff（模拟闪避）
                var dodgeBuff = UnifiedBuff.CreateTimedBuff(
                    "guerrilla_dodge", "游击闪避",
                    BuffSource.Innate, "guerrilla",
                    StatType.Defense, true, 1.0f,
                    1f, BuffStackRule.None, 1);
                ctx.Owner.RuntimeAttributes.ApplyBuff(dodgeBuff);
                ctx.Owner.RuntimeAttributes.DefensePercentBuff += 1.0f;
                ctx.Owner.RuntimeAttributes.Recalculate();

                // 位移：向随机方向移动一小段距离（通过修改 MoveSpeedBuff 模拟瞬移效果）
                // 实际位移由 BattleSimulation 在下一次移动时处理
                Debug.Log($"[GuerrillaEffect] 游击触发：第 {AttacksRequired} 次攻击后闪避+位移");
            }
        }
    }
}
