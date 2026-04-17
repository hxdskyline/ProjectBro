# Unity 预制体创建指南

本文档详细说明如何为族群系统创建Unity预制体。

---

## 1. TribeBuildPanel 预制体

**路径:** `Assets/Bundle/UI/TribeBuild/TribeBuildPanel.prefab`

### UI层次结构
```
TribeBuildPanel (RectTransform, Image, TribeBuildPanel)
├── TitlePanel (RectTransform)
│   └── TitleText (Text) - "族群管理"
├── InfoPanel (RectTransform)
│   ├── RoundText (Text) - "第1/20回合 [商店]"
│   ├── CatFoodText (Text) - "猫粮: 1000"
│   └── DeployCostText (Text) - "上阵消耗: 0"
├── TribeListContainer (RectTransform, GridLayoutGroup) - 左上角对齐
│   └── [运行时动态生成族群卡片]
├── ButtonPanel (RectTransform)
│   ├── BackButton (Button) - "返回"
│   ├── ShopButton (Button) - "商店"
│   └── StartBattleButton (Button) - "开始战斗"
└── ForcedPopupRoot (RectTransform) - 用于强制弹窗
```

### 组件设置

| 对象 | 组件 | 设置 |
|------|------|------|
| TribeBuildPanel | RectTransform | Anchor: Stretch, Size: (800, 600) |
| TribeBuildPanel | Image | Color: (0.1, 0.1, 0.12, 0.98) |
| TribeBuildPanel | TribeBuildPanel | 绑定下方字段 |
| TribeListContainer | GridLayoutGroup | Cell Size: (200, 120), Spacing: (10, 10), Start Corner: Upper Left, Child Alignment: Upper Left |

### 序列化字段绑定

```
_recruitmentPanel -> RecruitmentPanel 子对象（或拖入预制体，会自动实例化）
_ritualPanel -> RitualPanel 子对象（或拖入预制体，会自动实例化）
_shopPanel -> ShopPanel 子对象（或拖入预制体，会自动实例化）

_roundText -> InfoPanel/RoundText
_catFoodText -> InfoPanel/CatFoodText
_tribeListContainer -> TribeListContainer
_deployCostText -> InfoPanel/DeployCostText
_startBattleButton -> ButtonPanel/StartBattleButton
_shopButton -> ButtonPanel/ShopButton
_backButton -> ButtonPanel/BackButton
_forcedPopupRoot -> ForcedPopupRoot
```

### 子面板设置方式（二选一）

**方式一：作为子对象（推荐）**
1. 创建 TribeBuildPanel 预制体
2. 将 RecruitmentPanel、RitualPanel、ShopPanel 预制体拖入 TribeBuildPanel 作为子对象
3. 在 Inspector 中绑定子对象引用

**方式二：作为预制体引用**
1. 直接将预制体拖入 Inspector 的对应字段
2. 代码会自动检测并实例化预制体

---

## 1.1 TribeCard 预制体

**路径:** `Assets/Bundle/UI/TribeBuild/TribeCard.prefab`

### UI层次结构
```
TribeCard (RectTransform, Image, Toggle, TribeCard)
├── Portrait (Image) - 族群头像（左侧大图）
├── InfoPanel (RectTransform) - 右侧信息区
│   ├── Name (Text) - "缅因"
│   ├── Count (Text) - "小猫: 3 只"
│   ├── Status (Text) - "状态: 可出战"
│   └── Stats (Text) - "攻100 防80..."
└── DetailButton (Button) - 点击显示详情（可以是整个卡片或一个小图标按钮）
```

### 组件设置

| 对象 | 组件 | 设置 |
|------|------|------|
| TribeCard | RectTransform | Size: (200, 120) |
| TribeCard | Image | 背景色（根据族群类型动态设置） |
| TribeCard | Toggle | Group: 可选设置 ToggleGroup |
| TribeCard | TribeCard | 绑定下方字段 |
| Portrait | Image | 持族头像图片，由代码动态加载 |

### 序列化字段绑定

