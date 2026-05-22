using System;
using System.Collections.Generic;
using HexDemo;

namespace HexDemo.Battle
{
    [Serializable]
    public sealed class HexBattleStateSnapshot
    {
        public int turn;
        public HexBattleFaction currentSide;
        public List<HexUnitSnapshot> players = new();
        public List<HexUnitSnapshot> enemies = new();
        public List<HexHandVisibilitySnapshot> hands = new();
    }

    [Serializable]
    public sealed class HexUnitSnapshot
    {
        public string unitId;
        public HexBattleFaction faction;
        public int q;
        public int r;
        public int hp;
        public int maxHp;
        public int energy;
        public int stamina;
        public int armor;
        public bool alive;
    }

    [Serializable]
    public sealed class HexHandVisibilitySnapshot
    {
        public string playerId;
        public int handCount;
        public List<string> visibleCardIds = new();
    }
}
