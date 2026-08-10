using System;
using System.Linq;
using NUnit.Framework;

namespace HexDemo.EditorTests
{
    public sealed class HexEncounterGeneratorTests
    {
        [Test]
        public void FixedBattleMapPresets_MapDocumentedMvpSequence()
        {
            Assert.That(HexBattleMapPresetLibrary.GetForNode(HexMapNodeType.SmallBattle, 0).mapId, Is.EqualTo(HexBattleMapPresetLibrary.GoblinOpenId));
            Assert.That(HexBattleMapPresetLibrary.GetForNode(HexMapNodeType.SmallBattle, 1).mapId, Is.EqualTo(HexBattleMapPresetLibrary.SpearGoblinCoverId));
            Assert.That(HexBattleMapPresetLibrary.GetForNode(HexMapNodeType.SmallBattle, 2).mapId, Is.EqualTo(HexBattleMapPresetLibrary.SpearGoblinCoverId));
            Assert.That(HexBattleMapPresetLibrary.GetForNode(HexMapNodeType.SmallBattle, 3).mapId, Is.EqualTo(HexBattleMapPresetLibrary.OrcNarrowLaneId));
            Assert.That(HexBattleMapPresetLibrary.GetForNode(HexMapNodeType.EliteBattle, 0).mapId, Is.EqualTo(HexBattleMapPresetLibrary.LivingWallEliteId));
            Assert.That(HexBattleMapPresetLibrary.GetForNode(HexMapNodeType.Boss, 0).mapId, Is.EqualTo(HexBattleMapPresetLibrary.TribalChieftainBossId));
        }

        [Test]
        public void FixedBattleMapPresets_UseOnlyMvpTerrainAndKnownEnemies()
        {
            string[] ids =
            {
                HexBattleMapPresetLibrary.GoblinOpenId,
                HexBattleMapPresetLibrary.SpearGoblinCoverId,
                HexBattleMapPresetLibrary.OrcNarrowLaneId,
                HexBattleMapPresetLibrary.LivingWallEliteId,
                HexBattleMapPresetLibrary.TribalChieftainBossId,
            };

            foreach (string id in ids)
            {
                var preset = HexBattleMapPresetLibrary.Get(id);
                Assert.That(preset, Is.Not.Null, id);
                Assert.That(preset.width, Is.EqualTo(11), id);
                Assert.That(preset.height, Is.EqualTo(11), id);
                Assert.That(preset.enemySpawns, Is.Not.Empty, id);
                Assert.That(preset.enemySpawns.All(spawn => HexCardLibrary.GetEnemyDefinition(spawn.enemyDefinitionId) != null), Is.True, id);
                Assert.That(preset.terrainOverrides.All(IsMvpTerrainOverride), Is.True, id);
            }
        }

        [Test]
        public void OrcPreset_HasStraightClearOpeningChargeLane()
        {
            var preset = HexBattleMapPresetLibrary.Get(HexBattleMapPresetLibrary.OrcNarrowLaneId);
            var orc = preset.enemySpawns.First(spawn => spawn.enemyDefinitionId == HexEncounterGenerator.OrcWarriorId);

            Assert.That(preset.playerSpawnCoord, Is.EqualTo(new HexAxialCoord(3, 5)));
            Assert.That(orc.spawnCoord, Is.EqualTo(new HexAxialCoord(6, 5)));
            Assert.That(preset.terrainOverrides.Any(item => item.coord.Equals(new HexAxialCoord(4, 5)) || item.coord.Equals(new HexAxialCoord(5, 5))), Is.False);
        }

