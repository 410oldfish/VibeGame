# 文档 Layer 与命名规范

> **文档状态**：规范 v0.2  
> **Layer**：Foundation  
> **用途**：CCGS Layer 元数据、文件名前缀与文档头字段的统一约定。

## Summary

本项目策划文档只使用 CCGS 五层 Layer（Foundation / Core / Feature / Presentation / Polish），不再单独标注 Rule、Content、SpecKind。Layer 同时写在文档头与文件名前缀中；目录仍按**游戏系统**划分，不按 Layer 分文件夹。

## 1. Layer 定义

| Layer            | 含义                                   | 典型文档                                    |
| ---------------- | ------------------------------------ | --------------------------------------- |
| **Foundation**   | 无依赖的内容：跨系统语义、同步、设计规范、状态词条表           | `Foundation-状态技能与道具联动规则.md`             |
| **Core**         | 依赖**Foundation**；战斗公共时序、牌组流转、空间规则    | `Core-战斗系统.md`、`Core-六边形战斗规则.md`        |
| **Feature**      | 依赖**Core**：局外循环、职业/敌人/地形/奖励/遭遇等内容与机制 | `Feature-冒险地图与关卡设计.md`、`Feature-哥布林.md` |
| **Presentation** | UI、信息架构、反馈（不写玩法数值）                   | `Presentation-冒险地图界面设计.md`              |
| **Polish**       | 平衡假设、测试观察、验证记录                       | `Polish-原型数值与测试观察点.md`                  |

同一系统文件夹内可并存多个 Layer 前缀文件。例如 `冒险系统/` 下同时有 `Feature-*` 与 `Presentation-*`。

## 2. 文件命名

```
{Layer}-{原文件名}.md
```

示例：

- `Feature-冒险地图与关卡设计.md`
- `Presentation-冒险地图界面设计.md`
- `Core-六边形战斗规则.md`

**不重命名**：总纲 [`../游戏粗略策划案.md`](../游戏粗略策划案.md)（索引入口，无 Layer 前缀）。

### 2.1 职业条目子文件夹（职业系统）

单职业核心概念、卡牌体系与单牌设计放在 **`职业系统/{职业名}/`** 下，系统级文档仍留在 `职业系统/` 根目录：

```
职业系统/
  Feature-职业系统设计.md
  Presentation-职业选择与构筑界面设计.md
  战士/
    Feature-战士.md
    Feature-战士武器系统.md
    Feature-战士体系-*.md
    Feature-战士体系联动.md
  骑士/
    Feature-骑士.md
  德鲁伊/
    Feature-德鲁伊.md
```

文件名仍使用 `{Layer}-` 前缀；子文件夹仅按 **职业** 划分，不按 Layer 划分。

## 3. 文档头字段

### Feature / Core / Foundation 机制或内容文档（9 节结构）

```markdown
# 《VibeGame》[标题]

> **文档状态**：草案 v0.x
> **Layer**：Feature | Core | Foundation
> **Priority**：MVP
> **关联总案**：[链接]
> **Quick reference**：关键依赖 [系统名列表]

## Summary
…
```

### Presentation 文档

```markdown
> **Layer**：Presentation
> **关联主文档**：[`Feature-xxx.md`](Feature-xxx.md)
> **状态**：[待 UX 设计]
```

### Polish 文档

```markdown
> **Layer**：Polish
> **关联总案**：[链接]
```

**禁止**再使用：`文档类型`、`SpecKind`、`CCGS Layer`（与 `Layer` 重复）。

## 4. 子文档索引表

§7 索引第一列写 Layer 值（与文件名前缀一致），不再使用 Rule/Content/Presentation 类型列：

| Layer | 文档 | 职责 |
| --- | --- | --- |
| Feature | 本文档 | … |
| Feature | [`Feature-章节主题设计.md`](Feature-章节主题设计.md) | … |
| Presentation | [`Presentation-冒险地图界面设计.md`](Presentation-冒险地图界面设计.md) | … |

## 5. 边界约定

- **Feature 层**可写机制主文档，也可写内容条目、章节编排、敌人模板；是否「机制源」由 §1 文档定位与总纲边界决定，不由 Layer 单独决定。
- **Presentation 层**不得定义结算规则与数值；只描述 UI 需展示的系统接口数据。
- **Polish 层**不是玩法规则源；数值假设不得反向改写 Core/Feature 文档。

## 6. 参考示例

首份完整示例：[`../冒险系统/Feature-冒险地图与关卡设计.md`](../冒险系统/Feature-冒险地图与关卡设计.md)
