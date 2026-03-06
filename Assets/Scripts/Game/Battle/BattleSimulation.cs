using UnityEngine;

public struct BattleSimulationConfig
{
    public float AttackResolveDelay;
    public float AttackCooldown;
    public float MoveSpeed;
    public float AttackRange;
    public float SeekDelay;
    public float DeathDuration;
}

public class BattleSimulation
{
    private readonly BattleFighter[] _playerFighters;
    private readonly BattleFighter[] _enemyFighters;
    private readonly BattleSimulationConfig _config;

    private float _battleElapsed;

    public bool IsReady =>
        _playerFighters != null && _enemyFighters != null &&
        _playerFighters.Length > 0 && _enemyFighters.Length > 0;

    public BattleSimulation(BattleFighter[] playerFighters, BattleFighter[] enemyFighters, BattleSimulationConfig config)
    {
        _playerFighters = playerFighters;
        _enemyFighters = enemyFighters;
        _config = config;
        _battleElapsed = 0f;
    }

    public bool Tick(float deltaTime, out bool playerVictory)
    {
        playerVictory = false;
        _battleElapsed += deltaTime;

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

        if (self.AttackCooldownTimer > 0f)
        {
            self.AttackCooldownTimer -= deltaTime;
        }

        Vector3 toTarget = target.Transform.position - self.Transform.position;
        float distance = toTarget.magnitude;

        if (distance > _config.AttackRange)
        {
            Vector3 direction = toTarget.normalized;
            self.Transform.position += direction * (_config.MoveSpeed * deltaTime);
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
            self.AttackCooldownTimer = _config.AttackCooldown;
            self.PendingHitTimer = _config.AttackResolveDelay;
            self.PendingTarget = target;
            self.Avatar?.PlayAttackAndReturnIdle();
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

        int damage = Mathf.Max(1, attacker.Attack - defender.Defense);
        defender.HP = Mathf.Max(0, defender.HP - damage);
        Debug.Log($"[BattleManager] {attacker.Camp} attacks {defender.Camp}, damage={damage}, targetHP={defender.HP}");

        if (defender.HP <= 0)
        {
            StartDeath(defender);
        }
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
                Object.Destroy(fighter.Transform.gameObject);
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
