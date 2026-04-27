using UnityEngine;
using TribeSystem;
using System;

public struct BulletData
{
    public BattleFighter Attacker;
    public BattleFighter Target;
    public int Damage;
    public bool IsCritical;
}

public struct BattleSimulationConfig
{
    public float AttackResolveDelay;
    public float AttackCooldown;
    public float SeekDelay;
    public float DeathDuration;
}

public class BattleSimulation
{
    public static event Action<BulletData> OnBulletFired;

    private readonly BattleFighter[] _playerFighters;
    private readonly BattleFighter[] _enemyFighters;
    private readonly BattleSimulationConfig _config;

    private float _battleElapsed;
    private float _attackBuffTimer;
    private float _defenseBuffTimer;

    public bool IsReady =>
        _playerFighters != null && _enemyFighters != null &&
        _playerFighters.Length > 0 && _enemyFighters.Length > 0;

    public BattleSimulation(BattleFighter[] playerFighters, BattleFighter[] enemyFighters, BattleSimulationConfig config)
    {
        _playerFighters = playerFighters;
        _enemyFighters = enemyFighters;
        _config = config;
        _battleElapsed = 0f;
        _attackBuffTimer = 0f;
        _defenseBuffTimer = 0f;
    }

    /// <summary>
    /// 施放消耗品效果（对全体目标生效，无需选位）
    /// </summary>
    public void ApplyConsumable(ConsumableEffectType effectType)
    {
        switch (effectType)
        {
            case ConsumableEffectType.Bomb:
                ApplyBomb();
                break;
            case ConsumableEffectType.FreezeTrap:
                ApplyFreezeTrap();
                break;
            case ConsumableEffectType.HealPotion:
                ApplyHealPotion();
                break;
            case ConsumableEffectType.AttackBuff:
                ApplyAttackBuff();
                break;
            case ConsumableEffectType.DefenseBuff:
                ApplyDefenseBuff();
                break;
        }
    }

    private void ApplyBomb()
    {
        if (_enemyFighters == null) return;
        for (int i = 0; i < _enemyFighters.Length; i++)
        {
            var f = _enemyFighters[i];
            if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
            f.RuntimeAttributes.CurrentHp = Mathf.Max(0, f.RuntimeAttributes.CurrentHp - 200);
            if (f.RuntimeAttributes.CurrentHp <= 0) StartDeath(f);
        }
        Debug.Log("[Consumable] Bomb: 200 damage to all enemies");
    }

    private void ApplyFreezeTrap()
    {
        if (_enemyFighters == null) return;
        for (int i = 0; i < _enemyFighters.Length; i++)
        {
            var f = _enemyFighters[i];
            if (f == null || !f.IsAlive) continue;
            f.FreezeTimer = 3f;
        }
        Debug.Log("[Consumable] FreezeTrap: all enemies frozen for 3s");
    }

    private void ApplyHealPotion()
    {
        if (_playerFighters == null) return;
        for (int i = 0; i < _playerFighters.Length; i++)
        {
            var f = _playerFighters[i];
            if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
            int heal = Mathf.RoundToInt(f.RuntimeAttributes.MaxHp * 0.5f);
            f.RuntimeAttributes.CurrentHp = Mathf.Min(f.RuntimeAttributes.CurrentHp + heal, f.RuntimeAttributes.MaxHp);
        }
        Debug.Log("[Consumable] HealPotion: healed all allies for 50% MaxHp");
    }

    private void ApplyAttackBuff()
    {
        if (_playerFighters == null) return;
        for (int i = 0; i < _playerFighters.Length; i++)
        {
            var f = _playerFighters[i];
            if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
            f.RuntimeAttributes.AttackPercentBuff += 0.3f;
            f.RuntimeAttributes.Recalculate();
        }
        _attackBuffTimer = 15f;
        Debug.Log("[Consumable] AttackBuff: +30% ATK for 15s");
    }

    private void ApplyDefenseBuff()
    {
        if (_playerFighters == null) return;
        for (int i = 0; i < _playerFighters.Length; i++)
        {
            var f = _playerFighters[i];
            if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
            f.RuntimeAttributes.DefensePercentBuff += 0.3f;
            f.RuntimeAttributes.Recalculate();
        }
        _defenseBuffTimer = 15f;
        Debug.Log("[Consumable] DefenseBuff: +30% DEF for 15s");
    }

