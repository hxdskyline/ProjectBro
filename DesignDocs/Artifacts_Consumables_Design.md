# 奇物（Artifacts）与一次性道具（Consumables）系统设计

目的
- 定义奇物与消耗品的类型、数据结构、装备规则、生命周期、与战斗/游历/传习的交互点。

一、概念与分类
- 奇物（Artifacts）：可装备到猫身上的永久或有耐久的道具，提供被动或触发式效果（如提高外出掉落率、增加技能继承概率、战斗中属性加成等）。
- 一次性道具（Consumables）：消耗即生效，分为准备阶段道具（Preparation）与战斗阶段道具（InBattle）。

二、数据模型（补充 `artifact_table.json` 与新增 `consumable_table.json`）
- ArtifactRecord (表中的定义)
  - `id`, `key`, `name`, `quality` (common/rare/epic)
  - `effectType` (string)
  - `effectValue` (number|object)
  - `durability` (int|null)
  - `stackable` (bool)
  - `description` (string)
- PlayerArtifactInstance (持久化)
  - `instanceId` (long)
  - `artifactId` (int)
  - `ownerCatId` (long|null)
  - `remainingDurability` (int|null)
  - `acquiredAt` (timestamp)
- ConsumableRecord
  - `id`, `key`, `name`, `type` (Preparation|InBattle), `cost`, `effectPayload`, `cooldownSec`, `stackLimit`

三、效果类型示例（`effectType`）
- `battle_attack_pct`：在战斗中提升攻击力百分比
- `energy_max_add`：增加最大精力
- `outing_artifact_chance`：提高外出带回奇物概率
- `training_cost_pct`：降低传习消耗
- `skill_inherit_bonus`：提高技能继承概率
- `on_death_revive_once`：死亡时有概率保留 1 HP（触发型）

四、装备与使用规则
- 每只猫可装备 0 或 1 件奇物（可扩展为多槽位）
- 装备奇物会立即生效（如修改 stats），并在对应场景（战斗/游历）应用其效果
- 耐久性：若 `remainingDurability` 为数值，相关行为（触发/战斗次数/回合数）会消耗耐久，耐久为 0 则销毁并触发 `OnArtifactBroken` 事件
- 可堆叠性：部分奇物为可堆叠（如提供临时增益的符咒），以 `stackLimit` 控制最大堆数

五、道具使用流程
- 准备阶段道具：在构筑/准备界面使用，立即调整目标猫或全队状态（例如恢复精力、增加临时祝福）
- 战斗阶段道具：战斗中使用，接口需通过 `Simulation.UseConsumable(catId, consumableId)` 来调度并同步冷却
- 冷却与限制：为避免滥用，InBattle 道具可设置冷却；Preparation 道具设置可每日/回合使用次数限制

六、API（Manager）
- ArtifactManager
  - EquipArtifact(catId, artifactInstanceId) -> bool
  - UnequipArtifact(catId) -> bool
  - CreateArtifactInstance(artifactId) -> PlayerArtifactInstance
  - GetPlayerArtifacts() -> list
- ConsumableManager
  - UseConsumable(playerId, targetId, consumableId) -> UseResult
  - GetConsumableCooldown(consumableId)

七、与其他系统的钩子
- `OutingManager`：在结算时计算掉落时考虑装备奇物的 `outing_artifact_chance`
- `BreedingService`：在继承概率计算时，考虑装备/祝福修改 `skill_inherit_bonus`
- `TrainingManager`：在计算总能量消耗时，考虑 `training_cost_pct` 的减免
- `Simulation`：在战斗循环中查询 `battle_*` 类型奇物与一次性道具效果

八、UI/交互
- 装备界面：在 `CatDetailPanel` 中展示当前装备、剩余耐久、卸下与替换按钮
- 道具栏：准备阶段与战斗阶段分离显示，战斗中道具按钮在 HUD 上短时间展示并带冷却遮罩
- 文案：所有效果需有简短描述与详情 Tooltip（概率/持续回合/冷却）

九、持久化与交易
- 奇物实例需写入 `PlayerData.artifacts`，支持卖出/拆解（拆解产出部分材料或猫粮）
- 道具可在商店中购得或战斗/游历中掉落

十、测试与 QA
- 针对影响概率的奇物（如提高继承率），需做抽样测试（>=10k 次）并导出结果到 CSV
- 针对耐久道具，测试耐久触发/破碎的准确性与事件通知

十一、平衡与策划工具
- 提供 `ArtifactEffectTester` 的 Debug 界面，输入参数模拟多轮战斗/游历，查看效果覆盖面
- 导出表格模板供策划填写 `effectType` 与 `effectValue` 的语义含义

十二、验收标准
- 装备/使用流程在 UI 中可执行并在 `PlayerData` 中正确反映；奇物效果在相应系统（战斗/游历/传习）中按定义应用并可被 QA 抽样验证。

实现备注
- 建议路径：`Assets/Scripts/Game/CatSystem/ArtifactManager.cs`, `ConsumableManager.cs`，UI Prefabs：`Assets/Prefabs/UI/CatSystem/ArtifactSlot.prefab`。
