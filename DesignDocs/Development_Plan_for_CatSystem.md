# 猫猫系统开发计划（开发用，分解任务）

目标：落地 `DesignDocs/Gameplay_Design_for_Designers_v2.md` 中的新需求，按模块分解实现、测试与交付，并给出时间预估、交付物与验收标准。

参考文档：[Gameplay_Design_for_Designers_v2.md](DesignDocs/Gameplay_Design_for_Designers_v2.md)

里程碑与任务清单

- 里程碑 A（准备与表结构）：定义所有表/JSON、示例文件、序列化格式（1 周）
  - 任务 1：定义数据表与 JSON 示例（species_table, outing_config, artifact_table, ritual_config, traits_table, skills_table） （详见 [DesignDocs/Tables_Schema.md](DesignDocs/Tables_Schema.md)）
    - 交付：示例 JSON（位于 StreamingAssets/Tables/*.json）与字段文档
    - 验收：示例被 DataLoader 正确读取并反序列化（单元测试通过）

- 里程碑 B（后端/存储与模型）：扩展 DataManager 与 Cat 数据模型（1 周）
  - 任务 2：扩展存储与 DataManager（持久化 parents/children、artifacts、outingRequests） （参见 [DesignDocs/CatRoster_Design.md](DesignDocs/CatRoster_Design.md) 中的数据模型与存档说明）
    - 交付：DataManager 保存/加载回归测试
    - 验收：保存后重启游戏能完整恢复 CatRoster 与 Outing 状态

- 里程碑 C（核心玩法实现）：游历、繁殖、传习、奇物（2~3 周）
  - 任务 3：实现猫模型与花名册 UI（Roster、详情面板、筛选/排序） （详见 [DesignDocs/CatRoster_Design.md](DesignDocs/CatRoster_Design.md)）
  - 任务 4：游历（Outing）系统开发（请求、结算、结果生成） （详见 [DesignDocs/Outing_System_Design.md](DesignDocs/Outing_System_Design.md)）
  - 任务 5：繁殖/子代生成逻辑（继承规则、随机与上限裁剪） （详见 [DesignDocs/Breeding_System_Design.md](DesignDocs/Breeding_System_Design.md)）
  - 任务 6：传习（Training）系统（教师与学生流程、精力消耗） （详见 [DesignDocs/Training_System_Design.md](DesignDocs/Training_System_Design.md)）
  - 任务 7：奇物与一次性道具（表、装备槽、战斗/准备阶段效应） （详见 [DesignDocs/Artifacts_Consumables_Design.md](DesignDocs/Artifacts_Consumables_Design.md)）
    - 交付：各系统集成测试场景（示例流程：发起游历→进行战斗→结算并返回子代/奇物）
    - 验收：示例流程在 QA 环境可运行并符合概率统计预期（抽样测试）

- 里程碑 D（支撑系统与集成）：祭祀、商店、事件、战斗集成（1.5 周）
  - 任务 8：祭祀系统（献祭面板、奖励发放规则） （详见 [DesignDocs/Ritual_System_Design.md](DesignDocs/Ritual_System_Design.md)）
  - 任务 9：商店与随机事件（刷新逻辑、事件池） （详见 [DesignDocs/Shop_Events_Design.md](DesignDocs/Shop_Events_Design.md)）
  - 任务 10：战斗集成（在 BattleManager 中加入精力扣除、死亡处理、挡灾接口） （详见 [DesignDocs/Battle_Integration_Design.md](DesignDocs/Battle_Integration_Design.md)）
    - 验收：战斗开始/结束流程中能正确处理外出生效、精力扣除与死亡移除

- 里程碑 E（UI 完善、工具与平衡）：面板优化、概率可视化、平衡表（1 周）
  - 任务 11：UI 面板与交互细化（传习界面、祭祀界面、游历管理） （参见 [DesignDocs/CatRoster_Design.md](DesignDocs/CatRoster_Design.md) 中的 UI 建议）
  - 任务 12：QA 工具与概率可视化（Debug 菜单、抽样统计工具） （可参考 [DesignDocs/Gameplay_Design_for_Designers_v2.md](DesignDocs/Gameplay_Design_for_Designers_v2.md) 的概率要求）
  - 任务 13：平衡表与数值模板（Excel/CSV，含默认值与说明） （相关字段与格式见 [DesignDocs/Tables_Schema.md](DesignDocs/Tables_Schema.md)）

- 里程碑 F（收尾）：文档、本地化与发布准备（0.5 周）
  - 任务 14：文档、示例 JSON 与本地化（所有新文案的本地化条目） （参见各子系统文档，例如 [DesignDocs/Outing_System_Design.md](DesignDocs/Outing_System_Design.md)、[DesignDocs/CatRoster_Design.md](DesignDocs/CatRoster_Design.md)）

责任分配（建议）
- 系统设计 & 表结构：策划 + 技术设计工程师（1 人）
- 后端/持久化：1 人（DataManager 改动）
- 核心玩法逻辑（Outing、Breed、Training、Artifact）：2 人
- UI / UX：1 人
- QA / 概率统计工具：1 人

时间预估（总计）：约 7–9 周（可并行多个任务以缩短真实排期）

详细任务拆分（每项包含验收标准与示例交付物）

﻿# 猫猫系统 — 玩家说明书

简短说明：猫猫系统是游戏内的宠物养成与冒险模块，玩家可以收集、培养、派遣猫猫外出探险、繁殖后代、教导技能并装备奇物获得各种玩法收益与道具奖励。

玩法概览
- 花名册（Roster）：管理与查看你所有的猫猫，支持筛选、排序与改名。
- 游历（Outing）：派遣猫猫外出冒险，完成战斗与探索后可获得道具、奇物或子代线索。
- 繁殖（Breeding）：将两只猫配对以生成子代，子代会继承父母的属性与特质。
- 传习（Training）：使用有经验的猫教授技能给其他猫，消耗精力并可能替换/获得新技能。
- 奇物与道具：可以装备或一次性使用以改变战斗/探索结果。
- 祭祀、商店与随机事件：为玩法提供消耗、获取与临时增益的渠道。

核心系统（面向玩家的行为与示例）

1. 花名册（Roster）
- 做什么：在花名册中查看每只猫的属性、特质、技能与装备；可重命名、上阵或下阵。
- 玩家获益：快速筛选出适合外出或战斗的猫，保存收藏或标记心仪对象。
- 示例：筛选“敏捷>50 且 包含特质：夜视”的猫，用于夜间探险团队。

2. 游历（Outing）
- 做什么：选择 1-3 只猫发起外出，设定出发与返回时间，外出中会触发战斗或事件，结束时返回奖励。
- 奖励类型：金币、消耗品、奇物、子代线索或直接出现子代。
- 风险与机制：猫有精力值，精力耗尽时无法继续外出；在战斗中可能受伤或死亡（有保护机制可避免直接死亡）。
- 示例流程：选择两只猫→确认出发（耗时 2 小时）→战斗发生→结算：获得奇物 + 一次小概率产出稀有素材。

3. 繁殖（Breeding）
- 做什么：将符合条件的一公一母配对生成子代。可设置繁殖次数与加速选项。
- 继承规则（非技术细节，只面向玩家）：子代会在外观、部分基础属性与少量特质上继承父母，存在稀有性概率。
- 玩家提示：稀有特质更容易通过高稀有度父母或特殊奇物提高继承概率。

4. 传习（Training）
- 做什么：将经验猫作为教师，消耗精力让另一只猫学习新技能或提升技能熟练度。
- 限制：每次传习会消耗教师与学生的精力，可能需要消耗道具以保证成功率或降低失败惩罚。

5. 奇物与道具
- 做什么：奇物可装备在猫身上，产生被动或战前/战中效果；一次性道具用于战斗或外出结算时触发即时效果。
- 获得方式：游历、商店购买、事件奖励或祭祀所得。

6. 祭祀、商店与随机事件
- 祭祀：消耗特定道具或猫进行献祭以获得高价值奖励或短期祝福。
- 商店：定期刷新商品，可使用游戏货币或付费货币购买。某些高价值物品为限时出现。
- 随机事件：在游历或日常中可能触发，会提供抉择分支并影响即时奖励或后续剧情。

战斗与代入
- 战斗中：猫的装备、特质与技能会直接影响战斗结果；战斗胜利是获取高质量奖励的主要途径。
- 精力与死亡：精力决定行动次数或外出持续力；游戏提供替代/免死机制避免过度惩罚新手玩家。

新手引导与 UX 要点（面向玩家体验）
- 新玩家流程：
  - 进入猫猫系统后，会有简短引导：打开花名册→发起首次外出→结算获得奖励与道具→学习繁殖基础。
  - 提示重要：精力、装备与保存收藏。
- 常用 UI 快捷操作：一键筛选、批量派遣、自动返回与快速出售重复道具。

典型玩家任务举例
- 任务 A（探险）：派遣 3 只猫进行 4 小时外出以刷取奇物，目标产出为“探险之石”。
- 任务 B（培养）：使用传习功能让一只猫学会“防御提升”技能，准备参加挑战赛。
- 任务 C（繁殖）：配对两只稀有猫希望获得高稀有子代，使用限定奇物提升继承概率。

术语表（简短）
- 精力：猫进行外出或训练的消耗资源。
- 奇物：特殊装备或物品，能改变战斗/探索机制或提升继承概率。
- 子代线索：通过游历或特殊事件获得，用于解锁繁殖时段或提升稀有率。

常见问答（FAQ）
- 问：猫会永久死亡吗？
  - 答：游戏中存在死亡惩罚，但提供保护机制与复活道具以降低损失。
- 问：如何提高稀有子代概率？
  - 答：使用高稀有父母、特定奇物或参与限定活动均可提高概率。

反馈与支持
- 如果你发现玩法不清楚或出现问题，请在游戏内反馈界面提交问题，我们会根据反馈优化引导与数值。

-- 版本说明：此说明面向玩家体验，内部实现与数据表格为开发与策划使用的技术材料，会另行维护。
