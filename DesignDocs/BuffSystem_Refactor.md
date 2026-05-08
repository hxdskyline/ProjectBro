# Buff 系统重构计划

> **状态：全部完成** (2026-05-06)

## 现状概述

当前 buff 系统处于**双轨并行**状态：
- **旧轨**（半废弃）：`TemporaryBuff`、`PermanentBuffs`、`BuffEntry`、`TribeBuff`、`BuffScope` 枚举
- **新轨**（活跃）：`UnifiedBuff`、`BuffScopeFilter`、`BuffService`、`AuraService`

新轨已覆盖所有功能，但旧轨代码未清理，导致：重复逻辑、潜在 double-counting、dead code、以及若干 bug。

---

## 第一阶段：修复关键 Bug（优先级 P0）

### 1.1 `UnifiedBuff.TryStackOrRefresh` 不处理 `remainingRounds`

**文件**: [UnifiedBuff.cs:158-180](Assets/Scripts/Game/TribeSystem/UnifiedBuff.cs#L158-L180)

**问题**: `TryStackOrRefresh` 只更新 `remainingDuration`，不处理 `remainingRounds`。当两个相同 `buffId` 的 `TemporaryRoundBased` buff 叠加时，回合数不会刷新。

**修复**: 在三个 `case` 分支中增加 `remainingRounds` 处理：

```csharp
case BuffStackRule.Stack:
    if (currentStacks < maxStacks)
        currentStacks = Mathf.Min(currentStacks + incoming.currentStacks, maxStacks);
    remainingDuration = Mathf.Max(remainingDuration, incoming.remainingDuration);
    remainingRounds = Mathf.Max(remainingRounds, incoming.remainingRounds);  // 新增
    return true;

case BuffStackRule.DurationRefresh:
    remainingDuration = incoming.remainingDuration;
    remainingRounds = incoming.remainingRounds;  // 新增
    return true;

case BuffStackRule.None:
default:
    remainingDuration = Mathf.Max(remainingDuration, incoming.remainingDuration);
    remainingRounds = Mathf.Max(remainingRounds, incoming.remainingRounds);  // 新增
    return true;
```

### 1.2 `ApplyTemporaryStatBoost` 作用域 Bug

**文件**: [RitualService.cs:534-543](Assets/Scripts/Game/TribeSystem/RitualService.cs#L534-L543)

**问题**: 祈祀临时祝福使用 `BuffScopeFilter.All`，给**所有种族全员**加 buff，而不是目标种族。永久祈愿（`ApplyPermanentStatBoost`）正确使用了 `tribe.tribeType` 过滤。

**修复**: 将 `BuffScopeFilter.All` 改为按目标种族过滤：

```csharp
private void ApplyTemporaryStatBoost(TribeRecord tribe, StatType stat, int amount)
{
    float pct = amount / 100f;
    var effects = new List<BuffEffectItem> { new BuffEffectItem(stat, true, pct) };
    string buffId = $"Ritual_Temp_{tribe.tribeType}_{stat}_{amount}";
    string displayName = $"祈愿祝福：{GetStatName(stat)} +{amount}%（3回合）";
    // 修复：限定到目标种族全员，而非 All
    var scopeFilter = new BuffScopeFilter { tribe = tribe.tribeType };
    _auraService?.ApplyRoundBasedBuffToAll(
        scopeFilter, effects, 3,
        displayName, buffId, displayName);
}
```

### 1.3 `TribeStatsCalculator.CalculateLeaderStats` 双轨 double-counting 风险

**文件**: [TribeStatsCalculator.cs:78-86](Assets/Scripts/Game/TribeSystem/TribeStatsCalculator.cs#L78-L86)

**问题**: `CalculateLeaderStats` 先遍历 `ActiveBuffs`（新轨），再应用 `leader.temporaryBuff`（旧轨）。如果同一个 buff 同时存在于两个系统中，会被计算两次。

**修复**: 删除旧轨 `temporaryBuff` 的应用（第 78-86 行）。因为：
- 祭祀临时祝福已改用 `UnifiedBuff.TemporaryRoundBased`
- 旧 `TemporaryBuff` 不再有写入点
- 删除后消除 double-counting 风险

```csharp
// 删除以下代码块（第 78-86 行）：
// 应用限时加成（只有百分比）
if (leader.temporaryBuff != null && leader.temporaryBuff.IsActive())
{
    var t = leader.temporaryBuff;
    atk = ApplyModifiers(atk, t.attackPercent, 0f);
    def = ApplyModifiers(def, t.defensePercent, 0f);
    hp = ApplyModifiers(hp, t.hpPercent, 0f);
    moveSpd = ApplyModifiers(moveSpd, t.speedPercent, 0f);
}
```

---

## 第二阶段：移除旧 `TemporaryBuff` 系统（优先级 P1）

### 2.1 确认无写入点

当前 `TemporaryBuff` 的写入点：
- `RitualService.ApplyTemporaryStatBoost` — **已在 1.2 中修复为 UnifiedBuff**
- `LeaderData` 构造函数中 `temporaryBuff = null` — 初始值

读取点需逐一清理：

### 2.2 清理 `TribeStatsCalculator.cs`

- 删除第 78-86 行的 `temporaryBuff` 应用逻辑（已在 1.3 中完成）

### 2.3 清理 `TribeBuildPanel.cs`

- 删除 `ProcessRoundTransition` 中对 `temporaryBuff.DecreaseDuration()` 的调用
- 文件位置：搜索 `temporaryBuff.DecreaseDuration` 或 `DecreaseDuration`

### 2.4 清理 `TribeCard.cs`

- 删除 `leader.temporaryBuff.IsActive()` 检查和 `AddTemporaryBuffEntry(leader.temporaryBuff)` 调用
- 删除传递 `tribeTemporaryBuff: _tribe.temporaryBuff` 给 `CalculateCatStats` 的参数（该参数已不存在于方法签名中，是编译错误）

### 2.5 清理 `RecruitmentService.cs`

- 删除 `temporaryBuff = null` 初始化（可选，保留也无害）

### 2.6 从 `LeaderData` 移除字段

- 删除 `LeaderData.temporaryBuff` 字段声明（[TribeData.cs:75](Assets/Scripts/Game/TribeSystem/TribeData.cs#L75)）
- 删除构造函数中的 `temporaryBuff = null`（[TribeData.cs:99](Assets/Scripts/Game/TribeSystem/TribeData.cs#L99)）

### 2.7 从 `CardBuff.cs` 移除 `TemporaryBuff` 类

- 删除 `TemporaryBuff` 类定义（[CardBuff.cs:241-271](Assets/Scripts/Game/CardSystem/CardBuff.cs#L241-L271)）

---

## 第三阶段：统一 `BuffService` 实例化模式（优先级 P2）

### 3.1 问题

`new BuffService()` 在 3 处被调用（`AuraService.cs` ×2、`BattleManager.cs` ×1），每次创建新实例。`BuffService` 本身无状态（只持有 `_dataManager` 引用）。

### 3.2 方案：改为静态方法

`BuffService` 的所有方法都不依赖实例状态（`_dataManager` 每次都从 `GameManager.Instance` 获取）。改为静态类：

```csharp
public static class BuffService
{
    // 所有方法改为 static
    public static int RemoveChoiceBuffs(string choiceId) { ... }
    public static int ClearAllBattleBuffs() { ... }
    // ...
}
```

调用处改为 `BuffService.RemoveChoiceBuffs(choiceId)` 即可。

### 3.3 涉及文件

| 文件 | 变更 |
|---|---|
| `BuffService.cs` | 类改 `static`，删除构造函数和 `_dataManager` 字段，方法改 `static` |
| `AuraService.cs:103` | `new BuffService().RemoveChoiceBuffs(...)` → `BuffService.RemoveChoiceBuffs(...)` |
| `AuraService.cs:124` | `new BuffService().RemoveEquipmentBuffs(...)` → `BuffService.RemoveEquipmentBuffs(...)` |
| `BattleManager.cs:215` | `new BuffService().ClearAllBattleBuffs()` → `BuffService.ClearAllBattleBuffs()` |

---

## 第四阶段：消除 `AuraService` 中的 DRY 违规（优先级 P2）

### 4.1 问题

`ApplyEffectsToLeader` 和 `ApplyEffectsToCat` 逻辑几乎完全相同（[AuraService.cs:230-259](Assets/Scripts/Game/TribeSystem/AuraService.cs#L230-L259)），只是目标类型不同。`DataManager` 中的 `ApplyAuraEffectsToLeader` / `ApplyAuraEffectsToCat` 同样如此。

### 4.2 方案

利用 `LeaderData` 和 `CatData` 都实现了 `AddUnifiedBuff(UnifiedBuff)` 方法，提取通用方法：

```csharp
private static void ApplyEffectsGeneric<T>(T unit, List<BuffEffectItem> effects,
    string displayName, string uniqueId, BuffSource source, string description = null)
    where T : class
{
    if (effects == null) return;
    foreach (var eff in effects)
    {
        var buff = UnifiedBuff.CreateStatBuff(
            $"aura_{uniqueId}_{eff.statType}", displayName,
            source, uniqueId,
            eff.statType, eff.isPercent, eff.value,
            gameEffectType: eff.gameEffectType,
            description: description);
        // 使用动态分发或接口
    }
}
```

**但**：C# 2022 不支持对无约束泛型调用 `AddUnifiedBuff`。实际方案：

**方案 A（推荐）**：定义接口 `IHasBuffs`：

```csharp
public interface IHasBuffs
{
    bool AddUnifiedBuff(UnifiedBuff buff);
}
```

`LeaderData` 和 `CatData` 都实现此接口。然后：

```csharp
private static void ApplyEffects(IHasBuffs unit, List<BuffEffectItem> effects,
    string displayName, string uniqueId, BuffSource source, string description = null)
{
    if (effects == null) return;
    foreach (var eff in effects)
    {
        var buff = UnifiedBuff.CreateStatBuff(
            $"aura_{uniqueId}_{eff.statType}", displayName,
            source, uniqueId,
            eff.statType, eff.isPercent, eff.value,
            gameEffectType: eff.gameEffectType,
            description: description);
        unit.AddUnifiedBuff(buff);
    }
}
```

### 4.3 修复 `BuffSource.Equipment` 硬编码

**问题**: `ApplyEffectsToLeader` 和 `ApplyEffectsToCat` 中 `BuffSource` 固定为 `BuffSource.Equipment`，但 choice 来源可能是 `Ritual`、`Artifact` 等。

**修复**: 方法签名增加 `BuffSource source` 参数，由调用方传入。`RebuildAuraBuffs` 中根据 choice 的实际来源设置。

### 4.4 涉及文件

| 文件 | 变更 |
|---|---|
| `TribeData.cs` | `LeaderData` 和 `CatData` 实现 `IHasBuffs` 接口 |
| `AuraService.cs` | 提取 `ApplyEffects` 通用方法，删除 `ApplyEffectsToLeader`/`ApplyEffectsToCat` |
| `DataManager.cs` | 同理提取通用方法 |

---

## 第五阶段：清理旧类型定义（优先级 P3）

### 5.1 `BuffEntry` 类

**文件**: [CardBuff.cs:52-67](Assets/Scripts/Game/CardSystem/CardBuff.cs#L52-L67)

**分析**: `BuffEntry` 使用旧的 `BuffScope` 枚举（Leader/Cat/All），与新的 `BuffScopeFilter` 不兼容。检查所有引用后确认是 dead code（仅在 `CardBuff.cs` 中定义，无外部引用）。

**操作**: 删除 `BuffEntry` 类。

### 5.2 `TribeBuff` 类

**文件**: [CardBuff.cs:72-101](Assets/Scripts/Game/CardSystem/CardBuff.cs#L72-L101)

**分析**: `TribeBuff` 被 `PermanentBuffs.specialBuffs` 引用，而 `PermanentBuffs` 被 `LeaderData` 持有。`TribeCard.cs:411` 使用 `AddInnateBuffEntries(leader.permanentBuffs, font)` 显示天生 buff。

**操作**: **暂不删除**。`TribeBuff` 用于天生被动的 UI 显示（饕餮、牧群领袖等），`PermanentBuffs.specialBuffs` 是其容器。这属于天生被动系统的一部分，不在本次 buff 重构范围内。

### 5.3 `BuffScope` 枚举（旧）

**文件**: [CardBuff.cs:24-29](Assets/Scripts/Game/CardSystem/CardBuff.cs#L24-L29)

**分析**: `BuffScope` (Leader/Cat/All) 仅被 `BuffEntry` 使用。删除 `BuffEntry` 后即可删除。

**操作**: 随 `BuffEntry` 一起删除。

### 5.4 `BuffApplyScope` 枚举（已标记 Obsolete）

**文件**: [ChoiceSystem.cs](Assets/Scripts/Game/TribeSystem/ChoiceSystem.cs)

**分析**: 已标记 `[Obsolete]`，保留用于存档兼容。`GameChoice.GetScopeFilter()` 通过 `FromLegacy()` 迁移旧数据。

**操作**: **暂不删除**。等存档格式稳定后再清理。可在枚举注释中标注"存档兼容，可在 v2.0 删除"。

---

## 第六阶段：优化 `TribeStatsCalculator` 属性累加（优先级 P3）

### 6.1 问题

`CalculateLeaderStats` 和 `CalculateCatStats` 中的 buff 累加逻辑高度相似（遍历 `ActiveBuffs`，按 `statType` + `isPercent` 分桶累加），但分别实现。

### 6.2 方案

提取通用的 buff 累加结构：

```csharp
private struct StatAccumulator
{
    public float atkFlat, atkPct;
    public float defFlat, defPct;
    public float hpFlat, hpPct;
    public float spdFlat, spdPct;
    public float atkSpdFlat, atkSpdPct;

    public void Accumulate(UnifiedBuff buff)
    {
        float totalVal = buff.value * buff.currentStacks;
        switch (buff.statType)
        {
            case StatType.Attack:
                if (buff.isPercent) atkPct += totalVal; else atkFlat += totalVal; break;
            case StatType.Defense:
                if (buff.isPercent) defPct += totalVal; else defFlat += totalVal; break;
            case StatType.Hp:
                if (buff.isPercent) hpPct += totalVal; else hpFlat += totalVal; break;
            case StatType.MoveSpeed:
                if (buff.isPercent) spdPct += totalVal; else spdFlat += totalVal; break;
            case StatType.AttackSpeed:
                if (buff.isPercent) atkSpdPct += totalVal; else atkSpdFlat += totalVal; break;
        }
    }
}
```

然后 `CalculateLeaderStats` 和 `CalculateCatStats` 都使用 `StatAccumulator`，消除重复代码。

---

## 第七阶段：清理 `BattleManager` God Class（优先级 P3）

### 7.1 问题

`BattleManager` ~880 行，承担了战斗编排 + buff 生命周期管理 + 诊断日志等多个职责。

### 7.2 方案

将 buff 相关方法提取到独立服务：

```csharp
public static class BattleBuffService
{
    public static void RestorePersistentBuffsToRuntime(BattleFighter[] fighters) { ... }
    public static void SyncPersistentBuffsToLeaderData(BattleFighter[] fighters) { ... }
}
```

从 `BattleManager` 中移除这两个方法，改为调用 `BattleBuffService`。

### 7.3 清理诊断日志

`BattleManager` 和 `BattleSpawner` 中有多处 `Debug.Log` 诊断块（如 `[CreateFighter]` 日志、`[BattleManager]` buff 日志）。评估后删除或改为条件编译：

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[BattleManager] ...");
#endif
```

---

## 第八阶段：清理 `DataManager.RebuildAuraBuffs`（优先级 P3）

### 8.1 问题

`RebuildAuraBuffs` 中 `ApplyAuraEffectsToLeader` / `ApplyAuraEffectsToCat` 与 `AuraService` 中的同名方法逻辑重复。

### 8.2 方案

`DataManager.RebuildAuraBuffs` 改为调用 `AuraService` 的方法，而非自行实现：

```csharp
private void RebuildAuraBuffs()
{
    // ... 收集 auraEntries 不变 ...
    var auraService = new AuraService();
    foreach (var tribe in _playerData.tribes)
    {
        if (tribe == null || !tribe.isActive) continue;
        foreach (var entry in auraEntries)
        {
            var filter = entry.GetScopeFilter();
            if (tribe.leader != null && filter.Matches(true, tribe.tribeType, null))
            {
                auraService.ApplyEffectsToLeader(tribe.leader, entry.buffEffects, ...);
            }
            // ... cats ...
        }
    }
}
```

或更彻底：`AuraService` 提供 `RebuildAllAuraBuffs()` 方法，`DataManager` 直接调用。

---

## 实施顺序总结

| 步骤 | 阶段 | 预估改动 |
|---|---|---|
| 1 | 1.1 TryStackOrRefresh 修复 | 1 文件，~6 行 |
| 2 | 1.2 ApplyTemporaryStatBoost scope 修复 | 1 文件，~3 行 |
| 3 | 1.3 + 2.2-2.7 移除 TemporaryBuff | 5 文件，~30 行删除 |
| 4 | 3.1-3.3 BuffService 改静态 | 4 文件，~20 行改动 |
| 5 | 4.1-4.4 AuraService DRY 修复 | 3 文件，~40 行重构 |
| 6 | 5.1+5.3 删除 BuffEntry + BuffScope | 1 文件，~20 行删除 |
| 7 | 6.1-6.2 StatAccumulator 提取 | 1 文件，~30 行重构 |
| 8 | 7.1-7.3 BattleManager 提取 | 2 文件，~80 行移动 |
| 9 | 8.1-8.2 DataManager 去重 | 2 文件，~30 行重构 |

每步完成后应在 Unity 编辑器中验证无编译错误。

---

## 验证清单

- [x] Unity 编译无错误
- [x] 祭祀临时祝福只给目标种族全员（非 All）
- [x] 祭祀临时祝福 3 回合后正确消失
- [x] 光环 buff 只对匹配 scope 的单位生效
- [x] 战斗开始时 Persistent + TemporaryRoundBased buff 正确恢复到 RuntimeAttributes
- [x] 战斗结束时 persistent buff 正确同步回 LeaderData
- [x] 战斗结束时 BattleOnly buff 被清除
- [x] 旧存档能正常加载（BuffApplyScope 自动迁移）
- [x] 族长天生被动 UI 显示正常
