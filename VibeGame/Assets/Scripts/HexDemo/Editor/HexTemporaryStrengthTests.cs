using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HexDemo.EditorTests
{
    public sealed class HexTemporaryStrengthTests
    {
        private GameObject _host;
        private HexBattleUnit _unit;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("TemporaryStrengthTestUnit");
            _unit = _host.AddComponent<HexBattleUnit>();
            _unit.Initialize(
                new HexBattleUnitState
                {
                    id = "temporary_strength_test",
                    displayName = "Temporary Strength Test",
                    faction = HexBattleFaction.Player,
                    maxHealth = 30,
                    currentHealth = 30,
                    drawPerTurn = 0,
                    maxEnergy = 3,
                    maxMovePoints = 3,
                    profession = HexCardProfession.Warrior,
                },
                null,
                Array.Empty<HexCardDefinition>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void TemporaryStrength_LifetimesClearIndependentlyAndKeepPermanentStrength()
        {
            _unit.GainStrength(3);
            Assert.That(_unit.GainTemporaryStrength(2, HexTemporaryStrengthDuration.UntilEndOfTurn), Is.EqualTo(2));
            Assert.That(_unit.GainTemporaryStrength(4, HexTemporaryStrengthDuration.UntilEndOfBattle), Is.EqualTo(4));

            Assert.That(_unit.State.strength, Is.EqualTo(9));
            Assert.That(_unit.State.temporaryStrengthUntilEndOfTurn, Is.EqualTo(2));
            Assert.That(_unit.State.temporaryStrengthUntilEndOfBattle, Is.EqualTo(4));

            _unit.EndTurn();

            Assert.That(_unit.State.strength, Is.EqualTo(7));
            Assert.That(_unit.State.temporaryStrengthUntilEndOfTurn, Is.Zero);
            Assert.That(_unit.State.temporaryStrengthUntilEndOfBattle, Is.EqualTo(4));

            _unit.ClearAllTemporaryStrength();

            Assert.That(_unit.State.strength, Is.EqualTo(3));
            Assert.That(_unit.State.temporaryStrengthUntilEndOfBattle, Is.Zero);
        }

        [Test]
        public void TemporaryStrength_RejectedGainDoesNotCreateCleanupDebt()
        {
            _unit.GainStrength(5);
            _unit.State.curse = 1;

            int applied = _unit.GainTemporaryStrength(3, HexTemporaryStrengthDuration.UntilEndOfTurn);

            Assert.That(applied, Is.Zero);
            Assert.That(_unit.State.strength, Is.EqualTo(5));
            Assert.That(_unit.State.temporaryStrengthUntilEndOfTurn, Is.Zero);

            _unit.EndTurn();
            Assert.That(_unit.State.strength, Is.EqualTo(5));
        }

        [Test]
        public void TemporaryStrength_TriggersStrengthGainLinkExactlyOnce()
        {
            _unit.State.gainMoveOnStrengthOrToughness = true;
            _unit.State.currentMovePoints = 1;

            _unit.GainTemporaryStrength(3, HexTemporaryStrengthDuration.UntilEndOfTurn);

            Assert.That(_unit.State.currentMovePoints, Is.EqualTo(4));
        }

        [Test]
        public void StatusDisplay_SplitsPermanentAndTemporaryStrengthWithExpiryDetails()
        {
            _unit.GainStrength(3);
            _unit.GainTemporaryStrength(2, HexTemporaryStrengthDuration.UntilEndOfTurn);
            _unit.GainTemporaryStrength(4, HexTemporaryStrengthDuration.UntilEndOfBattle);

            var entries = HexBattleStatusDisplay.BuildMvpStatusEntries(_unit.State);
            BattleStatusEntry permanent = entries.Single(entry => entry.displayName == "力量");
            BattleStatusEntry temporary = entries.Single(entry => entry.displayName == "临时力量");

            Assert.That(permanent.stacks, Is.EqualTo(3));
            Assert.That(temporary.stacks, Is.EqualTo(6));
            Assert.That(temporary.tooltip, Does.Contain("回合末移除 2"));
            Assert.That(temporary.tooltip, Does.Contain("战斗末移除 4"));
        }

        [Test]
        public void ResetBattleState_ClearsEveryStrengthBucket()
        {
            _unit.GainStrength(3);
            _unit.GainTemporaryStrength(2, HexTemporaryStrengthDuration.UntilEndOfTurn);
            _unit.GainTemporaryStrength(4, HexTemporaryStrengthDuration.UntilEndOfBattle);

            _unit.ResetBattleState();

            Assert.That(_unit.State.strength, Is.Zero);
            Assert.That(_unit.State.temporaryStrengthUntilEndOfTurn, Is.Zero);
            Assert.That(_unit.State.temporaryStrengthUntilEndOfBattle, Is.Zero);
        }
    }
}
