# 商店与随机事件（Shop & Events）设计文档

目标
- 定义商店刷新、商品池、价格阶梯、流浪猫与随机事件池的结构与玩法流程，明确数据表、UI 交互、后端/本地实现接口与 QA 验收标准。

一、系统概述
- 商店是玩家获取奇物、消耗品和随机猫的重要渠道。商店分为常驻商店（常规货品）与周期性/事件商店（主题池）。
- 随机事件（Event）覆盖流浪猫、天气、驿站事件、敌方扰动等，会在设定回合概率触发，影响资源、猫或下一场战斗。

二、数据结构与表（建议放 `StreamingAssets/Tables/`）
- `shop_table.json`（商店池）
  - shopId, shopType (Merchant|EventShop|Special), refreshCostFormula (baseCost, multiplierStep), slots: [slotEntry]
  - slotEntry: { slotId, poolKey, guaranteedMin: int|null }
- `shop_pools/*.json`（池定义）
  - poolKey -> [ { type: "artifact"|"consumable"|"cat", id, weight, priceOverride|null } ]
- `event_pool.json`（随机事件池）
  - eventId, category (wanderer|weather|raid|gift), weight, triggerConstraints (roundRange, preconditions), payload (describes effect)
- `wanderer_table.json`（流浪猫条目）
  - id, speciesTemplateId, offeredPrice, rarityTag, extraAttrs

三、商店规则细化
- 商品抽取：每次刷新从每个 slot 的 pool 随机抽取 N 个条目（按权重），支持 `guaranteedMin` 保底。
- 刷新规则：两种刷新方式
  - 免费周期刷新：每 X 回合一次（由策划配置）
  - 付费手动刷新：玩家支付 `refreshCost = baseCost * multiplier^(timesRefreshedThisSession)`（multiplier>1）
- 价格策略：
  - 商品默认价格由表中 `priceOverride` 或 pool entry 的 basePrice 决定
  - 若商品为稀有猫，价格可为参考价 + 稀有溢价
- 商店库存持久性：玩家在一次游玩期内的商店刷新计数保存在 `PlayerData.shopSession`，退出/重进可按需求重置或保留（策划决定）
- 流浪猫购买：购买流浪猫需要支付猫粮并生成 `CatRecord`，生成时记录来源 `origin:"wanderer_shop"`

四、事件池规则
- 事件触发时间点：在每回合开始或特定节点触发（由 `DesignDocs/Gameplay_Design_for_Designers_v2.md` 中年/回合循环决定）
- 事件选择：按 `event_pool.json` 的权重和触发约束随机选取
- 事件效果示例：
  - 流浪猫三选一（wanderer）：展示三只候选猫，玩家可按价购买
  - 天气（weather）：下一场战斗敌方攻击/移动速度上下浮动（持续 1 波）
  - 抢劫（raid）：随机消耗一定猫粮或偷走随机道具（可被特质/奇物抵挡）
  - 礼物（gift）：发放少量猫粮或一次性道具作为事件奖励

五、API 与 Manager 设计
- `ShopManager` 接口
  - GetShopContents(shopId) -> ShopContents
  - RefreshShop(shopId, useCurrency:boolean) -> (success, newContents)
  - BuyItem(playerId, shopId, slotId) -> BuyResult
  - GetWandererCandidates() -> WandererEntry[]
- `EventManager` 接口
  - MaybeTriggerEvent(currentRound) -> (triggered:bool, EventEntry|null)
  - ExecuteEvent(eventId) -> EventResult
  - GetEventHistory() -> EventResult[]

六、UI/交互
- 商店面板（`ShopPanel`）
  - 栏目：商品格子、刷新按钮（显示刷新消耗）、流浪猫专栏（若触发）、购买确认弹窗
  - 购买流程：点击商品 → 弹出详细信息与价格 → 确认/取消 → 成交并产生 `BuyResult`（更新 `PlayerData`）
- 随机事件提示栏（`EventToast`）
  - 在回合开始若触发事件，短暂弹窗提示并提供“查看/跳过”选项，若有选择（如购买流浪猫）则进入相应 UI

七、边界与容错
- 避免重复扣费：`BuyItem` 必须保证幂等性（事务顺序：先锁定/验证货币与库存，写入变更，释放锁）
- 网络或崩溃恢复：将 `ShopSession` 的关键状态写入 `PlayerData`，断线恢复时以最后写入为准
- 事件冲突：若事件与商店同时触发（例如流浪猫事件），优先级由策划设定；建议事件先行，随后商店刷新/显示

八、平衡与策划工具
- 导出 shop_pools 的 CSV/Excel 以便策划批量编辑
- 提供 `EventSim` Debug 工具：快速批量模拟 N 次回合以统计事件触发率与收益分布

九、测试与验收
- 单元测试：`RefreshShop` 的权重抽样正确性、`BuyItem` 的幂等性与货币扣减准确性
- 集成测试：模拟 1k 次玩家刷新与购买，统计价格阶梯与购买分布是否符合策划期望
- QA 用例：流浪猫三选一正常展示且购买生成 `CatRecord`

十、实现备注
- 建议实现路径：`Assets/Scripts/Game/Shop/ShopManager.cs`, `Assets/Scripts/Game/Event/EventManager.cs`
- 表位置：`StreamingAssets/Tables/shop_table.json`, `StreamingAssets/Tables/shop_pools/*.json`, `StreamingAssets/Tables/event_pool.json`

验收标准（简明）
- 商店刷新与购买流程在 UI 中可执行且在 `PlayerData` 中持久化；事件在回合触发并产生对应效果，QA 能在抽样测试中验证概率分布。
