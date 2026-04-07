# 猫村守护者 - 系统设计文档

版本说明：本文件基于 `DesignDocs/新需求/新的需求改动很大.txt` 完全重构，描述族群统帅制的游戏系统。

版本历史
- v4.0：Phase 3-5 实现（UI层+战斗适配+回合管理完成）
- v3.1：Phase 1-2 实现（数据层+逻辑层已完成）
- v3.0：完全重构为族群统帅制系统，移除个体猫玩法
- v2.1：移除Discard（弃猫）功能，简化构筑系统
- v2.0：初始版本，完整系统设计

---

## 目录
1. 核心玩法概述
2. 猫猫族群系统
3. 猫猫属性系统
4. 猫猫技能系统
5. 货币系统
6. 招募&练兵系统
7. 祭祀系统
8. 商店系统
9. 一次性道具系统
10. 饰品系统
11. 上阵与战斗系统
12. 玩法循环设计
13. 数据表结构
14. UI/UX需求

---

## 实现状态

### ✅ Phase 1：数据层（已完成）
| 模块 | 文件路径 | 说明 |
|-----|---------|------|
| 核心数据 | `TribeSystem/TribeData.cs` | 定义了所有数据结构 |
| 配置表 | `StreamingAssets/Tables/tribe_config.json` | 六大族群配置 |
| 配置表 | `StreamingAssets/Tables/quality_config.json` | 品质比例配置 |
| 配置表 | `StreamingAssets/Tables/recruitment_config.json` | 招募选项配置 |
| 配置表 | `StreamingAssets/Tables/ritual_config.json` | 祭祀奖励配置 |
| 配置表 | `StreamingAssets/Tables/shop_config.json` | 商店配置 |
| 数据管理 | `Framework/DataManager.cs` | 新增族群相关字段和方法 |

### ✅ Phase 2：逻辑层（已完成）
| 模块 | 文件路径 | 说明 |
|-----|---------|------|
| 属性计算 | `TribeSystem/TribeStatsCalculator.cs` | 族长/小猫属性计算、统帅惩罚 |
| 配置加载 | `TribeSystem/TribeConfigLoader.cs` | 加载和管理配置数据 |
| 招募服务 | `TribeSystem/RecruitmentService.cs` | 招募选项生成和执行 |
| 祭祀服务 | `TribeSystem/RitualService.cs` | 祭祀流程和奖励 |
| 商店服务 | `TribeSystem/ShopService.cs` | 商店生成和买卖 |

### ✅ Phase 3：UI层（已完成）
| 模块 | 文件路径 | 说明 |
|-----|---------|------|
| 招募面板 | `UI/TribeBuild/RecruitmentPanel.cs` | 强制弹窗，三选一，支持运行时UI |
| 祭祀面板 | `UI/TribeBuild/RitualPanel.cs` | 两步选择弹窗，支持运行时UI |
| 商店面板 | `UI/TribeBuild/ShopPanel.cs` | 可选商店界面，支持运行时UI |
| 主面板 | `UI/TribeBuild/TribeBuildPanel.cs` | 主界面，继承UIPanel |

### ✅ Phase 4：战斗适配（已完成）
| 模块 | 文件路径 | 说明 |
|-----|---------|------|
| 战斗面板 | `UI/Panels/BattlePanel.cs` | 支持族长+小猫生成，移除赐福系统 |
| 战斗准备 | `UI/Panels/BattlePreparePanel.cs` | 族群选择界面，点击切换上阵 |
| 战斗恢复 | `BattlePanel.ProcessPostBattleRecovery()` | 失败族长休息1回合 |

### ✅ Phase 5：流程整合（已完成）
| 模块 | 文件路径 | 说明 |
|-----|---------|------|
| 回合管理 | `TribeSystem/RoundManager.cs` | 20回合循环，事件检测 |
| 存档管理 | `TribeSystem/TribeSaveManager.cs` | 关键时刻自动存档 |

---

## 1. 核心玩法概述

### 玩法定位
- **类型**：族群统帅策略游戏
- **核心循环**：招募&练兵 → 祭祀祈福 → 商店采购 → 上阵战斗
- **游戏时长**：约20回合（可扩展）
- **难度曲线**：通过回合数递增，第20回合Boss战

