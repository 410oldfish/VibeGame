using System;
using System.Collections.Generic;
using System.Linq;

namespace HexDemo
{
    public enum HexEncounterPlanKind
    {
        Normal = 0,
        EliteGoblinSquad = 1,
        EliteLivingWallPair = 2,
        Boss = 3,
    }

    [Serializable]
    public sealed class HexEncounterPlan
    {
        public HexEncounterPlanKind kind;
        public int seed;
        public List<string> enemyDefinitionIds = new();

        public string Signature => BuildSignature(enemyDefinitionIds);

        public static string BuildSignature(IEnumerable<string> ids) =>
            string.Join("+", (ids ?? Array.Empty<string>()).OrderBy(id => id, StringComparer.Ordinal));
    }

    public static class HexEncounterGenerator
    {
        public const string GoblinId = "goblin";
        public const string SkeletonId = "skeleton";
        public const string OrcWarriorId = "orc_warrior";
        public const string LivingWallId = "living_wall";

        public static HexEncounterPlan Generate(
            HexMapNodeType nodeType,
            int completedCombatCount,
            int seed,
            string previousNormalSignature = null)
        {
            var random = new Random(seed);
            return nodeType switch
            {
                HexMapNodeType.EliteBattle => GenerateElite(random, seed),
                HexMapNodeType.Boss => CreatePlan(HexEncounterPlanKind.Boss, seed, "tribal_chieftain"),
                _ => GenerateNormal(random, Math.Max(1, completedCombatCount + 1), seed, previousNormalSignature),
            };
        }

        private static HexEncounterPlan GenerateElite(Random random, int seed)
        {
            return random.Next(100) < 50
                ? CreatePlan(HexEncounterPlanKind.EliteGoblinSquad, seed, "goblin_captain", "spear_goblin", "spear_goblin")
                : CreatePlan(HexEncounterPlanKind.EliteLivingWallPair, seed, LivingWallId, LivingWallId);
        }

        private static HexEncounterPlan GenerateNormal(Random random, int combatIndex, int seed, string previousSignature)
        {
            HexEncounterPlan candidate = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                candidate = CreateNormalCandidate(random, combatIndex, seed);
                if (string.IsNullOrEmpty(previousSignature) || candidate.Signature != previousSignature)
                    return candidate;
            }

            return CreateDeterministicAlternative(candidate, combatIndex, seed, previousSignature);
        }

        private static HexEncounterPlan CreateNormalCandidate(Random random, int combatIndex, int seed)
        {
            int minionCount;
            int orcCount;
            int roll = random.Next(100);
            if (combatIndex <= 3)
            {
                minionCount = roll < 65 ? 2 : 3;
                orcCount = 0;
            }
            else if (roll < 30)
            {
                minionCount = 3;
                orcCount = 0;
            }
            else if (roll < 80)
            {
                minionCount = 2;
                orcCount = 1;
            }
            else
            {
                minionCount = 1;
                orcCount = 2;
            }

            var ids = new List<string>(minionCount + orcCount);
            for (int i = 0; i < orcCount; i++)
                ids.Add(OrcWarriorId);
            for (int i = 0; i < minionCount; i++)
                ids.Add(random.Next(2) == 0 ? GoblinId : SkeletonId);

            EnsureMinionConstraints(ids, combatIndex);
            Shuffle(ids, random);
            return CreatePlan(HexEncounterPlanKind.Normal, seed, ids.ToArray());
        }

        private static HexEncounterPlan CreateDeterministicAlternative(
            HexEncounterPlan candidate,
            int combatIndex,
            int seed,
            string previousSignature)
        {
            var ids = candidate?.enemyDefinitionIds?.ToList() ?? new List<string> { GoblinId, SkeletonId };
            int skeletonIndex = ids.FindIndex(id => id == SkeletonId);
            int goblinIndex = ids.FindIndex(id => id == GoblinId);
            if (skeletonIndex >= 0)
                ids[skeletonIndex] = GoblinId;
            else if (goblinIndex >= 0)
                ids[goblinIndex] = SkeletonId;

            EnsureMinionConstraints(ids, combatIndex);
            if (HexEncounterPlan.BuildSignature(ids) == previousSignature)
            {
                ids = combatIndex <= 3
                    ? new List<string> { GoblinId, SkeletonId, GoblinId }
                    : new List<string> { OrcWarriorId, GoblinId, SkeletonId };
            }

            return CreatePlan(HexEncounterPlanKind.Normal, seed, ids.ToArray());
        }

        private static void EnsureMinionConstraints(List<string> ids, int combatIndex)
        {
            if (ids == null || ids.Count == 0)
                return;

            bool hasGoblin = ids.Contains(GoblinId);
            bool allSkeleton = ids.All(id => id == SkeletonId);
            if (allSkeleton || (combatIndex == 1 && !hasGoblin))
            {
                int replaceIndex = ids.FindIndex(id => id == SkeletonId);
                if (replaceIndex >= 0)
                    ids[replaceIndex] = GoblinId;
            }
        }

        private static void Shuffle(List<string> ids, Random random)
        {
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (ids[i], ids[swapIndex]) = (ids[swapIndex], ids[i]);
            }
        }

        private static HexEncounterPlan CreatePlan(HexEncounterPlanKind kind, int seed, params string[] ids) =>
            new()
            {
                kind = kind,
                seed = seed,
                enemyDefinitionIds = ids?.ToList() ?? new List<string>(),
            };
    }
}
