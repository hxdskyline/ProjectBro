using UnityEngine;

public class BattleFighter
{
    public string Name;
    public BattleCamp Camp;
    public int HP;
    public int Attack;
    public int Defense;
    public BattleAvatar Avatar;
    public Transform Transform;
    public float AttackCooldownTimer;
    public float PendingHitTimer;
    public BattleFighter PendingTarget;
    public float BaseScale;
    public bool IsDying;
    public bool IsRemoved;
    public float DeathTimer;

    public bool IsDead => HP <= 0;
    public bool IsAlive => !IsRemoved && !IsDying && HP > 0;
}
