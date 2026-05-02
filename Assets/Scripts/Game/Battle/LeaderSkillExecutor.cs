using UnityEngine;
using TribeSystem;
using System.Collections.Generic;
using BattleSystem.Fighter;
using BattleSystem.Effects;

namespace BattleSystem
{
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
        private CorpseManager _corpseManager;
        private SummonManager _summonManager;

        public LeaderSkillExecutor(BattleFighter leader, TribeType tribeType)
        {
            _leader = leader;
            _tribeType = tribeType;
        }

        /// <summary>
        /// 设置尸体管理和召唤管理器引用
        /// </summary>
        public void SetManagers(CorpseManager corpseManager, SummonManager summonManager)
        {
            _corpseManager = corpseManager;
            _summonManager = summonManager;
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
                            // 战神意志：战神降临（2006）冷却 -20s
                            if (state.data.skillId == 2006 && HasLeaderBuff("warrior_will"))
                                state.cooldownTimer = Mathf.Max(0f, state.cooldownTimer - 20f);
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
                case 2001: // 饕餮：首次施加击杀回血+饱食 buff
                    return ExecuteGluttonyPassive(state);
                case 3001: // 龙语回响：周期性对随机敌人龙息
                    return ExecuteDragonEchoPassive(state, enemies);
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

        /// <summary>
        /// 饕餮（2001）：首次施加击杀回血 buff（OnKill 由 GluttonyEffect 处理）
        /// </summary>
        private bool ExecuteGluttonyPassive(SkillState state)
        {
            // 只施加一次 buff，后续由 IBuffEffect.OnKill 触发
            var existingBuff = FindBuff(state.data.skillId);
            if (existingBuff != null) return true;

            var buff = new UnifiedBuff
            {
                buffId = $"skill_{state.data.skillId}",
                displayName = "饕餮",
                source = BuffSource.Innate,
                sourceId = $"skill_{state.data.skillId}",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.HealAll, // 用 HealAll 作为占位，实际 OnKill 由外部处理
                effectParam1 = 0.1f, // 10% HP
                remainingDuration = -1f, // 永久本场
                tickInterval = 0f,
                tickTimer = 0f,
            };
            _leader.RuntimeAttributes.ApplyBuff(buff);
            Debug.Log($"[Skill] {_leader.Name} → 饕餮 buff 已施加（击杀恢复 10% HP）");
            return true;
        }

        /// <summary>
        /// 龙语回响（3001）：周期性对随机敌人喷射小型龙息
        /// </summary>
        private bool ExecuteDragonEchoPassive(SkillState state, BattleFighter[] enemies)
        {
            if (enemies == null || _leader.Transform == null) return false;

            // 找随机存活敌人
            var candidates = new List<BattleFighter>();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive)
                    candidates.Add(enemies[i]);
            }
            if (candidates.Count == 0) return false;

            var target = candidates[Random.Range(0, candidates.Count)];
            if (target.RuntimeAttributes == null) return false;

            // 10 火伤
            int damage = Mathf.RoundToInt(10 * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
            target.RuntimeAttributes.CurrentHp = Mathf.Max(0, target.RuntimeAttributes.CurrentHp - damage);
            Debug.Log($"[Skill] {_leader.Name} → 龙语回响 → {target.Name}，{damage} 火伤");

            if (target.RuntimeAttributes.CurrentHp <= 0)
                target.IsDying = true;
            return true;
        }

        private bool TryExecuteActive(SkillState state, BattleFighter[] allies, BattleFighter[] enemies)
        {
            // 通用效果分发：遍历 effects 列表，按 effectType + target 执行
            var effects = state.data.effects;
            if (effects == null || effects.Count == 0) return false;

            return ResolveEffectList(state, effects, allies, enemies, _leader);
        }

