using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public static class HexBattleSetupUtility
    {
        public static HexAxialCoord FindClosestExistingCoord(HexGrid grid, HexAxialCoord desired, IEnumerable<HexAxialCoord> blockedCoords = null)
        {
            var blocked = blockedCoords != null ? new HashSet<HexAxialCoord>(blockedCoords) : null;
            float bestDistance = float.PositiveInfinity;
            HexAxialCoord bestCoord = desired;
            foreach (var kvp in grid.Tiles)
            {
                if (blocked != null && blocked.Contains(kvp.Key))
                    continue;
                if (kvp.Value != null && (kvp.Value.Controller == null ? kvp.Value.BlocksMovement : !kvp.Value.Controller.CanEnter()))
                    continue;

                float distance = HexAxialCoord.Distance(kvp.Key, desired);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCoord = kvp.Key;
                }
            }

            return bestCoord;
        }
    }
}
