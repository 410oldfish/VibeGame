# HUD 设计 — 战斗界面

> **Status**: Approved  
> **Last Updated**: 2026-07-09  
> **Screen ID**: `BattleHud`  
> **Platform**: PC  
> **Accessibility Tier**: Basic  
> **Related**: [`battle-ux-brief.md`](battle-ux-brief.md)、[`Presentation-战斗界面与反馈设计.md`](../策划案/战斗系统/Presentation-战斗界面与反馈设计.md)

## 1. 目的与玩家需求

**玩家需求**：在己方回合快速判断威胁与资源，完成移动与出牌决策。  
**游戏需求**：展示权威战斗状态，接收结束回合等操作请求。

## 2. 到达时玩家上下文

| 问题 | 答案 |
|------|------|
| 刚在做什么 | 从冒险地图进入战斗 / 上一回合刚结束 |
| 情绪 | 中高紧张 — 需预判敌人 |
| 认知负荷 | 高 — 六边形站位 + 卡牌 |
| 首要目标 | 读懂敌人意图并打出最优解 |
| 恐惧 | 误读意图、漏看状态层数 |

## 3. 信息架构（分区）

```
PhaseBar        回合阶段 + 结束回合
PlayerStrip     生命/护甲/能量/力量 + StatusIconBar
EnemyPanel×N    名称/生命/护甲 + StatusIconBar + IntentRow + 顺序提示
PileBar         抽牌堆 | 弃牌堆 | 消耗牌堆
HandPanel       手牌（现有）
ActionPanel     回放按钮（现有）
```

### 3.1 多敌人布局（已定案）

- **≤2 敌人**：纵向堆叠全部 `EnemyPanel`
- **>2 敌人**：Tab 切换选中敌人（MVP 最多酋长战 1 首领，通常 ≤2）

### 3.2 牌区（已定案）

- 抽/弃/消耗三堆均可 PileInspect
- 手牌区数量不再写入长 `Status` 文本

### 3.3 文案

- 主 HUD 使用**简体中文**（结束回合、玩家回合、敌方回合等）
- 保留卡牌 `displayName` 原文

## 4. 交互流程

1. 进入战斗 → HUD 全分区刷新
2. 玩家回合 → 拖拽出牌；点击牌堆查看；阅读意图行
3. 点击结束回合 → 请求 `RequestEndTurn`
4. 敌方回合 → PhaseBar 切换；意图行随 `PrepareEnemyIntents` 刷新
5. 意图截流等移除槽牌 → 对应槽显示「空」，顺序提示重算

## 5. 视觉预算

| 区域 | 最大元素 |
|------|----------|
| PlayerStrip | 4 资源项 + 12 状态图标 |
| 单 EnemyPanel | 4 意图槽 + 8 状态图标 |
| HUD 面板透明度 | 0.85–0.92 |

## 6. 不改动项

飘字、出牌动画、PlayLog 回放、世界点击选目标 — 见 [`战斗功能实现.md`](../程序/战斗功能实现.md)。
