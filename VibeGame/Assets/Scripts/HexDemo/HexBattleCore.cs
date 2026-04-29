using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public enum HexBattleFaction
    {
        Player = 0,
        Enemy = 1,
    }

    public enum HexCardTargetType
    {
        Self = 0,
        EnemyUnit = 1,
    }

    public enum HexCardEffectType
    {
        Attack = 0,
        Defend = 1,
    }

    [Serializable]
    public sealed class HexCardDefinition
    {
        public string id;
        public string displayName;
        public HexCardEffectType effectType;
        public HexCardTargetType targetType;
        public int energyCost;
        public int amount;
        public int range;
        public int priority;
        public Color color;
    }

    [Serializable]
    public sealed class HexCardInstance
    {
        public string runtimeId;
        public HexCardDefinition definition;

        public HexCardInstance(HexCardDefinition definition)
        {
            runtimeId = Guid.NewGuid().ToString("N");
            this.definition = definition;
        }
    }

    [Serializable]
    public sealed class HexDeckState
    {
        private readonly List<HexCardInstance> _drawPile = new();
        private readonly List<HexCardInstance> _discardPile = new();
        private readonly List<HexCardInstance> _hand = new();

        public IReadOnlyList<HexCardInstance> DrawPile => _drawPile;
        public IReadOnlyList<HexCardInstance> DiscardPile => _discardPile;
        public IReadOnlyList<HexCardInstance> Hand => _hand;

        public void LoadStartingDeck(IEnumerable<HexCardDefinition> cardDefinitions)
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _hand.Clear();

            foreach (var definition in cardDefinitions)
                _drawPile.Add(new HexCardInstance(definition));

            Shuffle(_drawPile);
        }

        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                RefillDrawPileIfNeeded();
                if (_drawPile.Count == 0)
                    return;

                var nextCard = _drawPile[^1];
                _drawPile.RemoveAt(_drawPile.Count - 1);
                _hand.Add(nextCard);
            }
        }

        public void DiscardFromHand(HexCardInstance card)
        {
            if (card == null)
                return;

            if (_hand.Remove(card))
                _discardPile.Add(card);
        }

        public void DiscardHand()
        {
            for (int i = _hand.Count - 1; i >= 0; i--)
                _discardPile.Add(_hand[i]);

            _hand.Clear();
        }

        private void RefillDrawPileIfNeeded()
        {
            if (_drawPile.Count > 0 || _discardPile.Count == 0)
                return;

            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_drawPile);
        }

        private static void Shuffle(List<HexCardInstance> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
            }
        }
    }

    [Serializable]
    public sealed class HexBattleUnitState
    {
        public string id;
        public string displayName;
        public HexBattleFaction faction;
        public int maxHealth;
        public int currentHealth;
        public int armor;
        public int energy;
        public int drawPerTurn;
        public int maxEnergy;
        public int maxMovePoints;
        public int currentMovePoints;
        public int attackRange;
        public HexAxialCoord coord;

        public HexBattleUnitState Clone()
        {
            return (HexBattleUnitState)MemberwiseClone();
        }
    }

    public static class HexCardLibrary
    {
        private static readonly HexCardDefinition Attack = new()
        {
            id = "attack_strike",
            displayName = "Attack",
            effectType = HexCardEffectType.Attack,
            targetType = HexCardTargetType.EnemyUnit,
            energyCost = 1,
            amount = 6,
            range = 1,
            priority = 1,
            color = new Color(0.77f, 0.3f, 0.25f, 1f),
        };

        private static readonly HexCardDefinition Defend = new()
        {
            id = "defend_guard",
            displayName = "Defend",
            effectType = HexCardEffectType.Defend,
            targetType = HexCardTargetType.Self,
            energyCost = 1,
            amount = 5,
            range = 0,
            priority = 2,
            color = new Color(0.27f, 0.52f, 0.82f, 1f),
        };

        public static HexCardDefinition GetAttack() => Attack;
        public static HexCardDefinition GetDefend() => Defend;

        public static List<HexCardDefinition> CreateStarterDeck()
        {
            return new List<HexCardDefinition>
            {
                Attack, Attack, Attack, Attack,
                Defend, Defend, Defend, Defend,
            };
        }
    }
}
