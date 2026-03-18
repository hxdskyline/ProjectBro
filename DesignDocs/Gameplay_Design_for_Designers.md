# 玩法设计说明（给策划看的文档）

版本说明：基于当前工程实现的代码与资源撰写，仅描述已存在的玩法与数据流；未实现的功能不在本文档范围内。

目的：让策划能快速理解玩家看到和玩的是什么、交互流程如何、可调参数在哪里、以及如何通过编辑表/配置影响游戏表现。

一、核心玩法概述
- 玩家通过“构筑”卡池挑选、调整卡（猫咪），把卡放到不同功能区（待选、外出、赐福、属性提升、弃猫）。
- 当准备完毕后进入“出征准备”界面，上阵若干只猫进入战斗。战斗为关卡（battle）制，胜利后进入结算并获得奖励，战役进度推进。实现代码见技术文档。

二、玩家视角的主要流程（一步步）
1. 打开主界面，进入“构筑”界面（Card Build）。
   - 在“待选区”查看所有可用猫，点击或拖拽进行分配。参考实现：CardBuildPanel（界面、交互、数据由表驱动）。
2. 二级功能：
   - 外出（Outing）：最多派 2 只猫外出，外出会在战斗结算时可能带回一张奖励猫（由外出两只猫的属性混合生成）。
   - 赐福（Blessing）：系统在赐福面板随机给出若干候选，玩家点击激活一只，该猫在下一场战斗会获得一次性临时强化（不通过拖拽放入）。
   - 属性提升（Attribute Boost）：把卡安排到该区，战后确认可给这些卡永久属性加成（示例值见下）。
   - 弃猫（Discard）：把不需要的卡拖入后需要通过确认按钮永久删除。
   - 这些交互都是通过拖拽或点击实现；界面会提示数量/限制/当前激活状态。
3. 点击“开始战斗”→进入“出征准备”界面，进行最后确认（上阵顺序、祝福生效等）。
4. 进入战斗界面，战斗进行（可暂停），战斗结束后进入胜利/失败结算界面。

三、关键玩法规则（策划可直接使用）
- 上阵数量：上阵卡数量有上限（默认示例为 5，界面会显示上阵/最大）。在上阵区上限满后无法继续添加。
- 外出规则：外出区恰好 2 只时生效。战后若“外出结算”开启，会生成一张奖励卡，生成规则为两只外出猫属性平均并按性别带倍率（母猫固定 ×1.2，公猫随机 0.8–1.6），然后做四舍五入与下限保障。奖励为新卡直接加入外出或待选池。（实现参见 CardBuildPanel）
- 赐福规则：赐福由系统随机候选，点击激活后该卡在下一场上阵时会获得以下临时倍率：HP ×1.5、ATK ×1.6、DEF ×1.5、移速 ×1.3、攻击范围 ×1.25。赐福是否消耗由开始战斗时传入的标志控制（即可配置开关）。
- 属性提升（永久）：当“属性提升结算”开启时，会对被选中卡永久应用固定加值：+2 攻击、+1 防御、+12 生命、+0.1 移速、+0.1 攻击范围（示例数值可调整）。
- 战斗伤害规则（玩家能感知的）：受击伤害计算为：最终承伤 = max(1, 伤害值 - 防御值)。这意味着防御可以削减伤害但不会让伤害为 0（至少承受 1 点）。

四、奖励与货币
- 胜利奖励示例：当前实现为金钱 = 100 × 关卡数，经验 = 50 × 关卡数（用于示例/演示）。实际奖励表可由策划决定，代码位置在胜利面板中演示。胜利时会把奖励写入玩家货币系统。
- 货币类型：系统内置 gold 与 diamond（显示与消耗逻辑通过 CurrencyManager 封装）。

五、进度与关卡（战役）
- 关卡（Campaign）由外部 JSON 配置文件驱动（StreamingAssets 下的 battle_campaign_levels.json）。每一关可配置敌人单位 ID 列表，关卡的敌人数量与类型直接来自该文件。策划可以直接编辑该 JSON 来控制每关内容。系统会读取并序列化到运行时。此设计便于内容迭代。

