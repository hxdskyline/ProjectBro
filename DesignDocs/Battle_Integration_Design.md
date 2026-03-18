# 战斗集成：精力与死亡规则设计文档

目标
- 明确在 `BattleManager` 生命周期中如何处理猫的精力消耗、死亡判定、挡灾（Last Stand）接口、外出生效时机以及与 `DataManager` 的持久化点，保证战斗与持久玩法之间的一致性与可测试性。

一、核心原则
- 精力（Energy）与生命（HP）为两套独立资源；战斗开始时会有精力扣除规则（如外出、生效消耗），战斗中死亡会检查精力并可能导致永久死亡。
- 所有可能引起永久性改变（如猫永久死亡、献祭、弃猫）均需二次确认或在关键点写入持久化日志，保证回滚或补偿路径。

二、关键数据字段（推荐写入 `CatRecord.flags` 或 `PlayerData`）
- `isDeployed` (bool)
- `isOutingRequested` (bool)
- `isOutingActive` (bool)
- `hasUsedLastStand` (bool) // 挡灾次数标记（可为全局或玩家级）
- `deadPermanently` (bool)

三、精力扣除时机与规则
- 三类精力扣除触发点：
  1. 出征前（Begin Deployment）验证并扣除：对标记为 `isOutingRequested` 的猫在战斗开始时扣除 `outing_config.energyCostPerPair`（按对分配）。
  2. 参战精力消耗：每只参战猫在开始战斗时消耗固定 `battleParticipationEnergy`（建议 15），用于表示战斗疲劳。
  3. 死亡额外消耗：若单场战斗中死亡，额外扣除 `deathEnergyPenalty`（建议 30）；若扣除后精力 <= 0 则标记 `deadPermanently=true` 并从 `CatRoster` 中移除（或标为死亡状态，视策划决定）。

四、死亡处理流程
1. 单位在 Simulation 中 HP <= 0 触发 `OnUnitDeath(unitId, cause)` 回调
2. 若是玩家猫：先执行战斗内死亡结算（播放死亡动画、掉落效果）
3. 检查 `hasUsedLastStand`：
   - 若玩家尚有可用挡灾（`PlayerData.lastStandCount > 0`），并玩家选择使用或系统自动触发（可为消耗品/祝福触发），则消耗一次挡灾，单位恢复到 1 HP 并标记不扣除永久死亡能量（或按策划定义）
4. 若挡灾未使用或不可用：
   - 执行 `ApplyDeathEnergyPenalty(catId)`，若 `energy <= 0` 则 `deadPermanently=true`
   - 将 `isDeployed=false`、remove from current wave spawn list 等，以便下波替补
5. 战斗结束后（Victory/Defeat）统一写入 `PlayerData`：更新每只参战猫的 `energy`、`deadPermanently` 标志与可能的 `children`/`artifacts` 增加等

五、外出（Outing）生效与结算时序
- 外出请求在准备阶段可撤回（Pending）。当战斗正式开始时，`OutingManager.OnBattleStart()` 被调用：
  - 对 Pending Requests 进行生效验证与精力扣除（atomic），将其状态设为 `Active` 并计算 `returnCycle`
  - 若在生效时某猫已参战或精力不足，该对将取消并写入错误原因
- 返回结算在 `GameCycle.Tick()` 或 `BattleManager.OnBattleEnd()` 里触发（按设计选择）：
  - 推荐：在 `BattleManager.OnBattleEnd()` 调用 `OutingManager.ProcessReturns(currentCycle)`，这样能确保外出结果与战斗奖励同时展示并写入同一次持久化事务

六、挡灾（Last Stand）策略
- 挡灾计数存于 `PlayerData.lastStandCount` 或作为消耗品
- UI：在战斗准备界面显示剩余挡灾次数，战斗内在猫死亡时弹出使用确认（若允许玩家选择），或自动使用（按设置）
- 触发后效果：恢复至 1 HP、清除负面效果（可选）、消耗一次挡灾计数；记录 `OnLastStandUsed(catId, battleId)` 事件

七、持久化与事务
- 推荐事务顺序：
  1. 计算/验证所有变更（死亡、精力扣减、外出生效）
  2. 在内存更新 PlayerData
  3. 写入 `DataManager.SavePlayerData()`（节流/异步，但需保证写入成功回调前不丢失关键事务记录）
  4. 若写入失败，记录事务日志以便补偿
- 保存点建议：
  - `StartBattle`（在外出生效前做一次保存）
  - `OnBattleEnd`（结算结束后做一次保存，包含外出返回、奖励、死亡、精力变更）

八、API 与接口（`BattleManager` 扩展）
- OnBattleStart(levelId)
  - ValidateAndActivateOutings()
  - DeductParticipationEnergyForDeployed()
- OnUnitDeath(catId, cause)
  - HandleLastStandOrPermanentDeath(catId)
- OnBattleEnd(result)
  - ProcessOutingReturns(currentCycle)
  - PersistBattleResultsToPlayerData()

九、测试与 QA
- 单元测试：死亡能量扣除逻辑、挡灾触发与计数、外出在战开始时的原子生效
- 集成测试：模拟整场战斗（含多波）并在 `OnBattleEnd` 校验 `PlayerData` 中的精力与死亡标志一致
- 抽样测试：在 N=1000 场战斗模拟中统计永久死亡发生率，验证与策划设定一致（用于平衡）

十、兼容性与实现备注
- 保持现有 `BattleManager` 的接口兼容性，新增 `OnBattleStart/OnBattleEnd` 钩子以便外部模块订阅
- 推荐使用可注入的时间/随机提供者以便测试与复现
- 记录详细日志：`BattleLog { battleId, levelId, deployedCatIds, deaths[], lastStandUsed[], rewards[], seed }`

验收标准（简明）
- 战斗开始/结束的精力变更与死亡标志在 `PlayerData` 中正确反映；外出生效原子且在战后结算一次性写入；挡灾使用能阻止一次永久死亡并被正确记录。