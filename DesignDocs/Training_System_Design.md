# 传习系统（Training）设计文档

目的
- 定义教师向学生传授技能的业务规则、数据模型、UI 流程、校验与接口，确保与 `CatManager` / `DataManager` / `CatRoster` 集成。

一、核心规则概览
- 教师消耗精力按 `energyCostPerStudent` 计（配置项，默认 5）。
- 每次传习为一次事务（在准备阶段），教师最多向 `maxStudentsPerSession`（配置，默认 10）名学生传授同一技能。
- 学习必定成功（若学生已满技能槽，则触发替换流程，由玩家选择替换或自动按规则替换）。
- 每轮准备阶段仅允许执行一次传习操作（可配置）。

二、数据模型
- TrainingRequest
  - `requestId` (long)
  - `teacherId` (long)
  - `skillId` (int)
  - `studentIds` (long[])
  - `timestamp` (long)
  - `autoReplace` (bool) // 若 true 则自动替换随机技能
- TrainingResult
  - `requestId` (long)
  - `teacherId` (long)
  - `studentResults` ([ { studentId, addedSkillId, replacedSkillId|null, success:boolean } ])
  - `teacherEnergyConsumed` (int)

三、配置项（建议放 `StreamingAssets/Tables/training_config.json`）
- `energyCostPerStudent` (int)
- `maxStudentsPerSession` (int)
- `allowAutoReplace` (bool)
- `defaultReplacePolicy` (string: "oldest"|"lowest_power"|"random")

四、UI 流程
1. 入口：`RosterPanel` 或单独 `TrainingPanel` 打开传习界面
2. 选择教师（展示当前精力、技能列表）
3. 选技能（若教师没有技能则按钮变灰）
4. 选择学生（支持多选、分页），面板右侧显示总精力消耗与确认按钮
5. 若学生已满技能槽，弹出替换选择（对每名学生独立）或使用自动替换策略（批量模式）
6. 确认后执行传习，显示 `TrainingResultPanel`（逐学生显示新增技能与被替换的技能）

五、校验与边界
- 教师精力检查：在确认前计算所需精力（studentsCount × energyCostPerStudent），若不足则禁止提交并提示
- 学生状态检查：学生若处于外出/上阵/死亡则不可被选为学生
- 并发安全：在执行过程中锁定教师的相关标记，防止重复提交或并发修改

六、接口（`TrainingManager`）
- CanStartTraining(teacherId, students[]) -> (bool, errorMsg)
- SubmitTraining(TrainingRequest) -> TrainingResult
- GetTrainingHistory(playerId) -> TrainingResult[] // 用于展示记录

七、事件与持久化
- 事件：`OnTrainingCompleted(TrainingResult)`，`OnTrainingFailed(requestId, reason)`
- 结果持久化：更新 `CatRecord.skills` 与 `CatRecord.energy`，并触发 `CatManager.PersistChanges()`（节流）

八、测试与 QA
- 单元测试：验证能量计算、替换策略（oldest/lowest_power/random）正确性
- 集成测试：模拟教师向 10 名学生传授技能（包含满槽与非满槽），验证所有 `studentResults` 一致
- 边界测试：教师精力不足、学生在外出中的拒绝、重复提交保护

九、扩展点与平衡钩子
- 奇物可修改传习消耗或继承概率（例如 `artifact.effectType == "training_cost_pct"`）
- 某些祝福或活动可临时改变 `maxStudentsPerSession` 或 `energyCostPerStudent`

十、验收标准
- 传习界面可完成教师选取、学生多选、替换策略选择并保存结果；在 1000 次自动化抽样中替换策略产出符合预期分布。

实现备注
- 建议类名：`TrainingManager.cs`、UI Prefab：`Assets/Prefabs/UI/CatSystem/TrainingPanel.prefab`。 
- 随机/自动替换策略应可由策划配置并在 UI 中可见。