```
_nameText -> InfoPanel/Name
_countText -> InfoPanel/Count
_statusText -> InfoPanel/Status
_statsText -> InfoPanel/Stats
_backgroundImage -> TribeCard (自身的 Image)
_portraitImage -> Portrait
_toggle -> TribeCard (自身的 Toggle)
_detailButton -> DetailButton（可以是整个卡片背景或单独的按钮）
```

### 交互说明

- **Toggle**: 用于上阵/下阵切换
- **DetailButton**: 点击弹出详细 Tips 面板（可设为卡片背景，点击任意位置显示详情）

### 头像资源映射

| 族群类型 | Addressable 地址 | 文件名 |
|---------|-----------------|--------|
| 缅因 (Maine) | avatartemp/mianyin1, avatartemp/mianyin2 | mianyin1.png, mianyin2.png |
| 狸花 (Tabby) | avatartemp/lihua1, avatartemp/lihua2 | lihua1.png, lihua2.png |
| 大橘 (Orange) | avatartemp/daju1, avatartemp/daju2 | daju1.png, daju2.png |
| 奶牛 (Cow) | avatartemp/nainiu1, avatartemp/nainiu2 | nainiu1.png, nainiu2.png |
| 暹罗 (Siamese) | avatartemp/xianluo1, avatartemp/xianluo2 | xianluo1.png, xianluo2.png |
| 布偶 (Ragdoll) | avatartemp/buou1, avatartemp/buou2 | buou1.png, buou2.png |

**注意**: 代码会随机选择 variant 1 或 2 的头像。

### 颜色方案（由代码动态设置）

| 族群类型 | 背景色 RGBA |
|---------|-------------|
| 缅因 | (0.3, 0.5, 0.7, 1) |
| 狸花 | (0.6, 0.4, 0.3, 1) |
| 大橘 | (0.7, 0.5, 0.2, 1) |
| 奶牛 | (0.4, 0.4, 0.5, 1) |
| 暹罗 | (0.5, 0.4, 0.6, 1) |
| 布偶 | (0.7, 0.5, 0.6, 1) |

---

## 1.2 TribeDetailTips 预制体

**路径:** `Assets/Bundle/UI/TribeBuild/TribeDetailTips.prefab`

点击 TribeCard 时弹出的详细信息面板。

### UI层次结构
```
TribeDetailTips (RectTransform, Image, TribeDetailTips)
├── TitleBar (RectTransform)
│   ├── TribeName (Text) - "缅因猫族"
│   └── CloseButton (Button)
├── LeaderSection (RectTransform)
│   ├── LeaderTitle (Text) - "族长"
│   ├── LeaderName (Text) - "缅因族长"
│   └── LeaderStats (Text) - "攻击: 100\n防御: 80..."
├── CatsSection (RectTransform)
│   ├── CatsTitle (Text) - "小猫列表"
│   └── CatsList (RectTransform, VerticalLayoutGroup)
│       └── [运行时动态生成小猫卡片]
└── ActionSection (RectTransform)
    ├── RestButton (Button) - "族长休息"
    └── DeployButton (Button) - "上阵/下阵"
```

### 序列化字段绑定

```
_tribeNameText -> TitleBar/TribeName
_closeButton -> TitleBar/CloseButton
_leaderNameText -> LeaderSection/LeaderName
_leaderStatsText -> LeaderSection/LeaderStats
_catsListContainer -> CatsSection/CatsList
_restButton -> ActionSection/RestButton
_deployButton -> ActionSection/DeployButton
_deployButtonText -> ActionSection/DeployButton/Text (显示"上阵"或"下阵")
```

### 子面板设置方式（二选一）

**方式一：作为子对象（推荐）**
1. 创建 TribeBuildPanel 预制体
2. 将 RecruitmentPanel、RitualPanel、ShopPanel 预制体拖入 TribeBuildPanel 作为子对象
3. 在 Inspector 中绑定子对象引用

**方式二：作为预制体引用**
1. 直接将预制体拖入 Inspector 的对应字段
2. 代码会自动检测并实例化预制体

---

## 2. RecruitmentPanel 预制体

**路径:** `Assets/Bundle/UI/TribeBuild/RecruitmentPanel.prefab`

