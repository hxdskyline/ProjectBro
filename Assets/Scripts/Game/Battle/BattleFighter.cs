using UnityEngine;
using TribeSystem;
using System.Collections.Generic;

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
    public float FreezeTimer; // >0 时不攻击
    public TribeType TribeType; // 用于地形/天气 BUFF
    public List<TribeBuff> InnateBuffs; // 天生特殊 buff
    public bool HasDoubleHit; // 狸花连击标记
    public List<BuffEntry> BuffEntries; // buff 来源记录（用于 UI 显示）

    public int CurrentHp => RuntimeAttributes?.CurrentHp ?? 0;
    public bool IsDead => CurrentHp <= 0;
    public bool IsAlive => !IsRemoved && !IsDying && CurrentHp > 0;
}
