# 《VibeGame》战斗地图预设表

> **文档状态**：草案 v0.1
> **Layer**：Feature
> **Priority**：MVP
> **关联主文档**：[`Feature-战斗场景生成规则.md`](Feature-战斗场景生成规则.md)
> **Unity 对齐**：`HexBattleSandboxScenarioSO`、`TerrainOverride`、`EnemyConfig`、`Vector2Int(q,r)`

## Summary

本文档是第一章 MVP 固定战斗地图的单一登记表。当前阶段不做运行时随机铺地形，也不根据比例动态生成地图；进入战斗时按战斗序号或节点类型读取对应预设地图。

每张地图记录固定敌人、固定出生点、固定地形和测试配置摘要。后续 Unity 实现时，每张地图对应一个 `BattleSandboxScenarioSO` 资源，且 `generateFeatureTerrain = false`，只使用本文坐标表中的手写 overrides。

## 1. 记录规则

| 规则 | 说明 |
| --- | --- |
| 地图尺寸 | 第一批统一 `11 x 11`，坐标为轴向坐标 `q,r` |
| 选图方式 | MVP 固定映射：战斗序号 / 节点类型 → 地图 ID |
| 地形范围 | 仅使用 MVP 地形：普通地面、深坑、石台、木箱、可视区拾取物 |
| 随机地形 | 禁用；Unity 测试配置必须设置 `generateFeatureTerrain = false` |
| 敌人配置 | 敌人 ID 使用 Unity `HexSandboxEnemyType.ToDefinitionId()` 对应值 |
| 坐标权威 | 坐标表为实现权威；ASCII 草图只做快速阅读辅助 |

### 图例

| 符号 | 含义 | Unity 字段 |
| --- | --- | --- |
| `.` | 普通地面 | `zone = Normal` |
| `P` | 玩家出生点 | `player.spawnCoord` |
| `E1/E2` | 敌人出生点 | `enemies[].spawnCoord` |
| `W` | 活墙壁核心线 / 初始占格 | `enemyType = LivingWall`，含 partner core；Unity 只录入一条 EnemyConfig |
| `B` | 石台 / 障碍 | `structureType = Barrier`，`propId = stone_pillar` |
| `R` | 木箱 / 残骸 | `structureType = Ruin`，`propId = wood_crate`，`structureHp = 4` |
| `X` | 深坑 | `zone = Pit` |
| `H` | 生命回复拾取物 | `pickupType = Heal` |
| `S` | 临时力量拾取物 | `pickupType = TemporaryStrength` |

## 2. 第一章固定选图表

| 战斗位置 | 地图 ID | 标签 | 说明 |
| --- | --- | --- | --- |
| 普通战第 1 场 | `BMAP-01_Goblin_Open` | 开阔 | 首场教学，固定 2 只哥布林 |
| 普通战第 2 场 | `BMAP-02_SpearGoblin_Cover` | 开阔 / 掩体 | 引入投矛哥布林与掩体 |
| 普通战第 3 场 | `BMAP-02_SpearGoblin_Cover` | 开阔 / 掩体 | 第一批预设复用，后续可新增第 3 场专图 |
| 普通战第 4-9 场 | `BMAP-03_Orc_NarrowLane` | 狭道 | 第一批预设复用，覆盖兽人战士与直线威胁教学 |
| 精英战 | `BMAP-04_LivingWall_Elite` | 狭道 / 精英 | 活墙壁专属地图 |
| 首领战 | `BMAP-05_TribalChieftain_Boss` | 深坑密集 / 首领 | 部落酋长首领地图 |

> 后续需要更完整的第一章路线时，优先补 `BMAP-06_Orc_PitPressure`、`BMAP-07_GoblinCaptain_Elite` 等同序号替换图；在此之前不启用随机地图池。

## 3. 地图条目

### 3.1 `BMAP-01_Goblin_Open`

| 字段 | 值 |
| --- | --- |
| 战斗序号 | 普通战第 1 场 |
| 地图尺寸 | `11 x 11` |
| 标签 | 开阔 |
| 敌人 | 2 只哥布林 |
| 设计意图 | 教学公开意图、接近、基础打击；地形只提供轻量绕位，不制造深坑压力 |

```text
q:  1 2 3 4 5 6 7 8 9
r3  . . . . . . . . .
r4  . . . . B . E1 . .
r5  . . P . . R . . .
r6  . . . . R . E2 . .
r7  . . . . . . . . .
```

玩家出生点：

| q | r |
| ---: | ---: |
| 3 | 5 |

敌人出生点：

| 槽位 | enemyDefinitionId | 显示名 | q | r |
| --- | --- | --- | ---: | ---: |
| E1 | `goblin` | 哥布林 A | 7 | 4 |
| E2 | `goblin` | 哥布林 B | 7 | 6 |