### UI层次结构
```
RecruitmentPanel (RectTransform, Image, RecruitmentPanel)
├── Title (Text) - "招募&练兵"
├── Hint (Text) - "强制选择：请选择一项招募方案"
├── OptionsContainer (RectTransform, HorizontalLayoutGroup)
│   └── [运行时动态生成3个选项卡片]
└── ConfirmButton (Button)
    └── Label (Text) - "确认选择"
```

### 组件设置

| 对象 | 组件 | 设置 |
|------|------|------|
| RecruitmentPanel | RectTransform | Anchor: Center, Size: (700, 500) |
| RecruitmentPanel | Image | Color: (0.1, 0.08, 0.12, 0.98) |
| OptionsContainer | HorizontalLayoutGroup | Spacing: 20, Padding: 15, Child Alignment: Middle Center |
| ConfirmButton | Image | Color: (0.2, 0.5, 0.3, 1) |

### 序列化字段绑定

```
_titleText -> Title
_hintText -> Hint
_optionsContainer -> OptionsContainer
_confirmButton -> ConfirmButton
```

---

## 3. RitualPanel 预制体

**路径:** `Assets/Bundle/UI/TribeBuild/RitualPanel.prefab`

### UI层次结构
```
RitualPanel (RectTransform, Image, RitualPanel)
├── Title (Text) - "祭祀祈福"
├── Hint (Text) - "第一步：选择祭祀的族群（三选一）"
├── CardsContainer (RectTransform, HorizontalLayoutGroup) - 通用卡片容器，用于两步
└── ConfirmButton (Button) - "确认祭祀"
    └── Label (Text) - "确认"
```

### 组件设置

| 对象 | 组件 | 设置 |
|------|------|------|
| RitualPanel | RectTransform | Anchor: Center, Size: (700, 500) |
| RitualPanel | Image | Color: (0.1, 0.08, 0.12, 0.98) |
| CardsContainer | HorizontalLayoutGroup | Spacing: 20, Child Alignment: Middle Center |
| CardsContainer | Image | Color: (0, 0, 0, 0.2) - 背景装饰 |
| ConfirmButton | Image | Color: (0.5, 0.3, 0.2, 1) |

### 序列化字段绑定

```
_titleText -> Title
_hintText -> Hint
_cardsContainer -> CardsContainer
_confirmButton -> ConfirmButton
```

### 注意事项

- 脚本使用单一容器 `CardsContainer` 复用于两步选择（第一步选族群，第二步选消耗档位）
- `Hint` 文本会根据当前步骤动态更新提示内容
- `ConfirmButton` 会根据步骤切换点击事件
- 卡片在运行时动态生成

---

## 4. ShopPanel 预制体

**路径:** `Assets/Bundle/UI/TribeBuild/ShopPanel.prefab`

### UI层次结构
```
ShopPanel (RectTransform, Image, ShopPanel)
├── Title (Text) - "神秘商店"
├── CatFoodText (Text) - "猫粮: 1000"
├── ItemsContainer (RectTransform, GridLayoutGroup)
│   └── [运行时动态生成5个商品卡片]
├── RefreshButton (Button)
│   ├── Label (Text) - "刷新"
│   └── CostText (Text) - "50"
└── CloseButton (Button)
    └── Label (Text) - "关闭"
```

### 组件设置

| 对象 | 组件 | 设置 |
|------|------|------|
| ShopPanel | RectTransform | Anchor: Center, Size: (700, 500) |
| ShopPanel | Image | Color: (0.08, 0.1, 0.12, 0.98) |
| ItemsContainer | GridLayoutGroup | Cell Size: (150, 180), Spacing: (15, 15), Constraint: Fixed Columns = 5 |
| RefreshButton | Image | Color: (0.2, 0.4, 0.6, 1) |
| CloseButton | Image | Color: (0.5, 0.25, 0.2, 1) |

### 序列化字段绑定

```
_titleText -> Title
_catFoodText -> CatFoodText
_itemsContainer -> ItemsContainer
_refreshButton -> RefreshButton
_refreshCostText -> RefreshButton/CostText
_closeButton -> CloseButton
```

---

## 5. BattlePreparePanel 预制体

**路径:** `Assets/Bundle/UI/BattlePreparePanel.prefab`

