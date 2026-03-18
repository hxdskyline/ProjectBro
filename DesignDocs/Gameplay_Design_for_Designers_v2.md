# 玩法设计说明 v2（给策划看的详细需求文档）

版本说明：本文件在原有 `Gameplay_Design_for_Designers.md` 基础上，依据 `DesignDocs/新需求/` 下的需求草案，补充并细化每个系统模块的设计需求与实现要点，供策划与后端/客户端开发对接使用。

目标
- 输出对每个子系统（猫猫基础数据、构筑与出征、游历/繁殖、技能与传习、战斗系统、道具/奇物、商店与事件、祭祀、货币、存档与回合流程、UI/UX）的详细需求说明。
- 每个模块包含：功能简介、数据结构（关键字段）、UI/流程需求、交互细节、边界/容错规则、策划可配置项与示例数值、需要的后端/表或资源位置。

说明约定
- “猫”在文档中简称为“猫/猫猫”。
- 约定所有可配置表放于 `StreamingAssets` 根或 `StreamingAssets/Tables`（按模块说明细化）。
- 本文档只描述已列入新需求的实现细节，未说明的额外功能需另行评审。

目录
1. 猫猫基础系统（种族/性别/属性/上限/配饰/精力）
2. 构筑与出征准备
3. 游历与繁殖（外出）系统
4. 技能与传习系统
5. 战斗系统（战前、战中、结算）
6. 奇物（Artifacts）与一次性道具
7. 商店、流浪猫与随机事件
8. 祭祀系统
9. 货币与经济（猫粮）
10. 存档点与循环流程（年/回合）
11. UI/提示与玩家反馈
12. 数据表与示例字段
13. 兼容性与落地实现注意点


---

1. 猫猫基础系统

功能简介
- 每只猫有：唯一 ID、名字、种族、性别、四维属性（攻击、 防御、 生命（HP）、 移动速度）、技能列表（1~3）、特质（0~4）、配饰（0或1）、精力值（Energy）、当前等级/上限信息、父母关系摘录（可选）。

数据结构（关键字段）
- CatRecord (JSON / 表格字段)：
  - id: int
  - name: string
  - species: enum {Maine, Tabby, Orange, Cow, Siamese, Ragdoll, ...}
  - gender: enum {Male, Female}
  - baseAttack: int
  - baseDefense: int
  - baseHp: int
  - baseMoveSpeed: float
  - attackLimit: int
  - defenseLimit: int
  - hpLimit: int
  - moveSpeedLimit: float
  - energyMax: int
  - skills: int[] (skillIds)  // length 1~3
  - traits: int[] (traitIds)  // length 0~4
  - accessoryId: int | null
  - parents: int[] (parentIds) // optional, for lineage display
  - birthTimestamp: long
  - avatarDefinitionAddress: string

UI / 流程需求
- 猫详情面板：展示头像、名字（可改一次）、种族、性别、四维、技能、特质、配饰、当前精力/上限、父母与子女（仅列出直接关系）。
- 列表视图应支持根据属性排序/筛选（BP、攻击、稀有度、能否上阵等）。

交互与边界
- 名字修改：允许玩家对单只猫名修改一次，修改过的标记保存在 CatRecord（字段: nameChanged: bool）。UI 显示“已改名”。
- 属性上限：种族决定初始上限（在 species 表中定义），上限可通过长期玩法（道具、祭祀、属性提升）提升。上限提升要可记录历史变更（便于回溯/调试）。

策划可配置项
- 各种族的初始属性上限与倾向（Table: species_table）
- 初始精力范围（建议平均 100，min 50, max 150）
- 配饰效果表（accessory_table）

实现位置/表
- 新表：StreamingAssets/Tables/species_table.json, accessory_table.json, traits_table.json


2. 构筑与出征准备

功能简介
- 玩家从猫库中选择并分配猫到不同区：上阵（Deployed）、待命/储备（Reserve）、外出（Outing）、赐福候选（Blessing）、属性提升（AttributeBoost）、弃猫（Discard）。
- 最终由出征准备界面确认本轮上阵队列，传递给战斗系统。

流程与 UI
- 构筑主界面（CardBuildPanel）保留：待选区、外出、赐福、属性提升、弃猫等分区。
- 出征准备（BattlePreparePanel）：展示最终上阵列表（分组按阵列位置）、显示敌人预览、提供“开始战斗”按钮。

