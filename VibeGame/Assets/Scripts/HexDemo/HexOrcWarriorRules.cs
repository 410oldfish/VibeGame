using System;
using System.Collections.Generic;

namespace HexDemo
{
    public static class HexOrcWarriorRules
    {
        public const int MaxChargeDistance = 3;
        public const int BaseChargeDamage = 8;
        public const int EmpoweredChargeDamage = 10;
        public const int BaseKnockback = 1;
        public const int EmpoweredKnockback = 2;

        public static bool TryBuildChargePath(
            HexAxialCoord start,
            HexAxialCoord target,
            Func<HexAxialCoord, bool> isBlocked,
            out int direction,
            out List<HexAxialCoord> movementPath)
        {
            direction = -1;
            movementPath = new List<HexAxialCoord> { start };

            for (int candidateDirection = 0; candidateDirection < 6; candidateDirection++)
            {
                HexAxialCoord current = start;
                var candidatePath = new List<HexAxialCoord> { start };
                for (int distance = 1; distance <= MaxChargeDistance; distance++)
                {
                    current = HexAxialCoord.Neighbor(current, candidateDirection);
                    if (current.Equals(target))
                    {
                        direction = candidateDirection;
                        movementPath = candidatePath;
                        return true;
                    }

                    if (isBlocked != null && isBlocked(current))
                        break;

                    candidatePath.Add(current);
                }
            }

            return false;
        }
    }
}
