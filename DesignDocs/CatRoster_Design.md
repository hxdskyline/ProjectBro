# 猫模型与花名册（Cat Roster）设计

目标
- 明确 `CatRecord` 数据字段、运行时模型、UI 面板交互与持久化要求，供前端/UI/后端实现对接。

一、功能概览
- 管理玩家所有猫（查看、筛选、改名、装备奇物、上阵/外出/弃置），展示父母/子代关系与关键属性。

二、数据模型
- CatRecord (持久化到 `PlayerData.catRoster`)
  - `id` (long) 唯一实例 ID
  - `templateId` (int) 指向 `species_table.json` 的物种模板 id
  - `name` (string)
  - `nameChanged` (bool) 是否已改名（限制一次）
  - `gender` (string: "Male"|"Female")
  - `level` (int)
  - `attributes` (object): { attack:int, defense:int, hp:int, moveSpeed:float }
  - `energy` (int) 当前精力
  - `energyMax` (int)
  - `skills` (int[]) 技能 id 列表（长度 1~3）
  - `traits` (int[]) 特质 id 列表
  - `accessoryId` (int|null) 装备奇物 id
  - `parents` (long[]) 父母实例 id
  - `children` (long[]) 子代实例 id（可选，用于家谱展示）
  - `flags` (object) 运行时临时标记（例如: isOuting:boolean, isDeployed:boolean）
  - `createdAt` (long timestamp)

三、运行时 API（`CatManager`）
- GetCat(long id): CatRecord
- GetAllCats(): List<CatRecord>
- CreateCatFromTemplate(int templateId, optional parents[]): CatRecord
- RenameCat(long id, string newName): bool
- EquipArtifact(long catId, int artifactId): bool
- SetFlag(long id, flagName, value)
- PersistChanges() → 调用 `DataManager.SavePlayerData()`（节流，最小 5s）

四、UI 组件
- `RosterPanel`（主面板）
  - 功能：分页/滚动显示卡片，支持筛选（BP/攻击/防御/HP/精力/稀有度/未上阵）、排序、批量选择
  - 快捷操作：上阵/撤下、外出拖入、装备/卸下奇物、改名（一次）
  - 资源路径：`Assets/Prefabs/UI/CatSystem/RosterPanel.prefab`
- `CatDetailPanel`
  - 显示完整信息、可触发改名、查看父母/子女、装备快捷入口
  - 交互要点：具有明确二次确认的毁灭性操作（弃猫/献祭）

五、持久化与兼容
- 保存位置：`PlayerData.catRoster`（写入 `playerdata.json`，通过 `DataManager` 管理）
- 版本控制：`PlayerData` 须增加 `schemaVersion` 字段，若旧存档需运行迁移脚本（示例：将 `speciesKey` 转为 `templateId`）
- 写入策略：所有 UI 引发的改动先在内存模型更新，调用 `CatManager.PersistChanges()` 执行节流写入（避免频繁 IO）

六、事件与对外接口
- 事件：`OnCatCreated(CatRecord)`, `OnCatChanged(CatRecord)`, `OnCatDeleted(id)`
- 其他系统订阅示例：`OutingSystem` 订阅 `OnCatChanged`，`BattleManager` 在出战时读取 `isDeployed` 标记

七、验收标准
- 在 UI 中能列出所有猫并完成筛选/排序、改名一次且在重启后保持；装备奇物会影响战斗/游历行为（通过事件通知）。

八、实现备注
- 使用现有 `TableReader` 加载 `species_table.json`（位置：StreamingAssets/Tables/species_table.json），示例参见 [DesignDocs/Tables_Schema.md](DesignDocs/Tables_Schema.md)
- 推荐将 `CatManager` 放在 `Assets/Scripts/Game/CatSystem/CatManager.cs`，并提供 Editor 的 Mock Data 生成功能以便 QA 测试。


