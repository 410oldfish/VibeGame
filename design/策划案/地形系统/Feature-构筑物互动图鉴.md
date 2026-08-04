# 《VibeGame》构筑物互动图鉴

> **文档状态**：Content 图鉴 v0.2  
> **Layer**：Feature（Content 向）  
> **Priority**：Post-MVP 扩充（MVP 实例见 §2 索引 **已投入** 行）  
> **关联主文档**：[`Feature-地形与地形改装规则.md`](Feature-地形与地形改装规则.md)  
> **机制 enum**：`Barrier`（障碍）\| `Ruin`（残骸）  
> **已投入登记**：[`../设计规范/Feature-已投入设计词条登记.md`](../设计规范/Feature-已投入设计词条登记.md) §3

## Summary

西幻 DND 式 **可互动构筑物** 的 **逐条 Content 登记处**。每条记录 **类型、破坏方式、移除/受击后果、敌人联动**；遭遇/layout 通过 **`propId`** 引用，**不**在遭遇文档重复写机制全文。

**与主文档关系**：

- **障碍 / 残骸** 分型与 LOS/HP 规则 → 地形 Feature §3.1  
- **本图鉴** → 具体 **实例**（石台、木箱、生命树枝桠、骸骨堆…）  
- **临时覆盖**（圣域、毒雾等）→ 移除后可 **生成覆盖** 的条目在本表 `onRemove` 中指向覆盖 ID

---

## 1. 条目模板（Content 必填）

| 字段 | 说明 |
| --- | --- |
| `propId` | 唯一 ID，layout 引用 |
| `displayName` | UI 显示名 |
| `structureType` | **`Barrier`** \| **`Ruin`** |
| `RuinHP` | 仅 Ruin；默认见条 |
| `blocksLOS` | 默认：Barrier **true**，Ruin **false**（可 override） |
| `destroyBy` | `special_only` \| `normal_attack` \| `both`（罕见） |
| `onRemove` | 移除/HP 归零时效果列表（见 §1.1） |
| `onHit` | 每次被攻击穿透扣 HP 时（可选） |
| `onCollision` | 击退/冲撞终点（可选） |
| `adjacentAura` | 邻格被动（可选；单位 **站在普通地面**，构筑物格不可站） |
| `enemyHooks` | 与敌人条目联动 ID（可选） |
| `dndRef` | 设计灵感（表格列，非程序字段） |
| `mvpStatus` | `MVP` \| `Ch1+` \| `Ch2+` \| `Draft` |

### 1.1 `onRemove` 效果类型

| 类型 ID | 含义 | 示例 |
| --- | --- | --- |
| `field_pickup` | 同格生成 **可视区拾取物** | 生命回复、金币 |
| `temp_overlay` | 同格或邻格生成 **临时覆盖** N 回合 | 圣域、治疗雾气 |
| `transform_ground` | 改变 **基础地面** | 毒沼、植物地 |
| `spawn_prop` | 生成另一 **构筑物** | 落岩 → **`propId: wood_crate`** |
| `spawn_unit` | 召唤战斗单位 | 骸骨堆 → 骷髅兵 |
| `area_damage` | 范围伤害 | 火药桶爆炸 |
| `apply_status` | 对范围内单位施加状态 | 毒囊 → 中毒 |
| `enemy_trigger` | 触发遭遇脚本/敌人 buff | 祭坛激活 |
| `none` | 无 |

**多条 `onRemove`**：按数组顺序结算；Content 可配 **权重表**（如生命树 50% 拾取 / 50% 覆盖）。

### 1.2 敌人联动约定

| 模式 | 说明 |
| --- | --- |
| **邻接增益** | 指定 `enemyTag` 邻接该 prop 时获得 buff（如亡灵 + 骸骨堆） |
| **意图素材** | 敌人技能 **放置** / **转化** 该 prop（如酋长落岩 → **`propId: wood_crate`**） |
| **伪装** | prop 外观；交互后揭示敌人（**伪装箱** ↔ [`Feature-宝箱怪.md`](../敌人系统/敌人条目/未投入使用或未完成实现/Feature-宝箱怪.md)） |
| **依赖存在** | 敌人能力仅当场上存在某 `propId` 时可用（如德鲁伊 **生命树** 领域） |