固定地形：

| q | r | Zone | Structure | propId | HP | 说明 |
| ---: | ---: | --- | --- | --- | ---: | --- |
| 5 | 4 | `Normal` | `Barrier` | `stone_pillar` | — | 轻量遮挡 |
| 6 | 5 | `Normal` | `Ruin` | `wood_crate` | 4 | 中线可破坏资源 |
| 5 | 6 | `Normal` | `Ruin` | `wood_crate` | 4 | 侧翼可破坏资源 |

拾取物：无预置；木箱破坏后的掉落由运行时 `wood_crate` 默认规则处理。

测试配置摘要：

| 字段 | 值 |
| --- | --- |
| 建议资源名 | `BattleSandbox_BMAP_01_Goblin_Open.asset` |
| `useFixedRandomSeed` | `true` |
| `randomSeed` | `1101` |
| `terrain.generateFeatureTerrain` | `false` |
| `terrain.heightStep` | `0` |

### 3.2 `BMAP-02_SpearGoblin_Cover`

| 字段 | 值 |
| --- | --- |
| 战斗序号 | 普通战第 2 场；第 3 场临时复用 |
| 地图尺寸 | `11 x 11` |
| 标签 | 开阔 / 掩体 |
| 敌人 | 1 只哥布林 + 1 只投矛哥布林 |
| 设计意图 | 教学远程敌人、掩体、贴脸压力；玩家可选择绕开中线石台或破坏侧翼木箱 |

```text
q:  1 2 3 4 5 6 7 8 9
r3  . . . . . R . . .
r4  . . . . B . . E2 .
r5  . . P . . B . . .
r6  . . . R . . . E1 .
r7  . . . H . . . . .
```

玩家出生点：

| q | r |
| ---: | ---: |
| 3 | 5 |

敌人出生点：

| 槽位 | enemyDefinitionId | 显示名 | q | r |
| --- | --- | --- | ---: | ---: |
| E1 | `goblin` | 哥布林 | 8 | 6 |
| E2 | `spear_goblin` | 投矛哥布林 | 8 | 4 |

固定地形：

| q | r | Zone | Structure | propId | HP | 说明 |
| ---: | ---: | --- | --- | --- | ---: | --- |
| 5 | 4 | `Normal` | `Barrier` | `stone_pillar` | — | 中线掩体 |
| 6 | 5 | `Normal` | `Barrier` | `stone_pillar` | — | 阻断直线火力 |
| 6 | 3 | `Normal` | `Ruin` | `wood_crate` | 4 | 上侧可破坏资源 |
| 4 | 6 | `Normal` | `Ruin` | `wood_crate` | 4 | 玩家侧可破坏资源 |

预置拾取物：

| q | r | pickupType | pickupAmount | 说明 |
| ---: | ---: | --- | ---: | --- |
| 4 | 7 | `Heal` | 15 | 鼓励玩家试走侧翼路线 |

测试配置摘要：

| 字段 | 值 |
| --- | --- |
| 建议资源名 | `BattleSandbox_BMAP_02_SpearGoblin_Cover.asset` |
| `useFixedRandomSeed` | `true` |
| `randomSeed` | `1102` |
| `terrain.generateFeatureTerrain` | `false` |
| `terrain.heightStep` | `0` |

### 3.3 `BMAP-03_Orc_NarrowLane`

| 字段 | 值 |
| --- | --- |
| 战斗序号 | 普通战第 4-9 场临时复用 |
| 地图尺寸 | `11 x 11` |
| 标签 | 狭道 |
| 敌人 | 1 只兽人战士 + 1 只哥布林 |
| 设计意图 | 教学直线冲锋、绕位、撞障碍风险；兽人与玩家同 `r=5`，开局存在 3 格直线威胁路线 |

```text
q:  1 2 3 4 5 6 7 8 9
r3  . . . B . B X . .
r4  . . . . R . R . .
r5  . . P . . E1 . . .
r6  . . . . R . X . .
r7  . . X B . B E2 . .
```

玩家出生点：

| q | r |
| ---: | ---: |
| 3 | 5 |

敌人出生点：

| 槽位 | enemyDefinitionId | 显示名 | q | r |
| --- | --- | --- | ---: | ---: |
| E1 | `orc_warrior` | 兽人战士 | 6 | 5 |
| E2 | `goblin` | 哥布林 | 7 | 7 |

固定地形：

