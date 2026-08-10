# 《VibeGame》地形表

> **文档状态**：草案 v0.1
> **Layer**：Feature
> **Priority**：MVP
> **关联主文档**：[`Feature-地形与地形改装规则.md`](Feature-地形与地形改装规则.md)
> **实现核对基准（2026-08-10）**：`HexTerrainZoneType`、`HexTerrainStructureType`、`HexTerrainPickupType`、`HexTileEffectType`、`HexPropLibrary`、`PropEffectStub`、`TileModel`

## Summary

本文档是地形、构筑物、临时覆盖与可视区拾取物的条目参考表。主规则仍以 [`Feature-地形与地形改装规则.md`](Feature-地形与地形改装规则.md) 为准；构筑物逐条互动以 [`Feature-构筑物互动图鉴.md`](Feature-构筑物互动图鉴.md) 为准。

记录方式参考卡牌表与状态表：每个条目只在一个分区登记，分区同时考虑设计投入范围与运行时完成度。程序已有枚举或 stub 不等于设计已实装；只有已经进入 MVP 规则、敌我卡牌或场景流程的词条，才可进入“已实现或已接入运行时”。

## 1. 记录规则

| 规则 | 说明 |
| --- | --- |
| 权威顺序 | 本表负责条目状态；主地形文档负责规则语义；构筑物图鉴负责 prop 互动细节 |
| 层级 | `Zone`、`Structure`、`Overlay`、`FieldPickup` 四类分开登记 |
| 已实现 | 已有运行时字段与有效结算，且已进入当前 MVP 或调试战斗流程 |
| 未实现 | 已有设计或配置登记，但核心效果仍未接入、仅 stub、或不在当前投入范围 |
| 创意池 | 仅作为后续方向；不得被 MVP 卡牌、敌人、遭遇直接引用 |
| MVP 列 | `必须` = 第一章垂直切片可引用；`延后` = Post-MVP；`创意` = 未排期 |

## 2. 已实现或已接入运行时的地形词条（9）

| ID | 名称 | 层级 | 类型/枚举 | 效果摘要 | 实现状态 | MVP |
| --- | --- | --- | --- | --- | --- | --- |
| `normal_ground` | 普通地面 | Zone | `HexTerrainZoneType.Normal` | 正常通行、可站立、无固有战斗效果；可承载构筑物、覆盖与拾取物 | 已实现 | 必须 |
| `pit` | 深坑 | Zone | `HexTerrainZoneType.Pit` | 不可主动进入；阻断通行；作为击退落点风险地形 | 已实现；具体坑落伤害数值需继续由平衡文档确认 | 必须 |
| `stone_pillar` | 石台 | Structure | `Barrier` + `propId` | 不可通行、不可站立、阻挡 LOS；普通攻击不可破坏，只能由破障/特殊行动移除 | 已实现默认 Barrier prop | 必须 |
| `wood_crate` | 木箱 | Structure | `Ruin` + `propId` | 不可通行、不可站立；HP 4；攻击可穿透并削减 HP；归零后生成可视区拾取物 | 已实现默认 Ruin prop；掉落桥接已接入 | 必须 |
| `field_pickup_heal` | 生命回复拾取物 | FieldPickup | `HexTerrainPickupType.Heal` | 玩家进入拾取物格后立即恢复生命 | 已实现；默认数值由来源传入 | 必须 |
| `field_pickup_temp_strength` | 临时力量拾取物 | FieldPickup | `HexTerrainPickupType.TemporaryStrength` | 玩家进入拾取物格后获得临时力量 | 已实现；统一临时力量入口已接入 | 必须 |
| `field_pickup_temp_card` | 临时牌拾取物 | FieldPickup | `HexTerrainPickupType.TemporaryCard` | 玩家进入拾取物格后获得临时牌「投斧」 | 已实现；当前桥接为入手临时牌 | 必须 |
| `tile_effect_poisoned` | 毒性格子效果 | Overlay | `HexTileEffectType.Poisoned` | 单位进入含该效果的格子时施加流血/毒性类惩罚 | 已接入运行时；设计语义需与“毒雾/中毒”统一 | 延后 |
| `tile_effect_custom` | 自定义格子效果 | Overlay | `HexTileEffectType.Custom` | 消耗品与陷阱使用的通用占位效果 | 已接入存储、持续与移除；具体语义依来源解释 | 延后 |

