using UnityEngine;

public class BattleFighter
{
    public string Name;
    public BattleCamp Camp;
    public BattleUnitTypeConfig UnitType;
    public UnitStaticAttributes StaticAttributes;
    public UnitRuntimeAttributes RuntimeAttributes;
    public BattleAvatar Avatar;
    public Transform Transform;
    public float AttackCooldownTimer;
    public float PendingHitTimer;
    public BattleFighter PendingTarget;
    public float BaseScale;
    public bool IsDying;
    public bool IsRemoved;
    public float DeathTimer;

    public int CurrentHp => RuntimeAttributes?.CurrentHp ?? 0;
    public bool IsDead => CurrentHp <= 0;
    public bool IsAlive => !IsRemoved && !IsDying && CurrentHp > 0;
}
