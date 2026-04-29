using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public static class HexBattlePathing
    {
        public static List<HexAxialCoord> FindPath(
            HexGrid grid,
            HexAxialCoord start,
            HexAxialCoord goal,
            System.Func<HexAxialCoord, bool> isBlocked)
        {
            if (!grid.IsCoordInside(start) || !grid.IsCoordInside(goal))
                return null;

            if (start.Equals(goal))
                return new List<HexAxialCoord> { start };

            var open = new List<HexAxialCoord> { start };
            var cameFrom = new Dictionary<HexAxialCoord, HexAxialCoord>();
            var gScore = new Dictionary<HexAxialCoord, int> { [start] = 0 };
            var fScore = new Dictionary<HexAxialCoord, int> { [start] = HexAxialCoord.Distance(start, goal) };

            while (open.Count > 0)
            {
                HexAxialCoord current = open[0];
                int bestF = fScore.TryGetValue(current, out int firstF) ? firstF : int.MaxValue;

                for (int i = 1; i < open.Count; i++)
                {
                    var candidate = open[i];
                    int candidateF = fScore.TryGetValue(candidate, out int f) ? f : int.MaxValue;
                    if (candidateF < bestF)
                    {
                        current = candidate;
                        bestF = candidateF;
                    }
                }

                if (current.Equals(goal))
                    return ReconstructPath(cameFrom, current, start);

                open.Remove(current);

                foreach (var neighbor in grid.GetNeighbors(current))
                {
                    if (!neighbor.Equals(goal) && isBlocked != null && isBlocked(neighbor))
                        continue;

                    int tentativeG = gScore[current] + 1;
                    if (!gScore.TryGetValue(neighbor, out int oldG) || tentativeG < oldG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + HexAxialCoord.Distance(neighbor, goal);
                        if (!open.Contains(neighbor))
                            open.Add(neighbor);
                    }
                }
            }

            return null;
        }

        public static IEnumerable<HexAxialCoord> GetCoordsInRange(HexAxialCoord center, int range)
        {
            for (int dq = -range; dq <= range; dq++)
            {
                int minR = Mathf.Max(-range, -dq - range);
                int maxR = Mathf.Min(range, -dq + range);
                for (int dr = minR; dr <= maxR; dr++)
                    yield return new HexAxialCoord(center.q + dq, center.r + dr);
            }
        }

        public static HexAxialCoord WorldToAxial(HexGrid grid, Vector3 worldPoint)
        {
            Vector3 local = worldPoint - grid.transform.position;
            float x = local.x - GetGridOriginOffsetX(grid);
            float z = local.z - GetGridOriginOffsetZ(grid);

            float q = ((Mathf.Sqrt(3f) / 3f) * x - (1f / 3f) * z) / grid.hexSize;
            float r = ((2f / 3f) * z) / grid.hexSize;
            return CubeRound(q, r);
        }

        private static float GetGridOriginOffsetX(HexGrid grid)
        {
            var center = grid.AxialToWorld(new HexAxialCoord(0, 0));
            return center.x;
        }

        private static float GetGridOriginOffsetZ(HexGrid grid)
        {
            var center = grid.AxialToWorld(new HexAxialCoord(0, 0));
            return center.z;
        }

        private static HexAxialCoord CubeRound(float q, float r)
        {
            float x = q;
            float z = r;
            float y = -x - z;

            int rx = Mathf.RoundToInt(x);
            int ry = Mathf.RoundToInt(y);
            int rz = Mathf.RoundToInt(z);

            float xDiff = Mathf.Abs(rx - x);
            float yDiff = Mathf.Abs(ry - y);
            float zDiff = Mathf.Abs(rz - z);

            if (xDiff > yDiff && xDiff > zDiff)
                rx = -ry - rz;
            else if (yDiff > zDiff)
                ry = -rx - rz;
            else
                rz = -rx - ry;

            return new HexAxialCoord(rx, rz);
        }

        private static List<HexAxialCoord> ReconstructPath(
            Dictionary<HexAxialCoord, HexAxialCoord> cameFrom,
            HexAxialCoord current,
            HexAxialCoord start)
        {
            var path = new List<HexAxialCoord> { current };
            while (!current.Equals(start))
            {
                if (!cameFrom.TryGetValue(current, out var prev))
                    break;

                current = prev;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
