using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public static class HexLivingWallRules
    {
        public const int InitialFootprintSize = 3;
        public const int MaxFootprintSize = 6;
        public const int ArmorPerCell = 8;
        public const int SpikeDamage = 10;
        public const int SqueezeDamage = 50;
        public const int OffspringHealth = 50;
        public const int MaxOffspring = 2;
        public const float BreakDamageRatio = 0.2f;

        public static List<HexAxialCoord> CreateInitialOffsets(HexGrid grid, HexAxialCoord core, HexAxialCoord facingTarget)
        {
            int facing = grid != null
                ? HexBattlePathing.GetPrimaryDirectionIndex(grid, core, facingTarget)
                : 0;
            HexAxialCoord sideA = HexAxialCoord.Directions[(facing + 2) % HexAxialCoord.Directions.Length];
            HexAxialCoord sideB = HexAxialCoord.Directions[(facing + 5) % HexAxialCoord.Directions.Length];
            return new List<HexAxialCoord>
            {
                new(0, 0),
                sideA,
                sideB,
            };
        }

        public static HexAxialCoord ToWorldCoord(HexAxialCoord core, HexAxialCoord offset) =>
            new(core.q + offset.q, core.r + offset.r);

        public static HexAxialCoord Rotate180(HexAxialCoord center, HexAxialCoord coord) =>
            new(center.q * 2 - coord.q, center.r * 2 - coord.r);

        public static int GetBreakDamage(int maxHealth) =>
            Mathf.CeilToInt(Mathf.Max(0, maxHealth) * BreakDamageRatio);

        public static bool IsConnected(IReadOnlyList<HexAxialCoord> offsets)
        {
            if (offsets == null || offsets.Count == 0)
                return false;

            var remaining = new HashSet<HexAxialCoord>(offsets);
            var queue = new Queue<HexAxialCoord>();
            queue.Enqueue(offsets[0]);
            remaining.Remove(offsets[0]);
            while (queue.Count > 0)
            {
                HexAxialCoord current = queue.Dequeue();
                for (int direction = 0; direction < HexAxialCoord.Directions.Length; direction++)
                {
                    HexAxialCoord neighbor = HexAxialCoord.Neighbor(current, direction);
                    if (!remaining.Remove(neighbor))
                        continue;
                    queue.Enqueue(neighbor);
                }
            }

            return remaining.Count == 0;
        }
    }
}