### 核心变化（相比旧版本）
| 移除系统 | 替代/新增系统 |
|---------|--------------|
| 个体猫卡牌构筑 | 族群统帅制（1族长+N小兵） |
| 练功房玩法 | 招募&练兵（强制三选一） |
| 个体猫特质 | 族群心情系统 |
| 技能传习 | 商店购买技能（族长学习） |
| 精力值系统 | 完全移除 |
| 猫村死亡玩法 | 阵亡下回合恢复 |
| 收容流浪猫 | 招募新族群/已有族群扩容 |

---

## 2. 猫猫族群系统

### 2.1 族群设定

#### 六大族群
1. **缅因猫族** - 均衡型，初始小猫3只
2. **狸花猫族** - 攻击型，初始小猫5只
3. **大橘猫族** - 坦克型，初始小猫2只
4. **奶牛猫族** - 防御型，初始小猫4只
5. **暹罗猫族** - 敏捷型，初始小猫4只
6. **布偶猫族** - 特殊型，初始小猫3只

#### 族群结构
```
族群
├── 族长（Leader）
│   ├── 模型：等比例放大版本
│   ├── 属性：攻、防、血、速、统帅
│   └── 技能：最多3个（含普攻）
└── 小猫（Cats）
    ├── 数量：根据初始配置或招募获得
    ├── 品质：白/蓝/紫/金
    └── 属性：攻、防、血、速（基于族长属性百分比）
```

### 2.2 数据结构（已实现）

#### 核心枚举类型
```csharp
// 文件: TribeSystem/TribeData.cs
public enum TribeType
{
    Maine,      // 缅因猫族 - 均衡型
    Tabby,      // 狸花猫族 - 攻击型
    Orange,     // 大橘猫族 - 坦克型
    Cow,        // 奶牛猫族 - 防御型
    Siamese,    // 暹罗猫族 - 敏捷型
    Ragdoll     // 布偶猫族 - 特殊型
}

public enum CatQuality
{
    White,      // 菜鸟 - 10%~20%
    Blue,       // 老手 - 20%~30%
    Purple,     // 精英 - 30%~40%
    Gold        // 大师 - 40%~50%
}

public enum StatType
{
    Attack, Defense, Hp, Speed, Command
}
```

#### TribeRecord（族群数据）
```csharp
// 文件: TribeSystem/TribeData.cs
[System.Serializable]
public class TribeRecord
{
    public int tribeId;              // 族群ID
    public TribeType tribeType;      // 族群类型
    public LeaderData leader;        // 族长数据
    public List<CatData> cats;       // 小猫列表
    public string moodId;            // 当前心情ID
    public bool isActive;            // 是否激活

    // 辅助方法
    public int GetCatCount();        // 获取小猫总数
    public bool IsLeaderResting();   // 检查族长是否在休息
}
```

#### LeaderData（族长数据）
```csharp
[System.Serializable]
public class LeaderData
{
    public int leaderId;
    public string name;
    public int baseAttack;
    public int baseDefense;
    public int baseHp;
    public int baseSpeed;
    public int command;              // 统帅力
    public List<int> skillIds;       // 技能ID列表（最多3个）
    public PermanentBuffs permanentBuffs;   // 永久加成
    public TemporaryBuff temporaryBuff;     // 限时加成
    public int restTurns;            // 休息剩余回合
}
```

#### CatData（小猫数据）
```csharp
[System.Serializable]
public class CatData
{
    public long catId;
    public CatQuality quality;
    public float attackMultiplier;   // 攻击比例 0.1~0.5
    public float defenseMultiplier;
    public float hpMultiplier;
    public float speedMultiplier;

    // 创建指定品质的小猫
    public static CatData CreateWithQuality(CatQuality quality);

    // 尝试进化到下一品质（50%概率）
    public bool TryEvolve();
}
```

### 2.3 小猫品质系统

#### 品质与属性比例
| 品质 | 名称 | 属性比例 | 基础概率 |
|-----|------|---------|---------|
| White | 菜鸟 | 10%~20% | 40% |
| Blue | 老手 | 20%~30% | 30% |
| Purple | 精英 | 30%~40% | 20% |
| Gold | 大师 | 40%~50% | 10% |

#### 品质规则
- 小猫属性 = 族长基础属性 × 品质比例区间内的随机值
- 族长属性变化时，小猫属性同步变化
- 品质可通过特定方式提升
- 新获得族群的小猫默认为白色品质

### 2.4 族群获得与成长

