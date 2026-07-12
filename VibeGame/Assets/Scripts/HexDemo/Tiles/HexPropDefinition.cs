using System;
using System.Collections.Generic;

namespace HexDemo
{
    public enum HexPropDestroyBy
    {
        SpecialOnly = 0,
        NormalAttack = 1,
        Both = 2,
    }

    public enum HexPropOnRemoveType
    {
        None = 0,
        FieldPickup = 1,
        TempOverlay = 2,
        TransformGround = 3,
        SpawnProp = 4,
        SpawnUnit = 5,
        AreaDamage = 6,
        ApplyStatus = 7,
        EnemyTrigger = 8,
        PostBattleReward = 9,
    }

    [Serializable]
    public sealed class HexPropOnRemoveEffect
    {
        public HexPropOnRemoveType type = HexPropOnRemoveType.None;
        public string payloadId;
        public int amount;
        public string summary;

        public HexPropOnRemoveEffect Clone()
        {
            return new HexPropOnRemoveEffect
            {
                type = type,
                payloadId = payloadId,
                amount = amount,
                summary = summary,
            };
        }
    }

    [Serializable]
    public sealed class HexPropAdjacentAura
    {
        public string summary;
        public string enemyTag;
        public int radius = 1;

        public HexPropAdjacentAura Clone()
        {
            return new HexPropAdjacentAura
            {
                summary = summary,
                enemyTag = enemyTag,
                radius = radius,
            };
        }
    }

    [Serializable]
    public sealed class HexPropDefinition
    {
        public string propId;
        public string displayName;
        public HexTerrainStructureType structureType = HexTerrainStructureType.None;
        public int ruinHp;
        public bool blocksLos;
        public HexPropDestroyBy destroyBy = HexPropDestroyBy.SpecialOnly;
        public List<HexPropOnRemoveEffect> onRemoveEffects = new();
        public int? fuseTurns;
        public HexPropAdjacentAura adjacentAura;
        public bool postBattleReward;
        public string description;
        public string mvpStatus;

        public bool IsRuin => structureType == HexTerrainStructureType.Ruin;
        public bool IsBarrier => structureType == HexTerrainStructureType.Barrier;
    }
}
