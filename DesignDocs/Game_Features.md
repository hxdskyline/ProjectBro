# 游戏功能说明（基于当前代码实现）

> 本文档面向策划，描述当前工程已实现的玩法与流程。每一项功能均可在源码中找到对应实现（文末有索引）。文档只列出运行时已有逻辑，不进行功能猜测或未实现的扩展。

## 概览
- 主线：卡牌构筑 → 出征准备 → 关卡战斗 → 胜利结算。配套系统包括 UI 面板管理、Addressable 资源加载、音频、存档与货币管理、以及简单的 Avatar 动画系统。
- 启动与总控由 `GameManager` 统一负责模块初始化、载入与保存。参考：Assets/Scripts/Framework/GameManager.cs

## 启动与生命周期
- 初始化顺序（代码保证依赖顺序）：资源管理器 → 表格读取 → 数据管理 → 音频 → 场景 → UI → 关卡管理 → 战役运行时。详见 `GameManager.InitializeManagers()`。
- 游戏启动时调用 `GameManager.LoadGame()` 加载玩家数据并打开主界面；程序退出时会保存数据（`GameManager.SaveGame()`）。
  代码参考：Assets/Scripts/Framework/GameManager.cs

## 资源管理与 Addressable
- 运行时：统一通过 `ResourceManager` 加载/缓存/卸载资源，支持同步与异步接口、引用计数和 Addressables handle 的释放。
  关键方法：`LoadResource<T>`, `LoadResourceAsync<T>`, `LoadPrefab`, `InstantiatePrefab`, `UnloadResource`。
  代码参考：Assets/Scripts/Framework/ResourceManager.cs
- 编辑器：`AddressableAssetsBuilder` 提供一键扫描 `Assets/Bundle`、按规则生成 Address（去掉前缀并转小写）、创建组、构建 Catalog、或清空配置。便于资源命名与发布流程一致。参考：Assets/Editor/AddressableAssetsBuilder.cs

## UI 系统
- `UIManager` 负责面板同步/异步加载、按层级（Background/Normal/Top/PopUp/Alert）组织、缓存已打开面板以及关闭时释放资源。面板通过 Addressable 地址打开，例如 "ui/CardBuildPanel"。
  代码参考：Assets/Scripts/UI/UIManager.cs
- 常用面板：`CardBuildPanel`（构筑）、`BattlePreparePanel`（出征准备）、`BattlePanel`（战斗界面）、`VictoryPanel`（胜利结算）。这些由 `UIManager` 管理并通过资源加载实例化。

## 音频
- `AudioManager` 管理独立的 BGM AudioSource（loop）与 SFX 源池。支持同步/异步加载播放与音量控制，播放失败时会在日志中报错。参考：Assets/Scripts/Framework/AudioManager.cs

## 卡牌构筑（核心模块，详述）
- 数据来源：卡牌数据由 `card_build_cards.json`（StreamingAssets）加载为 `CardBuildCardData` 结构，字段包括 Id/Name/AvatarDefinitionAddress/Gender/Attack/Defense/Hp/MoveSpeed/AttackRange。实现见：Assets/Scripts/UI/Panels/CardBuildCardData.cs 与 CardBuildPanel 的加载逻辑。
  参考：Assets/Scripts/UI/Panels/CardBuildPanel.cs

- 界面分区与交互：
  - 区域：待选（Reserve）、外出（Outing）、赐福（Blessing）、属性提升（AttributeBoost）、弃猫（Discard）。
  - 交互方式：支持拖拽与点击。拖拽项由 `CardDragItem` / `PrepareCardDragItem` 实现，投放区由对应的 DropZone 组件负责事件转发，最终由面板（CardBuildPanel / BattlePreparePanel）执行规则校验。
  参考：Assets/Scripts/UI/Panels/PrepareCardDragItem.cs、BattlePrepareDeployedDropZone.cs、BattlePrepareOwnedDropZone.cs、CardBuildPanel.cs

- 外出（Outing）规则与奖励：
  - 外出区限定最多 2 张卡（`RequiredOutingCardCount = 2`）。战斗结束若开启外出结算，会调用 `CardBuildPanel.CreateOutingRewardCardFromActivePair()` 生成一张奖励卡。
  - 奖励卡规则：用外出两张卡的属性平均值计算基础值，然后按性别应用倍率（母 ×1.2；公 在 0.8~1.6 之间随机），对结果取整并应用最小值限制（如 HP >=1、MoveSpeed >=0.1 等）。名称从外出配置的前缀随机挑选。相关逻辑见：Assets/Scripts/UI/Panels/CardBuildPanel.cs