---

## 2. 总索引

| propId                                           | 显示名    | 类型        |      HP | MVP     | 移除后果                                       | 敌人联动                                 |
| ------------------------------------------------ | ------ | --------- | ------: | ------- | ------------------------------------------ | ------------------------------------ |
| [`stone_pillar`](#31-stone_pillar--石台)           | 石台     | Barrier   |       — | **MVP** | 无                                          |                                      |
| [`wood_crate`](#32-wood_crate--木箱)               | 木箱     | Ruin |       4 | **MVP** | 随机破旧武器掉落，可投掷                               | 拥有哥布林敌人的场景中可能出现                      |
| [`life_tree_bough`](#33-life_tree_bough--生命树·枝桠) | 生命树·枝桠 | Barrier   |       — | Ch1+    | 掉落治愈球，恢复 **20%** 最大生命                      | 拥有活墙壁敌人的场景中可能出现                      |
| [`iron_brazier`](#34-iron_brazier--火盆)           | 火盆     | Ruin |       6 | Ch1+    | 本格+邻格1 **着火场地** 2 回合（每回合开始 3 伤）            | 拥有地狱犬敌人的场景中可能出现                      |
| [`bone_pile`](#35-bone_pile--骸骨堆)                | 骸骨堆    | Ruin |       1 | Ch1+    | 召唤骷髅兵                                       | 拥有寄生藤蔓敌人的场景中可能出现                     |
| [`treasure_chest`](#36-treasure_chest--宝箱)       | 宝箱     | Ruin | 6/12/18 | Ch1+    | 根据宝箱等级，在战斗结束后，给予20/50/100金币，或者普通/罕见/稀有卡牌奖励 | 所有战斗中都有可能出现，敌人越困难，                   |
| [`mimic_chest`](#37-mimic_chest--伪装箱)            | 伪装箱    | Ruin |       4 | Ch1+    | 揭示宝箱怪                                      | 宝箱怪                                  |
| [`Ruin_planks`](#38-Ruin_planks--拒马木栅) | 拒马木栅   | Ruin |       5 | Ch1+    | 无 / 木刺覆盖                                   | 拥有哥布林敌人的场景中可能出<br>被击退到该残骸上会额外受到20点伤害 |
| [`ale_barrel`](#39-ale_barrel--火药桶)              | 火药桶    | Ruin |       3 | Ch2+    | 延迟一回合消失，并造成邻格2 40点伤害                       | —                                    |
| [`shrine_fragment`](#312-shrine_fragment--圣坛残片)  | 圣坛残片   | Barrier   |       — | Ch2+    | 圣域覆盖 2 回合                                  | 净化 debuff                            |
| [`thorn_bramble`](#313-thorn_bramble--荆棘丛)       | 荆棘丛    | Ruin |       3 | Ch1+    | -                                          | 寄生藤蔓 寄生残骸                            |
| [`cult_brazier`](#314-cult_brazier--邪火祭盆)        | 邪火祭盆   | Ruin |       5 | Ch2+    | 敌方全体 +1 力量                                 | 邪教精英                                 |
| [`webbed_corpse`](#315-webbed_corpse--蛛网尸骸)      | 蜘蛛巢    | Ruin |       4 | Ch2+    | 束缚  1 回合                                   | 蜘蛛类                                  |
| [`holy_font_basin`](#316-holy_font_basin--圣水盆)   | 圣水盆    | Ruin |       4 | Ch2+    | 治疗覆盖 3 回合                                  |                                      |
| 冰面                                               | 基础地形   | 是         |       否 | 否       | 主动移动时会额外滑行 **1** 格                         | 路径风险                                 |

---

## 3. 条目详述

### 3.1 `stone_pillar` · 石台

| 字段 | 值 |
| --- | --- |
| `structureType` | **Barrier** |
| `destroyBy` | `special_only` |
| `mvpStatus` | **MVP** |
| `dndRef` | Dungeon **pillar** |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `none` |
| `onCollision` | 击退终点停步；可对单位施加 **头晕目眩** token |

---

### 3.2 `wood_crate` · 木箱

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **4** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | **MVP** |
| `enemyHooks` | 哥布林遭遇；[`Feature-部落酋长.md`](../敌人系统/敌人条目/已投入使用并实现/Feature-部落酋长.md) 落岩放置 **`propId: wood_crate`** |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `field_pickup`：**`worn_weapon`**（权重表可混 **生命回复**） |
| `onCollision` | 残骸 **−1 HP** |

---

### 3.3 `life_tree_bough` · 生命树·枝桠

| 字段 | 值 |
| --- | --- |
| `structureType` | **Barrier** |
| `destroyBy` | `special_only` |
| `mvpStatus` | Ch1+ |
| `enemyHooks` | [`Feature-活墙壁.md`](../敌人系统/敌人条目/已投入使用并实现/Feature-活墙壁.md) 场景可出现 |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `field_pickup`：**`healing_orb`**（+**20%** 最大生命） |

---

### 3.4 `iron_brazier` · 火盆

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **6** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch1+ |
| `enemyHooks` | [`Feature-地狱犬.md`](../敌人系统/敌人条目/未投入使用或未完成实现/Feature-地狱犬.md) 场景 |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `temp_overlay`：**`ignition_field`**，**半径 1**（含本格），**2** 回合 |

> **着火场地** ≠ 角色 **燃烧** debuff；见 §4 `ignition_field`。

---

### 3.5 `bone_pile` · 骸骨堆

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **1** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch1+ |
| `enemyHooks` | 寄生藤蔓场景；[`Feature-骷髅兵.md`](../敌人系统/敌人条目/已投入使用并实现/Feature-骷髅兵.md) |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `spawn_unit`：**骷髅兵** ×1（同格或邻格空格） |

---

### 3.6 `treasure_chest` · 宝箱

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **6** / **12** / **18**（普通 / 罕见 / 稀有档） |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch1+ |
| `postBattleReward` | **true**（非战场拾取） |

| 档位 | 战后奖励（草案） |
| --- | --- |
| HP **6** | **20** 金币 **或** 普通卡牌 |
| HP **12** | **50** 金币 **或** 罕见卡牌 |
| HP **18** | **100** 金币 **或** 稀有卡牌 |

---

### 3.7 `mimic_chest` · 伪装箱

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **4** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch1+ |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `enemy_trigger`：**reveal_mimic** → 同格 [`Feature-宝箱怪.md`](../敌人系统/敌人条目/未投入使用或未完成实现/Feature-宝箱怪.md) |
| `onHit` | 遭遇可配：首次受击 **50%** 提前 reveal |

---

### 3.8 `Ruin_planks` · 拒马木栅

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **5** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch1+ |
| `enemyHooks` | 哥布林狭道 |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `none`（可选 `temp_overlay` **`wood_spikes`** 1 回合，Draft） |
| `onCollision` | 碰撞者额外 **20** 点伤害（撞残骸停步后结算） |

---

### 3.9 `ale_barrel` · 火药桶

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **3** |
| `destroyBy` | `normal_attack` |
| `fuseTurns` | **1**（受击或碰撞可 **`arm`**；**下回合开始** 引爆） |
| `mvpStatus` | Ch2+ |

| 触发 | 效果 |
| --- | --- |
| `onRemove`（引爆） | `area_damage`：**邻格 2** 内 **40** 伤；移除 prop |

---

### 3.10 `shrine_fragment` · 圣坛残片

| 字段 | 值 |
| --- | --- |
| `structureType` | **Barrier** |
| `destroyBy` | `special_only` |
| `mvpStatus` | Ch2+ |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `temp_overlay`：**`sanctuary`** 同格，**2** 回合 |

---

### 3.11 `thorn_bramble` · 荆棘丛

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **3** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch1+ |
| `enemyHooks` | [`Feature-寄生藤蔓.md`](../敌人系统/敌人条目/未投入使用或未完成实现/Feature-寄生藤蔓.md) **寄生残骸**（邻格互动） |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `none` |
| `onCollision` | 碰撞者 **束缚 +1** |

---

### 3.12 `cult_brazier` · 邪火祭盆

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **5** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch2+ |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `enemy_trigger`：**cult_surge** → 敌方全体 **力量 +1**（本场） |

---

### 3.13 `webbed_corpse` · 蜘蛛巢

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **4** |
| `destroyBy` | `normal_attack` |
| `mvpStatus` | Ch2+ |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `apply_status`：邻格 **1** 内随机 **1** 名单位 **束缚 1** 回合 |

---

### 3.14 `holy_font_basin` · 圣水盆

| 字段 | 值 |
| --- | --- |
| `structureType` | **Ruin** |
| `RuinHP` | **4** |
| `destroyBy` | `normal_attack` |
| `friendlyFire` | **false**（玩家攻击 **不** 扣 HP；**破障** 类可移除，Post-MVP） |
| `mvpStatus` | Ch2+ |

| 触发 | 效果 |
| --- | --- |
| `onRemove` | `temp_overlay`：**`holy_ground`** 同格 + **邻格 1**，**3** 回合 |

---

## 4. 临时覆盖 ID（构筑物衍生）

由 `onRemove` → `temp_overlay` 引用；持续回合与数值在 Content 调参。

| overlayId | 显示名 | 默认持续 | 效果摘要 |
| --- | --- | ---: | --- |
| **`ignition_field`** | **着火场地** | 2 | 格内单位 **回合开始 3** 伤；**不** 叠层进 **燃烧** debuff |
| `sanctuary` | 圣域 | 2 | 友方受伤 −2；亡灵敌方回合开始 3 伤 |
| `holy_ground` | 圣水地 | 3 | 友方 +3 生命/回合；亡灵 5 伤/回合 |
| `poison_mist` | 毒雾 | 1 | 回合结束 +1 中毒 |
| `wood_spikes` | 木刺 | 1 | 进入格 **流血 +1**（Draft） |
| `healing_mist` | 治疗雾气 | 3 | 友方回合开始 +5 生命（Post-MVP） |
| `fire` | 火焰 | 2 | 进入/回合开始 +1 **燃烧**（角色 debuff） |
| `arcane_field` | 奥术场 | 2 | 友方首张 **技能** −1 费（Post-MVP） |

完整覆盖池见 [`Feature-地形与地形改装规则.md`](Feature-地形与地形改装规则.md) §临时覆盖。

---

## 5. layout 配置示例

```yaml
# 遭遇 grid 单元（示意）
- hex: [3, 5]
  ground: normal
  propId: life_tree_bough
  onRemoveOverride:   # 可选；覆盖图鉴默认
    - type: temp_overlay
      overlayId: healing_mist
      radius: 1
      duration: 3
- hex: [4, 2]
  propId: mimic_chest
  isMimic: true
```

---

## 6. 验收要点

- **GIVEN** layout 引用合法 `propId`，**WHEN** Content 校验，**THEN** `structureType` 与 HP/destroyBy 一致。
- **GIVEN** Ruin HP 归零，**WHEN** 移除完成，**THEN** 按条目的 `onRemove` 顺序结算（拾取/覆盖/召唤不互相吞没）。
- **GIVEN** Barrier 仅受 `special_only`，**WHEN** 普通攻击穿透路径经过，**THEN** 不扣 HP、不触发 `onRemove`。
- **GIVEN** `adjacentAura` 配置，**WHEN** 单位与 prop **邻接** 且站在 **普通地面**，**THEN** 光环生效（**不** 要求占构筑物格）。

---

## 7. 维护约定

- 新增构筑物：**先** 在本图鉴增 §3 条目 + §2 索引行 → **再** 更新 [`Feature-已投入设计词条登记.md`](../设计规范/Feature-已投入设计词条登记.md) §3.2 状态 → 遭遇/敌人 **只引用** `propId`。
- 平衡数值在 [`Polish-原型数值与测试观察点.md`](../平衡验证/Polish-原型数值与测试观察点.md) 单独立项，本图鉴只写 **默认草案**。
