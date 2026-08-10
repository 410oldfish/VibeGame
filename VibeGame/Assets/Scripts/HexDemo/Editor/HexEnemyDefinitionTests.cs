using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexDemo.EditorTests
{
    public sealed class HexEnemyDefinitionTests
    {
        [Test]
        public void BuiltInEnemyIds_AreUniqueAndResolvable()
        {
            var ids = HexCardLibrary.GetBuiltInEnemyIds();
            Assert.That(ids.Count, Is.EqualTo(12));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            foreach (string id in ids)
            {
                Assert.That(HexCardLibrary.TryGetEnemyDefinition(id, out var definition), Is.True, id);
                Assert.That(definition.id, Is.EqualTo(id));
                Assert.That(definition.intentSlots.Count, Is.InRange(1, 4), id);
                Assert.That(definition.deckDefinitions, Is.Not.Empty, id);
                Assert.That(definition.deckDefinitions.All(card => card != null), Is.True, id);
            }
        }

        [Test]
        public void UnknownEnemy_DoesNotFallbackToGoblin()
        {
            LogAssert.Expect(LogType.Error, "Unknown enemy definition id: not_a_real_enemy");
            Assert.That(HexCardLibrary.GetEnemyDefinition("not_a_real_enemy"), Is.Null);
            LogAssert.Expect(LogType.Error, "Unknown enemy definition id: not_a_real_enemy");
            Assert.That(HexCardLibrary.TryGetEnemyDefinition("not_a_real_enemy", out _), Is.False);
        }

        [Test]
        public void EnemyDatabase_ContainsEveryBuiltInDefinition()
        {
            var database = Resources.Load<HexEnemyDatabaseSO>("HexEnemyDatabase");
            Assert.That(database, Is.Not.Null);
            Assert.That(database.enemies, Has.Count.EqualTo(12));
            Assert.That(database.enemies.Select(enemy => enemy.id).Distinct().Count(), Is.EqualTo(12));
            Assert.That(database.enemies.All(enemy => enemy.ToDefinition().deckDefinitions.Count > 0), Is.True);
        }

        [TestCase("goblin", 9, 2, false)]
        [TestCase("spear_goblin", 11, 2, true)]
        [TestCase("goblin_captain", 14, 3, true)]
        [TestCase("tribal_chieftain", 13, 4, true)]
        public void MvpEnemyDeckSnapshots_MatchDesign(string id, int deckCount, int slotCount, bool hasBottomCard)
        {
            var definition = HexCardLibrary.GetEnemyDefinition(id);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.deckDefinitions, Has.Count.EqualTo(deckCount));
            Assert.That(definition.intentSlots, Has.Count.EqualTo(slotCount));
            Assert.That(definition.bottomCard != null, Is.EqualTo(hasBottomCard));
        }

        [Test]
        public void TierOneEnemies_HaveNoBottomCards()
        {
            var goblin = HexCardLibrary.GetEnemyDefinition("goblin");
            var skeleton = HexCardLibrary.GetEnemyDefinition("skeleton");

            Assert.That(goblin.intentPattern, Is.EqualTo(HexEnemyIntentPattern.Fixed));
            Assert.That(goblin.intentSlots, Is.EqualTo(new[] { HexEnemyIntentSlotKind.Attack, HexEnemyIntentSlotKind.Move }));
            Assert.That(goblin.bottomCard, Is.Null);
            Assert.That(goblin.emptyDrawPileStrengthGain, Is.Zero);
            Assert.That(skeleton.displayName, Is.EqualTo("骷髅兵"));
            Assert.That(skeleton.intentPattern, Is.EqualTo(HexEnemyIntentPattern.Fixed));
            Assert.That(skeleton.bottomCard, Is.Null);
            Assert.That(skeleton.deckDefinitions, Has.Count.EqualTo(8));
            Assert.That(skeleton.intentSlots, Is.EqualTo(new[] { HexEnemyIntentSlotKind.Attack, HexEnemyIntentSlotKind.Move }));
        }

        [Test]
        public void TieredEnemyIntentSlots_DatabaseAndFallbackStayInSync()
        {
            MethodInfo fallbackFactory = typeof(HexCardLibrary).GetMethod(
                "CreateBuiltInEnemyDefinition",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(fallbackFactory, Is.Not.Null);

            AssertDatabaseAndFallback(
                fallbackFactory,
                "goblin",
                HexEnemyIntentPattern.Fixed,
                9,
                HexEnemyIntentSlotKind.Attack,
                HexEnemyIntentSlotKind.Move);
            AssertDatabaseAndFallback(
                fallbackFactory,
                "skeleton",
                HexEnemyIntentPattern.Fixed,
                8,
                HexEnemyIntentSlotKind.Attack,
                HexEnemyIntentSlotKind.Move);
            AssertDatabaseAndFallback(
                fallbackFactory,
                "orc_warrior",
                HexEnemyIntentPattern.LineCharge,
                10,
                HexEnemyIntentSlotKind.Move,
                HexEnemyIntentSlotKind.Attack);
        }

        [Test]
        public void BonePileSummonContract_UsesSkeletonDefinitionId()
        {
            var bonePile = HexPropLibrary.Get("bone_pile");
            Assert.That(bonePile, Is.Not.Null);
            Assert.That(
                bonePile.onRemoveEffects.Any(effect =>
                    effect.type == HexPropOnRemoveType.SpawnUnit &&
                    effect.payloadId == "skeleton" &&
                    effect.amount == 1),
                Is.True);
        }

        [Test]
        public void OrcWarriorDefinition_MatchesLineChargeDesign()
        {
            var definition = HexCardLibrary.GetEnemyDefinition("orc_warrior");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.displayName, Is.EqualTo("兽人战士"));
            Assert.That(definition.encounterType, Is.EqualTo(HexEnemyEncounterType.Normal));
            Assert.That(definition.intentPattern, Is.EqualTo(HexEnemyIntentPattern.LineCharge));
            Assert.That(definition.attackMaxRange, Is.EqualTo(3));
            Assert.That(definition.intentSlots, Is.EqualTo(new[] { HexEnemyIntentSlotKind.Move, HexEnemyIntentSlotKind.Attack }));
            Assert.That(definition.deckDefinitions, Has.Count.EqualTo(10));
            Assert.That(definition.bottomCard?.id, Is.EqualTo("enemy_orc_bottom"));

            var counts = CountById(definition.deckDefinitions);
            Assert.That(GetCount(counts, "enemy_orc_charge"), Is.EqualTo(4));
            Assert.That(GetCount(counts, "enemy_orc_heavy_slash"), Is.EqualTo(3));
            Assert.That(GetCount(counts, "enemy_orc_approach"), Is.EqualTo(2));
            Assert.That(GetCount(counts, "enemy_orc_stance"), Is.EqualTo(1));
            Assert.That(HexCardLibrary.GetCardById("enemy_orc_charge").amount, Is.EqualTo(8));
            Assert.That(HexCardLibrary.GetCardById("enemy_orc_heavy_slash").amount, Is.EqualTo(7));
        }

        private static void AssertDatabaseAndFallback(
            MethodInfo fallbackFactory,
            string id,
            HexEnemyIntentPattern expectedPattern,
            int expectedDeckCount,
            params HexEnemyIntentSlotKind[] expectedSlots)
        {
            var databaseDefinition = HexCardLibrary.GetEnemyDefinition(id);
            var fallbackDefinition = fallbackFactory.Invoke(null, new object[] { id }) as HexEnemyDefinition;

            Assert.That(databaseDefinition, Is.Not.Null, id);
            Assert.That(fallbackDefinition, Is.Not.Null, id);
            Assert.That(databaseDefinition.intentPattern, Is.EqualTo(expectedPattern), id);
            Assert.That(fallbackDefinition.intentPattern, Is.EqualTo(expectedPattern), id);
            Assert.That(databaseDefinition.intentSlots, Is.EqualTo(expectedSlots), id);
            Assert.That(fallbackDefinition.intentSlots, Is.EqualTo(expectedSlots), id);
            Assert.That(databaseDefinition.deckDefinitions, Has.Count.EqualTo(expectedDeckCount), id);
            Assert.That(fallbackDefinition.deckDefinitions, Has.Count.EqualTo(expectedDeckCount), id);
        }

        [Test]
        public void MvpCardValues_MatchDesign()
        {
            Assert.That(HexCardLibrary.GetCardById("enemy_goblin_strike").amount, Is.EqualTo(6));
            Assert.That(HexCardLibrary.GetCardById("enemy_spear_goblin_throw").amount, Is.EqualTo(4));
            Assert.That(HexCardLibrary.GetCardById("enemy_goblin_captain_guard").amount, Is.EqualTo(8));
            Assert.That(HexCardLibrary.GetCardById("enemy_chieftain_heavy_strike").amount, Is.EqualTo(15));
        }

        [Test]
        public void ChieftainPhaseTwo_ReplacesApproachWithQuake()
        {
            var definition = HexCardLibrary.GetEnemyDefinition("tribal_chieftain");
            var phaseOne = CountById(definition.deckDefinitions);
            var phaseTwo = CountById(definition.phaseTwoDeckDefinitions);
            Assert.That(GetCount(phaseOne, "enemy_goblin_approach"), Is.EqualTo(2));
            Assert.That(phaseOne.ContainsKey("enemy_chieftain_quake"), Is.False);
            Assert.That(GetCount(phaseTwo, "enemy_goblin_approach"), Is.EqualTo(0));
            Assert.That(GetCount(phaseTwo, "enemy_chieftain_quake"), Is.EqualTo(2));
        }

        [Test]
        public void SandboxEnemyEnum_CoversEveryPublicBuiltInEnemy()
        {
            var enumIds = System.Enum.GetValues(typeof(HexSandboxEnemyType))
                .Cast<HexSandboxEnemyType>()
                .Select(value => value.ToDefinitionId())
                .ToArray();
            Assert.That(enumIds, Has.Length.EqualTo(12));
            Assert.That(enumIds.Distinct().Count(), Is.EqualTo(12));
            Assert.That(enumIds.OrderBy(id => id), Is.EqualTo(HexCardLibrary.GetBuiltInEnemyIds().OrderBy(id => id)));
            Assert.That(enumIds, Does.Not.Contain("mind_tentacle"));
        }

        [Test]
        public void SandboxEnemyConfig_MigratesLegacyDefinitionId()
        {
            var config = new HexBattleSandboxScenarioSO.EnemyConfig();
            FieldInfo legacyField = typeof(HexBattleSandboxScenarioSO.EnemyConfig)
                .GetField("legacyEnemyDefinitionId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(legacyField, Is.Not.Null);
            legacyField.SetValue(config, "living_wall");

            Assert.That(config.MigrateLegacyId(), Is.True);
            Assert.That(config.enemyType, Is.EqualTo(HexSandboxEnemyType.LivingWall));
            Assert.That(config.DefinitionId, Is.EqualTo("living_wall"));
        }

        [Test]
        public void LivingWallDefinition_MatchesEightCardPairedDesign()
        {
            HexEnemyDefinition definition = HexCardLibrary.GetEnemyDefinition("living_wall");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.encounterType, Is.EqualTo(HexEnemyEncounterType.Elite));
            Assert.That(definition.intentPattern, Is.EqualTo(HexEnemyIntentPattern.PairedLivingWall));
            Assert.That(definition.intentSlots, Is.EqualTo(new[] { HexEnemyIntentSlotKind.Free }));
            Assert.That(definition.maxSummons, Is.EqualTo(2));
            Assert.That(definition.summonHealth, Is.EqualTo(50));

            var counts = CountById(definition.deckDefinitions);
            Assert.That(definition.deckDefinitions, Has.Count.EqualTo(8));
            Assert.That(GetCount(counts, "enemy_wall_advance"), Is.EqualTo(3));
            Assert.That(GetCount(counts, "enemy_wall_spike"), Is.EqualTo(2));
            Assert.That(GetCount(counts, "enemy_wall_reform"), Is.EqualTo(1));
            Assert.That(GetCount(counts, "enemy_wall_fortify"), Is.EqualTo(2));
            Assert.That(definition.bottomCard, Is.Null);
            Assert.That(HexCardLibrary.GetCardById("enemy_wall_spike").amount, Is.EqualTo(10));
        }

        [Test]
        public void LivingWallRules_CreateConnectedFootprintAndCeilBreakDamage()
        {
            var offsets = HexLivingWallRules.CreateInitialOffsets(
                null,
                new HexAxialCoord(4, 4),
                new HexAxialCoord(8, 1));
            Assert.That(offsets, Has.Count.EqualTo(HexLivingWallRules.InitialFootprintSize));
            Assert.That(HexLivingWallRules.IsConnected(offsets), Is.True);
            Assert.That(HexLivingWallRules.IsHorizontalLine(offsets), Is.True);
            Assert.That(HexLivingWallRules.GetBreakDamage(34), Is.EqualTo(7));
            Assert.That(HexLivingWallRules.GetBreakDamage(60), Is.EqualTo(12));
        }

        [Test]
        public void LivingWallRules_GrowsOnlyFromHorizontalEndpointsFromThreeToSixCells()
        {
            var offsets = HexLivingWallRules.CreateInitialOffsets(
                null,
                new HexAxialCoord(0, 0),
                new HexAxialCoord(3, -2));

            for (int expectedSize = HexLivingWallRules.InitialFootprintSize;
                 expectedSize <= HexLivingWallRules.MaxFootprintSize;
                 expectedSize++)
            {
                Assert.That(offsets, Has.Count.EqualTo(expectedSize));
                Assert.That(HexLivingWallRules.IsHorizontalLine(offsets), Is.True);
                Assert.That(offsets.All(offset => offset.q == 0), Is.True);

                if (expectedSize == HexLivingWallRules.MaxFootprintSize)
                    break;

                List<HexAxialCoord> candidates = HexLivingWallRules.GetHorizontalGrowthCandidates(offsets);
                Assert.That(candidates, Has.Count.EqualTo(2));
                Assert.That(candidates.All(offset => offset.q == 0), Is.True);
                offsets.Add(candidates[expectedSize % 2]);
            }

            Assert.That(HexLivingWallRules.GetHorizontalGrowthCandidates(offsets), Is.Empty);
        }

        [Test]
        public void LivingWallRules_MovementCannotCrossWallCellsOrConnectedSegments()
        {
            var gridObject = new GameObject("LivingWallMovementGrid");
            try
            {
                var grid = gridObject.AddComponent<HexGrid>();
                var occupied = new List<HexAxialCoord>
                {
                    new(2, 5),
                    new(2, 6),
                };

                Assert.That(HexLivingWallRules.MovementSegmentCrossesWall(
                    grid,
                    new HexAxialCoord(1, 5),
                    new HexAxialCoord(3, 5),
                    occupied), Is.True);
                Assert.That(HexLivingWallRules.MovementSegmentCrossesWall(
                    grid,
                    new HexAxialCoord(1, 6),
                    new HexAxialCoord(3, 5),
                    occupied), Is.True);
                Assert.That(HexLivingWallRules.MovementSegmentCrossesWall(
                    grid,
                    new HexAxialCoord(0, 5),
                    new HexAxialCoord(1, 5),
                    occupied), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void LivingWallReformRotation_IsPlayerCenteredAndReversible()
        {
            var player = new HexAxialCoord(5, 5);
            var first = new HexAxialCoord(2, 5);
            HexAxialCoord second = HexLivingWallRules.Rotate180(player, first);

            Assert.That(second, Is.EqualTo(new HexAxialCoord(8, 5)));
            Assert.That(HexAxialCoord.Distance(player, first), Is.EqualTo(3));
            Assert.That(HexAxialCoord.Distance(player, second), Is.EqualTo(3));
            Assert.That(HexLivingWallRules.Rotate180(player, second), Is.EqualTo(first));
        }

        [Test]
        public void LivingWallRuntimeState_CloneOwnsItsFootprintList()
        {
            var state = new HexBattleUnitState
            {
                livingWall = new HexLivingWallRuntimeState
                {
                    footprintOffsets = new List<HexAxialCoord> { new(0, 0), new(1, 0), new(-1, 0) },
                },
            };
            HexBattleUnitState clone = state.Clone();
            clone.livingWall.footprintOffsets.Add(new HexAxialCoord(0, 1));
            Assert.That(state.livingWall.footprintOffsets, Has.Count.EqualTo(3));
            Assert.That(clone.livingWall.footprintOffsets, Has.Count.EqualTo(4));
        }

        private static int GetCount(IReadOnlyDictionary<string, int> counts, string id) =>
            counts.TryGetValue(id, out int count) ? count : 0;

        private static Dictionary<string, int> CountById(IEnumerable<HexCardDefinition> cards) =>
            cards.GroupBy(card => card.id).ToDictionary(group => group.Key, group => group.Count());
    }
}
