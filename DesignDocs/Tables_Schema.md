# 表结构说明（示例字段说明）

此文档为 `StreamingAssets/Tables/` 下示例 JSON 的字段定义与说明，便于开发与策划对齐。

1. species_table.json
- id (int): 唯一 ID
- key (string): 程序内引用的 key
- name (string): 显示名
- baseLimits (object): 初始属性上限，包含 attack, defense, hp, moveSpeed
- tendencyModifiers (object): 种族倾向的属性倍率（用于成长/遗传计算）
- energyMax (int): 最大精力
- rarity (string): 稀有度标签（common/uncommon/rare/epic）
- avatarAddress (string): 资源地址（Addressables 地址）

2. outing_config.json
- maxPairs (int): 最多允许同时外出的配对数
- energyCostPerPair (int): 每对外出扣除的精力（生效时扣除）
- childCountProb (map): 返回子代数量概率，例如 {"0":0.4, "1":0.45, "2":0.15}
- childMultiplierRange (array[float, float]): 子代属性倍率区间
- artifactProbByQuality (map): 根据品质掉落概率
- guaranteeArtifactOnZeroChildren (bool): 若无子代是否保证掉落奇物
- minReturnDelayCycles / maxReturnDelayCycles: 返回延迟的最小/最大回合数

3. artifact_table.json
- id, key, name, quality (common/rare/epic)
- effectType (string): 统一的效果类型标识（battle_attack_pct, energy_max_add, outing_artifact_chance 等）
- effectValue (number): 与 effectType 对应的数值
- durability (int|null): 耐久度（若为 null 则永久）
- description (string): 描述（UI 用）

4. ritual_config.json
- allowedRounds (int[]): 可触发祭祀的回合列表
- options (array): 每种献祭选项的 id/label/最低成本/奖励池键
- blessingPool (string): 祝福池标识
- guaranteedBlessingChance (float): 惊喜祝福的基础概率

5. traits_table.json
- id, key, name, description
- effect (object): 具体效果描述（用于 Simulation）
- rarity

6. skills_table.json
- id, key, name, description
- cooldownSec (int)
- effect (object): 技能效果负载（damage, shield, buff 等）


使用说明
- 新增字段需同时在 `TableReader` 与使用者（系统）代码中增加解析与兼容逻辑。
- 任何表结构改动应伴随存档迁移逻辑（DataManager）与单元测试。

示例文件位置
- StreamingAssets/Tables/species_table.json
- StreamingAssets/Tables/outing_config.json
- StreamingAssets/Tables/artifact_table.json
- StreamingAssets/Tables/ritual_config.json
- StreamingAssets/Tables/traits_table.json
- StreamingAssets/Tables/skills_table.json

如果需要，我可以把这些 JSON 的解析单元测试（简单的 C# xUnit 或 UnityTest 框架）也生成一份示例。