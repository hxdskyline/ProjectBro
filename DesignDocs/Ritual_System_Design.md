# 祭祀系统设计文档

目的
- 细化 `DesignDocs/Gameplay_Design_for_Designers_v2.md` 中的祭祀功能，明确数据、交互、权重、实现接口、边界条件与验收标准，便于开发和策划对接。

概述
- 祭祀为周期性深度玩法节点（按回合或特定关卡触发），玩家可以献祭猫粮、奇物或猫本身以换取强力奖励或祝福。祭祀为高风险/高回报设计，涉及永久性资源变动（献祭猫）与临时/长期增益（祝福、奇物、属性提升）。

核心术语
- 献祭项（Offer）：可为 `food` / `artifact` / `cat`。
- 选项池（OptionPool）：每种献祭类型对应若干候选奖励池（例如 `ritual_food_rewards` 等）。
- 祝福（Blessing）：随机或权重产生的效果，可能为一次性战斗增益或长期属性提升。
- 祭祀事件（RitualEvent）：一次玩家执行祭祀的完整事务，包含选择、付出、结算与记录。

数据模型
- RitualConfig（已在 `StreamingAssets/Tables/ritual_config.json`，可扩展）
  - `allowedRounds` (int[]) 可触发祭祀的回合
  - `options` (array) 每项包含 id/label/minCost/minQuality/rewardPool
  - `blessingPool` (string)
  - `guaranteedBlessingChance` (float)
  - `specialEventPool` (string|null) // 可选，用于节日/限定祭祀

- RitualRequest
  - `requestId` (long)
  - `playerId` (string)
  - `round` (int)
  - `offerType` (string: "food"|"artifact"|"cat")
  - `offerPayload` (object) // { amount:int } 或 { artifactInstanceId } 或 { catId }
  - `selectedOptionId` (string) // 策划 UI 选择的献祭子项
  - `timestamp` (long)

- RitualResult
  - `requestId` (long)
  - `rewards` (array of RewardEntry)
  - `grantedBlessing` (Blessing|null)
  - `log` (string)

- RewardEntry
  - `type` (string: "cat"|"artifact"|"currency"|"buff")
  - `payload` (object)

交互流程
1. 触发入口
   - 在允许回合（`RitualConfig.allowedRounds`）弹出祭祀入口或在城镇/主界面展示可进入的 `RitualPanel`。
2. 选择献祭类型与具体投入
   - 玩家在界面上选择 `food`/`artifact`/`cat`，系统校验最小投入（`minCost` / `minQuality` / `minRarity`）和是否允许献祭该猫（不可献祭处于上阵/外出/保护中的猫）。
   - UI 展示每个选项可获得的奖励池预览与概率（大致提示，不需暴露全部权重），并提示永久性风险（献猫）
3. 确认与扣除
   - 玩家二次确认，系统扣除资源或移除猫（若是献祭猫则永久删除并写日志）。
4. 结算
   - 根据 `selectedOption` 的 rewardPool 抽取若干 `RewardEntry`（数量与权重由 rewardPool 定义），同时按 `guaranteedBlessingChance` 决定是否发放额外祝福。
   - 若奖励含猫，使用 `BreedingService` 或 `CatFactory` 生成新 `CatRecord`（注意 id 唯一）。
5. 展示结果
   - 弹出 `RitualResultPanel`，清晰展示消耗与获得（强调被献祭的永久性改变），并提供“查看新增猫”/“装备奇物位置”跳转选项。

rewardPool 说明（策划侧）
- rewardPool 为键，指向一组权重条目，例如：
  - `ritual_food_rewards`: [ {type: "artifact", id:101, weight:40}, {type:"currency", amount:500, weight:60} ]
- 奖励条目可包含保底规则（例如必定给一个 currency 或 至少一个低品质物品）。

祝福（Blessing）体系
- Blessing 包含：id / name / effectType / effectValue / durationRounds（若为临时） / persistent (bool)
- 示例：`{ id: "bless_hp_up", effectType:"hp_pct", effectValue:0.12, durationRounds:3, persistent:false }`
- 永久祝福需明确标注并写入 `PlayerData.blessings`（带有效期或无限期字段）。

校验与边界
- 献祭猫必须弹出二次确认并要求输入玩家密码或勾选框（防止误触）。
- 若玩家在结算前断线或程序崩溃，系统应保证服务器/本地事务性：写入 `PlayerData` 的步骤应采用事务式顺序（先扣资源，再写奖励，再写日志），并记录 `RitualRequest` 的状态以支持恢复。
- 若 rewardPool 中有生成猫但生成失败（异常），应回滚并给玩家补偿或标记为异常待补发。

API 设计（`RitualManager`）
- GetAvailableRituals(round) -> RitualConfig
- CreateRitualRequest(RitualRequest) -> (success, requestId|error)
- ExecuteRitual(requestId) -> RitualResult
- GetRitualHistory(playerId) -> RitualResult[]
- DebugForceGenerate(requestId, seed) // QA 用

UI 细节与文案建议
- `RitualPanel`：左侧选择献祭类型与投入，内容区展示 rewardPool 的简要预览与风险提示，右侧展示历史记录。底部有“确认献祭（需要再次确认）”按钮
- `RitualResultPanel`：显示“你献祭了 X，获得 Y”，若有祝福则展示祝福详情与持续回合
- 文案须清晰提示永久性改动（例如："献祭猫将永久失去该猫"）

日志与可追踪性
- 每次祭祀写入操作日志：`RitualLog { requestId, playerId, offerType, offerPayload, selectedOptionId, rewards, timestamp, seed }`
- 后台/QA 可导出日志以便统计奖励分布、检测异常

测试与验收
- 单元测试：rewardPool 的抽样分布（统计 N=10k 次，分布误差 <5%）
- 集成测试：献猫流程（含 UI 确认、存档写入、断点恢复）
- 安全测试：防止重复提交（幂等性校验）、事务失败回滚

兼容性与实现备注
- `ritual_config.json` 已存在基本字段，请在 `StreamingAssets/Tables/ritual_config.json` 添加 `rewardPools` 引用或单独文件 `StreamingAssets/Tables/ritual_reward_pools.json`
- 推荐实现位置：`Assets/Scripts/Game/CatSystem/RitualManager.cs` 与 UI Prefab `Assets/Prefabs/UI/CatSystem/RitualPanel.prefab`
- 强烈建议：在 `DataManager` 中为 `RitualRequest` 添加持久化记录，字段名 `PlayerData.ritualHistory`

验收标准（简明）
- 玩家能在允许回合正常发起祭祀并完成结算，奖励与祝福按配置生效；献祭猫的永久删除应在结果面板明确展示并能在 `RitualHistory` 中查到记录；QA 在 1k 次抽样中能验证奖励分布与设定一致。


