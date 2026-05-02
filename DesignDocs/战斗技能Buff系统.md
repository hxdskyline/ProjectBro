# 战斗技能 Buff 系统设计文档

版本：v1.0
日期：2026-05-02

---

## 一、系统概述

Buff 系统是战斗技能的核心数据载体。所有增益/减益效果（属性修改、状态效果、特殊能力）均通过统一的 `UnifiedBuff` 结构表示，经过 层级化 的应用链路最终影响战斗单位的运行时属性。

---

## 二、核心数据结构

### 2.1 UnifiedBuff（统一 Buff 表示）

```csharp
// 文件：TribeSystem/UnifiedBuff.cs
public class UnifiedBuff
{
    // ── 标识 ──
    string buffId;          // 唯一标识（如 "aura_orange_gen_burning_fight_Attack"）
    string displayName;     // 显示名（如 "燃烧斗志"）
    BuffSource source;      // 来源系统
    string sourceId;        // 来源 ID（choiceId / equipmentId）

    // ── 持久性与叠加 ──
    BuffPersistence persistence;  // Persistent（局内永久）/ BattleOnly（战斗内）
    BuffStackRule stackRule;       // None / Stack / DurationRefresh
    int maxStacks;                // 最大叠加层数
    int currentStacks;            // 当前层数

    // ── 属性修改 ──
    StatType statType;      // 影响的属性
    bool isPercent;         // 是否百分比
    float value;            // 数值（每层）

    // ── 特殊效果 ──
    GameEffect gameEffect;  // 特殊效果类型（DoT 等）
    int gameEffectType;     // 光环特殊效果 ID（来自 tribe_aura_config.json）
    float effectParam1;     // 效果参数1
    float effectParam2;     // 效果参数2

    // ── 生命周期 ──
    float remainingDuration; // 剩余时间（秒），-1=永久
    float tickInterval;      // 触发间隔（秒），0=不持续触发
    float tickTimer;         // 当前 tick 计时器
}
```

### 2.2 枚举定义

#### BuffPersistence（持久性）
| 值 | 说明 |
|---|---|
| `Persistent` | 局内成长：跨战斗永久生效（族长技能、光环、饰品） |
| `BattleOnly` | 战斗内：仅当前战斗有效，战斗结束清零 |

#### BuffStackRule（叠加规则）
| 值 | 说明 |
|---|---|
| `None` | 不可叠加，重复添加时刷新持续时间 |
| `Stack` | 可叠加，每层独立计算，取较长持续时间 |
| `DurationRefresh` | 不叠加层数，只刷新持续时间 |

#### BuffSource（来源系统）
| 值 | 说明 |
|---|---|
| `Recruitment` | 招募族长强化 |
| `Artifact` | 商店奇物 |
| `Ritual` | 祈祀奖励 |
| `Equipment` | 饰品（全局） |
| `Mood` | 心情修正 |
| `Innate` | 天生被动 |
| `Consumable` | 消耗品 |

#### BuffApplyScope（影响范围）
| 值 | 说明 |
|---|---|
| `All` | 全体（所有族长 + 所有小猫） |
| `AllLeaders` | 全体族长 |
| `AllCats` | 全体小猫 |
| `SingleTribeLeader` | 单族族长 |
| `SingleTribeCat` | 单族小猫 |
| `SingleTribeAll` | 单族全体（族长 + 小猫） |

#### BuffApplyType（应用类型）
| 值 | 说明 |
|---|---|
| `CurrentUnit` | 只影响当前已有单位 |
| `Aura` | 光环：当前单位 + 未来新获得的单位自动继承 |

---

## 三、Buff 生命周期

### 3.1 创建

Buff 通过以下工厂方法创建：

```csharp
// 永久属性 buff（用于光环、装备等）
UnifiedBuff.CreateStatBuff(buffId, displayName, source, sourceId,
    statType, isPercent, value, scope, gameEffectType)

// 限时 buff（用于战斗内效果）
UnifiedBuff.CreateTimedBuff(buffId, displayName, source, sourceId,
    statType, isPercent, value, duration, stackRule, maxStacks)
```

战斗内状态效果通过 `StatusEffectFactory` 创建（见第五节）。

### 3.2 应用（Apply）

```
RegisterChoice/RegisterEquipment
    ↓
ApplyToExistingUnits（按 scope 分发到 leader/cat）
    ↓
LeaderData.AddUnifiedBuff / CatData.AddUnifiedBuff
    ↓
检查是否已存在同 buffId → 存在则 TryStackOrRefresh，否则 Add
    ↓
如果 gameEffectType > 0 → 触发 IBuffEffect.OnBattleStart
    ↓
ApplyBuffPassiveEffect（处理 Slow/HuntMark 等被动效果）
```