#### 初始选择
- 游戏开始：六选二，玩家选择2个族群作为初始战力
- 初始小猫品质：固定白色

#### 游戏中扩容
- 特殊事件回合可获得新族群（上限4个）
- 可选择增加已有族群的小猫数量/质量
- 新族群小猫数量与初始一致，品质白色

---

## 3. 猫猫属性系统（已实现）

### 3.1 属性计算器 API

**文件位置**: `TribeSystem/TribeStatsCalculator.cs`

```csharp
public static class TribeStatsCalculator
{
    // 计算族长的最终属性（包含所有加成）
    public static LeaderStats CalculateLeaderStats(LeaderData leader);

    // 计算小猫的实际属性（基于族长属性）
    public static CatStats CalculateCatStats(CatData cat, LeaderStats leaderStats);

    // 计算统帅惩罚系数（0.5~1.0，1.0表示无惩罚）
    public static float CalculateSpeedPenaltyCoefficient(int catCount, int command);

    // 应用统帅惩罚后的速度
    public static int ApplyCommandPenaltyToSpeed(int baseSpeed, int catCount, int command);

    // 计算小猫实战速度（考虑统帅惩罚）
    public static int CalculateCatEffectiveSpeed(CatData cat, LeaderStats leaderStats,
                                                   int totalCatCount, int command);

    // 获取品质对应的属性比例范围
    public static (float min, float max) GetQualityRatioRange(CatQuality quality);

    // 随机生成指定品质的属性比例
    public static float RandomQualityMultiplier(CatQuality quality);

    // 根据基础概率随机生成品质
    public static CatQuality RandomCatQuality();

    // 计算伤害（技能系数 × 攻击力）
    public static int CalculateDamage(float skillCoefficient, int attack);

    // 计算受到的伤害（考虑防御）
    public static int CalculateReceivedDamage(float skillCoefficient, int enemyAttack, int myDefense);

    // 计算实际移动速度（基于速度属性）
    public static float CalculateMovementSpeed(int speedAttribute);

    // 计算攻击频率（基于速度属性）
    public static float CalculateAttackFrequency(int speedAttribute);
}
```

### 3.2 属性类型
| 属性 | 说明 | 计算方式 |
|-----|------|---------|
| 攻击 | 影响技能伤害 | 基础 × (1 + 百分比加成) + 绝对值加成 |
| 防御 | 减少受到伤害 | 基础 × (1 + 百分比加成) + 绝对值加成 |
| 血量 | 生命值上限 | 基础 × (1 + 百分比加成) + 绝对值加成 |
| 速度 | 影响移动/攻击频率 | 基础 × (1 + 百分比加成) + 绝对值加成 |
| 统帅 | 影响小猫战斗力 | 数值决定可带领小猫数量 |

#### 小猫属性
- 继承族长属性的百分比（根据品质决定）
- 无统帅属性

### 3.2 属性计算公式

#### 伤害计算
```
技能伤害 = 技能系数 × 攻击力
受到伤害 = MAX{(敌方攻击 - 我方防御), 0} / 敌方攻击 × 技能系数
```

#### 速度计算
```
实际移动速度 = {(速度属性 - 1000) / 1000} + 1
攻击频率 = 速度属性 / 2000
标准速度：1000 = 1单位/秒移动速度，0.5攻击频率（2秒1次）
```

#### 统帅效果
```
当小猫数量 ≤ 统帅力：属性正常
当小猫数量 > 统帅力：
  速度下降 = MIN{(超出数量 / 统帅力 - 1) × 10%, 50%}
  即：每超出10%速度下降10%，最多下降50%
```

### 3.3 属性加成来源

#### 永久加成（本存档永久生效）
- 奇物装备
- 一次性道具使用
- 祭祀祈福

#### 限时加成（指定回合内生效）
- 祭祀特定选项
- 一次性战斗道具

---

## 4. 猫猫技能系统

### 4.1 技能设定

#### 技能归属
- 技能绑定种族，而非个体
- 同一种族下，族长与小猫技能相同

#### 技能配置
- 每个种族独立技能池
- 族长初始仅拥有普攻（技能系数100%）
- 商店可购买新技能让族长学习
- 每个族长最多3个技能（含普攻）

### 4.2 技能学习

#### 学习方式
- 商店购买技能书
- 消耗猫粮
- 技能满时无法学习新技能

