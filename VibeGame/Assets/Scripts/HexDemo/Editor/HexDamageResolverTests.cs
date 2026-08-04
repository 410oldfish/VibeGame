using NUnit.Framework;
using UnityEngine;

namespace HexDemo.EditorTests
{
    public sealed class HexDamageResolverTests
    {
        [Test]
        public void CardBattleAmount_IsInstanceLocalAndResetsWithNewBattleInstance()
        {
            var definition = new HexCardDefinition { id = "test_growth", amount = 2 };
            var first = new HexCardInstance(definition);
            var second = new HexCardInstance(definition);

            first.IncreaseBattleAmount(6);

            Assert.That(first.EffectiveAmount, Is.EqualTo(8));
            Assert.That(second.EffectiveAmount, Is.EqualTo(2));
            Assert.That(definition.amount, Is.EqualTo(2));
            Assert.That(new HexCardInstance(definition).EffectiveAmount, Is.EqualTo(2));
        }

        [Test]
        public void Resolve_OverkillReportsActualHealthLoss()
        {
            HexBattleUnit target = CreateUnit("target", 2, 2);
            try
            {
                HexDamageResult result = HexDamageResolver.Resolve(
                    new HexDamageRequest(null, target, 10, HexDamageTags.Status));

                Assert.That(result.requestedDamage, Is.EqualTo(10));
                Assert.That(result.finalDamage, Is.EqualTo(10));
                Assert.That(result.healthLost, Is.EqualTo(2));
                Assert.That(result.armorLost, Is.Zero);
                Assert.That(result.killed, Is.True);
                Assert.That(target.State.currentHealth, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void Resolve_AttackAndNonAttackUseExplicitDefenseSemantics()
        {
            HexBattleUnit source = CreateUnit("source", 20, 20);
            HexBattleUnit attackTarget = CreateUnit("attackTarget", 20, 20);
            HexBattleUnit statusTarget = CreateUnit("statusTarget", 20, 20);
            try
            {
                attackTarget.State.deflect = 1;
                attackTarget.State.block = 3;
                statusTarget.State.deflect = 1;
                statusTarget.State.block = 3;

                HexDamageResult attack = HexDamageResolver.Resolve(
                    new HexDamageRequest(source, attackTarget, 8, HexDamageTags.Attack));
                HexDamageResult status = HexDamageResolver.Resolve(
                    new HexDamageRequest(source, statusTarget, 8, HexDamageTags.Status));

                Assert.That(attack.healthLost, Is.EqualTo(3));
                Assert.That(attackTarget.State.deflect, Is.Zero);
                Assert.That(status.healthLost, Is.EqualTo(8));
                Assert.That(statusTarget.State.deflect, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(attackTarget.gameObject);
                Object.DestroyImmediate(statusTarget.gameObject);
            }
        }

        [Test]
        public void AttackSnapshot_PreviewIsPureAndBatchConsumesOnceForEveryTarget()
        {
            HexBattleUnit source = CreateUnit("source", 30, 10);
            HexBattleUnit first = CreateUnit("first", 100, 100);
            HexBattleUnit second = CreateUnit("second", 100, 100);
            try
            {
                source.State.warriorNextAttackDamageBonus = 4;
                source.State.warriorFocusEffectDoubleThisCard = true;
                source.State.vigor = 2;
                source.State.momentum = 1;
                source.State.vampirism = 1;

                HexAttackModifierSnapshot snapshot = HexDamageResolver.CaptureAttackModifiers(source);
                int firstPreview = HexDamageResolver.PreviewModifiedDamage(snapshot, first, 5);
                int secondPreview = HexDamageResolver.PreviewModifiedDamage(snapshot, second, 5);

                Assert.That(firstPreview, Is.EqualTo(secondPreview));
                Assert.That(source.State.warriorNextAttackDamageBonus, Is.EqualTo(4));
                Assert.That(source.State.vigor, Is.EqualTo(2));
                Assert.That(source.State.momentum, Is.EqualTo(1));

                HexDamageResolver.ConsumeAttackModifiers(source, snapshot);
                HexDamageResult firstResult = HexDamageResolver.Resolve(
                    new HexDamageRequest(source, first, 5, HexDamageTags.Attack, snapshot));
                HexDamageResult secondResult = HexDamageResolver.Resolve(
                    new HexDamageRequest(source, second, 5, HexDamageTags.Attack, snapshot));
                int batchHealthLost = firstResult.healthLost + secondResult.healthLost;
                HexDamageResolver.CompleteAttackBatch(source, batchHealthLost);

                Assert.That(firstResult.healthLost, Is.EqualTo(secondResult.healthLost));
                Assert.That(source.State.warriorNextAttackDamageBonus, Is.Zero);
                Assert.That(source.State.warriorFocusEffectDoubleThisCard, Is.False);
                Assert.That(source.State.vigor, Is.Zero);
                Assert.That(source.State.momentum, Is.Zero);
                Assert.That(source.State.damageDealtThisTurn, Is.EqualTo(batchHealthLost));
                Assert.That(source.State.currentHealth, Is.EqualTo(source.State.maxHealth));
                Assert.That(source.State.vampirism, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void Heal_DoesNotReviveDefeatedUnit()
        {
            HexBattleUnit unit = CreateUnit("unit", 5, 5);
            try
            {
                HexDamageResolver.Resolve(new HexDamageRequest(null, unit, 5, HexDamageTags.Environment));
                unit.Heal(5);
                Assert.That(unit.State.currentHealth, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(unit.gameObject);
            }
        }

        private static HexBattleUnit CreateUnit(string name, int maxHealth, int currentHealth)
        {
            var gameObject = new GameObject(name);
            var unit = gameObject.AddComponent<HexBattleUnit>();
            unit.Initialize(
                new HexBattleUnitState
                {
                    id = name,
                    displayName = name,
                    maxHealth = maxHealth,
                    currentHealth = currentHealth,
                    faction = HexBattleFaction.Player,
                },
                null,
                System.Array.Empty<HexCardDefinition>());
            return unit;
        }
    }
}
