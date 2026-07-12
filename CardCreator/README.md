# Card Creator

## 牌表生成器（浏览设计表 / 组测试牌组）

从 `Feature-战士卡牌.md` 加载卡牌属性，按体系/类别/稀有度筛选与排序，选中后加入牌表，并导出给 Battle Sandbox 使用。

```powershell
cd CardCreator
python .\deck_table_builder.py
# 或双击
.\run_deck_table_builder.bat
```

功能：

- 实时显示选中卡牌全部字段（费用、范围、词缀、体系、协同、描述等）
- 按 **体系 / 类别 / 稀有度 / 分区 / 协同** 筛选，支持搜索与排序
- 将选中卡牌加入牌表（支持 ×4、MVP 初始 9 张）
- 导出 `exports/sandbox_deck.json`（含 `deckCardIds`）或纯 id 文本

Unity id 会从 `HexBattleCore.cs` 的 `W("warrior_…", "中文名", …)` 自动匹配。

## 旧版单卡草稿工具

Run:

```powershell
python .\card_creator.py
```

This tool lets you draft cards with these fields:

- `name`
- `cost` (integer like `-1`, `0`, `1`, `2`, or `X`)
- `profession`
- `rarity`
- `card_type`
- `description`

`profession` currently supports:

- `Warrior`
- `Paladin`
- `Druid`
- `Fighter`
- `Rogue`
- `General`
- `Slime`
- `Custom`

The draft list also supports filtering by:

- `card_type`
- `rarity`

You can also set a `Draft Profession` for the whole batch. After that, clicking `New` or adding the next card will automatically default the card's `profession` to that value.

Save output as JSON. After you finish a batch, send the exported file to Codex and it can be converted into the game's real card configuration.

Export format:

```json
{
  "cards": [
    {
      "name": "Throw Weapon",
      "cost": "X",
      "profession": "Warrior",
      "rarity": "Common",
      "card_type": "Attack",
      "description": "Deal 7 damage."
    }
  ]
}
```