#### 技能示例（待补充）
每个种族的专属技能设计待后续补充。

---

## 5. 货币系统

### 5.1 猫粮（唯一货币）

#### 初始数值
- 固定1000猫粮开局

#### 产出来源
- 战斗胜利（固定数值）
- 商店卖出道具
- 祭祀奖励

#### 消耗场景
- 招募&练兵（200~300猫粮）
- 祭祀（0~600猫粮可选）
- 商店购买道具/小猫（50~500猫粮）
- 商店刷新（递增消耗）

---

## 6. 招募&练兵系统（已实现）

### 6.1 招募服务 API

**文件位置**: `TribeSystem/RecruitmentService.cs`

```csharp
public class RecruitmentService
{
    // 生成三个随机招募选项
    public List<RecruitmentOption> GenerateOptions();

    // 执行新增族群招募
    public TribeRecord ExecuteNewTribeRecruitment(TribeType tribeType, long cost);

    // 执行增加小猫（返回实际增加数量）
    public int ExecuteAddCats(TribeRecord tribe, long cost);

    // 执行品质进化（返回进化成功数量）
    public int ExecuteQualityEvolution(TribeRecord tribe, long cost);

    // 执行族长属性提升
    public bool ExecuteLeaderBoost(TribeRecord tribe, StatType statType, long cost);
}
```

### 6.2 招募选项数据结构

```csharp
public enum RecruitmentOptionType
{
    NewTribe,           // 新增族群
    AddCats,            // 增加小猫
    QualityEvolution,   // 品质进化
    LeaderBoost         // 族长强化
}

[System.Serializable]
public class RecruitmentOption
{
    public RecruitmentOptionType optionType;
    public int cost;                          // 消耗猫粮
    public TribeType? targetTribeType;        // 目标族群类型（新增族群时）
    public int targetTribeId;                 // 目标族群ID（已有族群操作时）
    public string description;                // 描述文本
}
```

### 6.3 系统定位
- **强制操作**：每回合必须完成一次招募&练兵
- **三选一机制**：强制弹出三个选项，玩家必须选择其一

### 6.2 招募选项类型

#### 选项A：新增族群种类
- **触发条件**：当前族群数量 < 4
- **消耗**：300猫粮
- **效果**：获得一个新族群（六选一随机）

#### 选项B：已有族群新增数量
- **触发条件**：已有族群
- **消耗**：200猫粮
- **效果**：指定族群增加小猫
  - 缅因：+3只
  - 狸花：+5只
  - 大橘：+2只
  - 奶牛：+4只
  - 暹罗：+4只
  - 布偶：+3只

#### 选项C：已有族群品质进化
- **触发条件**：已有族群
- **消耗**：150猫粮
- **效果**：指定族群每只小猫50%概率进化至下一品质

#### 选项D：已有族群族长属性提升
- **触发条件**：已有族群
- **消耗**：150猫粮
- **效果**：指定族群族长攻/防/血/速/统帅提升20%

### 6.3 UI需求
- 强制弹窗，不可跳过
- 三个选项卡片化展示
- 显示消耗、效果描述
- 选择后自动关闭，进入下一阶段

---

## 7. 祭祀系统（已实现）

### 7.1 祭祀服务 API

**文件位置**: `TribeSystem/RitualService.cs`

```csharp
public class RitualService
{
    // 检查当前回合是否可以祭祀
    public bool CanDoRitual(int currentRound);

    // 获取祭祀种族选项（三选一）
    public List<TribeRecord> GetRitualRaceOptions();

    // 执行祭祀，返回奖励
    public RitualReward ExecuteRitual(TribeRecord tribe, int cost);
}
```

### 7.2 祭祀奖励数据结构

```csharp
public enum RitualRewardType
{
    LeaderStatBoostTemporary,   // 族长属性临时提升
    LeaderStatBoostPermanent,   // 族长属性永久提升（绝对值）
    LeaderStatBoostPercent,     // 族长属性永久提升（百分比）
    Cats,                       // 小猫
    Consumable,                 // 一次性道具
    CatFood,                    // 猫粮
    Accessory                   // 饰品
}

[System.Serializable]
public class RitualReward
{
    public List<RitualRewardItem> rewards;
}

[System.Serializable]
public class RitualRewardItem
{
    public RitualRewardType rewardType;
    public StatType? statType;       // 属性类型
    public int amount;               // 数值
    public int catCount;             // 小猫数量
    public TribeType catTribeType;   // 小猫族群
    public CatQuality? catQuality;   // 小猫品质
    public int consumableId;         // 道具ID
    public int accessoryId;          // 饰品ID
}
```

