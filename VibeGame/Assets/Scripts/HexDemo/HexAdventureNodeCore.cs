using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    [Serializable]
    public sealed class HexShopOffer
    {
        public HexCardDefinition card;
        public int price;
        public bool sold;
    }

    /// <summary>
    /// Run-state rules for a single shop visit. Presentation code only asks this
    /// object whether a purchase is legal and never mutates gold/deck directly.
    /// </summary>
    public sealed class HexShopSession
    {
        public const int CardRemovalPrice = 50;

        public readonly List<HexShopOffer> offers = new();
        public bool cardRemovalUsed { get; private set; }

        public static HexShopSession Create(HexCardProfession profession, int offerCount = 8)
        {
            var session = new HexShopSession();
            var cards = HexCardLibrary.GetRewardChoices(Mathf.Max(0, offerCount), profession);
            for (int i = 0; i < cards.Count; i++)
            {
                session.offers.Add(new HexShopOffer
                {
                    card = cards[i],
                    price = GetCardPrice(cards[i]),
                });
            }

            return session;
        }

        public bool TryBuyCard(HexRunState runState, int offerIndex, out string message)
        {
            message = string.Empty;
            if (runState == null || offerIndex < 0 || offerIndex >= offers.Count)
                return false;

            var offer = offers[offerIndex];
            if (offer == null || offer.card == null || offer.sold)
            {
                message = "This card is no longer available.";
                return false;
            }

            if (runState.gold < offer.price)
            {
                message = "Not enough gold.";
                return false;
            }

            runState.gold -= offer.price;
            runState.deckDefinitions.Add(offer.card);
            offer.sold = true;
            message = $"Added {offer.card.displayName} to the deck.";
            return true;
        }

        public bool TryRemoveCard(HexRunState runState, HexCardDefinition card, out string message)
        {
            message = string.Empty;
            if (runState == null || card == null || cardRemovalUsed)
                return false;
            if (runState.deckDefinitions.Count <= 1 || !runState.deckDefinitions.Contains(card))
            {
                message = "The selected card cannot be removed.";
                return false;
            }
            if (runState.gold < CardRemovalPrice)
            {
                message = "Not enough gold.";
                return false;
            }

            runState.gold -= CardRemovalPrice;
            runState.deckDefinitions.Remove(card);
            cardRemovalUsed = true;
            message = $"Removed {card.displayName} from the deck.";
            return true;
        }

        private static int GetCardPrice(HexCardDefinition card)
        {
            int rarityPrice = card?.rarity switch
            {
                "Rare" => 75,
                "Uncommon" => 52,
                _ => 32,
            };
            int energyAdjustment = card == null ? 0 : Mathf.Clamp(card.energyCost, 0, 3) * 3;
            return rarityPrice + energyAdjustment + UnityEngine.Random.Range(-4, 5);
        }
    }

    public enum HexAdventureEventEffect
    {
        None = 0,
        Gold = 1,
        Heal = 2,
        Damage = 3,
        MaxHealth = 4,
        RandomCard = 5,
    }

    [Serializable]
    public sealed class HexAdventureEventChoice
    {
        public string id;
        public string label;
        public int goldCost;
        public int minimumHealth;
        public HexAdventureEventEffect primaryEffect;
        public int primaryAmount;
        public HexAdventureEventEffect secondaryEffect;
        public int secondaryAmount;

        public bool CanChoose(HexRunState runState)
        {
            return runState != null && runState.gold >= goldCost && runState.currentHealth > minimumHealth;
        }
    }

    /// <summary>One generated event encounter and its one-shot resolution rules.</summary>
    public sealed class HexAdventureEventSession
    {
        public string id;
        public string title;
        public string description;
        public readonly List<HexAdventureEventChoice> choices = new();
        public bool resolved { get; private set; }

        public static HexAdventureEventSession CreateRandom(HexRunState runState)
        {
            int roll = UnityEngine.Random.Range(0, 3);
            return roll switch
            {
                0 => CreateForgottenShrine(runState),
                1 => CreateWanderingHealer(runState),
                _ => CreateAbandonedWagon(runState),
            };
        }

        public bool TryResolve(string choiceId, HexRunState runState, out string result)
        {
            result = string.Empty;
            if (resolved || runState == null)
                return false;

            var choice = choices.Find(item => item.id == choiceId);
            if (choice == null || !choice.CanChoose(runState))
            {
                result = "That choice is not currently available.";
                return false;
            }

            runState.gold -= choice.goldCost;
            var changes = new List<string>();
            ApplyEffect(runState, choice.primaryEffect, choice.primaryAmount, changes);
            ApplyEffect(runState, choice.secondaryEffect, choice.secondaryAmount, changes);
            resolved = true;
            result = changes.Count > 0 ? string.Join(" ", changes) : "You leave without incident.";
            return true;
        }

        private static HexAdventureEventSession CreateForgottenShrine(HexRunState runState)
        {
            int bloodCost = Mathf.Max(5, Mathf.CeilToInt(runState.maxHealth * 0.12f));
            var session = NewSession(
                "forgotten_shrine",
                "Forgotten Shrine",
                "A weathered altar hums beneath a cracked stone mask. Its bowl is dry, but warm to the touch.");
            session.choices.Add(Choice("blood", $"Offer {bloodCost} HP — gain 55 Gold", 0, bloodCost, HexAdventureEventEffect.Damage, bloodCost, HexAdventureEventEffect.Gold, 55));
            session.choices.Add(Choice("study", "Study the runes — gain a random card", 0, 0, HexAdventureEventEffect.RandomCard, 1));
            session.choices.Add(Choice("leave", "Leave the shrine"));
            return session;
        }

        private static HexAdventureEventSession CreateWanderingHealer(HexRunState runState)
        {
            int healAmount = Mathf.Max(8, Mathf.CeilToInt(runState.maxHealth * 0.3f));
            var session = NewSession(
                "wandering_healer",
                "Wandering Healer",
                "A masked traveler offers tinctures from a case of softly glowing bottles.");
            session.choices.Add(Choice("treatment", $"Pay 25 Gold — recover {healAmount} HP", 25, 0, HexAdventureEventEffect.Heal, healAmount));
            session.choices.Add(Choice("tonic", "Pay 45 Gold — gain 5 Max HP and heal 5 HP", 45, 0, HexAdventureEventEffect.MaxHealth, 5, HexAdventureEventEffect.Heal, 5));
            session.choices.Add(Choice("leave", "Decline the offer"));
            return session;
        }

        private static HexAdventureEventSession CreateAbandonedWagon(HexRunState runState)
        {
            int trapDamage = Mathf.Max(4, Mathf.CeilToInt(runState.maxHealth * 0.1f));
            var session = NewSession(
                "abandoned_wagon",
                "Abandoned Wagon",
                "An overturned merchant wagon blocks the road. A locked coffer lies beneath splintered boards.");
            session.choices.Add(Choice("coffer", $"Force the coffer — lose {trapDamage} HP, gain 45 Gold", 0, trapDamage, HexAdventureEventEffect.Damage, trapDamage, HexAdventureEventEffect.Gold, 45));
            session.choices.Add(Choice("supplies", "Search the supplies — gain a random card", 0, 0, HexAdventureEventEffect.RandomCard, 1));
            session.choices.Add(Choice("leave", "Keep moving"));
            return session;
        }

        private static HexAdventureEventSession NewSession(string id, string title, string description)
        {
            return new HexAdventureEventSession { id = id, title = title, description = description };
        }

        private static HexAdventureEventChoice Choice(
            string id,
            string label,
            int goldCost = 0,
            int minimumHealth = 0,
            HexAdventureEventEffect primaryEffect = HexAdventureEventEffect.None,
            int primaryAmount = 0,
            HexAdventureEventEffect secondaryEffect = HexAdventureEventEffect.None,
            int secondaryAmount = 0)
        {
            return new HexAdventureEventChoice
            {
                id = id,
                label = label,
                goldCost = goldCost,
                minimumHealth = minimumHealth,
                primaryEffect = primaryEffect,
                primaryAmount = primaryAmount,
                secondaryEffect = secondaryEffect,
                secondaryAmount = secondaryAmount,
            };
        }

        private static void ApplyEffect(HexRunState runState, HexAdventureEventEffect effect, int amount, List<string> changes)
        {
            switch (effect)
            {
                case HexAdventureEventEffect.Gold:
                    runState.gold = Mathf.Max(0, runState.gold + amount);
                    changes.Add($"Gold {Signed(amount)}.");
                    break;
                case HexAdventureEventEffect.Heal:
                {
                    int before = runState.currentHealth;
                    runState.currentHealth = Mathf.Min(runState.maxHealth, runState.currentHealth + Mathf.Max(0, amount));
                    changes.Add($"Recovered {runState.currentHealth - before} HP.");
                    break;
                }
                case HexAdventureEventEffect.Damage:
                    runState.currentHealth = Mathf.Max(1, runState.currentHealth - Mathf.Max(0, amount));
                    changes.Add($"Lost {amount} HP.");
                    break;
                case HexAdventureEventEffect.MaxHealth:
                    runState.maxHealth = Mathf.Max(1, runState.maxHealth + amount);
                    changes.Add($"Max HP {Signed(amount)}.");
                    break;
                case HexAdventureEventEffect.RandomCard:
                {
                    var card = HexCardLibrary.GetRandomRewardCard(runState.profession);
                    if (card != null)
                    {
                        runState.deckDefinitions.Add(card);
                        changes.Add($"Gained {card.displayName}.");
                    }
                    break;
                }
            }
        }

        private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
    }
}