**叠加判断逻辑**（`ApplyBuff`）：
```csharp
// 已存在同 buffId → 叠加或刷新
for (int i = 0; i < ActiveBuffs.Count; i++)
{
    if (ActiveBuffs[i].buffId == buff.buffId)
    {
        ActiveBuffs[i].TryStackOrRefresh(buff);  // 按 stackRule 处理
        existed = true;
        break;
    }
}
// 不存在 → 新增
if (!existed) ActiveBuffs.Add(buff.Clone());
```

### 3.3 持续与 Tick

每帧由 `UnitRuntimeAttributes.TickBuffs(deltaTime)` 驱动：

```
TickBuffs(deltaTime)
    ├── RecalculateSlowDebuff()        // 重算减速总和
    ├── 遍历 _activeBuffs（倒序）
    │   ├── 非永久 → remainingDuration -= deltaTime
    │   ├── tickInterval > 0 → tickTimer -= deltaTime
    │   │   └── tickTimer <= 0 → ApplyTickEffect（DoT 伤害等）
    │   ├── gameEffectType > 0 → IBuffEffect.OnTick(ctx, deltaTime)
    │   └── IsExpired → OnBuffExpired → Remove
    └── return BuffTickResult（dotDamage / freezeDuration / needsRecalculate）
```

### 3.4 移除

```csharp
// 按 buffId 移除
leader.RemoveBuff(buffId);

// 按 sourceId 移除（用于卸载装备/注销选择）
leader.RemoveBuffBySource(sourceId);

// 战斗结束清理 BattleOnly buff
leader.ClearBattleBuffs();
```

---

## 四、Buff 应用链路

### 4.1 层级架构

```
配置层          服务层              数据层              战斗层
─────────     ─────────           ─────────           ─────────
tribe_aura    TribeAuraService    LeaderData          BattleManager
_config.json  ↓                   CatData             ↓
              AuraService         ↓                   BattleSimulation
              ↓                   UnitRuntime         ↓
              DataManager         Attributes          BattleFighter
              (RebuildAuraBuffs)
```

### 4.2 族群光环流程（TribeAuraService）

```
tribe_aura_config.json
    ↓ 解析
TribeAuraService.ApplySingleAura(aura, tribeType)
    ↓ 读取 aura.scope
GameChoice.CreateBuff(scope = aura.scope, tribe = tribeType)
    ↓
AuraService.RegisterChoice(choice)
    ├── runChoices.Add(choice)        // 持久化
    └── ApplyToExistingUnits(...)     // 立即应用到现有 leader/cat
```

**关键设计**：每个光环的 `scope` 从 JSON 配置读取，不硬编码在代码中。

### 4.3 存档重建流程（DataManager.RebuildAuraBuffs）

存档加载后，`ActiveBuffs`（`[NonSerialized]`）需要从 `runChoices` / `runEquipments` 重建：

```
RebuildAuraBuffs()
    ├── 收集 runChoices 中 BuffApplyType.Aura 的条目
    ├── 收集 runEquipments 中 BuffApplyType.Aura 的条目
    │   └── 适配为 GameChoice（choiceId = equipmentId）
    └── 对每个族群遍历
        ├── MatchesScope(scope, targetTribeId, tribeType)
        ├── MatchesLeader(scope) → ApplyAuraEffectsToLeader
        └── MatchesCat(scope)    → ApplyAuraEffectsToCat
```

**注意事项**：
- `targetTribeType`（`TribeType?`）不能被 `JsonUtility` 序列化，改用 `targetTribeId`（int）
- buffId 使用唯一标识（choiceId/equipmentId），而非 displayName，避免同名装备去重

### 4.4 战斗中应用（BattleManager.ApplyAuraBuffs）

战斗开始时，从 leader 的 `ActiveBuffs` 提取 AttackSpeed 加成应用到 `RuntimeAttributes`：

```csharp
foreach (var buff in tribe.leader.ActiveBuffs)
{
    if (buff.statType == StatType.AttackSpeed && buff.persistence == BuffPersistence.Persistent)
    {
        fighter.RuntimeAttributes.AttackSpeedPercentBuff += buff.value * buff.currentStacks;
    }
}
fighter.RuntimeAttributes.Recalculate();
```

**注意**：只处理 AttackSpeed，其他属性由 `TribeStatsCalculator` 在战斗前计算，避免双重计数。

---

## 五、状态效果系统

### 5.1 StatusEffectFactory

创建战斗内状态效果的工厂类（`BattleSystem.Effects` 命名空间）：