限制与规则
- 上阵上限：5 per wave, reserve up to 3 waves (总最多 15)；在构筑阶段客户端应能配置 maxDeployedCount（写入表/Inspector）。
- 外出区：可最多选择 3 对（6只）用于游历（按新需求）。
- 赐福区：由系统在弹窗中提供随机候选（默认3选1），玩家点击激活。激活后该猫在下一场上阵时获得祝福，祝福是否消耗由游戏设置决定。
- 弃猫区：拖入需二次确认（弹窗），确认后永久删除。删除行为记录日志并可触发事件（DataManager）。

数据与接口
- 前端构筑时保存一个构筑快照对象：BuildSnapshot { deployedList: CatId[], reserveList: CatId[], outingPairs: [CatId,CatId][], blessingId: CatId|null, attributeBoostList: CatId[], discardList: CatId[] }
- 保存点/备份：在玩家点击 Start Challenge 或界面操作点（可选）写入 DataManager 的 playerdata.json。

策划可配置项
- maxDeployedCount（默认5）、maxWaves（默认3）、requiredOutingPairs（最多3）、blessingCandidatesCount（默认3）


3. 游历与繁殖（外出）系统

概念与关键规则
- 游历必须成对进行，最多 3 对同时游历；游历的猫在当前循环内不可参与战斗。
- 游历开始时不立即生效，直到进入战斗阶段才真正扣除精力并开始计算结果（允许玩家在准备阶段随意撤回）。
- 游历结果：每对返回时会带回 0~2 只小猫（满精力），可能带回奇物；若带回 0 小猫则一定带回一件稀有奇物（保证玩家获得价值）。

数值规则（示例）
- 子猫生成规则：对父母属性取较大值并乘以随机倍率 1.2~1.5，然后做向下/上限裁剪并四舍五入。
- 特质/技能继承概率：每项特质/技能分别 50% 继承父母；如果两方均有该特质/技能，继承概率可提高（例如 75%）。
- 若父母异种族，子代种族从父母任一随机选种（或按策划表配置概率）。

实现细节与数据结构
- OutingRequest { pairs: [ [id1,id2], ... ], startCycle: int, expectedReturnCycle: int }
- OutingResult { pair:[id,id], children: CatRecord[], artifacts: ArtifactId[], rareFlag: bool }

精力与消耗
- 游历消耗精力：20 点（按新需求），实际发生在战斗阶段开始时（当游历生效时才扣除）。
- 若游历期间猫精力不足导致死亡，则根据规则移除（优先判断：若精力耗尽则死亡）。

边界
- 玩家可随时在准备阶段撤回游历申请（不扣除精力）；进入战斗后则生效并扣除精力。

策划可配置项
- 每对带回子猫的概率分布（0/1/2）、稀有奇物触发几率，子猫属性倍率区间。

表/实现位置
- StreamingAssets/outing_config.json 包含：child_count_prob, artifact_prob_by_outcome, breed_rules


4. 技能与传习系统

核心需求
- 每只猫有 1~3 个技能；传习由一只教学猫向 1~10 只学习猫传授，教学猫消耗精力（每个学习者消耗 5 精力），学习必定成功。
- 学习猫若已满 3 技能，则需要替换一个技能，玩家可指定替换项。
- 每轮准备阶段最多进行 1 次传习操作。

流程与 UI
- 传习界面：选择教师猫（显示可教授技能列表）、选择 1~10 个学习猫（界面可批量选择）、确认替换规则（若需要替换则弹出替换选择），显示精力消耗总览与按钮确认。

数据结构与接口
- TrainingRequest { teacherId, studentIds[], skillIdToTeach, replacements: {studentId: replacedSkillId|null} }
- TrainingResult { teacherId, studentResults: [ {studentId, addedSkillId, replacedSkillId|null} ] }

约束与校验
- 教学猫必须满足最低精力（至少 5 * studentsCount）。
- 每轮限制 1 次，服务器/本地需记录本轮是否已执行（Field on PlayerState）。

策划配置
- 每轮最大学习人数（默认10）、每位学习者的教师精力消耗（默认5）。


5. 战斗系统（战前、战中、结算）