    private void UpdateTimers(float deltaTime)
    {
        // Freeze timers
        UpdateFreezeTimers(_playerFighters, deltaTime);
        UpdateFreezeTimers(_enemyFighters, deltaTime);

        // Attack buff expiry
        if (_attackBuffTimer > 0f)
        {
            _attackBuffTimer -= deltaTime;
            if (_attackBuffTimer <= 0f)
            {
                RemoveBuffFromFighters(_playerFighters, buffType: 0);
                Debug.Log("[Consumable] AttackBuff expired");
            }
        }

        // Defense buff expiry
        if (_defenseBuffTimer > 0f)
        {
            _defenseBuffTimer -= deltaTime;
            if (_defenseBuffTimer <= 0f)
            {
                RemoveBuffFromFighters(_playerFighters, buffType: 1);
                Debug.Log("[Consumable] DefenseBuff expired");
            }
        }
    }

    private void UpdateFreezeTimers(BattleFighter[] fighters, float deltaTime)
    {
        if (fighters == null) return;
        for (int i = 0; i < fighters.Length; i++)
        {
            var f = fighters[i];
            if (f != null && f.FreezeTimer > 0f)
                f.FreezeTimer -= deltaTime;
        }
    }

    /// <summary>
    /// buffType: 0=Attack, 1=Defense
    /// </summary>
    private void RemoveBuffFromFighters(BattleFighter[] fighters, int buffType)
    {
        if (fighters == null) return;
        for (int i = 0; i < fighters.Length; i++)
        {
            var f = fighters[i];
            if (f == null || f.RuntimeAttributes == null) continue;
            if (buffType == 0)
                f.RuntimeAttributes.AttackPercentBuff -= 0.3f;
            else
                f.RuntimeAttributes.DefensePercentBuff -= 0.3f;
            f.RuntimeAttributes.Recalculate();
        }
    }

    public bool Tick(float deltaTime, out bool playerVictory)
    {
        playerVictory = false;
        _battleElapsed += deltaTime;

        UpdateTimers(deltaTime);
        UpdatePendingHits(_playerFighters, deltaTime);
        UpdatePendingHits(_enemyFighters, deltaTime);
        UpdateDeathStates(_playerFighters, deltaTime);
        UpdateDeathStates(_enemyFighters, deltaTime);

        if (AreAllRemoved(_playerFighters) || AreAllRemoved(_enemyFighters))
        {
            playerVictory = AreAllRemoved(_enemyFighters) && !AreAllRemoved(_playerFighters);
            return true;
        }

        if (_battleElapsed >= _config.SeekDelay)
        {
            UpdateGroupAI(_playerFighters, _enemyFighters, deltaTime);
            UpdateGroupAI(_enemyFighters, _playerFighters, deltaTime);
        }
        else
        {
            PlayGroupIdle(_playerFighters);
            PlayGroupIdle(_enemyFighters);
        }

        return false;
    }

    private void PlayGroupIdle(BattleFighter[] fighters)
    {
        if (fighters == null)
        {
            return;
        }

        for (int i = 0; i < fighters.Length; i++)
        {
            fighters[i]?.Avatar?.PlayIdle();
        }
    }

    private void UpdateGroupAI(BattleFighter[] group, BattleFighter[] targets, float deltaTime)
    {
        if (group == null || targets == null)
        {
            return;
        }

        for (int i = 0; i < group.Length; i++)
        {
            BattleFighter self = group[i];
            if (self == null || !self.IsAlive)
            {
                continue;
            }

            BattleFighter target = FindNearestTarget(self, targets);
            if (target != null)
            {
                UpdateFighterAI(self, target, deltaTime);
            }
            else
            {
                // No valid enemy remains (or all enemies are in death state), stop running and return to idle.
                self.PendingTarget = null;
                self.Avatar?.PlayIdle();
            }
        }
    }