> `HexTileEffectType.Burning` 与 `HexTileEffectType.Entangled` 已有运行时分支，但当前没有稳定的 MVP 地形条目承载，暂不放入已实现表；见 §3。

## 3. 未实现、部分实现或未投入的地形词条

### 3.1 Zone 地面属性

| ID | 名称 | 层级 | 目标效果 | 当前状态 | MVP |
| --- | --- | --- | --- | --- | --- |
| `water` | 水域 | Zone | 限制移动；与电场、冰面、火焰发生反应 | 未实现；主文档已有设计方向 | 延后 |
| `mud` | 泥地 | Zone | 移动受阻，可与束缚、净化、植物生成联动 | 未实现；主文档已有设计方向 | 延后 |
| `poison_swamp` | 毒沼 | Zone | 移动或停留时施加中毒/毒性惩罚 | 未实现；需与单位状态“中毒”和 Overlay 毒雾区分 | 延后 |
| `plant_ground` | 植物地 | Zone | 德鲁伊、藤蔓、再生或阻挡生成的基础承载地形 | 未实现；主文档已有设计方向 | 延后 |

### 3.2 Structure 构筑物

| propId | 名称 | 类型 | HP | 目标效果 | 当前状态 | MVP |
| --- | --- | --- | ---: | --- | --- | --- |
| `life_tree_bough` | 生命树·枝桠 | Barrier | — | 破坏后生成治愈球 | 配置已登记；特殊破障与掉落可用性需验收 | 延后 |
| `iron_brazier` | 火盆 | Ruin | 6 | 归零后生成着火场地 | 配置已登记；onRemove 仍为 stub | 延后 |
| `bone_pile` | 骸骨堆 | Ruin | 1 | 归零后召唤骷髅；可提供亡灵邻接回血 | 配置已登记；召唤与光环仍为 stub | 延后 |
| `treasure_chest` | 宝箱 | Ruin | 12 | 归零后转为战后奖励 | 配置已登记；战后奖励发放仍需接入 | 延后 |
| `mimic_chest` | 伪装箱 | Ruin | 4 | 归零后揭示宝箱怪 | 配置已登记；揭示敌人仍为 stub | 延后 |
| `barricade_planks` | 拒马木栅 | Ruin | 5 | 击退撞上造成额外伤害或生成木刺 | 配置已登记；碰撞扩展和木刺仍需接入 | 延后 |
| `ale_barrel` | 火药桶 | Ruin | 3 | 受击装引信，下回合范围爆炸 | 配置已登记；引信和范围伤害仍为 stub | 延后 |
| `shrine_fragment` | 圣坛残片 | Barrier | — | 破障后生成圣域 | 配置已登记；覆盖效果仍为 stub | 延后 |
| `thorn_bramble` | 荆棘丛 | Ruin | 3 | 可被藤蔓寄生，或作为缠绕/荆棘来源 | 配置已登记；寄生规则未实现 | 延后 |
| `cult_brazier` | 邪火祭盆 | Ruin | 5 | 归零后敌方全体 +1 力量 | 配置已登记；敌方触发仍为 stub | 延后 |
| `webbed_corpse` | 蜘蛛巢 | Ruin | 4 | 移除后施加束缚 | 配置已登记；状态施加仍为 stub | 延后 |
| `holy_font_basin` | 圣水盆 | Ruin | 4 | 归零后生成圣水地 | 配置已登记；覆盖效果仍为 stub | 延后 |
| `consumable_iron_ball` | 铁球 | Ruin | 999 | 受击后沿方向滚动并碰撞造成伤害 | 配置已登记；作为工程装置使用，非基础地形池 | 延后 |

### 3.3 Overlay 临时覆盖