| q | r | Zone | Structure | propId | HP | 说明 |
| ---: | ---: | --- | --- | --- | ---: | --- |
| 4 | 3 | `Normal` | `Barrier` | `stone_pillar` | — | 上侧墙体 |
| 6 | 3 | `Normal` | `Barrier` | `stone_pillar` | — | 上侧墙体 |
| 4 | 7 | `Normal` | `Barrier` | `stone_pillar` | — | 下侧墙体 |
| 6 | 7 | `Normal` | `Barrier` | `stone_pillar` | — | 下侧墙体 |
| 5 | 4 | `Normal` | `Ruin` | `wood_crate` | 4 | 冲锋侧翼资源 |
| 5 | 6 | `Normal` | `Ruin` | `wood_crate` | 4 | 冲锋侧翼资源 |
| 7 | 4 | `Normal` | `Ruin` | `wood_crate` | 4 | 敌侧可破坏资源 |
| 7 | 3 | `Pit` | `None` | — | — | 上侧落点风险 |
| 3 | 7 | `Pit` | `None` | — | — | 玩家侧落点风险 |
| 7 | 6 | `Pit` | `None` | — | — | 敌侧落点风险 |

拾取物：无预置；木箱破坏后的掉落由运行时 `wood_crate` 默认规则处理。

测试配置摘要：

| 字段 | 值 |
| --- | --- |
| 建议资源名 | `BattleSandbox_BMAP_03_Orc_NarrowLane.asset` |
| `useFixedRandomSeed` | `true` |
| `randomSeed` | `1103` |
| `terrain.generateFeatureTerrain` | `false` |
| `terrain.heightStep` | `0` |

### 3.4 `BMAP-04_LivingWall_Elite`

| 字段 | 值 |
| --- | --- |
| 战斗序号 | 精英战 |
| 地图尺寸 | `11 x 11` |
| 标签 | 狭道 / 精英 |
| 敌人 | 活墙壁专属遭遇 |
| 设计意图 | 测试配对推进、空间挤压、破阵；中央被活墙壁主线压迫，但上下两侧必须保留绕行空间 |

```text
q:  1 2 3 4 5 6 7 8 9
r2  . . . . . . . . .
r3  . . X . . W . X .
r4  . . . R . W . R .
r5  . . P . B W B . .
r6  . . . R . W . R .
r7  . . X . . W . X .
r8  . . . . . W . . .
r9  . . . . . . . . .
```

玩家出生点：

| q | r |
| ---: | ---: |
| 3 | 5 |

敌人出生点：

| 槽位 | enemyDefinitionId | 显示名 | q | r | livingWallPartnerSpawnCoord |
| --- | --- | --- | ---: | ---: | --- |
| E1 | `living_wall` | 活墙壁 | 6 | 4 | `6,7` |

> Unity 只配置一条 `EnemyConfig`，`enemyType = LivingWall`；Bootstrap 会根据 `spawnCoord = 6,4` 与 `livingWallPartnerSpawnCoord = 6,7` 生成 A/B 两面活墙壁。初始占格由活墙壁规则生成：A 约占 `6,3`、`6,4`、`6,5`，B 约占 `6,6`、`6,7`、`6,8`。

固定地形：

| q | r | Zone | Structure | propId | HP | 说明 |
| ---: | ---: | --- | --- | --- | ---: | --- |
| 5 | 5 | `Normal` | `Barrier` | `stone_pillar` | — | 玩家侧中央阻隔 |
| 7 | 5 | `Normal` | `Barrier` | `stone_pillar` | — | 敌侧中央阻隔 |
| 4 | 4 | `Normal` | `Ruin` | `wood_crate` | 4 | 左上破阵资源 |
| 4 | 6 | `Normal` | `Ruin` | `wood_crate` | 4 | 左下破阵资源 |
| 8 | 4 | `Normal` | `Ruin` | `wood_crate` | 4 | 右上破阵资源 |
| 8 | 6 | `Normal` | `Ruin` | `wood_crate` | 4 | 右下破阵资源 |
| 3 | 3 | `Pit` | `None` | — | — | 上侧绕行边界 |
| 3 | 7 | `Pit` | `None` | — | — | 下侧绕行边界 |
| 8 | 3 | `Pit` | `None` | — | — | 敌侧上边界 |
| 8 | 7 | `Pit` | `None` | — | — | 敌侧下边界 |

拾取物：无预置；本图优先测试空间压迫，不额外发放补偿拾取。

测试配置摘要：

| 字段 | 值 |
| --- | --- |
| 建议资源名 | `BattleSandbox_BMAP_04_LivingWall_Elite.asset` |
| `useFixedRandomSeed` | `true` |
| `randomSeed` | `1104` |
| `terrain.generateFeatureTerrain` | `false` |
| `terrain.heightStep` | `0` |

### 3.5 `BMAP-05_TribalChieftain_Boss`

