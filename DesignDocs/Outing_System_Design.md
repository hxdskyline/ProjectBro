# 游历（Outing）系统设计文档

目标
- 详细定义游历的请求、结算、概率、与与战斗/存档的交互点，提供接口与验收测试。

一、概念回顾
- 游历以「配对」为单位（父/母对），玩家可同时派出最多 `maxPairs` 对（由 `outing_config.json` 控制）。
- 在战斗开始生效，游历消耗精力并在若干回合后返回，返回时可能带回子代和/或奇物。

二、数据结构
- OutingRequest
  - `requestId` (long)
  - `pairs` (array of [long catIdA, long catIdB])
  - `initiatedCycle` (int)
  - `returnCycle` (int) // 计算为 initiatedCycle + random(minReturnDelayCycles, maxReturnDelayCycles)
  - `status` (enum: Pending, Active, Returned, Cancelled)

- OutingResult
  - `requestId` (long)
  - `pair` ([catIdA, catIdB])
  - `children` (CatRecord[]) 已生成的子代实例
  - `artifacts` (int[]) 掉落的奇物 id 列表
  - `notes` (string) 调试日志

三、配置（引用现有）
- 使用 `StreamingAssets/Tables/outing_config.json`：包含 `energyCostPerPair`、`childCountProb`、`childMultiplierRange`、`artifactProbByQuality`、`guaranteeArtifactOnZeroChildren` 等。

四、生命周期与流程
1. 发起请求（准备界面）
   - 校验：玩家有足够空位（`maxPairs`）、所选猫当前非上阵/非已外出/非死亡、并且未超过每猫可外出冷却（可选）
   - 创建 `OutingRequest`，标记猫 `flags.isOutingRequested = true`，UI 显示“已安排外出（待生效）”
2. 在玩家点击“开始战斗”（或战斗真正开始时）
   - 生效校验：再次验证猫的精力是否足够（`energy >= energyCostPerPair`），若不足则该对取消并记录失败原因（并可在 UI 显示退回/成功提示）
   - 扣除精力：对通过验证的每对扣除 `energyCostPerPair`，将状态 `Pending -> Active`，设置 `returnCycle`
3. 结算（每个游戏回合结束时或专门的周期驱动）
   - 当 `currentCycle >= returnCycle` 将该 `OutingRequest` 状态标为 `Returned` 并触发 `OutingResult` 生成流程
4. 返回处理
   - 生成子代数量：根据 `childCountProb` 随机抽取 0/1/2
   - 子代属性生成：调用 Breeding 模块（见繁殖设计）生成子代 `CatRecord`
   - 掉落奇物：按 `artifactProbByQuality` 随机决定品质并从 `artifact_table.json` 池中挑选
   - 如果 `0` 子代且 `guaranteeArtifactOnZeroChildren==true`，强制至少掉落一件奇物
   - 把 `OutingResult` 写入 `PlayerData`，并触发 UI 通知（例如弹窗显示结果）

五、API（`OutingManager`）
- RequestOuting(List<[catA,catB]> pairs) -> (success, requestId or error)
- CancelOuting(requestId) -> bool (only allowed while Pending)
- OnBattleStart() -> internal: validate and activate pending requests
- TickCycle(currentCycle) -> process return of Active requests
- GetPendingRequests(), GetHistory()

六、UI 交互
- 在 `CardBuildPanel` 增加“外出区”展示、撤回按钮、预计返回回合与预估奖励（概率提示）
- 在结算时弹出 `OutingResultPanel` 展示每对的结果（子代缩略图、奇物、详细按钮跳转到 `CatDetailPanel`）

七、容错与边界
- 发起后在战前可撤回：撤回时将 `flags.isOutingRequested=false` 并删除请求（无消耗）
- 生效时若猫在战斗中死亡或在生效前被弃置：该对取消并可能触发额外惩罚（例如资源损失），策略由策划决定
- 多重并发：确保 `OutingManager` 的请求在 Activate 时以原子操作检查并扣除精力，避免竞态

八、日志与 QA
- 输出完整调试日志：requestId, pairIds, initiatedCycle, returnCycle, randomSeed, chosenChildCount, chosenArtifacts
- 提供 Debug 命令：强制触发返回、强制生成特定 childCount，用于 QA 抽样

九、验收标准
- 发起外出->战后结算流程在本地正常工作，子代/奇物按照 `outing_config.json` 的概率分布产生并写入 `PlayerData`；在 1k 次抽样测试下统计分布与设定误差 <5%。

十、实现备注
- `OutingManager` 推荐路径：`Assets/Scripts/Game/CatSystem/OutingManager.cs`
- 使用 `DataManager` 保存 `PlayerData.outingRequests` 与 `PlayerData.outingHistory`
- 对随机性使用可注入的 `IRandomProvider`，以便测试覆盖


