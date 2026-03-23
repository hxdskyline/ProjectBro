# CardBuild 子面板预制体说明

此目录用于存放 CardBuild 系统的子面板预制体。

## 需要创建的预制体

| 预制体名称 | 脚本组件 | 说明 |
|-----------|---------|------|
| `OutingPanel.prefab` | `OutingPanel` | 游历面板 - 派遣猫咪外出 |
| `BlessingPanel.prefab` | `BlessingPanel` | 赐福面板 - 祈祷选择 |
| `AttributeBoostPanel.prefab` | `AttributeBoostPanel` | 属性提升面板 - 战后强化 |
| `DiscardPanel.prefab` | `DiscardPanel` | 弃猫面板 - 删除猫咪 |

## 在 Unity 中创建预制体的步骤

### 1. 创建 OutingPanel 预制体

1. 在 Hierarchy 中创建空物体，命名为 `OutingPanel`
2. 添加组件：`Image`（背景）、`OutingPanel` 脚本
3. 创建子物体结构：
   ```
   OutingPanel
   ├── Title (Text) - 标题 "游历区"
   ├── Hint (Text) - 提示文本
   ├── List (Image + HorizontalLayoutGroup) - 卡片容器
   ├── ConfirmButton (Image + Button) - 确认按钮
   │   └── Label (Text) - "确认游历"
   └── CloseButton (Image + Button) - 关闭按钮
       └── Label (Text) - "关闭"
   ```
4. 在 OutingPanel 脚本中绑定各个 UI 组件引用
5. 将整个物体拖入此目录创建预制体

### 2. 创建 BlessingPanel 预制体

结构类似 OutingPanel，但：
- 使用 `HorizontalLayoutGroup` 便于横向排列 3 张候选卡
- 只有关闭按钮，没有确认按钮
- 背景色建议使用金色系

### 3. 创建 AttributeBoostPanel 预制体

结构同 OutingPanel：
- 确认按钮文本改为 "确认强化"
- 背景色建议使用蓝色系

### 4. 创建 DiscardPanel 预制体

结构同 OutingPanel：
- 确认按钮文本改为 "确认删除"
- 确认按钮颜色使用红色（危险操作提示）
- 背景色建议使用深红色系

## 预制体绑定到 CardBuildPanel

完成预制体创建后，在 Unity 中：

1. 打开 `CardBuildPanel.prefab`
2. 找到 `CardBuildPanel` 脚本组件
3. 在 Inspector 中找到 "子面板预制体" 区域
4. 将各个子面板预制体拖入对应字段：
   - `Outing Panel Prefab` → `OutingPanel.prefab`
   - `Blessing Panel Prefab` → `BlessingPanel.prefab`
   - `Attribute Boost Panel Prefab` → `AttributeBoostPanel.prefab`
   - `Discard Panel Prefab` → `DiscardPanel.prefab`

## 回退机制

如果预制体未配置，代码会自动：
1. 尝试通过 ResourceManager 加载 `ui/CardBuild/OutingPanel` 等地址
2. 如果仍失败，则运行时动态创建 UI（使用默认布局）

因此即使不创建预制体，系统也能正常工作。