总体要求
- 战斗为关卡制，每回合有多波敌人，并在进度条到终点刷新 Boss（像植物大战僵尸的持续刷怪节奏）。
- 玩家上阵最多 5 只/波，最多 3 波（共 15）作为总备战队；当一波全部死亡，下波自动上阵；但为容错机制，猫村有一次免费“挡灾”可避免一次战局全灭（全局一次，除非道具增加次数）。
- 战斗中精力消耗：参战猫每场战斗消耗 15 点精力，死亡额外消耗 30 点（若精力耗尽则真正死亡，并在战后从编队中移除）。

战斗前（配置）
- `BattlePreparePanel` 提供：最终部署列表、敌人预览、是否开启外出结算/赐福消耗/属性提升结算的开关、开始战斗按钮。

战斗进行（节奏）
- 采用 Simulation 模型负责单位 AI/寻敌/攻击/死亡判定。当前实现 Demo 级别，但需扩展支持：多波刷新、进度条触发 Boss、一次性道具冷却、技能释放时机。
- Suggestion for Simulation APIs:
  - Simulation.Start(levelConfig, playerFighters[], enemyWaves[])
  - Simulation.Tick(deltaTime) -> returns {ongoing, victory, defeat, currentWave, playerDeaths[]}

结算
- 胜利：触发 VictoryPanel，显示奖励（基于关卡、进度与表现计算）、调用 `BattleCampaignRuntime.AdvanceAfterVictory(levelId)` 更新关卡进度，调用 `CurrencyManager.AddCurrency` 发放猫粮。
- 失败：若挡灾未用且玩家已开启自动挡灾（或有道具），触发挡灾并恢复到战前准备或触发特殊惩罚/消耗。否则视为挑战失败并可能重置进度。

UI/玩家感知规则
- 战斗面板显示：当前波数、进度条、剩余玩家生命/精力、可用一次性道具、暂停/自动战斗（自动或加速）按钮。

策划可配置项
- waves per level, wave spawn rate, boss wave interval, damage resolution delay, player per-wave size, reserve behavior, block-disaster count default (1)


6. 奇物（Artifacts）与一次性道具

奇物功能
- 每只猫可装备一件奇物；奇物有品质（蓝、紫、橙），提供属性增幅、被动技能或触发效果（如增加游历带回概率、提升学习继承率、增加精力回复等）。
- 奇物可能有耐久度（可选）；每次战斗或使用后耐久降低，到 0 被销毁。

一次性道具
- 战斗中与准备阶段可用道具分开：战斗中为投放类（炸弹、冰冻、回复、DoT 等），准备阶段为增益/资源类（增加猫粮、恢复精力、直接增加经验等）。
- 所有一次性道具可能存在公共冷却（Global Cooldown）限制。

数据字段
- Artifact { id, name, quality, effectType, effectValue, durability (nullable), description }
- Consumable { id, name, type, effectPayload, cost }

获取与产出
- 游历、战斗掉落、商店购买、祭祀奖励、放归猫村可获取奇物或消耗品。


7. 商店、流浪猫与随机事件

商店
- 在指定回合开放，初期每次商店固定 5 件商品（可刷新的），刷新消耗猫粮且每次刷新价格提高。商品包括奇物、一次性道具、随机猫猫。

流浪猫拜访
- 每 5 回合必定触发一次随机事件：流浪猫三选一，价格不同、属性不同。至少一只价格偏低保证新手入手可能。实现为事件队列。

随机事件
- 设计一套概率与事件池（StreamingAssets/event_pool.json），包括：天气、资源奖励、敌方扰动、幸存猫负面效果等。


8. 祭祀系统

规则
- 在指定回合（策划配置）允许祭祀一次，三选一献祭：猫粮、奇物、或一只猫（玩家可选择）。
- 奖励形式与概率：根据献祭类型决定奖励（详见新需求文件），并额外给出一次随机祝福（从祝福池随机）。

实现细则
- RitualConfig 表定义：允许祭祀的回合、可献祭项目、每种献祭的最小成本、奖励池权重。
- 祝福表示例：全队属性百分比提升（持续一回合）、单回合战斗属性加成、未满技能猫随机获得技能、未满特质猫随机获得特质等。


9. 货币与经济（猫粮）

基本
- 单一主要货币：猫粮（CatFood）。
- 增减来源：游历、战斗胜利、放归猫村、商店卖出、任务/事件。
- 消耗场景：技能传习、祭祀、商店购买、刷新的道具、某些奇物强化。

API
- CurrencyManager 提供统一接口：GetAmount(), Add(amount), TrySpend(amount), SetAmount(amount)，并支持保存开关。

