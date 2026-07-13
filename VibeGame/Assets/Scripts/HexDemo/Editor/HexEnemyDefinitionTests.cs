using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace HexDemo.EditorTests
{
    public sealed class HexEnemyDefinitionTests
    {
        [Test]
        public void BuiltInEnemyIds_AreUniqueAndResolvable()
        {
            var ids = HexCardLibrary.GetBuiltInEnemyIds();
            Assert.That(ids, Has.Count.EqualTo(11));
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
            Assert.That(HexCardLibrary.GetEnemyDefinition("not_a_real_enemy"), Is.Null);
            Assert.That(HexCardLibrary.TryGetEnemyDefinition("not_a_real_enemy", out _), Is.False);
        }

        [TestCase("goblin", 9, 2)]
        [TestCase("spear_goblin", 11, 2)]
        [TestCase("goblin_captain", 14, 3)]
        [TestCase("tribal_chieftain", 13, 4)]
        public void MvpEnemyDeckSnapshots_MatchDesign(string id, int deckCount, int slotCount)
        {
            var definition = HexCardLibrary.GetEnemyDefinition(id);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.deckDefinitions, Has.Count.EqualTo(deckCount));
            Assert.That(definition.intentSlots, Has.Count.EqualTo(slotCount));
            Assert.That(definition.bottomCard, Is.Not.Null);
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

        private static int GetCount(IReadOnlyDictionary<string, int> counts, string id) =>
            counts.TryGetValue(id, out int count) ? count : 0;

        private static Dictionary<string, int> CountById(IEnumerable<HexCardDefinition> cards) =>
            cards.GroupBy(card => card.id).ToDictionary(group => group.Key, group => group.Count());
    }
}
