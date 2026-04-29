using System;
using UnityEngine;

namespace HexDemo
{
    /// <summary>
    /// Axial coordinates (q, r) for pointy-top hex grids.
    /// </summary>
    [Serializable]
    public readonly struct HexAxialCoord : IEquatable<HexAxialCoord>
    {
        public readonly int q;
        public readonly int r;

        public HexAxialCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public bool Equals(HexAxialCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexAxialCoord other && Equals(other);
        public override int GetHashCode() => (q * 397) ^ r;
        public override string ToString() => $"(q={q}, r={r})";

        public static readonly HexAxialCoord[] Directions =
        {
            new HexAxialCoord(1, 0),
            new HexAxialCoord(1, -1),
            new HexAxialCoord(0, -1),
            new HexAxialCoord(-1, 0),
            new HexAxialCoord(-1, 1),
            new HexAxialCoord(0, 1),
        };

        public static HexAxialCoord Neighbor(HexAxialCoord a, int directionIndex)
        {
            var d = Directions[directionIndex % Directions.Length];
            return new HexAxialCoord(a.q + d.q, a.r + d.r);
        }

        /// <summary>
        /// Hex distance in axial coords.
        /// Formula works by converting to cube coords.
        /// </summary>
        public static int Distance(HexAxialCoord a, HexAxialCoord b)
        {
            int dq = a.q - b.q;
            int dr = a.r - b.r;
            int ds = (a.q + a.r) - (b.q + b.r);
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
        }
    }
}

