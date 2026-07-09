using System.Collections.Generic;

namespace HexDemo
{
    public sealed class TileModel
    {
        public HexAxialCoord coord;
        public float topHeight;
        public HexTerrainBaseType baseTerrain = HexTerrainBaseType.Ground;
        public HexTerrainStructureType structureType = HexTerrainStructureType.None;
        public int structureHp;
        public HexTerrainPickupType pickupType = HexTerrainPickupType.None;
        public int pickupAmount;
        public readonly List<HexTileEffectState> effects = new();

        public bool BlocksMovement => baseTerrain == HexTerrainBaseType.Pit || structureType != HexTerrainStructureType.None;
        public bool BlocksLineOfSight => structureType == HexTerrainStructureType.HighGround;
        public bool HasRuin => structureType == HexTerrainStructureType.Ruin && structureHp > 0;
        public bool CanEnter => !BlocksMovement;
    }
}