| 字段 | 值 |
| --- | --- |
| 战斗序号 | 首领战 |
| 地图尺寸 | `11 x 11` |
| 标签 | 深坑密集 / 首领 |
| 敌人 | 部落酋长 |
| 设计意图 | 章节主题总结，检验玩家对障碍、残骸、深坑落点的理解；中央压力高，但玩家出生区必须保留安全回合 |

```text
q:  1 2 3 4 5 6 7 8 9
r3  . . . X . B . X .
r4  . . . . R . R . .
r5  . . P S . R . E1 .
r6  . . . . R . R . .
r7  . . . X . B . X .
r8  . . . . . . . . .
```

玩家出生点：

| q | r |
| ---: | ---: |
| 3 | 5 |

敌人出生点：

| 槽位 | enemyDefinitionId | 显示名 | q | r |
| --- | --- | --- | ---: | ---: |
| E1 | `tribal_chieftain` | 部落酋长 | 8 | 5 |

固定地形：

| q | r | Zone | Structure | propId | HP | 说明 |
| ---: | ---: | --- | --- | --- | ---: | --- |
| 6 | 3 | `Normal` | `Barrier` | `stone_pillar` | — | 上侧硬阻隔 |
| 6 | 7 | `Normal` | `Barrier` | `stone_pillar` | — | 下侧硬阻隔 |
| 5 | 4 | `Normal` | `Ruin` | `wood_crate` | 4 | 中央残骸资源 |
| 7 | 4 | `Normal` | `Ruin` | `wood_crate` | 4 | 敌侧残骸资源 |
| 6 | 5 | `Normal` | `Ruin` | `wood_crate` | 4 | 中央可破坏阻隔 |
| 5 | 6 | `Normal` | `Ruin` | `wood_crate` | 4 | 中央残骸资源 |
| 7 | 6 | `Normal` | `Ruin` | `wood_crate` | 4 | 敌侧残骸资源 |
| 4 | 3 | `Pit` | `None` | — | — | 上侧深坑 |
| 8 | 3 | `Pit` | `None` | — | — | 敌侧上深坑 |
| 4 | 7 | `Pit` | `None` | — | — | 下侧深坑 |
| 8 | 7 | `Pit` | `None` | — | — | 敌侧下深坑 |

预置拾取物：

| q | r | pickupType | pickupAmount | 说明 |
| ---: | ---: | --- | ---: | --- |
| 4 | 5 | `TemporaryStrength` | 2 | 给首领战一个明确争夺点 |

测试配置摘要：

| 字段 | 值 |
| --- | --- |
| 建议资源名 | `BattleSandbox_BMAP_05_TribalChieftain_Boss.asset` |
| `useFixedRandomSeed` | `true` |
| `randomSeed` | `1105` |
| `terrain.generateFeatureTerrain` | `false` |
| `terrain.heightStep` | `0` |

## 4. 验收标准

- **GIVEN** 任意本文地图，**WHEN** 转成 Unity `BattleSandboxScenarioSO`，**THEN** `terrain.generateFeatureTerrain = false`，且所有地形只来自坐标表。
- **GIVEN** `BMAP-01_Goblin_Open`，**WHEN** 玩家开始第一场战斗，**THEN** 不存在深坑，且玩家到任一哥布林至少有一条不被构筑物阻断的路径。
- **GIVEN** `BMAP-03_Orc_NarrowLane`，**WHEN** 开局校验兽人战士与玩家位置，**THEN** 二者位于同一 `r=5` 直线，距离为 3，且中间 `4,5`、`5,5` 均可通行。
- **GIVEN** `BMAP-04_LivingWall_Elite`，**WHEN** 生成活墙壁，**THEN** A/B 两面墙不与玩家出生点或固定构筑物重叠，并保留上下绕行空间。
- **GIVEN** `BMAP-05_TribalChieftain_Boss`，**WHEN** 玩家回合开始，**THEN** 玩家出生点周围至少有 3 个普通可通行格，避免首回合被地形锁死。

## 5. 关联文档

| 文档 | 用途 |
| --- | --- |
| [`Feature-战斗场景生成规则.md`](Feature-战斗场景生成规则.md) | 固定选图规则与场景输入协议 |
| [`../遭遇系统/Feature-遭遇与原型战斗设计.md`](../遭遇系统/Feature-遭遇与原型战斗设计.md) | 第一章敌人与学习顺序 |
| [`../地形系统/Feature-地形表.md`](../地形系统/Feature-地形表.md) | MVP 地形词条与实现状态 |
| [`../地形系统/Feature-地形与地形改装规则.md`](../地形系统/Feature-地形与地形改装规则.md) | 通行、破坏、拾取与击退地形规则 |