### UI层次结构
```
BattlePreparePanel (RectTransform, Image, BattlePreparePanel)
├── PrepareContentRoot (RectTransform)
│   ├── PrepareTitleText (Text) - "战前准备"
│   ├── PrepareSummaryText (Text) - "持有: 2 | 上阵: 1/6 | 敌人: 1"
│   ├── PrepareStatusText (Text) - "点击族群卡片进行上阵/下阵"
│   ├── OwnedTribesRoot (RectTransform, VerticalLayoutGroup) - 持有族群
│   ├── DeployedTribesRoot (RectTransform, VerticalLayoutGroup) - 上阵区域
│   ├── EnemyCardsRoot (RectTransform, VerticalLayoutGroup) - 敌人列表
│   ├── PrepareBackButton (Button) - "返回构筑"
│   └── PrepareStartButton (Button) - "进入战斗"
```

### 组件设置

| 对象 | 组件 | 设置 |
|------|------|------|
| BattlePreparePanel | RectTransform | Anchor: Stretch, Size: Full Screen |
| BattlePreparePanel | Image | Color: (0.06, 0.09, 0.14, 0.96) |
| OwnedTribesRoot | VerticalLayoutGroup | Spacing: 8, Padding: 8 |
| DeployedTribesRoot | VerticalLayoutGroup | Spacing: 8, Padding: 8 |
| EnemyCardsRoot | VerticalLayoutGroup | Spacing: 8, Padding: 8 |

### 序列化字段绑定

```
_titleText -> PrepareTitleText
_summaryText -> PrepareSummaryText
_statusText -> PrepareStatusText
_ownedTribesRoot -> OwnedTribesRoot
_deployedTribesRoot -> DeployedTribesRoot
_enemyCardsRoot -> EnemyCardsRoot
_backButton -> PrepareBackButton
_startBattleButton -> PrepareStartButton
```

---

## 6. SettingsPanel 预制体

**路径:** `Assets/Bundle/UI/SettingsPanel.prefab`
**Addressable 地址:** `ui/settingspanel`

### UI层次结构
```
SettingsPanel (RectTransform, Image, CanvasGroup, SettingsPanel)
├── Background (Image) - 半透明遮罩
├── ContentPanel (RectTransform, Image) - 主面板容器
│   ├── CloseButton (Button) - 右上角关闭
│   │   └── Label (Text) - "×"
│   ├── LeftPanel (RectTransform) - 左栏
│   │   ├── MasterVolumeToggle (Toggle) - 音量总开关
│   │   │   ├── Label (Text) - "音量总开关"
│   │   │   ├── Background (Image) - Toggle背景
│   │   │   └── Checkmark (Image) - 勾选标记
│   │   ├── SfxVolumeToggle (Toggle) - 音效开关
│   │   │   ├── Label (Text) - "音效开关"
│   │   │   ├── Background (Image)
│   │   │   └── Checkmark (Image)
│   │   └── BgmVolumeToggle (Toggle) - BGM开关
│   │       ├── Label (Text) - "BGM开关"
│   │       ├── Background (Image)
│   │       └── Checkmark (Image)
│   ├── RightPanel (RectTransform) - 右栏
│   │   └── TipsText (Text) - "祝你好运"
│   └── ButtonBar (RectTransform) - 底部按钮栏
│       ├── ConfirmButton (Button) - "确认"
│       │   └── Label (Text) - "确认"
│       └── CancelButton (Button) - "取消"
│           └── Label (Text) - "取消"
```

### 组件设置

| 对象 | 组件 | 设置 |
|------|------|------|
| SettingsPanel | RectTransform | Anchor: Stretch, Full Screen |
| SettingsPanel | Image | Color: (0, 0, 0, 0.5) - 半透明遮罩 |
| SettingsPanel | SettingsPanel | 绑定下方字段 |
| ContentPanel | RectTransform | Anchor: Center, Size: (600, 400) |
| ContentPanel | Image | Color: (0.12, 0.12, 0.15, 0.98) |
| LeftPanel | VerticalLayoutGroup | Spacing: 20, Padding: 20, Child Alignment: Upper Left |
| RightPanel | LayoutGroup | 居中显示 TipsText |
| ButtonBar | HorizontalLayoutGroup | Spacing: 40, Child Alignment: Middle Center |
| ConfirmButton | Image | Color: (0.2, 0.5, 0.3, 1) |
| CancelButton | Image | Color: (0.5, 0.25, 0.2, 1) |