| 方法 | 效果 | 叠加 | 说明 |
|---|---|---|---|
| `CreatePoison(dps, duration, maxStacks)` | 毒 | 可叠 | 每秒 dps × 层数 |
| `CreateBleed(dps, duration, maxStacks)` | 流血 | 可叠 | 每秒 dps × 层数 |
| `CreateBurn(dps, duration, maxStacks)` | 燃烧 | 可叠 | 每秒 dps × 层数 |
| `CreateFreeze(duration)` | 冻结 | 不可叠 | 定身，刷新持续时间 |
| `CreateSlow(speedReduction%, duration, maxStacks)` | 减速 | 可叠 | 移速 -value%，上限 90% |
| `CreateHuntMark(damageBonus%, duration)` | 狩猎标记 | 不可叠 | 受到伤害 +value% |
| `CreateFullnessStack(hpPerStack, atkPerStack)` | 饱食（HP） | 永久可叠 | 每层 +HP |
| `CreateFullnessAtkStack(atkPerStack)` | 饱食（ATK） | 永久可叠 | 每层 +ATK |
| `CreateDragonCharge(spellDmgPerStack)` | 龙语充能 | 永久可叠 | 每层 +法伤% |
| `CreateHunterFocus(markedDmgPerStack)` | 猎手专注 | 永久可叠 | 每层 +对标记伤害% |

### 5.2 DoT 处理

`UnitRuntimeAttributes.ApplyTickEffect` 处理 DoT tick：

```csharp
switch (buff.gameEffect)
{
    case GameEffect.Poison:
    case GameEffect.Bleed:
    case GameEffect.Burn:
        result.dotDamage += Mathf.RoundToInt(buff.effectParam1 * buff.currentStacks);
        break;
}
```

`BuffTickResult.dotDamage` 由 `BattleSimulation` 消费，直接扣减目标 HP。

### 5.3 控制效果

- **冻结**：`BattleSimulation` 检查 `freezeDuration > 0` 跳过 AI/移动/攻击
- **减速**：通过 `RecalculateSlowDebuff()` 汇总所有 Slow buff，设置 `SpeedPercentDebuff`（上限 90%）
- **狩猎标记**：通过 `RecalculateHuntMarkDebuff()` 汇总，设置 `DamageReceivePercentBuff`

---

## 六、属性修正体系

### 6.1 UnitRuntimeAttributes 修正字段

```
攻击修正 = (ActiveBuffs百分比 + AttackPercentBuff - AttackPercentDebuff) × 基础攻击 + (ActiveBuffs固定 + AttackFlatBuff - AttackFlatDebuff)
防御修正 = 同理
生命修正 = 同理
速度修正 = 基础速度 × (1 + spdPct) + spdFlat
攻速修正 = 基础攻速 / 1000 × (1 + atkSpdPct)
```

### 6.2 Recalculate() 计算流程

```csharp
Recalculate()
    ├── 从 ActiveBuffs 汇总到临时变量（abAtkPct, abDefPct, ...）
    ├── 加上外部字段（地形/天气/装备等直接修改的字段）
    ├── 减去 debuff 字段
    └── 计算最终属性
        ├── Attack = Max(1, RoundToInt(base × (1+atkPct) + atkFlat))
        ├── Defense = Max(0, ...)
        ├── MaxHp = Max(1, ...)
        ├── CorrectedMoveSpeed = Max(1, base × (1+spdPct) + spdFlat) / 1000
        └── CorrectedAttackSpeed = Max(0.1, base / 1000 × (1+atkSpdPct))
```

### 6.3 伤害公式

```
DMG = Max(CATK - CDEF, 0)
DR = Max(1 - CDEF / (CDEF + 100), 0.2)
FDMG = Max(DMG × DR × SkillMultiplier, 1) + TrueDamage
```

其中 `SkillMultiplier` 和 `TrueDamage` 由 buff 的 `gameEffectType` 特殊效果提供。

---

## 七、特殊效果系统（IBuffEffect）

### 7.1 gameEffectType 机制

`gameEffectType` 是一个 int 型 ID，映射到具体的 `IBuffEffect` 实现。通过 `BuffEffectRegistry` 注册和查找。

生命周期回调：
| 回调 | 触发时机 |
|---|---|
| `OnBattleStart(ctx)` | buff 首次应用时 |
| `OnTick(ctx, deltaTime)` | 每帧 tick |
| `OnAttackHit(ctx)` | 攻击命中时 |
| `OnKill(ctx)` | 击杀敌人时 |
| `OnDeath(ctx)` | 自身死亡时 |
| `OnExpire(ctx)` | buff 过期时 |

### 7.2 tribe_aura_config.json 中的 gameEffectType

| ID 范围 | 族群 | 说明 |
|---|---|---|
| 100-109 | 狸花猫 | 穿刺箭、毒箭、精准、游击、暗杀等 |
| 200-209 | 大橘 | 双斧、鸡腿、狂战士、冲锋、集群等 |
| 300-309 | 奶牛猫 | 淬毒之爪、亡者供养、骨质增生等 |
| 400-409 | 暹罗猫 | 法术迸发、寒冰触须、法力护盾等 |