### 7.3 系统定位
- **强制操作**：从第3回合开始，每3回合强制一次
- **两步选择**：选择种族 → 选择消耗

### 7.2 祭祀流程

#### 第一步：选择种族
- 三选一：从已有族群中随机选3个
- 必须选择一个

#### 第二步：选择消耗
- 三档消耗：
  1. 0猫粮（免费）
  2. 50~150猫粮（随机生成具体数值）
  3. 400~600猫粮（随机生成具体数值）

### 7.3 祭祀奖励

#### 0消耗组
随机获得1~2项：
- 本回合某种族族长某属性提升X点（限时）
- 获得1~3只某种族白色品质小猫
- 获得一次性道具
- 获得1~100点猫粮

#### 50~150消耗组
随机获得1~2项：
- 某种族族长某属性永久提升X点
- 获得1~3只某种族蓝/紫品质小猫（每只独立50%概率）
- 获得1个随机一次性道具
- 获得2~5倍消耗数量的猫粮

#### 400~600消耗组
随机获得1~2项：
- 某种族族长某属性永久提升X%
- 获得1~3只某种族紫/金品质小猫（每只独立50%概率）
- 获得3个随机一次性道具
- 获得1个饰品
- 获得3~6倍消耗数量的猫粮

### 7.4 配置需求
- 奖励池需可配置
- 概率分布需可调整
- 属性提升数值需可配置

---

## 8. 商店系统（已实现）

### 8.1 商店服务 API

**文件位置**: `TribeSystem/ShopService.cs`

```csharp
public class ShopService
{
    // 检查当前回合是否可以开放商店
    public bool CanOpenShop(int currentRound);

    // 生成商店商品列表（5个）
    public List<ShopItem> GenerateShopItems();

    // 计算刷新消耗
    public int CalculateRefreshCost();

    // 执行刷新（返回新商品列表，失败返回null）
    public List<ShopItem> RefreshShop();

    // 购买物品
    public bool BuyItem(ShopItem item);

    // 卖出消耗品（返回实际售价）
    public int SellConsumable(int consumableId, int basePrice);

    // 卖出小猫（返回实际售价）
    public int SellCat(TribeRecord tribe, CatData cat);
}
```

### 8.2 商店物品数据结构

```csharp
public enum ShopItemType
{
    Artifact,       // 奇物
    Consumable,     // 一次性道具
    Cat             // 小猫
}

[System.Serializable]
public class ShopItem
{
    public int itemId;
    public ShopItemType itemType;
    public TribeType? catTribeType; // 猫的族群类型
    public CatQuality? catQuality;  // 猫的品质
    public int basePrice;
    public string name;
    public string description;

    // 获取实际价格（有随机浮动）
    public int GetActualPrice();
}
```

### 8.3 系统定位
- **自选操作**：非强制，玩家可选择是否购买
- **开放时机**：从第5回合开始，每5回合开放一次

### 8.2 商店机制

#### 商品数量
- 固定5个道具位
- 可消耗猫粮刷新

#### 刷新消耗
- 第一次：50猫粮
- 后续：每次+50猫粮（递增）

### 8.3 商品类型

#### 可购买
| 类型 | 价格 | 说明 |
|-----|------|-----|
| 奇物 | 500猫粮 | 固定价格 |
| 一次性道具 | 50~100猫粮 | 基础价格 |
| 已有族群小猫 | 基础×50%~150% | 随机浮动 |

#### 可卖出
| 类型 | 价格 | 说明 |
|-----|------|-----|
| 一次性道具 | 原价50% | 固定5折 |
| 小猫 | 可配置 | 根据品种/品质 |

### 8.4 商店流程
- 商店出现后，回合结束前可：
  - 随意刷新商品
  - 买进卖出
  - 查看族群/道具详情

---

## 9. 一次性道具系统

### 9.1 道具分类

#### 战斗中使用
- 爆炸类：范围伤害
- 冰冻类：冻结敌人
- DoT类：持续伤害
- 回复类：恢复生命
- 增益类：临时属性提升
- **限制**：公共CD，不可连续使用

