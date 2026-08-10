# 《VibeGame》战斗场景生成规则

> **文档状态**：草案 v0.2
> **Layer**：Feature
> **Priority**：MVP
> **关联主文档**：[`Feature-冒险地图与关卡设计.md`](Feature-冒险地图与关卡设计.md)  
> **关联总案**：[`../游戏粗略策划案.md`](../游戏粗略策划案.md)

## Summary

定义从冒险战斗节点到战斗系统所需的场景参数。MVP 第一版采用 **固定预设地图**：按战斗序号或节点类型读取完整地图配置，不做运行时随机铺地形。

## 1. 文档定位

本文件负责战斗场景生成的具体内容与参数规则，不定义战斗内结算。

## 2. 内容编排规则

冒险机制层文档要求进入战斗时向战斗系统提供五类输入；本文档定义每类输入的数据来源。

MVP 固定规则：

- 选图方式：战斗序号 / 节点类型 → 地图 ID，见 [`Feature-战斗地图预设表.md`](Feature-战斗地图预设表.md)。
- 地形生成：不按比例随机生成；不根据地形标签实时铺设地形。
- 地形标签：只用于文档归类、遭遇预览与后续扩展，不作为当前运行时生成规则。
- Unity 调试资源：每张地图对应一个 `BattleSandboxScenarioSO`，并设置 `terrain.generateFeatureTerrain = false`。

## 3. 具体内容

| 输入项 | 说明 | MVP 数据来源 |
| --- | --- | --- |
| 地图轮廓 | 六边形竞技场尺寸与边界 | 战斗地图预设表，第一批统一 `11 x 11` |
| 固定地形 | 深坑、石台、木箱、预置拾取物 | 战斗地图预设表坐标表 |
| 互动设施 | 可交互构筑物 placement | 战斗地图预设表中的 `propId` 与 HP |
| 单位出生区域 | 玩家/敌人初始站位 | 战斗地图预设表中的玩家与敌人坐标 |
| 环境规则 | 本章环境关键词与全局修正 | 第一章仅使用地图标签；无额外全局修正 |

### 3.1 MVP 选图映射

| 战斗位置 | 地图 ID | 说明 |
| --- | --- | --- |
| 普通战第 1 场 | `BMAP-01_Goblin_Open` | 哥布林基础教学 |
| 普通战第 2 场 | `BMAP-02_SpearGoblin_Cover` | 投矛哥布林与掩体 |
| 普通战第 3 场 | `BMAP-02_SpearGoblin_Cover` | 第一批预设复用 |
| 普通战第 4-9 场 | `BMAP-03_Orc_NarrowLane` | 兽人战士与狭道压力 |
| 精英战 | `BMAP-04_LivingWall_Elite` | 活墙壁专属地图 |
| 首领战 | `BMAP-05_TribalChieftain_Boss` | 部落酋长首领地图 |

完整坐标表见 [`Feature-战斗地图预设表.md`](Feature-战斗地图预设表.md)。

### 3.2 Unity 配置要求

| 字段 | MVP 要求 |
| --- | --- |
| `useFixedRandomSeed` | `true` |
| `terrain.width` / `terrain.height` | `11 / 11` |
| `terrain.generateFeatureTerrain` | `false` |
| `terrain.overrides` | 完整录入预设表固定地形、构筑物、拾取物 |
| `player.spawnCoord` | 使用预设表玩家出生点 |
| `enemies` | 使用预设表敌人 ID、显示名与出生点 |
| 活墙壁 | 必须额外设置 `livingWallPartnerSpawnCoord` |

## 4. 子文档索引

| Layer | 文档 | 职责 |
| --- | --- | --- |
| Feature | [`Feature-冒险地图与关卡设计.md`](Feature-冒险地图与关卡设计.md) | 场景生成入口 |
| Feature | 本文档 | 生成规则与参数 |
| Feature | [`Feature-战斗地图预设表.md`](Feature-战斗地图预设表.md) | 固定地图、敌人、出生点与地形坐标表 |
| Feature | [`../遭遇系统/Feature-遭遇与原型战斗设计.md`](../遭遇系统/Feature-遭遇与原型战斗设计.md) | 原型战场示例 |

## 5. 交叉引用

| 本内容引用 | 目标文档 | 引用元素 | 性质 |
| --- | --- | --- | --- |
| 地图预设 | [`Feature-战斗地图预设表.md`](Feature-战斗地图预设表.md) | 战斗序号到地图 ID 的固定映射 | 数据依赖 |
| 地形数据 | [`../地形系统/Feature-地形表.md`](../地形系统/Feature-地形表.md) | MVP 可用地形词条 | 数据依赖 |
| 遭遇组合 | [`../遭遇系统/Feature-遭遇与原型战斗设计.md`](../遭遇系统/Feature-遭遇与原型战斗设计.md) | 敌人与强度 | 数据依赖 |

## 6. 验收标准

- **GIVEN** MVP 第一章进入战斗节点，**WHEN** 场景参数被准备，**THEN** 通过战斗序号或节点类型选中唯一地图 ID。
- **GIVEN** 任意 MVP 预设地图，**WHEN** 进入 Unity 调试场景，**THEN** `terrain.generateFeatureTerrain = false`，且场上固定地形与预设表坐标一致。
- **GIVEN** 地图标签为 `开阔`、`狭道` 或 `深坑密集`，**WHEN** MVP 运行时准备地图，**THEN** 标签只用于展示和归类，不触发随机地形生成。