        /// <summary>
        /// 通用效果列表执行器：遍历 effects，处理条件分支，按 effectType 分发
        /// </summary>
        private bool ResolveEffectList(SkillState state, List<SkillEffectEntry> effects, BattleFighter[] allies, BattleFighter[] enemies, BattleFighter caster)
        {
            bool anyApplied = false;
            for (int i = 0; i < effects.Count; i++)
            {
                var eff = effects[i];

                // 条件分支：检查发送者（caster）是否有指定 buff
                if (!string.IsNullOrEmpty(eff.conditionBuffId))
                {
                    bool hasBuff = caster != null && HasCasterBuff(caster, eff.conditionBuffId);
                    var branch = hasBuff ? eff.conditionEffects : eff.conditionFallbackEffects;
                    if (branch != null && branch.Count > 0)
                        anyApplied |= ResolveEffectList(state, branch, allies, enemies, caster);
                    continue;
                }

                switch (eff.effectType)
                {
                    case SkillEffectType.ApplyBuff:
                        anyApplied |= ExecuteApplyBuff(eff, allies, enemies);
                        break;
                    case SkillEffectType.Heal:
                        anyApplied |= ExecuteHealEffect(eff, allies);
                        break;
                    case SkillEffectType.ConsumeCorpse:
                        anyApplied |= ExecuteConsumeCorpse(eff);
                        break;
                    case SkillEffectType.SummonUnit:
                        anyApplied |= ExecuteSummonUnit(eff);
                        break;
                    case SkillEffectType.ResurrectCorpse:
                        anyApplied |= ExecuteResurrectCorpse(eff);
                        break;
                    case SkillEffectType.Damage:
                        anyApplied |= ExecuteDamageEffect(eff, allies, enemies);
                        break;
                    case SkillEffectType.ApplyPoison:
                        anyApplied |= ExecuteApplyPoisonEffect(eff, allies, enemies);
                        break;
                    case SkillEffectType.ApplyBleed:
                        anyApplied |= ExecuteApplyBleedEffect(eff, allies, enemies);
                        break;
                    case SkillEffectType.ApplyFreeze:
                        anyApplied |= ExecuteApplyFreezeEffect(eff, allies, enemies);
                        break;
                    case SkillEffectType.ApplySlow:
                        anyApplied |= ExecuteApplySlowEffect(eff, allies, enemies);
                        break;
                    case SkillEffectType.ApplyHuntMark:
                        anyApplied |= ExecuteApplyHuntMarkEffect(eff, allies, enemies);
                        break;
                    default:
                        // 仍走 skillId 硬编码（复杂伤害公式、特殊组合等）
                        anyApplied |= ExecuteComplexSkill(state, allies, enemies);
                        return anyApplied;
                }
            }
            return anyApplied;
        }