#### 准备环节使用
- 增加猫粮
- 给指定族群增加单回合buff

### 9.2 道具获取
- 战斗胜利（概率获得）
- 商店购买
- 祭祀奖励

### 9.3 道具销毁
- 使用后销毁
- 商店出售销毁

### 9.4 初始实现
- 暂实现2个战斗道具
- 暂实现1个准备道具

---

## 10. 饰品系统

### 10.1 饰品图鉴

#### 图鉴状态
| 状态 | 说明 | 显示内容 |
|-----|------|---------|
| 未解锁 | 从未在任何存档获得 | 灰色?图标占位 |
| 激活 | 当前存档已获得 | 彩色图标+名字+效果 |
| 未激活 | 其他存档获得，当前未获得 | 灰色图标+名字+效果 |

### 10.2 饰品效果
- 参考《兄弟》设计
- 提供族群级效果
- 可装备于族群（具体规则待定）

### 10.3 获取途径
- 祭祀高消耗组
- 战斗胜利（低概率）

---

## 11. 上阵与战斗系统

### 11.1 上阵规则

#### 族群选择
- 最少1个族群
- 最多所有族群
- 选择族群的所有小猫全部上阵

#### 上阵消耗
- 消耗猫粮（数量可配置）
- 族群越多，消耗越高
- 胜利奖励固定

#### 阵型布置
- **不可布置阵型**（简化设计）
- 随机出现在场内
- 按就近原则开始战斗

### 11.2 战斗规则

#### 胜利条件
- 消灭所有敌人
- 获得战利品
- 进入下一回合

#### 失败条件
- 战斗失败
- 随机损失1个参战族群
- 剩余1个族群时失败

### 11.3 阵亡恢复

#### 小猫阵亡
- 下回合立即恢复

#### 族长阵亡
- 需休息一回合
- 休息期间该族群不能参战

---

## 12. 玩法循环设计

### 12.1 回合结构

每回合 = 战前准备 + 战斗阶段

#### 第一回合
1. **战前准备**
   - 六选二部族组建初始猫村（新游戏开局）
   - 可使用一次性道具
   - 查看饰品图鉴
   - 查看族群效果
   - 选择上阵族群，开始战斗
2. **战斗阶段**
   - 上阵猫猫与敌人决斗

#### 第二回合
1. **战前准备**
   - 强制弹出招募&练兵三选一
   - 可使用一次性道具
   - 查看饰品图鉴/族群效果
   - 选择上阵族群，开始战斗
2. **战斗阶段**
   - 上阵猫猫与敌人决斗

#### 第三回合
1. **战前准备**
   - 强制弹出招募&练兵三选一
   - 强制弹出祭祀（从第3回开始，每3回一次）
   - 可使用一次性道具
   - 查看饰品图鉴/族群效果
   - 选择上阵族群，开始战斗
2. **战斗阶段**
   - 上阵猫猫与敌人决斗

#### 第五回合
1. **战前准备**
   - 强制弹出招募&练兵三选一
   - 提示出现商店（从第5回开始，每5回一次）
   - 可使用一次性道具
   - 查看饰品图鉴/族群效果
   - 选择上阵族群，开始战斗
2. **战斗阶段**
   - 上阵猫猫与敌人决斗

### 12.2 多事件处理
- 招募、祭祀、商店同时出现时：
- 顺序：招募（强制）→ 祭祀（强制）→ 商店（非强制）

### 12.3 游戏长度
- 初步预计20组循环
- 第20回合：Boss来袭
- 挑战成功：通关，开启竞速/伤害排行榜
- 挑战失败：重新开局

### 12.4 存档点
- 招募前
- 招募选择后
- 祭祀前
- 祭祀选择后
- 商店出现前
- 购买道具后（防止刷道具）
- 战斗结束时刻

---

## 13. 数据表结构（已实现）

### 13.1 配置文件

所有配置文件位于 `StreamingAssets/Tables/` 目录，由 `TribeConfigLoader` 加载。

#### tribe_config.json（族群配置）
**文件路径**: `StreamingAssets/Tables/tribe_config.json`