        [Test]
        public void FirstThreeCombats_UseDocumentedWeakPoolWeightsAndConstraints()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                int roll = new Random(seed).Next(100);
                HexEncounterPlan plan = HexEncounterGenerator.Generate(HexMapNodeType.SmallBattle, 0, seed);
                Assert.That(plan.enemyDefinitionIds, Has.Count.EqualTo(roll < 65 ? 2 : 3), $"seed={seed}");
                Assert.That(plan.enemyDefinitionIds, Has.None.EqualTo(HexEncounterGenerator.OrcWarriorId));
                Assert.That(plan.enemyDefinitionIds, Does.Contain(HexEncounterGenerator.GoblinId));
                Assert.That(plan.enemyDefinitionIds.All(id => id == HexEncounterGenerator.GoblinId || id == HexEncounterGenerator.SkeletonId), Is.True);
            }
        }

        [Test]
        public void FourthCombat_UsesDocumentedNormalTemplates()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                int roll = new Random(seed).Next(100);
                int expectedOrcs = roll < 30 ? 0 : roll < 80 ? 1 : 2;
                HexEncounterPlan plan = HexEncounterGenerator.Generate(HexMapNodeType.SmallBattle, 3, seed);
                Assert.That(plan.enemyDefinitionIds, Has.Count.EqualTo(3));
                Assert.That(plan.enemyDefinitionIds.Count(id => id == HexEncounterGenerator.OrcWarriorId), Is.EqualTo(expectedOrcs), $"seed={seed}, roll={roll}");
                Assert.That(plan.enemyDefinitionIds.Any(id => id == HexEncounterGenerator.GoblinId || id == HexEncounterGenerator.SkeletonId), Is.True);
                Assert.That(plan.enemyDefinitionIds.All(id => id == HexEncounterGenerator.SkeletonId), Is.False);
            }
        }

        [Test]
        public void ConsecutiveNormalEncounters_AvoidSameComposition()
        {
            HexEncounterPlan first = HexEncounterGenerator.Generate(HexMapNodeType.SmallBattle, 3, 8404);
            HexEncounterPlan second = HexEncounterGenerator.Generate(HexMapNodeType.SmallBattle, 3, 8404, first.Signature);
            Assert.That(second.Signature, Is.Not.EqualTo(first.Signature));
        }

        [Test]
        public void ElitePool_IsFiftyFiftyByRollAndLivingWallIsAlwaysPaired()
        {
            bool sawGoblinSquad = false;
            bool sawLivingWall = false;
            for (int seed = 1; seed <= 200; seed++)
            {
                int roll = new Random(seed).Next(100);
                HexEncounterPlan plan = HexEncounterGenerator.Generate(HexMapNodeType.EliteBattle, 0, seed);
                HexEncounterPlanKind expected = roll < 50
                    ? HexEncounterPlanKind.EliteGoblinSquad
                    : HexEncounterPlanKind.EliteLivingWallPair;
                Assert.That(plan.kind, Is.EqualTo(expected));
                if (plan.kind == HexEncounterPlanKind.EliteLivingWallPair)
                {
                    sawLivingWall = true;
                    Assert.That(plan.enemyDefinitionIds, Is.EqualTo(new[] { "living_wall", "living_wall" }));
                }
                else
                {
                    sawGoblinSquad = true;
                    Assert.That(plan.enemyDefinitionIds, Is.EqualTo(new[] { "goblin_captain", "spear_goblin", "spear_goblin" }));
                }
            }

            Assert.That(sawGoblinSquad && sawLivingWall, Is.True);
        }

        [Test]
        public void OrcChargeRules_RequireStraightClearPathAndExcludeTargetCellFromMovement()
        {
            var start = new HexAxialCoord(2, 2);
            HexAxialCoord target = HexAxialCoord.Neighbor(HexAxialCoord.Neighbor(start, 0), 0);
            bool valid = HexOrcWarriorRules.TryBuildChargePath(start, target, _ => false, out int direction, out var path);

            Assert.That(valid, Is.True);
            Assert.That(direction, Is.EqualTo(0));
            Assert.That(path, Has.Count.EqualTo(2));
            Assert.That(path[^1], Is.Not.EqualTo(target));

            HexAxialCoord blocker = HexAxialCoord.Neighbor(start, 0);
            Assert.That(HexOrcWarriorRules.TryBuildChargePath(start, target, coord => coord.Equals(blocker), out _, out _), Is.False);
            Assert.That(HexOrcWarriorRules.TryBuildChargePath(start, new HexAxialCoord(3, 3), _ => false, out _, out _), Is.False);
        }

        private static bool IsMvpTerrainOverride(HexBattleMapTerrainOverride item)
        {
            if (item == null)
                return false;

            bool mvpZone = item.zone == HexTerrainZoneType.Normal || item.zone == HexTerrainZoneType.Pit;
            bool mvpStructure = item.structureType == HexTerrainStructureType.None ||
                                item.structureType == HexTerrainStructureType.Barrier ||
                                item.structureType == HexTerrainStructureType.Ruin;
            bool mvpProp = string.IsNullOrWhiteSpace(item.propId) ||
                           item.propId == HexPropLibrary.DefaultBarrierPropId ||
                           item.propId == HexPropLibrary.DefaultRuinPropId;
            bool mvpPickup = item.pickupType == HexTerrainPickupType.None ||
                             item.pickupType == HexTerrainPickupType.Heal ||
                             item.pickupType == HexTerrainPickupType.TemporaryStrength ||
                             item.pickupType == HexTerrainPickupType.TemporaryCard;
            return mvpZone && mvpStructure && mvpProp && mvpPickup;
        }
    }
}