        /// <summary>
        /// 检查技能发送者（caster）RuntimeAttributes 中是否有指定 buffId 的 buff
        /// </summary>
        private bool HasCasterBuff(BattleFighter caster, string buffId)
        {
            if (caster?.RuntimeAttributes?.ActiveBuffs == null) return false;
            for (int i = 0; i < caster.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (caster.RuntimeAttributes.ActiveBuffs[i].buffId == buffId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 复杂技能的硬编码入口（伤害公式特殊、多效果组合等）
        /// </summary>
        private bool ExecuteComplexSkill(SkillState state, BattleFighter[] allies, BattleFighter[] enemies)
        {
            switch (state.data.skillId)
            {
                case 1004: return ExecuteFreezeTarget(state, enemies, 3f);
                case 1005: return ExecuteBoneSpike(state, enemies);
                case 2003: return ExecuteSlowArea(state, enemies, 0.3f, 6f, 4f);
                case 2004: return ExecuteDefenseBuff(state, allies, 2, -1f);
                case 2005: return ExecuteThrowUnit(state, enemies, 50f);
                case 2006: return ExecuteAvatar(state);
                case 3002: return ExecuteDragonBreath(state, enemies);
                case 3003: return ExecutePhaseShift(state, allies, enemies);
                case 3004: return ExecuteShield(state);
                case 3005: return ExecuteFlameStorm(state, enemies);
                case 4002: return ExecutePiercingShot(state, enemies);
                case 4003: return ExecutePoisonBlade(state, enemies);
                case 4004: return ExecuteBearTrap(state, enemies);
                case 4005: return ExecuteStealth(state);
                default: return false;
            }
        }

        /// <summary>
        /// 通用治疗效果：按 target 类型选择目标，value = 治疗百分比
        /// </summary>
        private bool ExecuteHealEffect(SkillEffectEntry eff, BattleFighter[] allies)
        {
            var targets = ResolveTargets(eff.target, allies, null);
            if (targets == null) return false;
            int healed = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var a = targets[i];
                if (a == null || !a.IsAlive || a.RuntimeAttributes == null) continue;
                int heal = Mathf.RoundToInt(a.RuntimeAttributes.MaxHp * eff.value);
                a.RuntimeAttributes.CurrentHp = Mathf.Min(a.RuntimeAttributes.CurrentHp + heal, a.RuntimeAttributes.MaxHp);
                healed++;
            }
            return healed > 0;
        }

        /// <summary>
        /// 通用 buff 赋予效果：按 target 类型选择目标，buffId 指定 buff 工厂方法
        /// </summary>
        private bool ExecuteApplyBuff(SkillEffectEntry eff, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var targets = ResolveTargets(eff.target, allies, enemies);
            if (targets == null) return false;

            int layers = Mathf.Max(1, Mathf.RoundToInt(eff.value));
            int applied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive || t.RuntimeAttributes == null) continue;

                for (int k = 0; k < layers; k++)
                {
                    var buff = StatusEffectFactory.CreateBuff(eff.buffId);
                    if (buff == null) continue;

                    // 饱食层特殊处理：MaxHp 增长后补血
                    if (buff.buffId == "fullness_stack")
                    {
                        int prevMaxHp = t.RuntimeAttributes.MaxHp;
                        t.RuntimeAttributes.ApplyBuff(buff);
                        var atkBuff = StatusEffectFactory.CreateBuff("fullness_atk_stack");
                        if (atkBuff != null) t.RuntimeAttributes.ApplyBuff(atkBuff);
                        t.RuntimeAttributes.Recalculate();
                        t.RuntimeAttributes.CurrentHp += t.RuntimeAttributes.MaxHp - prevMaxHp;
                    }
                    else
                    {
                        t.RuntimeAttributes.ApplyBuff(buff);
                    }
                }
                applied++;
            }
            return applied > 0;
        }

        /// <summary>
        /// 根据 TargetType 解析目标列表
        /// </summary>
        private List<BattleFighter> ResolveTargets(TargetType targetType, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var result = new List<BattleFighter>();
            switch (targetType)
            {
                case TargetType.Self:
                    if (_leader != null && _leader.IsAlive) result.Add(_leader);
                    break;
                case TargetType.AllAllies:
                    if (allies != null)
                        for (int i = 0; i < allies.Length; i++)
                            if (allies[i] != null && allies[i].IsAlive) result.Add(allies[i]);
                    break;
                case TargetType.AllEnemies:
                    if (enemies != null)
                        for (int i = 0; i < enemies.Length; i++)
                            if (enemies[i] != null && enemies[i].IsAlive) result.Add(enemies[i]);
                    break;
                case TargetType.Area:
                    // 区域目标由具体 effect 处理（需要 radius），此处 fallback 到 AllAllies
                    if (allies != null)
                        for (int i = 0; i < allies.Length; i++)
                            if (allies[i] != null && allies[i].IsAlive) result.Add(allies[i]);
                    break;
            }
            return result;
        }

        // ── 通用效果执行方法 ──

        /// <summary>
        /// 消耗尸体：value = 消耗数量
        /// </summary>
        private bool ExecuteConsumeCorpse(SkillEffectEntry eff)
        {
            if (_corpseManager == null)
            {
                Debug.LogWarning("[Skill] CorpseManager 未设置，无法消耗尸体");
                return false;
            }
            int count = Mathf.Max(1, Mathf.RoundToInt(eff.value));
            // 高效分尸：每次消耗算作 3 具
            if (HasLeaderBuff("efficient_corpse"))
                count *= 3;
            int consumed = 0;
            for (int i = 0; i < count; i++)
            {
                if (_corpseManager.ConsumeCorpse())
                    consumed++;
                else
                    break;
            }
            if (consumed > 0)
                Debug.Log($"[Skill] {_leader.Name} 消耗了 {consumed} 具尸体");
            return consumed > 0;
        }

