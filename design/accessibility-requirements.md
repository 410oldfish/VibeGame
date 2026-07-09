# 无障碍要求 — VibeGame MVP

> **Tier**: Basic  
> **Last Updated**: 2026-07-09  
> **Applies to**: 战斗 HUD、冒险 UI（后续）

## Basic 档承诺

| 要求 | 战斗 HUD 实现 |
|------|----------------|
| 键鼠完整可操作 | 所有按钮可点击；模态可 Esc/右键关闭 |
| 对比度 | 正文与背景 ≥ 4.5:1（见 `battle-visual-spec.md`） |
| 非纯颜色状态 | 意图槽形状+文字；状态图标+数字+中文 Tooltip |
| 字号 | HUD 主文字 ≥ 18px @ 1920×1080 |
| 动效 | 飘字/槽位变化可快速完成；无强制长时间动画 |

## 不在 MVP 范围

- 完整屏幕阅读器朗读
- 色盲专用模式
- 手柄导航（Post-MVP）

## 验收

战斗 HUD 审查时 accessibility-specialist 按上表逐项勾选。
