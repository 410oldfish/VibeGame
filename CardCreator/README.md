# Card Creator

Run:

```powershell
python .\Tools\CardCreator\card_creator.py
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
