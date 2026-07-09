using System.Collections.Generic;

namespace HexDemo
{
    public enum BattleHudStatusKind
    {
        Strength,
        Block,
        Steady,
        Vampirism,
        Burn,
        Bleed,
        Vulnerable,
        Bind,
        Stun,
    }

    public sealed class BattleStatusEntry
    {
        public BattleHudStatusKind kind;
        public string displayName;
        public string tooltip;
        public int stacks;
        public bool isBuff;
        public string shortLabel;
    }

    public sealed class BattleIntentSlotView
    {
        public HexEnemyIntentSlotKind slotKind;
        public string slotLabel;
        public string cardName;
        public int cardCost;
        public bool isEmpty;
        public int executionOrder;
    }

    public sealed class BattleUnitHudView
    {
        public string displayName;
        public int currentHealth;
        public int maxHealth;
        public int armor;
        public int energy;
        public int maxEnergy;
        public int power;
        public List<BattleStatusEntry> statuses = new();
        public List<BattleIntentSlotView> intentSlots = new();
        public string intentOrderHint;
        public int enemyIndex;
    }

    public sealed class BattlePileCounts
    {
        public int draw;
        public int hand;
        public int discard;
        public int exhaust;
    }

    public sealed class BattleHudSnapshot
    {
        public string phaseLabel;
        public bool canEndTurn;
        public BattleUnitHudView player = new();
        public List<BattleUnitHudView> enemies = new();
        public BattlePileCounts piles = new();
    }
}
