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
            return new List<HexAxialCoord>
            {
                new(0, 0),
                new(0, -1),
                new(0, 1),
            };
        }

        public static bool IsHorizontalLine(IReadOnlyList<HexAxialCoord> offsets)
        {
            if (offsets == null || offsets.Count == 0)
                return false;

            var unique = new HashSet<HexAxialCoord>(offsets);
            if (unique.Count != offsets.Count || !unique.Contains(new HexAxialCoord(0, 0)))
                return false;

            int minR = int.MaxValue;
            int maxR = int.MinValue;
            for (int i = 0; i < offsets.Count; i++)
            {
                if (offsets[i].q != 0)
                    return false;
                minR = Mathf.Min(minR, offsets[i].r);
                maxR = Mathf.Max(maxR, offsets[i].r);
            }

            return maxR - minR + 1 == offsets.Count;
        }

        public static List<HexAxialCoord> GetHorizontalGrowthCandidates(IReadOnlyList<HexAxialCoord> offsets)
        {
            var result = new List<HexAxialCoord>();
            if (!IsHorizontalLine(offsets) || offsets.Count >= MaxFootprintSize)
                return result;

            int minR = int.MaxValue;
            int maxR = int.MinValue;
            for (int i = 0; i < offsets.Count; i++)
            {
                minR = Mathf.Min(minR, offsets[i].r);
                maxR = Mathf.Max(maxR, offsets[i].r);
            }

            result.Add(new HexAxialCoord(0, minR - 1));
            result.Add(new HexAxialCoord(0, maxR + 1));
            return result;
        }

        public static bool MovementSegmentCrossesWall(
            HexGrid grid,
            HexAxialCoord from,
            HexAxialCoord to,
            IReadOnlyList<HexAxialCoord> occupiedCoords)
        {
            if (grid == null || occupiedCoords == null || occupiedCoords.Count == 0 || from.Equals(to))
                return false;

            Vector3 fromWorld = grid.AxialToWorld(from);
            Vector3 toWorld = grid.AxialToWorld(to);
            var movementStart = new Vector2(fromWorld.x, fromWorld.z);
            var movementEnd = new Vector2(toWorld.x, toWorld.z);
            float cellInteriorRadius = grid.hexSize * Mathf.Sqrt(3f) * 0.5f;

            for (int i = 0; i < occupiedCoords.Count; i++)
            {
                HexAxialCoord occupied = occupiedCoords[i];
                if (from.Equals(occupied) || to.Equals(occupied))
                    return true;

                Vector3 centerWorld = grid.AxialToWorld(occupied);
                var center = new Vector2(centerWorld.x, centerWorld.z);
                if (DistanceToSegment(center, movementStart, movementEnd) < cellInteriorRadius - 0.0001f)
                    return true;
            }

            for (int first = 0; first < occupiedCoords.Count; first++)
            {
                for (int second = first + 1; second < occupiedCoords.Count; second++)
                {
                    if (HexAxialCoord.Distance(occupiedCoords[first], occupiedCoords[second]) != 1)
                        continue;

                    Vector3 firstWorld = grid.AxialToWorld(occupiedCoords[first]);
                    Vector3 secondWorld = grid.AxialToWorld(occupiedCoords[second]);
                    if (SegmentsProperlyIntersect(
                        movementStart,
                        movementEnd,
                        new Vector2(firstWorld.x, firstWorld.z),
                        new Vector2(secondWorld.x, secondWorld.z)))
                        return true;
                }
            }

            return false;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return Vector2.Distance(point, start);

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static bool SegmentsProperlyIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            const float epsilon = 0.0001f;
            float abC = Cross(a, b, c);
            float abD = Cross(a, b, d);
            float cdA = Cross(c, d, a);
            float cdB = Cross(c, d, b);
            return ((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon)) &&
                   ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon));
        }

        private static float Cross(Vector2 origin, Vector2 first, Vector2 second) =>
            (first.x - origin.x) * (second.y - origin.y) -
            (first.y - origin.y) * (second.x - origin.x);

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
