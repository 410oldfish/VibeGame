using System.Collections.Generic;

namespace HexDemo
{
    public static class HexBattleStatusDisplay
    {
        public static List<BattleStatusEntry> BuildMvpStatusEntries(HexBattleUnitState state)
        {
            var entries = new List<BattleStatusEntry>(12);
            if (state == null)
                return entries;

            TryAdd(entries, BattleHudStatusKind.Strength, state.strength, "力量", "攻击伤害增加", true, "力");
            TryAdd(entries, BattleHudStatusKind.Block, state.block, "格挡", "受伤时减少等量伤害", true, "格");
            TryAdd(entries, BattleHudStatusKind.Steady, state.toughness, "稳固", "无法被动移动", true, "固");
            TryAdd(entries, BattleHudStatusKind.Vampirism, state.vampirism, "吸血", "下次造成伤害时恢复等量生命", true, "吸");
            TryAdd(entries, BattleHudStatusKind.Burn, state.burn, "燃烧", "回合开始受到伤害，层数减半", false, "燃");
            TryAdd(entries, BattleHudStatusKind.Bleed, state.bleed, "流血", "回合结束受到层数伤害后清除", false, "血");
            TryAdd(entries, BattleHudStatusKind.Vulnerable, state.vulnerable, "易伤", "受到伤害 +25%", false, "易");
            TryAdd(entries, BattleHudStatusKind.Bind, state.bind, "束缚", "无法移动", false, "束");
            TryAdd(entries, BattleHudStatusKind.Stun, state.stun, "眩晕", "无法行动", false, "晕");
            return entries;
        }

        private static void TryAdd(
            List<BattleStatusEntry> entries,
            BattleHudStatusKind kind,
            int stacks,
            string displayName,
            string rule,
            bool isBuff,
            string shortLabel)
        {
            if (stacks <= 0)
                return;

            entries.Add(new BattleStatusEntry
            {
                kind = kind,
                displayName = displayName,
                tooltip = $"{displayName} ×{stacks} — {rule}",
                stacks = stacks,
                isBuff = isBuff,
                shortLabel = shortLabel,
            });
        }

        public static string GetIntentSlotLabel(HexEnemyIntentSlotKind kind)
        {
            return kind switch
            {
                HexEnemyIntentSlotKind.Move => "移动",
                HexEnemyIntentSlotKind.Attack => "攻击",
                HexEnemyIntentSlotKind.Free => "自由",
                _ => kind.ToString(),
            };
        }

        public static string GetIntentSlotShort(HexEnemyIntentSlotKind kind)
        {
            return kind switch
            {
                HexEnemyIntentSlotKind.Move => "移",
                HexEnemyIntentSlotKind.Attack => "攻",
                HexEnemyIntentSlotKind.Free => "自",
                _ => "?",
            };
        }
    }
}
