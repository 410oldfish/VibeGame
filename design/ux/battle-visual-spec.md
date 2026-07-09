# 战斗 UI 视觉规格

> **Status**: Approved (Phase 2)  
> **Last Updated**: 2026-07-09  
> **Author**: art-director (lean style sheet)

## 色板

| 用途 | 色值 | 对比备注 |
|------|------|----------|
| HUD 面板底 | `#141A22` @ 88% | 白字可读 |
| 主文字 | `#F2F4F8` | — |
| 次级文字 | `#A8B0C0` | — |
| 增益条 | `#3DB87A` | 配图标 |
| 减益条 | `#E85A4A` | 配图标 |
| 意图·移动 | `#4A9FE8` | +「移」字 |
| 意图·攻击 | `#E85A4A` | +「攻」字 |
| 意图·自由 | `#9A7AE8` | +「自」字 |

## 字体

- 复用 `HexTMPFontProvider` / `HexChineseDynamic SDF`
- PhaseBar 30px Bold；资源 22px；意图槽 18px

## 图标资产（占位）

| 资产 | 尺寸 | 首版实现 |
|------|------|----------|
| 9 状态图标 | 24×24 | 程序化色块+缩写（力/格/固/吸/燃/血/易/束/晕） |
| 3 槽位图标 | 16×16 | 文字徽章 |
| 牌堆图标 | 32×32 | 文字「抽」「弃」「耗」 |

## Prefab 节点（目标树）

```
BattleHudCanvas
├── HUD (PhaseBar, PlayerStrip, StatusIconBar)
├── EnemyIntentPanel (EnemyIntentRow × N)
├── ResourcePanel
├── HandPanel
├── ActionPanel
├── DrawPile / DiscardPile / ExhaustPile
├── PileModal / PlayLogModal / EnemyHandOverlay
```

## 动画

- 意图槽填充：0.15s 淡入（可 `Time.unscaledDeltaTime` 快速完成）
- 状态层数变化：数字弹跳 0.1s
- 尊重 `Application.isFocused`；无系统 reduced-motion API 时保持短时

## 分辨率

- 基准 1920×1080；1280×720 下 EnemyPanel 宽度 100%，意图槽横向滚动