- 赐福（Blessing）：
  - 赐福候选由系统随机提供（在二级界面显示），通过点击激活某只猫作为赐福对象。赐福不是通过拖拽直接放入。
  - 被赐福的卡会在战斗开始时被传入 `BattlePanel`，并在 `BuildPlayerSpawnDefinitions` 中应用固定倍率的临时加成（见下文战斗部分）。
  代码参考：Assets/Scripts/UI/Panels/CardBuildPanel.cs、Assets/Scripts/UI/Panels/BattlePanel.cs

- 属性提升（永久加成）：
  - 在结算时，如果开启属性提升，会对选中卡施加固定加值：+2 ATK、+1 DEF、+12 HP、+0.1 MoveSpeed、+0.1 AttackRange，并把卡返回待选区。实现见：`CardBuildPanel.ApplyAttributeBoostRewards()`。

## 出征准备
- `CardBuildPanel` 点击“开始战斗”会打开 `BattlePreparePanel` 并传入持有卡列表、默认上阵、外出/赐福 ID 列表和若干控制标志（是否发放外出奖励、是否消耗赐福、是否授予属性提升）。
  参考：Assets/Scripts/UI/Panels/CardBuildPanel.cs -> `OnStartBattleButtonClicked()` 与 BattlePreparePanel 的 `SetupBattle()`。
- `BattlePreparePanel` 支持拖放上阵、显示敌人列表（由 `BattleCampaignRuntime` 提供）并在点击“进入战斗”后把最终上阵卡数组传给 `BattlePanel.StartBattle()`。
  参考：Assets/Scripts/UI/Panels/BattlePreparePanel.cs

## 战斗流程（关键逻辑）
- 启动：`BattlePanel.StartBattle(...)` 接收关卡 ID、上阵卡数据、赐福卡 ID 列表和结算标志；内部创建 `BattleFlowController` 并调用 `StartBattle`。
  参考：Assets/Scripts/UI/Panels/BattlePanel.cs
- 流程控制：`BattleFlowController` 负责寻找或创建 `BattleManager`，把配置（预制体、avatar、敌人数、玩家 fighter 定义）下发到 `BattleManager`，并启动战斗。支持暂停/恢复（通过 `Time.timeScale`），结束后会触发回调通知 UI。参考：Assets/Scripts/Game/Battle/BattleFlowController.cs
- 战斗实现：`BattleManager` 为当前实现的核心战斗容器，负责生成战斗单位（调用 `BattleSpawner`）、构建 `BattleSimulation`（用于 demo 模拟攻击与判定）、并以协程 `DemoBattleLoop` 推进模拟直到胜负。战斗参数（攻击间隔、寻敌间隔、死亡时长等）是可序列化字段，策划可在 Inspector 直接微调。参考：Assets/Scripts/Game/Battle/BattleManager.cs

- 暂停/继续：战斗暂停通过 `BattleFlowController.TogglePause()` -> `BattleManager.PauseBattle()`（内部设置 `Time.timeScale = 0`）实现。参考：BattleFlowController、BattleManager、BattlePanel 的暂停按钮回调。

## 单位与伤害规则（基础）
- 单位类 `Unit`（基类）从表格读取基础数据并按等级/星级做简单乘法放大：level 每级 +10%，star 每星 +20%，两者相乘后应用到基础属性。
  参考：Assets/Scripts/Game/Unit.cs
- 伤害计算：`TakeDamage(int damage)` 使用 `actualDamage = max(1, damage - defense)`，并减少 HP，下限为 0。此为当前战斗的基础受伤规则。参考：Assets/Scripts/Game/Unit.cs

## 战役配置（Campaign）
- `BattleCampaignRuntime` 从 `StreamingAssets/battle_campaign_levels.json` 加载每关的敌方单位 ID 列表，提供敌人数量、推进与是否完成的接口。策划可以直接编辑该 JSON 来配置每一战的敌人阵容。参考：Assets/Scripts/Game/Battle/BattleCampaignRuntime.cs

## 存档与货币
- 存档：玩家数据以 JSON 存放在 `Application.persistentDataPath/PlayerData/playerdata.json`，结构为 `PlayerData`（包含 id/name/level/currentLevel/gold/diamond/currencies 等）。读取/写入由 `DataManager` 负责。
  参考：Assets/Scripts/Framework/DataManager.cs
