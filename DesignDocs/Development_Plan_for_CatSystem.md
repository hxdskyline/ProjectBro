# 猫猫系统开发计划（开发用，分解任务）

目标：落地 `DesignDocs/Gameplay_Design_for_Designers_v2.md` 中的新需求，按模块分解实现、测试与交付，并给出时间预估、交付物与验收标准。

参考文档：[Gameplay_Design_for_Designers_v2.md](DesignDocs/Gameplay_Design_for_Designers_v2.md)

里程碑与任务清单

- 里程碑 A（准备与表结构）：定义所有表/JSON、示例文件、序列化格式（1 周）
  - 任务 1：定义数据表与 JSON 示例（species_table, outing_config, artifact_table, ritual_config, traits_table, skills_table）
    - 交付：示例 JSON（位于 StreamingAssets/Tables/*.json）与字段文档
    - 验收：示例被 DataLoader 正确读取并反序列化（单元测试通过）

- 里程碑 B（后端/存储与模型）：扩展 DataManager 与 Cat 数据模型（1 周）
  - 任务 2：扩展存储与 DataManager（持久化 parents/children、artifacts、outingRequests）
    - 交付：DataManager 保存/加载回归测试
    - 验收：保存后重启游戏能完整恢复 CatRoster 与 Outing 状态

- 里程碑 C（核心玩法实现）：游历、繁殖、传习、奇物（2~3 周）
  - 任务 3：实现猫模型与花名册 UI（Roster、详情面板、筛选/排序）
  - 任务 4：游历（Outing）系统开发（请求、结算、结果生成）
  - 任务 5：繁殖/子代生成逻辑（继承规则、随机与上限裁剪）
  - 任务 6：传习（Training）系统（教师与学生流程、精力消耗）
  - 任务 7：奇物与一次性道具（表、装备槽、战斗/准备阶段效应）
    - 交付：各系统集成测试场景（示例流程：发起游历→进行战斗→结算并返回子代/奇物）
    - 验收：示例流程在 QA 环境可运行并符合概率统计预期（抽样测试）

- 里程碑 D（支撑系统与集成）：祭祀、商店、事件、战斗集成（1.5 周）
  - 任务 8：祭祀系统（献祭面板、奖励发放规则）
  - 任务 9：商店与随机事件（刷新逻辑、事件池）
  - 任务 10：战斗集成（在 BattleManager 中加入精力扣除、死亡处理、挡灾接口）
    - 验收：战斗开始/结束流程中能正确处理外出生效、精力扣除与死亡移除

- 里程碑 E（UI 完善、工具与平衡）：面板优化、概率可视化、平衡表（1 周）
  - 任务 11：UI 面板与交互细化（传习界面、祭祀界面、游历管理）
  - 任务 12：QA 工具与概率可视化（Debug 菜单、抽样统计工具）
  - 任务 13：平衡表与数值模板（Excel/CSV，含默认值与说明）

- 里程碑 F（收尾）：文档、本地化与发布准备（0.5 周）
  - 任务 14：文档、示例 JSON 与本地化（所有新文案的本地化条目）

责任分配（建议）
- 系统设计 & 表结构：策划 + 技术设计工程师（1 人）
- 后端/持久化：1 人（DataManager 改动）
- 核心玩法逻辑（Outing、Breed、Training、Artifact）：2 人
- UI / UX：1 人
- QA / 概率统计工具：1 人

时间预估（总计）：约 7–9 周（可并行多个任务以缩短真实排期）

详细任务拆分（每项包含验收标准与示例交付物）

1) 定义数据表与 JSON 示例（当前进行中）
- 内容：确定字段、示例值、默认值与容错逻辑。
- 交付：
  - StreamingAssets/Tables/species_table.json（示例）
  - StreamingAssets/Tables/outing_config.json（示例）
  - StreamingAssets/Tables/artifact_table.json（示例）
- 验收：示例 JSON 能被现有 TableReader 解析并打印样例记录。

2) 扩展 DataManager（持久化）
- 内容：新增 CatRoster 保存结构、OutingRequests 列表、Artifact 与 Consumable 列表。
- 实作要点：向 `PlayerData` 加字段，增加数据迁移函数（兼容旧存档）；写入操作需节流（例如 5s 最小间隔）。
- 验收：做一次保存/重启恢复测试，数据完全一致。

3) 猫模型与花名册 UI
- 内容：实现 `CatRecord` 运行时模型、Roster 面板、详情查看与改名一次性操作。
- 交付：UI 面板 prefab、筛选/排序接口、单元测试用 Mock 数据。
- 验收：策划能通过 UI 查看/筛选/改名并保存生效。

4) 游历（Outing）系统
- 内容：申请、取消、入队、结算（战后触发）的完整逻辑与概率表。
- 交付：OutingRequest API、结算器、示例概率表。
- 验收：在集成测试中发起外出并在战后以预期概率返回结果且写入玩家库存。

5) 繁殖/子代生成
- 内容：实现子代生成算法（属性继承、特质/技能继承概率、种族判定）、稀有性处理。
- 交付：生成函数、概率参数表、生成日志模式便于统计。
- 验收：在 10k 次抽样测试下，继承率与策划期望基本一致（误差在可接受范围内）。

6) 传习（Training）系统
- 内容：教师选择、学生选择、精力消耗、技能替换流程。
- 交付：TrainingRequest/Result API、UI Draft、精力校验逻辑。
- 验收：单次训练消耗正确、技能插入或替换按规则执行。

7) 奇物与一次性道具
- 内容：装备槽、奇物效果框架、耐久度处理、道具使用接口（准备/战斗）
- 交付：artifact_table.json、Consumable 表、示例奇物 prefab
- 验收：装备奇物能改变战斗/游历/传习结果（示例校验场景）

8) 祭祀系统
- 内容：祭祀触发面板、献祭类型与奖励池、祝福生成逻辑
- 交付：ritual_config.json、UI 面板、奖励分发逻辑
- 验收：祭祀流程触发、献祭结果与文案一致并写入日志

9) 商店与随机事件
- 内容：商店刷新规则、事件池实现、流浪猫三选一流程
- 交付：shop_table.json、event_pool.json、购买/刷新逻辑
- 验收：刷新计费正确、事件按概率触发且被记录

10) 战斗集成（精力/死亡）
- 内容：在 `BattleManager` 生命周期中加入精力扣除、死亡判定与挡灾 Hook
- 交付：修改点 PR、回归测试场景（战斗胜利/失败/死亡）
- 验收：战斗结束后精力/死亡状态与预期一致，外出结算在战后触发

11) UI 面板与交互
- 内容：新增/扩展传习/祭祀/商店/游历管理界面，交互动画与确认流程
- 验收：面板交互无卡死、关键破坏性操作有二次确认

12) QA 工具与概率可视化
- 内容：开发 Debug 菜单（强制生成游历结果、抽样统计接口）与概率可视化界面
- 验收：QA 可运行抽样并导出 CSV 做统计

13) 平衡表与数值模板
- 内容：导出 Excel/CSV 模板，列出所有可调参数、默认值与备注
- 验收：策划接受并能在表格中直接改数值进行迭代测试

14) 文档与本地化
- 内容：补全文档、所有新文案推送本地化表、写入 README 与部署说明
- 验收：本地化条目覆盖所有 UI 文案，文档能指导新开发者接手

风险与缓解
- 随机性导致平衡难调：提供概率可视化工具并在开发早期做大量抽样。
- 存档兼容问题：实现迁移脚本并在每次存档结构变更时增加版本字段与兼容逻辑。
- 界面复杂导致 UX 问题：先实现最小可用版本（MVP），再做迭代优化。

交付物位置（约定）
- 所有示例表放：`StreamingAssets/Tables/`（示例文件名见任务 1）
- UI Prefabs：`Assets/Prefabs/UI/CatSystem/`（Roster, Detail, Training, Ritual, Shop）
- 代码变更：`Assets/Scripts/Game/CatSystem/`（新模块），以及必要的 DataManager 改动

下一步（我可以现在执行）
- 生成并提交示例 JSON：`outing_config.json` 与 `species_table.json`（示例），把文件放到 `StreamingAssets/Tables/`。

如果同意，我现在开始生成上述两个示例 JSON 文件并提交到仓库。