        /// <summary>
        /// 召唤单位：value = 模板 ID，duration = 持续时间
        /// </summary>
        private bool ExecuteSummonUnit(SkillEffectEntry eff)
        {
            if (_summonManager == null)
            {
                Debug.LogWarning("[Skill] SummonManager 未设置，无法召唤单位");
                return false;
            }

            int templateId = Mathf.RoundToInt(eff.value);
            float lifetime = eff.duration > 0 ? eff.duration : 15f;
            Vector3 spawnPos = _leader.Transform != null ? _leader.Transform.position : Vector3.zero;

            // 根据模板 ID 创建召唤数据
            SummonData data = CreateSummonData(templateId);
            if (data == null)
            {
                Debug.LogWarning($"[Skill] 未知召唤模板 ID: {templateId}");
                return false;
            }
            data.lifetime = lifetime;
            data.isPlayerOwned = true;

            _summonManager.SpawnSummon(data, spawnPos);
            Debug.Log($"[Skill] {_leader.Name} 召唤了 {data.summonName}，持续 {lifetime}s");
            return true;
        }

        /// <summary>
        /// 复活尸体：value = 复活血量百分比
        /// </summary>
        private bool ExecuteResurrectCorpse(SkillEffectEntry eff)
        {
            if (_corpseManager == null)
            {
                Debug.LogWarning("[Skill] CorpseManager 未设置，无法复活尸体");
                return false;
            }

            float healPercent = eff.value > 0 ? eff.value : 0.5f;
            // 转生精通：复活血量提升至 75%
            if (HasLeaderBuff("resurrect_mastery"))
                healPercent = 0.75f;

            // 消耗最新的一具友方尸体
            var corpse = _corpseManager.ConsumeLatestPlayerCorpse();
            if (corpse == null || corpse.fighter == null || corpse.fighter.RuntimeAttributes == null)
            {
                Debug.Log($"[Skill] {_leader.Name} 无法复活：没有友方尸体");
                return false;
            }

            // 复活：恢复血量到指定百分比
            var attrs = corpse.fighter.RuntimeAttributes;
            int healAmount = Mathf.RoundToInt(attrs.MaxHp * healPercent);
            attrs.CurrentHp = Mathf.Max(1, healAmount);

            // 清除死亡状态
            corpse.fighter.IsDying = false;
            corpse.fighter.IsRemoved = false;

            Debug.Log($"[Skill] {_leader.Name} 复活了 {corpse.fighter.Name}，恢复 {healAmount} HP ({healPercent * 100}%)");
            return true;
        }

        /// <summary>
        /// 通用伤害效果：value = 伤害值（受攻防公式影响）
        /// </summary>
        private bool ExecuteDamageEffect(SkillEffectEntry eff, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var targets = ResolveTargets(eff.target, allies, enemies);
            if (targets == null) return false;
            int hitCount = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive || t.RuntimeAttributes == null) continue;

                int rawDmg = Mathf.Max(0, _leader.RuntimeAttributes.Attack - t.RuntimeAttributes.Defense);
                float dr = Mathf.Max(0.2f, 1f - (float)t.RuntimeAttributes.Defense / (t.RuntimeAttributes.Defense + 100f));
                int damage = Mathf.Max(1, Mathf.RoundToInt((rawDmg + eff.value) * dr));
                t.RuntimeAttributes.CurrentHp = Mathf.Max(0, t.RuntimeAttributes.CurrentHp - damage);
                hitCount++;

