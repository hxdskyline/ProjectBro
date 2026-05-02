using UnityEngine;
using TribeSystem;
using System.Collections.Generic;

/// <summary>
/// 首领技能执行器 — 管理战斗中首领技能的冷却、AI 选择和效果执行
/// </summary>
public class LeaderSkillExecutor
{
    private class SkillState
    {
        public LeaderSkillData data;
        public float cooldownTimer;
        public float passiveTimer;
    }

    private readonly BattleFighter _leader;
    private readonly TribeType _tribeType;
    private readonly List<SkillState> _skills = new List<SkillState>();

    public LeaderSkillExecutor(BattleFighter leader, TribeType tribeType)
    {
        _leader = leader;
        _tribeType = tribeType;
    }

    /// <summary>
    /// 从配置表加载技能
    /// </summary>
    public void LoadSkills(LeaderSkillConfigTable config)
    {
        if (config == null) return;
        var skills = config.GetSkillsForTribe(_tribeType);
        for (int i = 0; i < skills.Count; i++)
        {
            _skills.Add(new SkillState
            {
                data = skills[i],
                cooldownTimer = 0f,
                passiveTimer = 0f
            });
        }
    }

    /// <summary>
    /// 每帧 tick，检查技能冷却和被动触发
    /// </summary>
    public void Tick(float deltaTime, BattleFighter[] allies, BattleFighter[] enemies)
    {
        if (_leader == null || !_leader.IsAlive) return;

        for (int i = 0; i < _skills.Count; i++)
        {
            var state = _skills[i];

            if (state.data.skillType == SkillType.Passive)
            {
                // 被动技能：按间隔检查触发条件
                state.passiveTimer -= deltaTime;
                if (state.passiveTimer <= 0f)
                {
                    state.passiveTimer = state.data.passiveCheckInterval;
                    TryExecutePassive(state, allies, enemies);
                }
            }
            else
            {
                // 主动技能：冷却完毕后自动释放
                if (state.cooldownTimer > 0f)
                {
                    state.cooldownTimer -= deltaTime;
                }
                else
                {
                    if (TryExecuteActive(state, allies, enemies))
                    {
                        state.cooldownTimer = state.data.cooldown;
                    }
                }
            }
        }
    }

    private bool TryExecutePassive(SkillState state, BattleFighter[] allies, BattleFighter[] enemies)
    {
        // 根据技能 ID 执行不同的被动逻辑
        switch (state.data.skillId)
        {
            case 1001: // 牧群领袖：场上友方数量 → 攻击力加成
                return ExecuteHerdLeader(state, allies);
            case 2001: // 饕餮：击杀恢复（由外部死亡事件触发，这里不做检查）
                return false;
            case 3001: // 龙语回响：每次施法触发（由外部施法事件触发）
                return false;
            case 4001: // 狩猎印记：攻击标记目标
                return ExecuteHuntMark(state, enemies);
            default:
                return false;
        }
    }

