# 交互模式库 — VibeGame

> **Status**: Approved  
> **Last Updated**: 2026-07-09  
> **Platform**: PC（键鼠优先）

## ModalDismiss — 模态层关闭

| 项 | 规范 |
|----|------|
| 触发 | 点击半透明遮罩、Close 按钮、**右键**、**Esc**（若焦点在 UI） |
| 适用 | 牌堆预览、出牌回放、敌人手牌/意图详情 |
| 行为 | 关闭最顶层模态；不穿透到世界点击 |
| 实现锚点 | `HexBattleUI.CloseEnemyHandPopup`、`CloseTopModal` |

## PileInspect — 牌堆查看

| 项 | 规范 |
|----|------|
| 触发 | 点击抽牌堆 / 弃牌堆 / **消耗牌堆** 按钮 |
| 展示 | 网格卡牌缩略图 + 标题「抽牌堆 (N)」等 |
| 交互 | 战斗中**只读**；Close 遵循 ModalDismiss |
| 数据 | `GetLocalDrawPile` / `GetLocalDiscardPile` / `GetLocalExhaustPile` |

## IntentInspect — 意图槽查看

| 项 | 规范 |
|----|------|
| 主展示 | HUD `EnemyIntentRow` 常驻显示 1~4 槽 |
| 次级 | 悬停/点击单槽 → 卡牌名、费用、槽位标签、简短描述 |
| 执行顺序 | 槽下方一行提示（ApproachStrike / Ranged 规则） |
| 空槽 | 意图被消耗后显示「空」占位，不收缩槽位 |

## StatusTooltip — 状态图标

| 项 | 规范 |
|----|------|
| 展示 | 图标 + 层数数字；增益左、减益右（或色条区分） |
| 悬停 | 显示中文名 + 层数 + 一句规则摘要 |
| 编码 | **形状/文字 + 颜色** 双编码，不单靠颜色 |
| MVP 白名单 | 见 `battle-intent-and-status.md` §3 |

## HandCardDrag — 手牌拖拽出牌

| 项 | 规范 |
|----|------|
| 状态 | 已实现（`HexCardView`） |
| 约束 | 不改变；本迭代不重构 |