                if (t.RuntimeAttributes.CurrentHp <= 0)
                    t.IsDying = true;
            }
            return hitCount > 0;
        }

        /// <summary>
        /// 通用中毒效果：value = DPS，duration = 持续秒数
        /// </summary>
        private bool ExecuteApplyPoisonEffect(SkillEffectEntry eff, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var targets = ResolveTargets(eff.target, allies, enemies);
            if (targets == null) return false;
            int applied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive || t.RuntimeAttributes == null) continue;
                t.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreatePoison(eff.value, eff.duration));
                applied++;
            }
            return applied > 0;
        }

        /// <summary>
        /// 通用流血效果：value = DPS，duration = 持续秒数
        /// </summary>
        private bool ExecuteApplyBleedEffect(SkillEffectEntry eff, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var targets = ResolveTargets(eff.target, allies, enemies);
            if (targets == null) return false;
            int applied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive || t.RuntimeAttributes == null) continue;
                t.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBleed(eff.value, eff.duration));
                applied++;
            }
            return applied > 0;
        }

        /// <summary>
        /// 通用冻结效果：value = 冻结秒数
        /// </summary>
        private bool ExecuteApplyFreezeEffect(SkillEffectEntry eff, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var targets = ResolveTargets(eff.target, allies, enemies);
            if (targets == null) return false;
            int applied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive || t.RuntimeAttributes == null) continue;
                float duration = eff.duration > 0 ? eff.duration : eff.value;
                t.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFreeze(duration));
                t.FreezeTimer = Mathf.Max(t.FreezeTimer, duration);
                applied++;
            }
            return applied > 0;
        }

        /// <summary>
        /// 通用减速效果：value = 减速百分比，duration = 持续秒数
        /// </summary>
        private bool ExecuteApplySlowEffect(SkillEffectEntry eff, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var targets = ResolveTargets(eff.target, allies, enemies);
            if (targets == null) return false;
            int applied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive || t.RuntimeAttributes == null) continue;
                t.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateSlow(eff.value, eff.duration));
                applied++;
            }
            return applied > 0;
        }

        /// <summary>
        /// 通用狩猎标记效果：value = 易伤百分比，duration = 持续秒数
        /// </summary>
        private bool ExecuteApplyHuntMarkEffect(SkillEffectEntry eff, BattleFighter[] allies, BattleFighter[] enemies)
        {
            var targets = ResolveTargets(eff.target, allies, enemies);
            if (targets == null) return false;
            int applied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || !t.IsAlive || t.RuntimeAttributes == null) continue;
                float duration = eff.duration > 0 ? eff.duration : 5f;
                t.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateHuntMark(eff.value, duration));
                applied++;
            }
            return applied > 0;
        }

        /// <summary>
        /// 根据模板 ID 创建召唤数据
        /// </summary>
        private SummonData CreateSummonData(int templateId)
        {
            switch (templateId)
            {
                case 10001: // 白骨小猫
                    return new SummonData
                    {
                        summonName = "白骨小猫",
                        hp = 1,
                        attack = 2,
                        moveSpeed = 1.5f,
                        attackSpeed = 1.0f,
                        lifetime = 15f,
                        isPlayerOwned = true
                    };
                default:
                    return null;
            }
        }

        /// <summary>
        /// 相位转移（3003）：对友方无敌 2s，对敌方放逐 4s
        /// </summary>
        private bool ExecutePhaseShift(SkillState state, BattleFighter[] allies, BattleFighter[] enemies)
        {
            // 找最近的友方或敌方
            var allyTarget = FindNearestAliveAlly(allies);
            var enemyTarget = FindNearestAliveEnemy(enemies);

            // 优先对友方使用（无敌 2s）
            if (allyTarget != null && allyTarget.Transform != null)
            {
                allyTarget.IsInvulnerable = true;
                // 2 秒后解除无敌
                var buff = UnifiedBuff.CreateTimedBuff(
                    "phase_shift_ally", "相位转移",
                    BuffSource.Innate, "phase_shift",
                    StatType.Defense, true, 0f,
                    2f, BuffStackRule.None, 1);
                buff.remainingDuration = 2f;
                allyTarget.RuntimeAttributes.ApplyBuff(buff);
                Debug.Log($"[Skill] {_leader.Name} → 相位转移 → {allyTarget.Name}，无敌 2s");
                return true;
            }

            // 对敌方使用（放逐 4s = 无敌但无法行动）
            if (enemyTarget != null && enemyTarget.Transform != null)
            {
                enemyTarget.IsInvulnerable = true;
                enemyTarget.FreezeTimer = Mathf.Max(enemyTarget.FreezeTimer, 4f);
                var buff = UnifiedBuff.CreateTimedBuff(
                    "phase_shift_enemy", "放逐",
                    BuffSource.Innate, "phase_shift",
                    StatType.Defense, true, 0f,
                    4f, BuffStackRule.None, 1);
                buff.remainingDuration = 4f;
                enemyTarget.RuntimeAttributes.ApplyBuff(buff);
                Debug.Log($"[Skill] {_leader.Name} → 相位转移 → {enemyTarget.Name}，放逐 4s");
                return true;
            }

            return false;
        }

        private BattleFighter FindNearestAliveAlly(BattleFighter[] allies)
        {
            if (allies == null || _leader.Transform == null) return null;

            BattleFighter nearest = null;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < allies.Length; i++)
            {
                var a = allies[i];
                if (a == null || !a.IsAlive || a == _leader || a.Transform == null) continue;
                float dist = Vector3.Distance(_leader.Transform.position, a.Transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = a;
                }
            }
            return nearest;
        }

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

        private bool ExecuteSlowArea(SkillState state, BattleFighter[] enemies, float slowPercent, float duration, float radius)
        {
            if (enemies == null || _leader.Transform == null) return false;

            // 精酿：酒雾额外附加 75% 易伤
            bool hasFineBrew = HasLeaderBuff("fine_brew");

            int hitCount = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive || e.Transform == null || e.RuntimeAttributes == null) continue;

                float dist = Vector3.Distance(_leader.Transform.position, e.Transform.position);
                if (dist <= radius)
                {
                    e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateSlow(slowPercent, duration));
                    if (hasFineBrew)
                        e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateHuntMark(0.75f, duration));
                    hitCount++;
                }
            }
            Debug.Log($"[Skill] {_leader.Name} → 酒雾，减速 {hitCount} 个目标{(hasFineBrew ? "（精酿：+75% 易伤）" : "")}");
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

            // 战神意志：持续时间 +5s
            float duration = 20f;
            if (HasLeaderBuff("warrior_will"))
                duration += 5f;

            // 攻击力翻倍（+100%）
            var atkBuff = UnifiedBuff.CreateTimedBuff(
                "avatar_atk", "天神下凡",
                BuffSource.Innate, "avatar",
                StatType.Attack, true, 1.0f,
                duration, BuffStackRule.None, 1);
            _leader.RuntimeAttributes.ApplyBuff(atkBuff);
            _leader.RuntimeAttributes.AttackPercentBuff += 1.0f;

            // 减伤 50%
            _leader.RuntimeAttributes.DefensePercentBuff += 0.5f;
            _leader.RuntimeAttributes.Recalculate();
            Debug.Log($"[Skill] {_leader.Name} → 天神下凡！攻击翻倍，减伤 50%，持续 {duration}s");
            return true;
        }

        private bool ExecuteDragonBreath(SkillState state, BattleFighter[] enemies)
        {
            if (enemies == null || _leader.Transform == null) return false;

            bool hasFrostNova = HasLeaderBuff("frost_nova");
            bool hasDoubleBreath = HasLeaderBuff("double_breath");

            // 第一次龙息
            int hitCount = ExecuteDragonBreathInternal(enemies, 1f, hasFrostNova);

            // 双重吐息：第二发伤害减半
            if (hasDoubleBreath)
            {
                int hitCount2 = ExecuteDragonBreathInternal(enemies, 0.5f, hasFrostNova);
                Debug.Log($"[Skill] {_leader.Name} → 龙息术（双重吐息），第二发命中 {hitCount2} 个目标");
            }

            Debug.Log($"[Skill] {(hasFrostNova ? "冰霜新星" : "龙息术")}，命中 {hitCount} 个目标");
            return hitCount > 0;
        }

        private int ExecuteDragonBreathInternal(BattleFighter[] enemies, float damageMultiplier, bool isFrost)
        {
            int hitCount = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive || e.Transform == null || e.RuntimeAttributes == null) continue;

                float dist = Vector3.Distance(_leader.Transform.position, e.Transform.position);
                if (dist <= 3f)
                {
                    int damage = Mathf.RoundToInt(25 * damageMultiplier * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
                    e.RuntimeAttributes.CurrentHp = Mathf.Max(0, e.RuntimeAttributes.CurrentHp - damage);

                    if (isFrost)
                    {
                        // 冰霜新星：冻结 1 秒
                        e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFreeze(1f));
                        e.FreezeTimer = Mathf.Max(e.FreezeTimer, 1f);
                    }
                    else
                    {
                        e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(5f, 4f));
                    }
                    hitCount++;

                    if (e.RuntimeAttributes.CurrentHp <= 0)
                        e.IsDying = true;
                }
            }
            return hitCount;
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

            // 风暴之眼：范围 +30%，燃烧 +4s
            float radius = 6f;
            float burnDuration = 8f;
            if (HasLeaderBuff("storm_eye"))
            {
                radius *= 1.3f;
                burnDuration += 4f;
            }

            int hitCount = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive || e.Transform == null || e.RuntimeAttributes == null) continue;

                float dist = Vector3.Distance(_leader.Transform.position, e.Transform.position);
                if (dist <= radius)
                {
                    int damage = Mathf.RoundToInt(60 * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
                    e.RuntimeAttributes.CurrentHp = Mathf.Max(0, e.RuntimeAttributes.CurrentHp - damage);
                    e.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(8f, burnDuration));
                    hitCount++;

                    if (e.RuntimeAttributes.CurrentHp <= 0)
                        e.IsDying = true;
                }
            }
            Debug.Log($"[Skill] {_leader.Name} → 烈焰风暴{(HasLeaderBuff("storm_eye") ? "（风暴之眼）" : "")}，命中 {hitCount} 个目标，半径 {radius:F1}");
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

            // 陷阱大师：定身 +2s
            float freezeDuration = 3f;
            if (HasLeaderBuff("trap_master"))
                freezeDuration += 2f;

            // 定身
            target.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFreeze(freezeDuration));
            target.FreezeTimer = Mathf.Max(target.FreezeTimer, freezeDuration);

            // 15 物理伤害
            int damage = Mathf.RoundToInt(15 * (1f + _leader.RuntimeAttributes.Attack * 0.01f));
            target.RuntimeAttributes.CurrentHp = Mathf.Max(0, target.RuntimeAttributes.CurrentHp - damage);

            // 2 层毒
            for (int i = 0; i < 2; i++)
                target.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreatePoison(3f, 6f));

            Debug.Log($"[Skill] {_leader.Name} → 捕兽夹 → {target.Name}，定身 {freezeDuration}s + {damage} 伤害 + 2 层毒");
            if (target.RuntimeAttributes.CurrentHp <= 0)
                target.IsDying = true;
            return true;
        }

        private bool ExecuteStealth(SkillState state)
        {
            // 风之祝福：隐匿 +2s，伤害加成 100%
            float stealthDuration = 3f;
            float atkBonus = 0.5f;
            if (HasLeaderBuff("wind_blessing"))
            {
                stealthDuration += 2f;
                atkBonus = 1.0f;
            }

            var atkBuff = UnifiedBuff.CreateTimedBuff(
                "stealth_atk", "隐匿",
                BuffSource.Innate, "stealth",
                StatType.Attack, true, atkBonus,
                stealthDuration, BuffStackRule.None, 1);
            _leader.RuntimeAttributes.ApplyBuff(atkBuff);
            _leader.RuntimeAttributes.AttackPercentBuff += atkBonus;
            _leader.RuntimeAttributes.Recalculate();
            _leader.IsStealthed = true;
            Debug.Log($"[Skill] {_leader.Name} → 隐匿，攻击 +{atkBonus * 100}% 持续 {stealthDuration}s，不可被选中");
            return true;
        }

        // ── 工具方法 ──

        /// <summary>
        /// 检查族长是否拥有指定 buffId 的光环 buff
        /// </summary>
        private bool HasLeaderBuff(string buffId)
        {
            if (_leader?.RuntimeAttributes?.ActiveBuffs == null) return false;
            for (int i = 0; i < _leader.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (_leader.RuntimeAttributes.ActiveBuffs[i].buffId == buffId)
                    return true;
            }
            return false;
        }

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
}