---

## 八、特殊 Buff 效果一览

### 8.1 天生被动（PermanentBuffs.specialBuffs）

| 族群 | buffId | 效果类型 | 说明 |
|---|---|---|---|
| 大橘 | `innate_饕餮` | KillHealSatiety | 击杀回 10% 最大生命 + 1 层饱食 |
| 奶牛猫 | `innate_牧群领袖` | AttackPerFriendlyUnit | 每个友方单位 +1 攻击 |
| 狸花猫 | `innate_狩猎印记` | MarkTargetDamageAmp | 攻击标记目标，受伤 +30% |
| 暹罗猫 | `innate_龙语回响` | DragonBreathOnCast | 施法时喷射龙息（10 火伤） |

### 8.2 战斗内成长 Buff

| buffId | 效果 | 触发方式 |
|---|---|---|
| `fullness_stack` | 每层 +HP | 大橘击杀触发 |
| `fullness_atk_stack` | 每层 +ATK | 大橘击杀触发 |
| `dragon_charge` | 每层 +法伤% | 暹罗施法触发 |
| `hunter_focus` | 每层 +对标记伤害% | 狸花攻击标记目标触发 |

### 8.3 饱食层处理流程

```
大橘击杀敌人
    ├── BattleManager.UpdateBattleGrowth
    │   ├── prevMaxHp = runtime.MaxHp
    │   ├── runtime.ApplyBuff(CreateFullnessStack(60HP, 4ATK))
    │   ├── runtime.ApplyBuff(CreateFullnessAtkStack(4ATK))
    │   ├── runtime.Recalculate()
    │   └── runtime.CurrentHp += MaxHp - prevMaxHp  // 补充因上限增加的血量
    └── buff 栏显示 "饱食xN +XXX"
```

---

## 九、存档与序列化

### 9.1 持久化策略

| 数据 | 存储位置 | 说明 |
|---|---|---|
| `runChoices` | `PlayerData` | Aura 类型的 choice 记录 |
| `runEquipments` | `PlayerData` | 装备记录 |
| `ActiveBuffs` | **不存储** | `[NonSerialized]`，加载时重建 |

### 9.2 重建流程

```
DataManager.LoadPlayerData()
    → EnsurePlayerDataDefaults()
        → RebuildAuraBuffs()
            → 遍历 runChoices + runEquipments
            → 按 scope 分发到 leader/cat 的 ActiveBuffs
```

### 9.3 序列化陷阱

- `TribeType?`（nullable enum）不能被 `JsonUtility` 序列化 → 改用 `targetTribeId`（int）
- `ActiveBuffs` 是 `[NonSerialized]` → 必须从 `runChoices`/`runEquipments` 重建
- buffId 必须唯一（使用 choiceId/equipmentId），否则同名装备会被去重

---

## 十、UI 显示

### 10.1 Buff 栏分组逻辑

`TribeCard.AddUnifiedBuffEntries` 按 `displayName` 分组显示：

```
同来源的多个 buff 合并显示：
  苍蝇拍x2 +40攻击力
  双斧 +30%攻速
  饱食x5 +300生命 +20攻击
```

### 10.2 属性颜色编码

| 属性 | 颜色 |
|---|---|
| 攻击 | 红 |
| 防御 | 蓝 |
| 速度 | 金 |
| 生命 | 绿 |
| 攻速 | 橙 |
| 统御 | 紫 |

---

## 十一、关键文件索引

| 文件 | 职责 |
|---|---|
| `TribeSystem/UnifiedBuff.cs` | Buff 数据结构、叠加逻辑 |
| `TribeSystem/ChoiceSystem.cs` | GameChoice、BuffApplyScope 等枚举 |
| `TribeSystem/CardBuff.cs` | BuffSource、BuffScope、InnateEffectType、PermanentBuffs |
| `TribeSystem/UnitTypes.cs` | UnitRuntimeAttributes、TickBuffs、Recalculate |
| `TribeSystem/AuraService.cs` | 光环注册/注销/补发 |
| `TribeSystem/TribeAuraService.cs` | 族群光环配置加载与应用 |
| `TribeSystem/BuffService.cs` | Buff 移除/清理 |
| `Battle/StatusEffectFactory.cs` | 战斗内状态效果工厂 |
| `Battle/BattleManager.cs` | ApplyAuraBuffs、UpdateBattleGrowth |
| `Framework/DataManager.cs` | RebuildAuraBuffs（存档重建） |
| `UI/TribeBuild/TribeCard.cs` | Buff 栏 UI 显示 |
