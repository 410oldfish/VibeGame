using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexBattleMapPreset
    {
        public readonly string mapId;
        public readonly HexEncounterPlanKind encounterKind;
        public readonly int randomSeed;
        public readonly int width;
        public readonly int height;
        public readonly HexAxialCoord playerSpawnCoord;
        public readonly IReadOnlyList<HexBattleMapEnemySpawn> enemySpawns;
        public readonly IReadOnlyList<HexBattleMapTerrainOverride> terrainOverrides;

        public HexBattleMapPreset(
            string mapId,
            HexEncounterPlanKind encounterKind,
            int randomSeed,
            int width,
            int height,
            HexAxialCoord playerSpawnCoord,
            IReadOnlyList<HexBattleMapEnemySpawn> enemySpawns,
            IReadOnlyList<HexBattleMapTerrainOverride> terrainOverrides)
        {
            this.mapId = mapId;
            this.encounterKind = encounterKind;
            this.randomSeed = randomSeed;
            this.width = width;
            this.height = height;
            this.playerSpawnCoord = playerSpawnCoord;
            this.enemySpawns = enemySpawns ?? new List<HexBattleMapEnemySpawn>();
            this.terrainOverrides = terrainOverrides ?? new List<HexBattleMapTerrainOverride>();
        }
    }

    public sealed class HexBattleMapEnemySpawn
    {
        public readonly string enemyDefinitionId;
        public readonly string displayName;
        public readonly HexAxialCoord spawnCoord;
        public readonly bool hasLivingWallPartner;
        public readonly HexAxialCoord livingWallPartnerSpawnCoord;

        public HexBattleMapEnemySpawn(
            string enemyDefinitionId,
            string displayName,
            HexAxialCoord spawnCoord,
            bool hasLivingWallPartner = false,
            HexAxialCoord livingWallPartnerSpawnCoord = default)
        {
            this.enemyDefinitionId = enemyDefinitionId;
            this.displayName = displayName;
            this.spawnCoord = spawnCoord;
            this.hasLivingWallPartner = hasLivingWallPartner;
            this.livingWallPartnerSpawnCoord = livingWallPartnerSpawnCoord;
        }
    }

    public sealed class HexBattleMapTerrainOverride
    {
        public readonly HexAxialCoord coord;
        public readonly HexTerrainZoneType zone;
        public readonly HexTerrainStructureType structureType;
        public readonly string propId;
        public readonly int structureHp;
        public readonly HexTerrainPickupType pickupType;
        public readonly int pickupAmount;

        public HexBattleMapTerrainOverride(
            int q,
            int r,
            HexTerrainZoneType zone = HexTerrainZoneType.Normal,
            HexTerrainStructureType structureType = HexTerrainStructureType.None,
            string propId = "",
            int structureHp = 0,
            HexTerrainPickupType pickupType = HexTerrainPickupType.None,
            int pickupAmount = 0)
        {
            coord = new HexAxialCoord(q, r);
            this.zone = zone;
            this.structureType = structureType;
            this.propId = propId ?? string.Empty;
            this.structureHp = structureHp;
            this.pickupType = pickupType;
            this.pickupAmount = pickupAmount;
        }
    }

    public static class HexBattleMapPresetLibrary
    {
        public const string GoblinOpenId = "BMAP-01_Goblin_Open";
        public const string SpearGoblinCoverId = "BMAP-02_SpearGoblin_Cover";
        public const string OrcNarrowLaneId = "BMAP-03_Orc_NarrowLane";
        public const string LivingWallEliteId = "BMAP-04_LivingWall_Elite";
        public const string TribalChieftainBossId = "BMAP-05_TribalChieftain_Boss";

        private static readonly Dictionary<string, HexBattleMapPreset> ById = BuildMap();

        public static HexBattleMapPreset Get(string mapId) =>
            !string.IsNullOrWhiteSpace(mapId) && ById.TryGetValue(mapId, out var preset) ? preset : null;

        public static HexBattleMapPreset GetForNode(HexMapNodeType nodeType, int completedCombatCount)
        {
            if (nodeType == HexMapNodeType.EliteBattle)
                return Get(LivingWallEliteId);
            if (nodeType == HexMapNodeType.Boss)
                return Get(TribalChieftainBossId);

            int battleNumber = Mathf.Max(1, completedCombatCount + 1);
            if (battleNumber == 1)
                return Get(GoblinOpenId);
            if (battleNumber <= 3)
                return Get(SpearGoblinCoverId);

            return Get(OrcNarrowLaneId);
        }

        public static HexEncounterPlan CreateEncounterPlan(HexBattleMapPreset preset, int seed)
        {
            var ids = preset?.enemySpawns?.Select(spawn => spawn.enemyDefinitionId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
                ?? new List<string>();
            if (preset != null && preset.encounterKind == HexEncounterPlanKind.EliteLivingWallPair && ids.Count == 1)
                ids.Add(ids[0]);

            return new HexEncounterPlan
            {
                kind = preset?.encounterKind ?? HexEncounterPlanKind.Normal,
                seed = seed,
                enemyDefinitionIds = ids,
            };
        }

        public static void ApplyTerrainOverrides(HexGrid grid, HexBattleMapPreset preset)
        {
            if (grid == null || preset == null)
                return;

            foreach (var item in preset.terrainOverrides)
            {
                if (item == null || !grid.TryGetTile(item.coord, out var tile) || tile == null)
                {
                    Debug.LogWarning($"[BattleMapPreset] Missing tile for override in {preset.mapId}: {item?.coord.ToString() ?? "<null>"}");
                    continue;
                }

                tile.zone = item.zone;
                if (!string.IsNullOrWhiteSpace(item.propId))
                    tile.SetProp(item.propId, item.structureHp > 0 ? item.structureHp : (int?)null);
                else
                    tile.SetStructure(item.structureType, item.structureHp);

                tile.SetPickup(item.pickupType, item.pickupAmount);
            }
        }

        private static Dictionary<string, HexBattleMapPreset> BuildMap()
        {
            var presets = new[]
            {
                CreateGoblinOpen(),
                CreateSpearGoblinCover(),
                CreateOrcNarrowLane(),
                CreateLivingWallElite(),
                CreateTribalChieftainBoss(),
            };
            return presets.ToDictionary(preset => preset.mapId, preset => preset, System.StringComparer.Ordinal);
        }

        private static HexBattleMapPreset CreateGoblinOpen() => new(
            GoblinOpenId,
            HexEncounterPlanKind.Normal,
            1101,
            11,
            11,
            new HexAxialCoord(3, 5),
            new[]
            {
                new HexBattleMapEnemySpawn("goblin", "哥布林 A", new HexAxialCoord(7, 4)),
                new HexBattleMapEnemySpawn("goblin", "哥布林 B", new HexAxialCoord(7, 6)),
            },
            new[]
            {
                Barrier(5, 4),
                Ruin(6, 5),
                Ruin(5, 6),
            });

        private static HexBattleMapPreset CreateSpearGoblinCover() => new(
            SpearGoblinCoverId,
            HexEncounterPlanKind.Normal,
            1102,
            11,
            11,
            new HexAxialCoord(3, 5),
            new[]
            {
                new HexBattleMapEnemySpawn("goblin", "哥布林", new HexAxialCoord(8, 6)),
                new HexBattleMapEnemySpawn("spear_goblin", "投矛哥布林", new HexAxialCoord(8, 4)),
            },
            new[]
            {
                Barrier(5, 4),
                Barrier(6, 5),
                Ruin(6, 3),
                Ruin(4, 6),
                Pickup(4, 7, HexTerrainPickupType.Heal, 15),
            });

        private static HexBattleMapPreset CreateOrcNarrowLane() => new(
            OrcNarrowLaneId,
            HexEncounterPlanKind.Normal,
            1103,
            11,
            11,
            new HexAxialCoord(3, 5),
            new[]
            {
                new HexBattleMapEnemySpawn("orc_warrior", "兽人战士", new HexAxialCoord(6, 5)),
                new HexBattleMapEnemySpawn("goblin", "哥布林", new HexAxialCoord(7, 7)),
            },
            new[]
            {
                Barrier(4, 3),
                Barrier(6, 3),
                Barrier(4, 7),
                Barrier(6, 7),
                Ruin(5, 4),
                Ruin(5, 6),
                Ruin(7, 4),
                Pit(7, 3),
                Pit(3, 7),
                Pit(7, 6),
            });

        private static HexBattleMapPreset CreateLivingWallElite() => new(
            LivingWallEliteId,
            HexEncounterPlanKind.EliteLivingWallPair,
            1104,
            11,
            11,
            new HexAxialCoord(3, 5),
            new[]
            {
                new HexBattleMapEnemySpawn("living_wall", "活墙壁", new HexAxialCoord(6, 4), true, new HexAxialCoord(6, 7)),
            },
            new[]
            {
                Barrier(5, 5),
                Barrier(7, 5),
                Ruin(4, 4),
                Ruin(4, 6),
                Ruin(8, 4),
                Ruin(8, 6),
                Pit(3, 3),
                Pit(3, 7),
                Pit(8, 3),
                Pit(8, 7),
            });

        private static HexBattleMapPreset CreateTribalChieftainBoss() => new(
            TribalChieftainBossId,
            HexEncounterPlanKind.Boss,
            1105,
            11,
            11,
            new HexAxialCoord(3, 5),
            new[]
            {
                new HexBattleMapEnemySpawn("tribal_chieftain", "部落酋长", new HexAxialCoord(8, 5)),
            },
            new[]
            {
                Barrier(6, 3),
                Barrier(6, 7),
                Ruin(5, 4),
                Ruin(7, 4),
                Ruin(6, 5),
                Ruin(5, 6),
                Ruin(7, 6),
                Pit(4, 3),
                Pit(8, 3),
                Pit(4, 7),
                Pit(8, 7),
                Pickup(4, 5, HexTerrainPickupType.TemporaryStrength, 2),
            });

        private static HexBattleMapTerrainOverride Barrier(int q, int r) =>
            new(q, r, structureType: HexTerrainStructureType.Barrier, propId: HexPropLibrary.DefaultBarrierPropId);

        private static HexBattleMapTerrainOverride Ruin(int q, int r) =>
            new(q, r, structureType: HexTerrainStructureType.Ruin, propId: HexPropLibrary.DefaultRuinPropId, structureHp: 4);

        private static HexBattleMapTerrainOverride Pit(int q, int r) =>
            new(q, r, zone: HexTerrainZoneType.Pit);

        private static HexBattleMapTerrainOverride Pickup(int q, int r, HexTerrainPickupType type, int amount) =>
            new(q, r, pickupType: type, pickupAmount: amount);
    }
}
