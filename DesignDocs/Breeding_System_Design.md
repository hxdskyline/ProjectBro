# 繁殖与子代生成（Breeding）设计文档

目标
- 定义子代生成算法、特质/技能继承规则、稀有性与变异机制，以及对外 API 与测试要求。

一、概念与约束
- 繁殖由 `Outing` 或专门的繁殖流程触发（本阶段与 `Outing` 集成）。
- 子代属性来源于父母的属性与物种模板（`species_table.json`），并受 `childMultiplierRange` 与种族倾向影响。

二、输入/输出
- 输入：父母 `CatRecord` 两个实例、配置（从 `outing_config.json` 或专门的 `breeding_config.json` 获取）
- 输出：0~N 个 `CatRecord`（新实例，带唯一 id），包括继承的 `skills` 与 `traits`

三、核心算法（示例实现）
1. 确定子代数量：使用 `childCountProb` 概率表抽样（例如 0/1/2）
2. 计算基准属性：对每项属性取父母值的平均值，乘以随机倍率 r ∈ childMultiplierRange
   - raw = round((parentA.attr + parentB.attr)/2 * r)
3. 性别与种族判定：
   - 若同种族则继承父母种族
   - 若异种族按策划权重随机决定或生成“混血”种族（可选）
   - 性别随机（50/50）或按父母概率表
4. 属性上限裁剪：对 raw 值应用 `species_table.template.tendencyModifiers`，并裁剪到 `baseLimits` + growth cap
5. 技能继承：
   - 对父母每个技能做独立继承判断（基准 50%），若双亲均有该技能可提升继承概率（例如 75%）
   - 若子代技能槽已满（>3），随机替换已有技能或按策划规则保留
6. 特质继承：类似技能，单独概率表（traits_table.json 中可含 inheritedChance 字段）
7. 稀有/变异判定：按设定小概率触发变异，变异可提升某项属性或增加稀有特质

四、数据结构与示例字段
- BreedingConfig（可选单独表）
  - `childCountProb`, `multiplierRange`, `skillBaseInheritChance`, `traitBaseInheritChance`, `mutationRate`
- OffspringRecord（同 `CatRecord`）需标注 `origin: { fatherId, motherId, method:"outing" }`

五、可配置点
- 基础继承率（技能/特质）、变异率、双亲共通技能加成、稀有度提升规则、种族混血映射表

六、测试与 QA
- 抽样测试：对 N=10k 次繁殖，统计技能继承率/特质继承率/平均属性增益，结果需符合策划设定 (±5%)
- 边界测试：父母属性极端值、父母为死亡/非法状态、父母已在外出中的情况

七、实现 API（`BreedingService`）
- GenerateOffspring(father:CatRecord, mother:CatRecord, config) -> List<CatRecord>
- ComputeInheritanceProbability(skillId, father, mother, config) -> float
- ApplyMutation(offspring, config) -> bool

八、性能与一致性
- 随机数提供器 `IRandomProvider` 注入，以保持测试可复现
- 大批量生成建议分帧或使用 Job/线程池执行，避免主线程卡顿

九、与其他系统的集成点
- `OutingManager` 在返回阶段调用 `BreedingService.GenerateOffspring`
- `DataManager` 负责把子代写入 `PlayerData.catRoster` 并触发 `OnCatCreated` 事件
- `Artifact`/`Trait` 的效果可修改继承概率（通过 `effectType` 钩子）

十、验收标准
- 在模拟环境下运行 10k 次繁殖抽样，统计结果与策划设定一致；子代记录能在 UI 中正常展示并可被上阵/传习/装备奇物