```json
{
  "tribes": [
    {
      "tribeType": 0,
      "tribeName": "缅因猫族",
      "initialCatCount": 3,
      "leaderBaseStats": {
        "attack": 100,
        "defense": 80,
        "hp": 1000,
        "speed": 1000,
        "command": 10
      }
    },
    {
      "tribeType": 1,
      "tribeName": "狸花猫族",
      "initialCatCount": 5,
      "leaderBaseStats": {
        "attack": 120,
        "defense": 60,
        "hp": 900,
        "speed": 1100,
        "command": 12
      }
    }
    // ... 其他族群
  ]
}
```

#### quality_config.json（品质配置）
**文件路径**: `StreamingAssets/Tables/quality_config.json`

```json
{
  "qualities": [
    {
      "quality": 0,
      "qualityName": "菜鸟",
      "minRatio": 0.1,
      "maxRatio": 0.2,
      "baseProbability": 0.4
    },
    {
      "quality": 1,
      "qualityName": "老手",
      "minRatio": 0.2,
      "maxRatio": 0.3,
      "baseProbability": 0.3
    }
    // ... 蓝色、紫色、金色
  ]
}
```

#### recruitment_config.json（招募配置）
**文件路径**: `StreamingAssets/Tables/recruitment_config.json`

```json
{
  "options": {
    "newTribe": {
      "cost": 300,
      "description": "获得一个新的族群"
    },
    "addCats": {
      "cost": 200,
      "description": "为已有族群增加小猫",
      "catCounts": {
        "0": 3,  // 缅因
        "1": 5,  // 狸花
        "2": 2,  // 大橘
        "3": 4,  // 奶牛
        "4": 4,  // 暹罗
        "5": 3   // 布偶
      }
    },
    "qualityEvolution": {
      "cost": 150,
      "description": "已有族群小猫品质进化",
      "evolutionChance": 0.5
    },
    "leaderBoost": {
      "cost": 150,
      "description": "已有族群族长属性提升",
      "boostPercent": 0.2
    }
  }
}
```

#### ritual_config.json（祭祀配置）
**文件路径**: `StreamingAssets/Tables/ritual_config.json`

```json
{
  "tiers": [
    {
      "tierName": "free",
      "costRange": [0, 0],
      "rewardCount": [1, 2],
      "rewards": [
        {
          "type": "LeaderStatBoostTemporary",
          "weight": 30,
          "statTypes": ["attack", "defense", "hp", "speed"],
          "minAmount": 10,
          "maxAmount": 50
        }
        // ... 其他奖励
      ]
    }
    // ... low, high 档位
  ],
  "ritualInterval": 3,
  "startRound": 3
}
```

#### shop_config.json（商店配置）
**文件路径**: `StreamingAssets/Tables/shop_config.json`

```json
{
  "baseRefreshCost": 50,
  "refreshIncrement": 50,
  "slotCount": 5,
  "shopInterval": 5,
  "startRound": 5,
  "items": {
    "artifact": {
      "basePrice": 500
    },
    "consumable": {
      "basePriceMin": 50,
      "basePriceMax": 100
    },
    "cat": {
      "basePrices": {
        "0": 100,
        "1": 80,
        "2": 120
      },
      "qualityBonusMultipliers": {
        "0": 1.0,
        "1": 2.0,
        "2": 4.0,
        "3": 8.0
      },
      "priceVariation": 0.5
    }
  }
}
```

### 13.2 存档数据结构（已实现）

#### PlayerData 扩展
**文件位置**: `Framework/DataManager.cs`

```csharp
[System.Serializable]
public class PlayerData
{
    // 原有字段...
    public string playerId;
    public string playerName;
    // ...

    // TribeSystem 新增字段
    public List<TribeSystem.TribeRecord> tribes;      // 族群列表
    public int currentRound;                          // 当前回合
    public long catFood;                              // 猫粮数量
    public List<string> unlockedAccessories;          // 饰品图鉴解锁
    public int shopRefreshCount;                      // 商店刷新次数
    public int lastShopRound;                         // 上次商店回合

    // 旧系统字段（标记为 Obsolete）
    [System.Obsolete("Use TribeSystem instead")]
    public List<CatRecord> catRoster;
    // ...
}
```

