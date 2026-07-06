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
        public List<HexTileSnapshot> tiles = new();
        public List<HexEnemyIntentSnapshot> enemyIntents = new();
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
        public string enemyDefinitionId;
    }

    [Serializable]
    public sealed class HexHandVisibilitySnapshot
    {
        public string playerId;
        public int handCount;
        public List<string> visibleCardIds = new();
    }

    [Serializable]
    public sealed class HexTileSnapshot
    {
        public int q;
        public int r;
        public HexTerrainBaseType baseTerrain;
        public HexTerrainStructureType structureType;
        public int structureHp;
        public HexTerrainPickupType pickupType;
        public int pickupAmount;
    }

    [Serializable]
    public sealed class HexEnemyIntentSnapshot
    {
        public string unitId;
        public string enemyDefinitionId;
        public List<HexEnemyIntentSlotSnapshot> slots = new();
    }

    [Serializable]
    public sealed class HexEnemyIntentSlotSnapshot
    {
        public HexEnemyIntentSlotKind slotKind;
        public string cardRuntimeId;
        public string cardId;
    }
}