### 序列化字段绑定

```
_closeButton -> ContentPanel/CloseButton
_masterVolumeToggle -> LeftPanel/MasterVolumeToggle
_sfxVolumeToggle -> LeftPanel/SfxVolumeToggle
_bgmVolumeToggle -> LeftPanel/BgmVolumeToggle
_confirmButton -> ButtonBar/ConfirmButton
_cancelButton -> ButtonBar/CancelButton
```

### 注意事项

- 关闭按钮在右上角（Anchor: Top-Right）
- 左栏和右栏使用水平排列（ContentPanel 用 HorizontalLayoutGroup 或手动布局）
- 设置面板作为 PopUp 层显示，关闭后 MainPanel 仍在底层

---

## 创建步骤

### 方式一：手动创建

1. **创建Canvas**
   - Hierarchy右键 → UI → Canvas
   - Canvas Scaler设置为 "Scale With Screen Size", Reference Resolution: (1920, 1080)

2. **创建Panel**
   - Hierarchy右键 → UI → Panel
   - 重命名为预制体名称（如 TribeBuildPanel）

3. **添加子对象**
   - 按照上述层次结构创建子对象
   - 设置每个对象的RectTransform和组件

4. **绑定脚本**
   - 将对应脚本添加到根对象
   - 在Inspector中拖拽绑定序列化字段

5. **保存为预制体**
   - 将创建好的Panel拖到 Project 窗口的对应文件夹
   - 删除Scene中的对象

### 方式二：使用代码生成（已实现）

所有Panel类都已实现运行时UI创建逻辑，可以作为后备方案：
- 如果找不到预制体或组件未绑定，会自动创建UI
- 适合快速原型开发

**推荐做法：**
- 先让程序自动生成UI
- 在Unity Editor中调整布局和样式
- 保存为预制体

---

## 命名规范

| 类型 | 命名格式 | 示例 |
|------|----------|------|
| 预制体文件 | PascalCase.prefab | TribeBuildPanel.prefab |
| GameObject | PascalCase | TitlePanel |
| Transform/子对象 | PascalCase | InfoText |
| 文本内容 | 中文 | "族群管理" |

---

## 颜色主题

| 用途 | RGBA值 |
|------|--------|
| 背景深色 | (0.06, 0.09, 0.14, 0.96) |
| 背景浅色 | (0.87, 0.91, 0.96, 0.98) |
| 按钮确认 | (0.2, 0.5, 0.3, 1) |
| 按钮关闭 | (0.5, 0.25, 0.2, 1) |
| 按钮商店 | (0.2, 0.4, 0.6, 1) |
| 文本白色 | (1, 1, 1, 1) |
| 文本标题 | (0.1, 0.15, 0.25, 1) |
| 文本金色 | (1, 0.9, 0.3, 1) |
| 文本警告 | (1, 0.7, 0.3, 1) |

---

## 字体设置

- 使用Unity内置字体: `LegacyRuntime.ttf`
- 标题字号: 24-42
- 正文字号: 14-20
- 提示字号: 16-18

---

## 测试清单

创建完成后，请测试：

- [ ] TribeBuildPanel能正确显示所有子面板
- [ ] RecruitmentPanel显示3个选项卡片
- [ ] RitualPanel能正常两步操作
- [ ] ShopPanel能正确显示5个商品
- [ ] BattlePreparePanel能正确显示族群列表
- [ ] 所有按钮点击有响应
- [ ] 回合/猫粮/消耗显示正确
- [ ] 强制弹窗使用ForcedPopupRoot显示

---

## 快速复制脚本

为了快速创建预制体结构，可以将以下Editor脚本放到 `Assets/Editor/` 目录：

```csharp
// 在Unity Editor中选择GameObject，执行菜单自动创建UI结构
```

（此部分可后续补充）
