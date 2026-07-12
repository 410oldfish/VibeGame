using System.Collections.Generic;

namespace HexDemo
{
    public sealed class TileModel
    {
        public HexAxialCoord coord;
        public float topHeight;
        public HexTerrainZoneType zone = HexTerrainZoneType.Normal;
        public HexTerrainStructureType structureType = HexTerrainStructureType.None;
        public string propId;
        public int structureHp;
        public int structureMaxHp;
        public int? fuseTurns;
        public bool fuseArmed;
        public bool postBattleReward;
        public HexPropAdjacentAura adjacentAura;
        public readonly List<HexPropOnRemoveEffect> onRemoveEffects = new();
        public HexTerrainPickupType pickupType = HexTerrainPickupType.None;
        public int pickupAmount;
        public readonly List<HexTileEffectState> effects = new();

        public bool ZoneBlocksEntry => zone == HexTerrainZoneType.Pit;
        public bool BlocksMovement => ZoneBlocksEntry || structureType != HexTerrainStructureType.None;
        public bool BlocksLineOfSight => structureType == HexTerrainStructureType.Barrier;
        public bool HasRuin => structureType == HexTerrainStructureType.Ruin && structureHp > 0;
        public bool HasBarrier => structureType == HexTerrainStructureType.Barrier;
        public bool CanEnter => !BlocksMovement;
        public bool IsNonNormalZone => zone != HexTerrainZoneType.Normal;

        public void ClearPropRuntime()
        {
            propId = null;
            structureHp = 0;
            structureMaxHp = 0;
            fuseTurns = null;
            fuseArmed = false;
            postBattleReward = false;
            adjacentAura = null;
            onRemoveEffects.Clear();
        }

        public void ApplyPropDefinition(HexPropDefinition definition)
        {
            ClearPropRuntime();
            if (definition == null)
                return;

            propId = definition.propId;
            structureType = definition.structureType;
            structureMaxHp = definition.IsRuin ? UnityEngine.Mathf.Max(1, definition.ruinHp) : 0;
            structureHp = structureMaxHp;
            fuseTurns = definition.fuseTurns;
            fuseArmed = false;
            postBattleReward = definition.postBattleReward;
            adjacentAura = definition.adjacentAura?.Clone();
            if (definition.onRemoveEffects != null)
            {
                for (int i = 0; i < definition.onRemoveEffects.Count; i++)
                {
                    if (definition.onRemoveEffects[i] != null)
                        onRemoveEffects.Add(definition.onRemoveEffects[i].Clone());
                }
            }
        }
    }
}