#### DataManager 新增方法
```csharp
// 族群管理
public TribeRecord AddTribe(TribeRecord tribe, bool saveImmediately = true);
public List<TribeRecord> GetTribes();
public TribeRecord GetTribe(int tribeId);
public bool RemoveTribe(int tribeId, bool saveImmediately = true);

// 回合管理
public int GetCurrentRound();
public void SetCurrentRound(int round, bool saveImmediately = true);

// 猫粮管理
public long GetCatFood();
public void SetCatFood(long amount, bool saveImmediately = true);
public void AddCatFood(long amount, bool saveImmediately = true);
public bool TrySpendCatFood(long amount, bool saveImmediately = true);

// 饰品图鉴
public void UnlockAccessory(string accessoryId, bool saveImmediately = true);
public bool IsAccessoryUnlocked(string accessoryId);

// 商店管理
public int GetShopRefreshCount();
public void SetShopRefreshCount(int count, bool saveImmediately = true);
public void IncrementShopRefreshCount(bool saveImmediately = true);
public int GetLastShopRound();
public void SetLastShopRound(int round, bool saveImmediately = true);
```

---

## 14. UI/UX需求

### 14.1 核心界面

#### 主界面（待重构）
- 显示当前回合数
- 显示猫粮数量
- 显示拥有的族群列表
- 各功能区入口

#### 族群详情面板
- 族长信息（属性、技能）
- 小猫列表（品质、属性比例）
- 族群心情效果
- 上阵/休息状态

#### 招募&练兵弹窗
- 三个选项卡片
- 强制选择标识
- 消耗与效果说明

#### 祭祀弹窗
- 种族选择（三选一）
- 消耗选择（三档）
- 奖励预览（可选）

#### 商店界面
- 5个商品位
- 刷新按钮与价格
- 买入卖出操作

### 14.2 交互规范

#### 强制操作
- 无法跳过
- 明确标识"强制选择"
- 遮盖其他操作

#### 自选操作
- 可随时关闭
- 保存当前状态
- 可重新打开

---

## 15. 待补充系统

### 15.1 猫猫心情（后续补充）
- 每三回合族长获得随机心情
- 心情影响族群属性/战斗
- 示例：
  - 贪杯：速度-20%，攻击+30%
  - 胆小如鼠：遇老鼠时速度+20%，防御+20%，攻击-10%
- 可通过奇物/商店替换

### 15.2 扩展功能（有时间制作）
- 增加命中、闪避属性
- 族长固定名字，玩家可修改
- 小猫随机名字
- 显示小猫具体属性列表
- 每回合消耗猫粮（基于小猫总数）
- 饰品改为装备生效（每族长3个饰品位）
- 战斗可布置出战位置/顺序
- 多波次敌人（植物大战僵尸模式）
- 随机地形
- 受击僵直、霸体、击飞等效果

---

---

## 关键文件索引

### 数据结构
- `Assets/Scripts/Game/TribeSystem/TribeData.cs` - 核心数据类定义

### 服务类
- `Assets/Scripts/Game/TribeSystem/TribeStatsCalculator.cs` - 属性计算
- `Assets/Scripts/Game/TribeSystem/TribeConfigLoader.cs` - 配置加载
- `Assets/Scripts/Game/TribeSystem/RecruitmentService.cs` - 招募服务
- `Assets/Scripts/Game/TribeSystem/RitualService.cs` - 祭祀服务
- `Assets/Scripts/Game/TribeSystem/ShopService.cs` - 商店服务

### 数据管理
- `Assets/Scripts/Framework/DataManager.cs` - 持久化管理

### UI面板
- `Assets/Scripts/UI/TribeBuild/TribeBuildPanel.cs` - 主面板
- `Assets/Scripts/UI/TribeBuild/RecruitmentPanel.cs` - 招募面板
- `Assets/Scripts/UI/TribeBuild/RitualPanel.cs` - 祭祀面板
- `Assets/Scripts/UI/TribeBuild/ShopPanel.cs` - 商店面板
- `Assets/Scripts/UI/Panels/BattlePanel.cs` - 战斗面板
- `Assets/Scripts/UI/Panels/BattlePreparePanel.cs` - 战斗准备

### 配置文件
- `StreamingAssets/Tables/tribe_config.json` - 族群配置
- `StreamingAssets/Tables/quality_config.json` - 品质配置
- `StreamingAssets/Tables/recruitment_config.json` - 招募配置
- `StreamingAssets/Tables/ritual_config.json` - 祭祀配置
- `StreamingAssets/Tables/shop_config.json` - 商店配置
- `StreamingAssets/battle_campaign_levels.json` - 战斗关卡配置

---

需求以 `DesignDocs/新需求/新的需求改动很大.txt` 为准，本文档为系统设计参考。