    /// <summary>
    /// 牧群领袖：场上每有一个友方单位，族长攻击力 +1
    /// </summary>
    private bool ExecuteHerdLeader(SkillState state, BattleFighter[] allies)
    {
        if (allies == null || _leader.RuntimeAttributes == null) return false;

        int aliveCount = 0;
        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i] != null && allies[i].IsAlive && !allies[i].IsDying)
                aliveCount++;
        }

        // 应用攻击力加成（每友方 +1，BattleOnly 持续 2 秒不断刷新）
        var buff = UnifiedBuff.CreateTimedBuff(
            "herd_leader_atk", "牧群领袖",
            BuffSource.Innate, "herd_leader",
            StatType.Attack, false, aliveCount,
            2f, BuffStackRule.None, 1);
        _leader.RuntimeAttributes.ApplyBuff(buff);
        _leader.RuntimeAttributes.AttackFlatBuff += aliveCount;
        _leader.RuntimeAttributes.Recalculate();
        return true;
    }

    /// <summary>
    /// 狩猎印记：攻击时标记目标
    /// </summary>
    private bool ExecuteHuntMark(SkillState state, BattleFighter[] enemies)
    {
        // 由攻击系统处理（ApplyAttackTriggeredEffects），被动只负责提供 buff
        // 在族长自身上添加"攻击附加标记"buff
        var existingBuff = FindBuff(state.data.skillId);
        if (existingBuff == null)
        {
            var buff = new UnifiedBuff
            {
                buffId = $"skill_{state.data.skillId}",
                displayName = state.data.skillName,
                source = BuffSource.Innate,
                sourceId = $"skill_{state.data.skillId}",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.HuntMark,
                effectParam1 = 0.3f,  // 30% 易伤
                effectParam2 = 5f,    // 持续 5 秒
                remainingDuration = -1f,  // 永久本场
                tickInterval = 0f,
                tickTimer = 0f,
            };
            _leader.RuntimeAttributes.ApplyBuff(buff);
        }
        return true;
    }

    private bool TryExecuteActive(SkillState state, BattleFighter[] allies, BattleFighter[] enemies)
    {
        // 根据目标类型选择目标
        switch (state.data.skillId)
        {
            case 1002: // 白骨复生 — 需要尸体（暂时跳过，尸体系统未实现）
            case 1003: // 毒爆
            case 1006: // 转生仪式
                return false; // 等待尸体系统

            case 1004: // 骨牢 — 冻结最近敌人
                return ExecuteFreezeTarget(state, enemies, 3f);

            case 1005: // 骨刺 — 对所有敌人造成伤害+流血
                return ExecuteBoneSpike(state, enemies);

            case 2002: // 第二顿早餐 — 治疗所有友方
                return ExecuteHealAllies(state, allies, 0.5f);

            case 2003: // 酒雾 — 减速所有敌人
                return ExecuteSlowArea(state, enemies, 0.3f, 6f, 4f);

            case 2004: // 熔炉锻造 — 友方防御加成
                return ExecuteDefenseBuff(state, allies, 2, -1f);

            case 2005: // 致命投掷 — 对最近敌人造成大量伤害
                return ExecuteThrowUnit(state, enemies, 50f);

            case 2006: // 天神下凡 — 攻击力翻倍 + 减伤
                return ExecuteAvatar(state);

            case 3002: // 龙息术 — 区域火焰伤害 + 燃烧
                return ExecuteDragonBreath(state, enemies);

            case 3003: // 相位转移
                return false; // 需要友方选择逻辑

            case 3004: // 能量护盾
                return ExecuteShield(state);

            case 3005: // 烈焰风暴 — 大范围火焰伤害 + 燃烧
                return ExecuteFlameStorm(state, enemies);

            case 4002: // 穿云箭 — 对最近敌人造成伤害
                return ExecutePiercingShot(state, enemies);

            case 4003: // 淬毒利刃 — 对最近敌人造成伤害+毒
                return ExecutePoisonBlade(state, enemies);

            case 4004: // 捕兽夹 — 冻结+伤害+毒
                return ExecuteBearTrap(state, enemies);

            case 4005: // 隐匿 — 自身隐身+攻击加成
                return ExecuteStealth(state);

            default:
                return false;
        }
    }

    // ── 技能执行方法 ──

    private bool ExecuteFreezeTarget(SkillState state, BattleFighter[] enemies, float duration)
    {
        var target = FindNearestAliveEnemy(enemies);
        if (target == null || target.RuntimeAttributes == null) return false;

        target.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFreeze(duration));
        target.FreezeTimer = Mathf.Max(target.FreezeTimer, duration);
        Debug.Log($"[Skill] {_leader.Name} → 骨牢 → {target.Name}，冻结 {duration}s");
        return true;
    }

    private bool ExecuteBoneSpike(SkillState state, BattleFighter[] enemies)
    {
        if (enemies == null) return false;
        int hitCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive || e.RuntimeAttributes == null) continue;

            // 20 物理伤害
            int rawDmg = Mathf.Max(0, _leader.RuntimeAttributes.Attack - e.RuntimeAttributes.Defense);
            float dr = Mathf.Max(0.2f, 1f - (float)e.RuntimeAttributes.Defense / (e.RuntimeAttributes.Defense + 100f));
            int damage = Mathf.Max(1, Mathf.RoundToInt(rawDmg * dr * 0.5f)); // 技能倍率 0.5
            e.RuntimeAttributes.CurrentHp = Mathf.Max(0, e.RuntimeAttributes.CurrentHp - damage);

            // 附加流血
            e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBleed(5f, 4f));
            hitCount++;

            if (e.RuntimeAttributes.CurrentHp <= 0)
                e.IsDying = true;
        }
        Debug.Log($"[Skill] {_leader.Name} → 骨刺，命中 {hitCount} 个目标");
        return hitCount > 0;
    }

    private bool ExecuteHealAllies(SkillState state, BattleFighter[] allies, float healPercent)
    {
        if (allies == null) return false;
        int healed = 0;
        for (int i = 0; i < allies.Length; i++)
        {
            var a = allies[i];
            if (a == null || !a.IsAlive || a.RuntimeAttributes == null) continue;
            if (a.TribeType != TribeType.Orange) continue; // 只治疗橘猫

            int heal = Mathf.RoundToInt(a.RuntimeAttributes.MaxHp * healPercent);
            a.RuntimeAttributes.CurrentHp = Mathf.Min(a.RuntimeAttributes.CurrentHp + heal, a.RuntimeAttributes.MaxHp);
            healed++;
        }
        Debug.Log($"[Skill] {_leader.Name} → 第二顿早餐，治疗 {healed} 只橘猫");
        return healed > 0;
    }

    private bool ExecuteSlowArea(SkillState state, BattleFighter[] enemies, float slowPercent, float duration, float radius)
    {
        if (enemies == null || _leader.Transform == null) return false;
        int hitCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive || e.Transform == null || e.RuntimeAttributes == null) continue;

            float dist = Vector3.Distance(_leader.Transform.position, e.Transform.position);
            if (dist <= radius)
            {
                e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateSlow(slowPercent, duration));
                hitCount++;
            }
        }
        Debug.Log($"[Skill] {_leader.Name} → 酒雾，减速 {hitCount} 个目标");
        return hitCount > 0;
    }

    private bool ExecuteDefenseBuff(SkillState state, BattleFighter[] allies, int defBonus, float duration)
    {
        if (allies == null) return false;
        for (int i = 0; i < allies.Length; i++)
        {
            var a = allies[i];
            if (a == null || !a.IsAlive || a.RuntimeAttributes == null) continue;
            if (a.TribeType != TribeType.Orange) continue;

            var buff = UnifiedBuff.CreateTimedBuff(
                "forge_armor", "精钢",
                BuffSource.Innate, "forge_anvil",
                StatType.Defense, false, defBonus,
                duration, BuffStackRule.Stack, 5);
            a.RuntimeAttributes.ApplyBuff(buff);
            a.RuntimeAttributes.DefenseFlatBuff += defBonus;
            a.RuntimeAttributes.Recalculate();
        }
        Debug.Log($"[Skill] {_leader.Name} → 熔炉锻造，+{defBonus} 护甲");
        return true;
    }

    private bool ExecuteThrowUnit(SkillState state, BattleFighter[] enemies, float damage)
    {
        var target = FindNearestAliveEnemy(enemies);
        if (target == null || target.RuntimeAttributes == null) return false;

        int dmg = Mathf.RoundToInt(damage);
        target.RuntimeAttributes.CurrentHp = Mathf.Max(0, target.RuntimeAttributes.CurrentHp - dmg);
        Debug.Log($"[Skill] {_leader.Name} → 致命投掷 → {target.Name}，{dmg} 伤害");

        if (target.RuntimeAttributes.CurrentHp <= 0)
            target.IsDying = true;
        return true;
    }

    private bool ExecuteAvatar(SkillState state)
    {
        if (_leader.RuntimeAttributes == null) return false;

        // 攻击力翻倍（+100%）
        var atkBuff = UnifiedBuff.CreateTimedBuff(
            "avatar_atk", "天神下凡",
            BuffSource.Innate, "avatar",
            StatType.Attack, true, 1.0f,
            20f, BuffStackRule.None, 1);
        _leader.RuntimeAttributes.ApplyBuff(atkBuff);
        _leader.RuntimeAttributes.AttackPercentBuff += 1.0f;

        // 减伤 50%
        _leader.RuntimeAttributes.DefensePercentBuff += 0.5f;
        _leader.RuntimeAttributes.Recalculate();
        Debug.Log($"[Skill] {_leader.Name} → 天神下凡！攻击翻倍，减伤 50%，持续 20s");
        return true;
    }

    private bool ExecuteDragonBreath(SkillState state, BattleFighter[] enemies)
    {
        if (enemies == null || _leader.Transform == null) return false;
        int hitCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive || e.Transform == null || e.RuntimeAttributes == null) continue;

            float dist = Vector3.Distance(_leader.Transform.position, e.Transform.position);
            if (dist <= 3f)
            {
                int damage = Mathf.RoundToInt(25 * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
                e.RuntimeAttributes.CurrentHp = Mathf.Max(0, e.RuntimeAttributes.CurrentHp - damage);
                e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(5f, 4f));
                hitCount++;

                if (e.RuntimeAttributes.CurrentHp <= 0)
                    e.IsDying = true;
            }
        }
        Debug.Log($"[Skill] {_leader.Name} → 龙息术，命中 {hitCount} 个目标");
        return hitCount > 0;
    }

    private bool ExecuteShield(SkillState state)
    {
        if (_leader.RuntimeAttributes == null) return false;
        // 简化实现：减伤 50% 持续 6 秒
        var buff = UnifiedBuff.CreateTimedBuff(
            "energy_shield", "能量护盾",
            BuffSource.Innate, "energy_shield",
            StatType.Defense, true, 0.5f,
            6f, BuffStackRule.None, 1);
        _leader.RuntimeAttributes.ApplyBuff(buff);
        _leader.RuntimeAttributes.DefensePercentBuff += 0.5f;
        _leader.RuntimeAttributes.Recalculate();
        Debug.Log($"[Skill] {_leader.Name} → 能量护盾，减伤 50% 持续 6s");
        return true;
    }

    private bool ExecuteFlameStorm(SkillState state, BattleFighter[] enemies)
    {
        if (enemies == null || _leader.Transform == null) return false;
        int hitCount = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive || e.Transform == null || e.RuntimeAttributes == null) continue;

            float dist = Vector3.Distance(_leader.Transform.position, e.Transform.position);
            if (dist <= 6f)
            {
                int damage = Mathf.RoundToInt(60 * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
                e.RuntimeAttributes.CurrentHp = Mathf.Max(0, e.RuntimeAttributes.CurrentHp - damage);
                e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(8f, 8f));
                hitCount++;

                if (e.RuntimeAttributes.CurrentHp <= 0)
                    e.IsDying = true;
            }
        }
        Debug.Log($"[Skill] {_leader.Name} → 烈焰风暴，命中 {hitCount} 个目标");
        return hitCount > 0;
    }

    private bool ExecutePiercingShot(SkillState state, BattleFighter[] enemies)
    {
        var target = FindNearestAliveEnemy(enemies);
        if (target == null || target.RuntimeAttributes == null) return false;

        float mult = target.RuntimeAttributes.ActiveBuffs != null ? 1f : 1f;
        // 检查是否有狩猎标记
        if (target.RuntimeAttributes.ActiveBuffs != null)
        {
            for (int i = 0; i < target.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (target.RuntimeAttributes.ActiveBuffs[i].gameEffect == GameEffect.HuntMark)
                {
                    mult = 2f; // 标记目标双倍伤害
                    break;
                }
            }
        }

        int damage = Mathf.RoundToInt(50 * mult * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
        target.RuntimeAttributes.CurrentHp = Mathf.Max(0, target.RuntimeAttributes.CurrentHp - damage);
        Debug.Log($"[Skill] {_leader.Name} → 穿云箭 → {target.Name}，{damage} 伤害");

        if (target.RuntimeAttributes.CurrentHp <= 0)
            target.IsDying = true;
        return true;
    }

    private bool ExecutePoisonBlade(SkillState state, BattleFighter[] enemies)
    {
        var target = FindNearestAliveEnemy(enemies);
        if (target == null || target.RuntimeAttributes == null) return false;

        int damage = Mathf.RoundToInt(10 * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
        target.RuntimeAttributes.CurrentHp = Mathf.Max(0, target.RuntimeAttributes.CurrentHp - damage);
        // 3 层毒，每层每秒 3 点，持续 6 秒
        for (int i = 0; i < 3; i++)
            target.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreatePoison(3f, 6f));

        Debug.Log($"[Skill] {_leader.Name} → 淬毒利刃 → {target.Name}，{damage} 伤害 + 3 层毒");
        if (target.RuntimeAttributes.CurrentHp <= 0)
            target.IsDying = true;
        return true;
    }

    private bool ExecuteBearTrap(SkillState state, BattleFighter[] enemies)
    {
        var target = FindNearestAliveEnemy(enemies);
        if (target == null || target.RuntimeAttributes == null) return false;

        // 定身 3 秒
        target.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFreeze(3f));
        target.FreezeTimer = Mathf.Max(target.FreezeTimer, 3f);

        // 15 物理伤害
        int damage = Mathf.RoundToInt(15 * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
        target.RuntimeAttributes.CurrentHp = Mathf.Max(0, target.RuntimeAttributes.CurrentHp - damage);

        // 2 层毒
        for (int i = 0; i < 2; i++)
            target.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreatePoison(3f, 6f));

        Debug.Log($"[Skill] {_leader.Name} → 捕兽夹 → {target.Name}，定身 3s + {damage} 伤害 + 2 层毒");
        if (target.RuntimeAttributes.CurrentHp <= 0)
            target.IsDying = true;
        return true;
    }

    private bool ExecuteStealth(SkillState state)
    {
        // 隐匿 3 秒 + 攻击 +50%
        var atkBuff = UnifiedBuff.CreateTimedBuff(
            "stealth_atk", "隐匿",
            BuffSource.Innate, "stealth",
            StatType.Attack, true, 0.5f,
            3f, BuffStackRule.None, 1);
        _leader.RuntimeAttributes.ApplyBuff(atkBuff);
        _leader.RuntimeAttributes.AttackPercentBuff += 0.5f;
        _leader.RuntimeAttributes.Recalculate();
        Debug.Log($"[Skill] {_leader.Name} → 隐匿，攻击 +50% 持续 3s");
        return true;
    }

    // ── 工具方法 ──

    private BattleFighter FindNearestAliveEnemy(BattleFighter[] enemies)
    {
        if (enemies == null || _leader.Transform == null) return null;

        BattleFighter nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive || e.Transform == null) continue;
            float dist = Vector3.Distance(_leader.Transform.position, e.Transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = e;
            }
        }
        return nearest;
    }

    private UnifiedBuff FindBuff(int skillId)
    {
        if (_leader.RuntimeAttributes == null || _leader.RuntimeAttributes.ActiveBuffs == null) return null;
        string buffId = $"skill_{skillId}";
        for (int i = 0; i < _leader.RuntimeAttributes.ActiveBuffs.Count; i++)
        {
            if (_leader.RuntimeAttributes.ActiveBuffs[i].buffId == buffId)
                return _leader.RuntimeAttributes.ActiveBuffs[i];
        }
        return null;
    }
}