经济平衡点
- 策划需定义：每年的猫粮产出与预计消耗，确保后期稀缺性与成长曲线合理。


10. 存档点与回合/年流程

流程概述
- 游戏以“年”为循环，每年包含：战前准备阶段（玩家可多次操作，直到开始战斗）、战斗阶段（可能多波）、战后结算。
- 存档点：建议在玩家点击“开始挑战（Start Challenge）”时保存一次；另外在战斗结束时保存一次。可选：在每次战前阶段玩家完成关键操作时自动保存（更稳健但写盘频繁）。

数据保全与回滚
- SaveSnapshot 包含：PlayerData、CatRoster、OutingRequests、CurrentCycle、Inventory（Artifacts/Consumables）、CampaignProgress。
- 恢复时需完整重建 UI 与逻辑状态（包括是否已使用挡灾次数等一次性标志）。


11. UI/提示与玩家反馈

核心原则
- 明确、及时的反馈：所有潜在造成永久损失的操作（弃猫、祭祀献猫）需清晰警示与二次确认。游历、传习等消耗精力的操作需展示预估消耗与剩余精力警告。

关键界面需求
- 构筑界面：支持筛选、排序（BP/攻击/防御/HP/移动速度/精力）、多选拖拽、快速查看父母/子女。外出与赐福候选展示清晰图标与操作撤回。
- 战前准备：展示敌人波与 Boss 触发进度条、预览奖励、确认开关（外出结算、赐福消耗、属性提升结算）。
- 战斗界面：展示波数、进度条、玩家猫的精力/生命/状态、可用一次性道具列表、暂停与加速按钮。
- 胜利/失败结算：展示奖励明细（猫粮/奇物/子猫）、受损猫与死亡猫列表、是否触发外出/属性提升/赐福消耗结算。

本地化与文案
- 所有概率/随机结果需以易懂文本展示，例如“带回 0~2 只小猫（概率：40%/40%/20%）”，祝福/道具效果需提供一行简短说明和详细信息。


12. 数据表与示例字段

建议表（每表放 StreamingAssets/Tables 或 StreamingAssets 根，JSON 格式）：
- species_table.json: { speciesId, name, baseLimits, tendencyModifiers }
- card_build_cards.json: 现有卡牌库
- battle_campaign_levels.json: 各关敌方 unit id 列表
- outing_config.json: { child_prob, child_multiplier_range, artifact_prob }
- artifact_table.json: 奇物定义
- shop_table.json: 商店初始池
- ritual_config.json: 祭祀可用回合与奖励池
- traits_table.json, skills_table.json

字段示例：见上文 CatRecord


13. 兼容性与落地实现注意点

工程集成建议
- 将硬编码常量（赐福倍率、属性提升值、游历能量消耗等）迁移到 JSON 或 ScriptableObject 以便策划快速迭代。
- 战斗的 Simulation 层应设计为插件式，支持替换 Demo 实现，并提供明确接口（Start/Stop/Tick/ApplySkill/UseItem）。

性能与存盘
- 游历/生成子猫可能瞬时生成多条记录，建议把子猫生成逻辑在非主线程或分帧执行，避免卡顿。
- 存档（playerdata.json）应压缩或节流写入，避免频繁磁盘 IO。

开发与测试注意
- 每个有概率的功能需配套“概率可视化”工具（用于 QA 统计抽样），例如游历带回概率、技能继承率、传习成功率。
- 提供命令/Debug 菜单支持：强制触发游历结果、给予道具、强制复活、快速跳年等，便于策划与 QA 验证。


---

附：对接清单（给开发的任务条目）
- 新增表结构与解析：species_table.json, outing_config.json, artifact_table.json, ritual_config.json
- 扩展 DataManager 保存结构以支持 Lineage（parents/children）与 artifact 持久化
- 扩展 UI：传习界面、祭祀界面、商店刷新/价格阶梯、游历候选管理
- Simulation 扩展：多波刷新、Boss 触发、一次性道具冷却、死亡/精力消耗规则
- 后端/本地化：所有随机文案与概率说明需在本地化资源表中包含短说明 + 详细 tooltip


如需我把本文件转换为 Excel 模板（列出所有可配置字段与默认值），或把关键表的 JSON 示例先行创建（例如 `outing_config.json`、`species_table.json` 的示例），我可以继续生成。