| overlayId | 名称 | 层级 | 目标效果 | 当前状态 | MVP |
| --- | --- | --- | --- | --- | --- |
| `ignition_field` | 着火场地 | Overlay | 格内单位回合开始受伤；不等同于单位状态“燃烧” | 图鉴已有来源；prop onRemove 仍为 stub | 延后 |
| `burning_tile` | 火焰覆盖 | Overlay | 进入或回合开始时施加燃烧 | 有 `HexTileEffectType.Burning` 分支；设计条目未正式投入 | 延后 |
| `ice` | 冰面 | Overlay/Zone 待定 | 主动移动后沿方向额外滑行 1 格 | 仅图鉴/主文档提及；未实现 | 延后 |
| `electric_field` | 电场 | Overlay | 进入或停留时受到感电影响；水域扩大范围 | 未实现 | 延后 |
| `poison_mist` | 毒雾 | Overlay | 回合结束或进入时施加中毒 | 有图鉴条目；当前运行时 `Poisoned` 语义需重命名/统一 | 延后 |
| `sanctuary` | 圣域 | Overlay | 友方受伤减少，亡灵敌人受伤 | 图鉴已有来源；效果仍为 stub | 延后 |
| `holy_ground` | 圣水地 | Overlay | 友方恢复，亡灵受伤 | 图鉴已有来源；效果仍为 stub | 延后 |
| `wood_spikes` | 木刺 | Overlay | 进入格施加流血 | 图鉴已有来源；效果仍为 stub | 延后 |
| `healing_mist` | 治疗雾 | Overlay | 回合开始恢复生命 | 图鉴参考池；未实现 | 延后 |
| `arcane_field` | 奥术场 | Overlay | 改变费用、抽牌或施法收益 | 图鉴参考池；未实现 | 延后 |

## 4. 创意池

创意池条目仅用于后续提案和玩法孵化。进入敌人、卡牌或遭遇前，必须先迁移到 §3 或 §2，并补齐效果、持续时间、数值与视觉反馈。

| ID | 名称 | 层级 | 创意方向 | 可能维度 |
| --- | --- | --- | --- | --- |
| `steam_cloud` | 蒸汽云 | Overlay | 火焰与水域反应生成；阻挡远程视线 1 回合 | 生存、移动 |
| `conductive_water` | 导电水域 | Zone/Overlay | 水域被电场命中后扩散感电风险 | 输出、移动 |
| `cracked_ice` | 破裂冰面 | Overlay | 连续站立或受击后破裂为深坑/水域 | 移动、生存 |
| `vine_wall` | 藤蔓墙 | Structure | 由植物地生长出的临时 Barrier，可被火焰快速清除 | 生存、移动 |
| `spore_field` | 孢子地 | Overlay | 进入后给双方洗入轻量污染牌或施加虚弱 | 抽牌运转、生存 |
| `magnetic_ore` | 磁石矿 | Structure | 吸引铁球、武器或金属敌人，改变路径 | 移动、输出 |
| `unstable_rune` | 不稳定符文 | Overlay | 被攻击或踩踏后引爆，对邻格造成属性伤害 | 输出 |
| `wind_lane` | 风道 | Zone/Overlay | 顺风移动距离 +1，逆风移动费用或距离惩罚 | 移动、费用 |
| `grave_soil` | 墓土 | Zone | 亡灵单位增强；圣水地可净化 | 生存、输出 |
| `mirror_shard` | 镜面碎片 | Structure | 反射一次直线攻击或改变射线路径 | 输出、生存 |

## 5. 关联文档

| 文档 | 用途 |
| --- | --- |
| [`Feature-地形与地形改装规则.md`](Feature-地形与地形改装规则.md) | 地形分层、通行、破坏、覆盖与改装动作主规则 |
| [`Feature-构筑物互动图鉴.md`](Feature-构筑物互动图鉴.md) | 构筑物实例、propId、HP、onRemove 与敌人联动 |
| [`Presentation-地形可视化与反馈设计.md`](Presentation-地形可视化与反馈设计.md) | 地形、构筑物、拾取物、覆盖层的玩家可见反馈 |
| [`../设计规范/Feature-已投入设计词条登记.md`](../设计规范/Feature-已投入设计词条登记.md) | MVP 已投入词条登记 |
| [`../平衡验证/Polish-MVP第一章垂直切片.md`](../平衡验证/Polish-MVP第一章垂直切片.md) | 第一章 MVP 边界与验收范围 |