六、可编辑与可调项（策划关注）
- 表 / JSON：
  - 卡牌池：`card_build_cards.json`（卡牌基础属性与 Avatar 地址）
  - 战役配置：`battle_campaign_levels.json`（每关敌方单位 ID 列表）
  - 外出奖励配置：`outing_reward_config.json`（外出奖励名称前缀、数值范围）
  以上文件位于 `StreamingAssets`。修改后不需改代码，重启或重新加载可生效。

- Inspector（运行时或预置体）：
  - `BattleManager` 中含多个可调字段（攻击节奏、寻敌延迟、死亡时长、单位生成范围、Tint 等），这些可直接在场景或预制体上通过 Inspector 调整。
  - 某些常量（例如赐福倍率、属性提升数值）当前在代码中为常量，若希望不需要程序修改，建议迁移到表或可编辑 ScriptableObject。

七、UI 流程与体验提示（给策划的 UX 建议）
- 构筑页应明确显示当前外出/赐福/属性提升的状态与效果预览（示例逻辑已经在界面文本中体现）。
- 当外出或赐福影响上阵卡时，应该在上阵卡上做明显标识（例如图标/Label），以避免玩家误判。当前实现会在卡牌名称或统计中显示“[游历中]”或“[赐福]”。
- 弃猫操作需要二次确认（当前实现为弹窗中的确认按钮），建议在 UI 文案中清晰提示“永久删除”与可撤销流程（在确认前允许撤回）。

八、数据校验与兼容性注意
- 卡牌数值读自 JSON 且有最小值保护（例如 HP 最小 1，移速最小 0.1），设计新卡时请避免填写过小或过大的极端值。系统对非法或缺失字段有容错（回退默认值）。
- 资源地址由编辑器工具 `AddressableAssetsBuilder` 生成（规则：去掉 Assets/Bundle 前缀并转小写），因此替换资源请保留地址一致或使用工具重新生成。

九、交互示意（简化版）
1. 构筑界面：拖拽/点击 → 放入外出/赐福/属性提升 → 点击“开始战斗”
2. 出征准备：最终上阵确认（可撤回/查看敌人） → 进入战斗
3. 战斗：可暂停/继续 → 结算（胜利/失败）
4. 胜利：奖励显示 → 奖励应用（货币、属性提升、外出奖励） → 返回构筑

十、文件索引（便于策划定位到可编辑资源/配置）
- 构筑面板（UI/交互）：Assets/Scripts/UI/Panels/CardBuildPanel.cs
- 出征准备：Assets/Scripts/UI/Panels/BattlePreparePanel.cs
- 战斗入口与流程：Assets/Scripts/UI/Panels/BattlePanel.cs、Assets/Scripts/Game/Battle/BattleFlowController.cs
- 战斗实现（可调字段）：Assets/Scripts/Game/Battle/BattleManager.cs
- 胜利/结算：Assets/Scripts/UI/Panels/VictoryPanel.cs
- 卡牌数据结构：Assets/Scripts/UI/Panels/CardBuildCardData.cs
- 卡牌 JSON：StreamingAssets/card_build_cards.json
- 战役配置：StreamingAssets/battle_campaign_levels.json
- 外出配置：StreamingAssets/outing_reward_config.json
- 存档与货币：Assets/Scripts/Framework/DataManager.cs、Assets/Scripts/Framework/CurrencyManager.cs
- 资源加载与 Addressable 工具：Assets/Scripts/Framework/ResourceManager.cs、Assets/Editor/AddressableAssetsBuilder.cs

十一、后续工作（建议）
- 把赐福倍率与属性提升数值迁移为可配置表（便于平衡调整，无需改代码）。
- 输出一份“策划可直接编辑的平衡表模板”，包含卡牌基值、赐福倍率、外出奖励范围、每关敌人权重等。
- 如需，我可以把本文件转换为 Excel/CSV 模板，或把可编辑字段提取成单独的 JSON/ScriptableObject 清单，便于你直接开始数值设计。

---
保留的技术文档已存放为：[DesignDocs/Game_Features.md](DesignDocs/Game_Features.md)
新创建的面向策划的文件：[DesignDocs/Gameplay_Design_for_Designers.md](DesignDocs/Gameplay_Design_for_Designers.md)

需要我现在把“可调常量”导出为表格模板，还是先把赐福与外出相关的字段改为可编辑配置（创建示例 JSON）？