# 战斗 UI 引擎实现说明

> **Status**: Approved (Phase 3.0)  
> **Engine**: Unity 6000 URP  
> **Framework**: UGUI + TextMeshPro

## 框架选择

延续 **UGUI + TMP**，不引入 UI Toolkit 双栈。

## Prefab 加载

1. `Resources.Load<GameObject>("UI/Battle/BattleHudCanvas")`
2. `#if UNITY_EDITOR` 回退 `AssetDatabase.LoadAssetAtPath`
3. 子面板同名路径 `Resources/UI/Battle/Panels/{name}`

## 新增组件

| 类型 | 职责 |
|------|------|
| `BattleHudTypes.cs` | Snapshot DTO |
| `HexStatusIconBar.cs` | 状态图标条 |
| `HexEnemyIntentRow.cs` | 单敌人意图行 |
| `HexBattleStatusDisplay.cs` | MVP 状态枚举与 Tooltip |

## Widget 挂载

- `EnemyIntentPanel`：程序化创建或挂于 HUD 下方
- `ExhaustPile`：与 Draw/Discard 对称布局

## 后续迁移

TEngine `UIWindow` 迁移路径：将 `HexBattleUI` 拆为 `BattleHudWindow` + Widget，Snapshot 接口不变。

## 清理

- 删除未引用 `BattleHUDRoot.prefab`
- 根目录重复 Modal 以 `Panels/` 为准