- 货币：`CurrencyManager` 封装增减/消费逻辑，底层依赖 `ICurrencyStorage`（项目中由 `DataManager` 实现）。提供 `GetCurrencyAmount/AddCurrency/TrySpendCurrency/SetCurrencyAmount/Save` 等接口，货币 key 标准化函数 `GetCurrencyKey`（例如 gold、diamond）。参考：Assets/Scripts/Framework/CurrencyManager.cs
- 结算写入：`VictoryPanel` 在显示胜利奖励时会调用 `CurrencyManager.AddCurrency` 更新货币并调用 `BattleCampaignRuntime.AdvanceAfterVictory` 更新战役进度。参考：Assets/Scripts/UI/Panels/VictoryPanel.cs

## Avatar 与动画系统
- `AvatarAnimationDefinition`（ScriptableObject）定义动作及对应帧资源地址；`BattleAvatar` 负责加载定义并通过 `AvatarSequencePlayer` 播放动作（Idle/Run/Attack/Death），支持同步与异步加载并能在动画结束回归 Idle。参考：Assets/Scripts/Game/Battle/Avatar/AvatarAnimationDefinition.cs、Assets/Scripts/Game/Battle/Avatar/BattleAvatar.cs

## 胜利结算
- `VictoryPanel.ShowVictoryRewards(levelId)` 负责展示胜利文案、播放结算动画、发放奖励（示例实现为 Gold = 100 * level、Exp = 50 * level），并调用 `BattleCampaignRuntime.AdvanceAfterVictory(levelId)` 与 `CurrencyManager.AddCurrency(Gold, amount)` 更新进度与货币。参考：Assets/Scripts/UI/Panels/VictoryPanel.cs

## 已实现的可调常量（策划可关注）
- 赐福倍率（BattlePanel 中常量）：HP×1.5、ATK×1.6、DEF×1.5、MoveSpeed×1.3、AttackRange×1.25。参考：Assets/Scripts/UI/Panels/BattlePanel.cs
- 属性提升数值（CardBuildPanel 中常量）：+2 ATK、+1 DEF、+12 HP、+0.1 MoveSpeed、+0.1 AttackRange。参考：Assets/Scripts/UI/Panels/CardBuildPanel.cs
- 胜利奖励示例公式在 `VictoryPanel` 中实现（Gold/Exp 基于关卡乘数）。参考：Assets/Scripts/UI/Panels/VictoryPanel.cs

## 限制与注意（基于当前代码）
- 当前 `BattleManager`/`BattleSimulation` 提供的是 demo 风格的模拟流程，若需复杂技能、命中判定、连携或更精细的数值平衡，需要在 `BattleSimulation` 层扩展。
- 若希望策划更方便地调整赐福倍率或属性提升，建议将这些硬编码常量迁移到可编辑的配置表或 JSON。
- 资源地址由 `AddressableAssetsBuilder` 规则生成，替换资源时请保持地址一致或通过该工具重新生成 Addressable 映射。

## 代码索引（快速跳转）
- GameManager：Assets/Scripts/Framework/GameManager.cs
- ResourceManager：Assets/Scripts/Framework/ResourceManager.cs
- TableReader：Assets/Scripts/Framework/TableReader.cs
- DataManager：Assets/Scripts/Framework/DataManager.cs
- CurrencyManager：Assets/Scripts/Framework/CurrencyManager.cs
- UIManager：Assets/Scripts/UI/UIManager.cs
- CardBuildPanel：Assets/Scripts/UI/Panels/CardBuildPanel.cs
- BattlePreparePanel：Assets/Scripts/UI/Panels/BattlePreparePanel.cs
- BattlePanel：Assets/Scripts/UI/Panels/BattlePanel.cs
- BattleFlowController：Assets/Scripts/Game/Battle/BattleFlowController.cs
- BattleManager：Assets/Scripts/Game/Battle/BattleManager.cs
- BattleCampaignRuntime：Assets/Scripts/Game/Battle/BattleCampaignRuntime.cs
- Unit：Assets/Scripts/Game/Unit.cs
- Avatar 系统：Assets/Scripts/Game/Battle/Avatar/AvatarAnimationDefinition.cs、Assets/Scripts/Game/Battle/Avatar/BattleAvatar.cs
- Addressable 工具：Assets/Editor/AddressableAssetsBuilder.cs

---

如果需要，我可以：
- 把文档中的“可调常量”列成单独表格（便于平衡表）；
- 按模块继续把实现细节（函数签名、JSON 字段名、可编辑项）逐条列出，便于策划直接转入表格设计。
