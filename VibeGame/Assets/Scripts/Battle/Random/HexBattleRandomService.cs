using System;

namespace HexDemo.Battle
{
    public sealed class HexBattleRandomService
    {
        private readonly Random _random;

        public int Seed { get; }
        public int RandomCallIndex { get; private set; }

        public HexBattleRandomService(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            RandomCallIndex++;
            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
