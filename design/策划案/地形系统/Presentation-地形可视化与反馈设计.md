# 《VibeGame》地形可视化与反馈设计

> **文档状态**：草案 v0.2  
> **Layer**：Presentation  
> **关联主文档**：[`Feature-地形与地形改装规则.md`](Feature-地形与地形改装规则.md)

## Summary

定义 Zone / 构筑物（Barrier / Ruin）/ 临时覆盖的玩家可见反馈，以及点击详情与残骸 HP 条。本文件不定义地形规则与数值。

## 1. 信息架构

| 层 | 可见要求 |
| --- | --- |
| Zone | 非 `Normal`（如深坑）需有可辨色调或标记；可点击查看详情 |
| Barrier | 与 Ruin 分形态（默认 Sphere / 可换 Prefab）；点击详情 |
| Ruin | 默认 Cube / 可换 Prefab；**世界空间 HP 条**（当前/最大）；点击详情 |
| 拾取物 | 独立图标；仅玩家可拾取 |
| 临时覆盖 | 格面染色或 VFX（现有效果层） |

需展示的信息：

- **构筑物互动预览**：HP 归零或破障后的 `onRemove` 摘要；邻接光环可选范围圈。
- **火药桶**：`fuseTurns` / `armed` 文案（详情面板优先；格上倒计时 Post-MVP）。
- **宝箱**：`postBattleReward` 在详情中标明「战后奖励」。
- 击退预览：终点为残骸/障碍时的风险标注（后续）。

## 2. 交互流程

### 2.1 点击查看详情（已实现目标）

- **触发**：玩家回合、未拖牌、非忙状态时，左键点击格子。
- **优先**：点中敌人 → 敌人手牌悬浮窗（现有逻辑）。
- **否则**：若格子为 Barrier、Ruin 或非 `Normal` Zone → 打开 **世界空间悬浮详情面板**（交互对齐敌人手牌弹层：屏幕坐标锚定、点空白关闭）。
- **面板内容**：显示名、类型（Zone / Barrier / Ruin）、通行与 LOS、Ruin HP、破坏方式、`onRemove` / `fuse` / `aura` / `postBattleReward` 摘要。

### 2.2 残骸 HP 条

- 仅 Ruin 显示；挂在结构视觉上方；结构清除时销毁。
- Barrier 与普通 Zone 不显示 HP 条。

## 3. 视觉与音频反馈

- Barrier / Ruin 默认运行时 primitive（Sphere / Cube），`HexGrid` 可指定 Prefab。
- 详情面板样式贴近战斗 UI 弹层，避免新视觉体系。

## 4. 待设计问题

- 元素反应（火+水、电+水等）是否需要组合动画优先级表？
- 引信倒计时是否需要格上数字 VFX（当前仅详情文案）。