    private BattleFighter FindNearestTarget(BattleFighter self, BattleFighter[] targets)
    {
        BattleFighter nearest = null;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            BattleFighter candidate = targets[i];
            if (candidate == null || !candidate.IsAlive || candidate.Transform == null || self.Transform == null)
            {
                continue;
            }

            Vector3 delta = candidate.Transform.position - self.Transform.position;
            float sqr = delta.sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void UpdateFighterAI(BattleFighter self, BattleFighter target, float deltaTime)
    {
        if (self == null || target == null || self.Transform == null || target.Transform == null)
        {
            return;
        }

        if (!self.IsAlive || !target.IsAlive)
        {
            return;
        }

        // Frozen: cannot move or attack
        if (self.FreezeTimer > 0f)
        {
            self.Avatar?.PlayIdle();
            return;
        }

        if (self.AttackCooldownTimer > 0f)
        {
            self.AttackCooldownTimer -= deltaTime;
        }

        Vector3 toTarget = target.Transform.position - self.Transform.position;
        float distance = toTarget.magnitude;

        float attackRange = GetAttackRange(self);
        if (distance > attackRange)
        {
            Vector3 direction = toTarget.normalized;
            self.Transform.position += direction * (GetMoveSpeed(self) * deltaTime);
            UpdateFacing(self, direction.x);
            self.Avatar?.PlayRun();
            return;
        }

        UpdateFacing(self, toTarget.x);

        if (self.PendingHitTimer > 0f)
        {
            return;
        }

        if (self.AttackCooldownTimer <= 0f)
        {
            // 攻击速度: AS = CS/2000, 攻击间隔 = 1/AS
            float attackSpeed = self.RuntimeAttributes?.CorrectedAttackSpeed ?? 1f;
            self.AttackCooldownTimer = 1f / Mathf.Max(0.001f, attackSpeed);
            self.Avatar?.PlayAttackAndReturnIdle();

            // 狸花（远程）：发射子弹，不走 PendingHit
            if (self.TribeType == TribeType.Tabby)
            {
                int damage = CalculateDamage(self, target);
                bool isCritical = false;
                // 15%概率造成双倍伤害（单次伤害，不再发射两颗子弹）
                if (self.HasDoubleHit && UnityEngine.Random.value < 0.15f)
                {
                    damage *= 2;
                    isCritical = true;
                }
                OnBulletFired?.Invoke(new BulletData
                {
                    Attacker = self,
                    Target = target,
                    Damage = damage,
                    IsCritical = isCritical
                });
                return;
            }

            self.PendingHitTimer = _config.AttackResolveDelay;
            self.PendingTarget = target;
            return;
        }

        self.Avatar?.PlayIdle();
    }

    private void UpdateFacing(BattleFighter fighter, float xDirection)
    {
        if (fighter == null || fighter.Transform == null)
        {
            return;
        }

        if (Mathf.Abs(xDirection) < 0.001f)
        {
            return;
        }

        float scale = Mathf.Max(0.1f, fighter.BaseScale);
        float signedX = xDirection >= 0f ? -scale : scale;
        Vector3 localScale = fighter.Transform.localScale;
        fighter.Transform.localScale = new Vector3(signedX, Mathf.Abs(localScale.y), 1f);
    }

    private void UpdatePendingHits(BattleFighter[] attackers, float deltaTime)
    {
        if (attackers == null)
        {
            return;
        }

        for (int i = 0; i < attackers.Length; i++)
        {
            UpdatePendingHit(attackers[i], deltaTime);
        }
    }

    private void UpdatePendingHit(BattleFighter attacker, float deltaTime)
    {
        if (attacker == null || attacker.PendingHitTimer <= 0f)
        {
            return;
        }

        attacker.PendingHitTimer -= deltaTime;
        if (attacker.PendingHitTimer > 0f)
        {
            return;
        }

        BattleFighter defender = attacker.PendingTarget;
        attacker.PendingTarget = null;

        if (defender == null || !defender.IsAlive)
        {
            return;
        }

        UnitRuntimeAttributes attackerRuntime = attacker.RuntimeAttributes;
        UnitRuntimeAttributes defenderRuntime = defender.RuntimeAttributes;
        if (attackerRuntime == null || defenderRuntime == null)
        {
            return;
        }

        // 狸花连击：攻击2次
        int hitCount = attacker.HasDoubleHit ? 2 : 1;

        for (int hit = 0; hit < hitCount; hit++)
        {
            if (!defender.IsAlive) break;

            // 需求公式: FDMG = MAX[DMG * DR * SKILLMULT * PBUFF + ABUFF, 1] + TD
            // DMG = MAX(CATK - CDEF, 0)
            // DR = MAX(1 - CDEF/(CDEF+100), 0.2)
            int rawDmg = Mathf.Max(0, attackerRuntime.Attack - defenderRuntime.Defense);
            float dr = Mathf.Max(0.2f, 1f - (float)defenderRuntime.Defense / (defenderRuntime.Defense + 100f));
            float skillMult = attackerRuntime.SkillMultiplier;
            float dmgPercentMod = 1f + defenderRuntime.DamageReceivePercentBuff;
            int dmgFlatMod = defenderRuntime.DamageReceiveFlatBuff;
            float finalF = rawDmg * dr * skillMult * dmgPercentMod + dmgFlatMod;
            int damage = Mathf.Max(1, Mathf.RoundToInt(finalF)) + attackerRuntime.TrueDamage;
            int newHp = Mathf.Max(0, defenderRuntime.CurrentHp - damage);
            defenderRuntime.CurrentHp = newHp;

            // Show damage popup and update HUD if present
            if (defender != null && defender.Transform != null)
            {
                var hud = defender.Transform.GetComponent<FighterHUD>();
                if (hud != null)
                {
                    hud.ShowDamage(damage);
                    hud.UpdateHp(defenderRuntime.CurrentHp);
                }
            }
        }

        if (defenderRuntime.CurrentHp <= 0)
        {
            StartDeath(defender);
        }
    }

    /// <summary>
    /// 计算单次攻击伤害
    /// </summary>
    private int CalculateDamage(BattleFighter attacker, BattleFighter defender)
    {
        UnitRuntimeAttributes attackerRuntime = attacker.RuntimeAttributes;
        UnitRuntimeAttributes defenderRuntime = defender.RuntimeAttributes;
        if (attackerRuntime == null || defenderRuntime == null) return 0;

        int rawDmg = Mathf.Max(0, attackerRuntime.Attack - defenderRuntime.Defense);
        float dr = Mathf.Max(0.2f, 1f - (float)defenderRuntime.Defense / (defenderRuntime.Defense + 100f));
        float skillMult = attackerRuntime.SkillMultiplier;
        float dmgPercentMod = 1f + defenderRuntime.DamageReceivePercentBuff;
        int dmgFlatMod = defenderRuntime.DamageReceiveFlatBuff;
        float finalF = rawDmg * dr * skillMult * dmgPercentMod + dmgFlatMod;
        return Mathf.Max(1, Mathf.RoundToInt(finalF)) + attackerRuntime.TrueDamage;
    }

    private float GetMoveSpeed(BattleFighter fighter)
    {
        return fighter?.RuntimeAttributes != null
            ? Mathf.Max(0.001f, fighter.RuntimeAttributes.CorrectedMoveSpeed)
            : 2.2f;
    }

    private float GetAttackRange(BattleFighter fighter)
    {
        return fighter?.RuntimeAttributes != null
            ? Mathf.Max(0.1f, fighter.RuntimeAttributes.AttackRange)
            : 1.0f;
    }

    private void StartDeath(BattleFighter fighter)
    {
        if (fighter == null || fighter.IsRemoved || fighter.IsDying)
        {
            return;
        }

        fighter.IsDying = true;
        fighter.PendingHitTimer = 0f;
        fighter.AttackCooldownTimer = 0f;
        fighter.PendingTarget = null;
        fighter.DeathTimer = Mathf.Max(0.1f, _config.DeathDuration);

        // Keep death presentation consistent: face left from death start until removal.
        if (fighter.Transform != null)
        {
            float scale = Mathf.Max(0.1f, fighter.BaseScale);
            Vector3 localScale = fighter.Transform.localScale;
            fighter.Transform.localScale = new Vector3(scale, Mathf.Abs(localScale.y), 1f);
        }

        fighter.Avatar?.PlayDeath();
    }

    private void UpdateDeathStates(BattleFighter[] fighters, float deltaTime)
    {
        if (fighters == null)
        {
            return;
        }

        for (int i = 0; i < fighters.Length; i++)
        {
            BattleFighter fighter = fighters[i];
            if (fighter == null || !fighter.IsDying || fighter.IsRemoved)
            {
                continue;
            }

            fighter.DeathTimer -= deltaTime;
            if (fighter.DeathTimer > 0f)
            {
                continue;
            }

            if (fighter.Transform != null)
            {
                UnityEngine.Object.Destroy(fighter.Transform.gameObject);
            }

            fighter.Transform = null;
            fighter.Avatar = null;
            fighter.IsRemoved = true;
        }
    }

    private bool AreAllRemoved(BattleFighter[] fighters)
    {
        if (fighters == null || fighters.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < fighters.Length; i++)
        {
            if (fighters[i] != null && !fighters[i].IsRemoved)
            {
                return false;
            }
        }

        return true;
    }
}
