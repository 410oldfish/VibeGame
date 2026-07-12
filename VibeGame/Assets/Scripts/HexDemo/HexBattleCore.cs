using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        Direction = 2,
        Tile = 3,
    }

    public enum HexCardType
    {
        Attack = 0,
        Skill = 1,
        Power = 2,
        Status = 3,
        Curse = 4,
        Action = 5,
        Special = 6,
    }

    public enum HexCardProfession
    {
        Common = 0,
        Warrior = 1,
        Monster = 2,
        Druid = 3,
        Paladin = 4,
    }

    public enum HexDruidFormType
    {
        None = 0,
        Mammoth = 1,
        Toad = 2,
        LavaLizard = 3,
        Rafflesia = 4,
    }

    public enum HexWeaponType
    {
        None = 0,
        Sword = 1,
        Axe = 2,
        Hammer = 3,
    }

    public enum HexCardEffectType
    {
        Attack = 0,
        Defend = 1,
        MoveToward = 2,
        Move = 3,
        MoveAway = 4,
        AddFear = 5,
        PlaceRuin = 6,
        DestroyBarrier = 7,
        /// <summary>Obsolete alias for DestroyBarrier.</summary>
        DestroyHighGround = DestroyBarrier,
        None = 8,
    }

    public enum HexEnemyEncounterType
    {
        Normal = 0,
        Elite = 1,
        Boss = 2,
    }

    public enum HexEnemyIntentPattern
    {
        Fixed = 0,
        ApproachStrike = 1,
        Ranged = 2,
        Stationary = 3,
    }

    public enum HexEnemyIntentSlotKind
    {
        Move = 0,
        Attack = 1,
        Free = 2,
    }

    public enum HexTerrainZoneType
    {
        Normal = 0,
        Pit = 1,
    }

    /// <summary>Obsolete. Prefer HexTerrainZoneType. Ground==Normal.</summary>
    public enum HexTerrainBaseType
    {
        Ground = 0,
        Pit = 1,
        Normal = 0,
    }

    public enum HexTerrainStructureType
    {
        None = 0,
        Barrier = 1,
        Ruin = 2,
        /// <summary>Obsolete alias for Barrier.</summary>
        HighGround = Barrier,
    }

    public enum HexTerrainPickupType
    {
        None = 0,
        Heal = 1,
        TemporaryStrength = 2,
        TemporaryCard = 3,
    }

    public enum HexCardKeywordType
    {
        Knockback = 0,
        Bleed = 1,
        Vulnerable = 2,
        Stun = 3,
        Retain = 4,
        Exhaust = 5,
        Burn = 6,
        Entangle = 7,
        Void = 8,
        Weak = 9,
        Phase = 10,
        Extend = 11,
        Pull = 12,
    }

    public enum HexMapNodeType
    {
        Start = 0,
        SmallBattle = 1,
        EliteBattle = 2,
        Event = 3,
        Shop = 4,
        Rest = 5,
        Boss = 6,
    }

    [Serializable]
    public sealed class HexCardDefinition
    {
        public string id;
        public string displayName;
        public HexCardType cardType;
        public HexCardProfession profession;
        public HexCardEffectType effectType;
        public HexCardTargetType targetType;
        public int energyCost;
        public int amount;
        public int range;
        public int castRange;
        public int effectRadius;
        public int priority;
        public string rarity;
        public string description;
        public Color color;
        public bool isUnplayable;
        public bool upgraded;
        public string[] tags;
    }

    [Serializable]
    public sealed class HexEnemyDefinition
    {
        public string id;
        public string displayName;
        public HexEnemyEncounterType encounterType;
        public HexEnemyIntentPattern intentPattern;
        public int attackMinRange = 1;
        public int attackMaxRange = 1;
        public int emptyDrawPileStrengthGain;
        public List<HexEnemyIntentSlotKind> intentSlots = new();
        public List<HexCardDefinition> deckDefinitions = new();
        public HexCardDefinition bottomCard;
    }

    [Serializable]
    public sealed class HexEnemyIntentSlot
    {
        public HexEnemyIntentSlotKind slotKind;
        public HexCardInstance card;
    }

    [Serializable]
    public sealed class HexCardKeywordEffect
    {
        public HexCardKeywordType keywordType;
        public int amount;
    }

    [Serializable]
    internal sealed class HexCardExportFile
    {
        public List<HexCardExportData> cards;
    }

    [Serializable]
    internal sealed class HexCardExportData
    {
        public string card_id;
        public string name;
        public string cost;
        public string profession;
        public string rarity;
        public string card_type;
        public string target_type;
        public int cast_range;
        public int effect_radius;
        public int attack_range;
        public string description;
        public bool is_directional;
    }

    /// <summary>
    /// 卡牌运行时临时词缀层：不污染定义数据，区分“回合内”与“单次动作内”两种生命周期。
    /// </summary>
    public sealed class CardRuntimeFlagLayer
    {
        private readonly HashSet<string> _roundFlags = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _actionFlags = new(StringComparer.OrdinalIgnoreCase);

        public bool Has(string flagId)
        {
            return !string.IsNullOrWhiteSpace(flagId) && (_roundFlags.Contains(flagId) || _actionFlags.Contains(flagId));
        }

        public void AddRoundFlag(string flagId)
        {
            if (!string.IsNullOrWhiteSpace(flagId))
                _roundFlags.Add(flagId);
        }

        public void AddActionFlag(string flagId)
        {
            if (!string.IsNullOrWhiteSpace(flagId))
                _actionFlags.Add(flagId);
        }

        public void Remove(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
                return;

            _roundFlags.Remove(flagId);
            _actionFlags.Remove(flagId);
        }

        public void ResetRoundFlags() => _roundFlags.Clear();

        public void ResetActionFlags() => _actionFlags.Clear();
    }

    [Serializable]
    public sealed class HexCardInstance
    {
        public string runtimeId;
        public HexCardDefinition definition;
        public bool upgraded;
        public int temporaryCostModifier;
        public bool costsNoEnergyThisTurn;
        public bool costsNoEnergyThisBattle;
        public bool exhaustWhenPlayed;

        private readonly CardRuntimeFlagLayer _runtimeFlags = new();

        public HexCardInstance(HexCardDefinition definition)
        {
            runtimeId = Guid.NewGuid().ToString("N");
            this.definition = definition;
            upgraded = definition != null && definition.upgraded;
        }

        public bool HasRuntimeFlag(string flagId) => _runtimeFlags.Has(flagId);

        /// <summary>添加临时词缀。roundScope 为 true 表示回合结束清理，否则动作结束清理。</summary>
        public void AddTempFlag(string flagId, bool roundScope = true)
        {
            if (roundScope)
                _runtimeFlags.AddRoundFlag(flagId);
            else
                _runtimeFlags.AddActionFlag(flagId);
        }

        public void RemoveTempFlag(string flagId) => _runtimeFlags.Remove(flagId);

        public void ResetRoundFlags() => _runtimeFlags.ResetRoundFlags();

        public void ResetActionFlags() => _runtimeFlags.ResetActionFlags();
    }

    [Serializable]
    public sealed class HexDeckState
    {
        private readonly List<HexCardInstance> _drawPile = new();
        private readonly List<HexCardInstance> _discardPile = new();
        private readonly List<HexCardInstance> _hand = new();
        private readonly List<HexCardInstance> _exhaustPile = new();

        public IReadOnlyList<HexCardInstance> DrawPile => _drawPile;
        public IReadOnlyList<HexCardInstance> DiscardPile => _discardPile;
        public IReadOnlyList<HexCardInstance> Hand => _hand;
        public IReadOnlyList<HexCardInstance> ExhaustPile => _exhaustPile;

        public void LoadStartingDeck(IEnumerable<HexCardDefinition> cardDefinitions)
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _hand.Clear();
            _exhaustPile.Clear();

            foreach (var definition in cardDefinitions)
                _drawPile.Add(new HexCardInstance(definition));

            Shuffle(_drawPile);
        }

        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var nextCard = DrawCard(out _);
                if (nextCard == null)
                    return;
            }
        }

        public HexCardInstance DrawCard(out bool emptiedDrawPile)
        {
            emptiedDrawPile = false;
            RefillDrawPileIfNeeded();
            if (_drawPile.Count == 0)
                return null;

            var nextCard = _drawPile[^1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            _hand.Add(nextCard);
            emptiedDrawPile = _drawPile.Count == 0;
            return nextCard;
        }

        public HexCardInstance DrawFirstMatchingToHand(Predicate<HexCardDefinition> predicate, out bool emptiedDrawPile)
        {
            emptiedDrawPile = false;
            RefillDrawPileIfNeeded();
            if (_drawPile.Count == 0)
                return null;

            for (int i = _drawPile.Count - 1; i >= 0; i--)
            {
                var candidate = _drawPile[i];
                if (candidate?.definition == null || predicate == null || !predicate(candidate.definition))
                    continue;

                _drawPile.RemoveAt(i);
                _hand.Add(candidate);
                emptiedDrawPile = _drawPile.Count == 0;
                return candidate;
            }

            return null;
        }

        public HexCardInstance DrawRandomToHand(out bool emptiedDrawPile)
        {
            emptiedDrawPile = false;
            RefillDrawPileIfNeeded();
            if (_drawPile.Count == 0)
                return null;

            int index = UnityEngine.Random.Range(0, _drawPile.Count);
            var card = _drawPile[index];
            _drawPile.RemoveAt(index);
            _hand.Add(card);
            emptiedDrawPile = _drawPile.Count == 0;
            return card;
        }

        public void DiscardFromHand(HexCardInstance card, bool exhaust = false)
        {
            if (card == null)
                return;

            if (_hand.Remove(card))
            {
                if (exhaust)
                    _exhaustPile.Add(card);
                else
                    _discardPile.Add(card);
            }
        }

        public void DiscardHand(Func<HexCardInstance, bool> shouldRetain = null, Func<HexCardInstance, bool> shouldExhaust = null)
        {
            for (int i = _hand.Count - 1; i >= 0; i--)
            {
                var card = _hand[i];
                if (shouldRetain != null && shouldRetain(card))
                    continue;

                if (shouldExhaust != null && shouldExhaust(card))
                    _exhaustPile.Add(card);
                else
                    _discardPile.Add(card);

                _hand.RemoveAt(i);
            }
        }

        public void AddToDrawPile(HexCardDefinition definition, bool shuffle = true)
        {
            if (definition == null)
                return;

            _drawPile.Add(new HexCardInstance(definition));
            if (shuffle)
                Shuffle(_drawPile);
        }

        public void AddToDiscardPile(HexCardDefinition definition)
        {
            if (definition == null)
                return;

            _discardPile.Add(new HexCardInstance(definition));
        }

        public void AddToHand(HexCardDefinition definition)
        {
            if (definition == null)
                return;

            _hand.Add(new HexCardInstance(definition));
        }

        public void AddToExhaustPile(HexCardDefinition definition)
        {
            if (definition == null)
                return;

            _exhaustPile.Add(new HexCardInstance(definition));
        }

        public HexCardInstance TakeFromExhaustPileToHand(int index = 0)
        {
            if (index < 0 || index >= _exhaustPile.Count)
                return null;

            var card = _exhaustPile[index];
            _exhaustPile.RemoveAt(index);
            _hand.Add(card);
            return card;
        }

        public void AddCardInstanceToHand(HexCardInstance card)
        {
            if (card == null)
                return;

            _hand.Add(card);
        }

        public bool TryRemoveFromDrawOrDiscard(Predicate<HexCardDefinition> predicate, out HexCardInstance card)
        {
            card = null;
            if (predicate == null)
                return false;

            for (int i = _drawPile.Count - 1; i >= 0; i--)
            {
                var candidate = _drawPile[i];
                if (candidate?.definition == null || !predicate(candidate.definition))
                    continue;

                _drawPile.RemoveAt(i);
                card = candidate;
                return true;
            }

            for (int i = _discardPile.Count - 1; i >= 0; i--)
            {
                var candidate = _discardPile[i];
                if (candidate?.definition == null || !predicate(candidate.definition))
                    continue;

                _discardPile.RemoveAt(i);
                card = candidate;
                return true;
            }

            return false;
        }

        public void ClearBattleState()
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _hand.Clear();
            _exhaustPile.Clear();
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
        public int bleed;
        public int poison;
        public int vulnerable;
        public int weak;
        public int stun;
        public int blind;
        public int nausea;
        public int curse;
        public int allure;
        public bool hasAllureSource;
        public HexAxialCoord allureSourceCoord;
        public int taunt;
        public int tauntActiveThisTurn;
        public bool hasTauntSource;
        public HexAxialCoord tauntSourceCoord;
        public int confusion;
        public int bind;
        public int burn;
        public int entangle;
        public int armorBreak;
        public int brittle;
        public int disarm;
        public int cold;
        public int fatigue;
        public int paralysis;
        public int paralysisActiveThisTurn;
        public int slow;
        public int frozen;
        public int strength;
        public int toughness;
        public int agility;
        public int wisdom;
        public int humility;
        public int luck;
        public int vigor;
        public int vampirism;
        public int regeneration;
        public int holyShield;
        public int immunity;
        public int invincible;
        public int deflect;
        public int block;
        public int thorns;
        public int skillCooldown;
        public int nextAttackDrawCards;
        public int nextAttackApplyVulnerable;
        public int energy;
        public int drawPerTurn;
        public int maxEnergy;
        public int maxMovePoints;
        public int currentMovePoints;
        public int attackRange;
        public int emptyDrawPileStrengthGain;
        public string enemyDefinitionId;
        public int enemyAttackMinRange = 1;
        public int enemyAttackMaxRange = 1;
        public HexWeaponType weapon;
        public bool drawDisabledThisTurn;
        public int attackRepeatBonusThisTurn;
        public int damageDealtThisTurn;
        public int armorOnAttackCardThisTurn;
        public int armorOnSkillCard;
        public int firstAttackBurnAmount;
        public int consumableAttackBurnBonus;
        public int consumableCoffeeTurns;
        public int consumableCoffeeAmount;
        public int consumableWisdomTurns;
        public int consumableWisdomAmount;
        public int consumableEggTartTurns;
        public int flyingSecretTurns;
        public int stealSecretTurns;
        public int consumableTempStrength;
        public int consumableTempToughness;
        public bool firstAttackBonusPending;
        public bool weaponSkillFree;
        public int extraEnergyPerTurn;
        public int extraMovePerTurn;
        public bool cannotUseSkills;
        public bool weaponPassivesDoubleThisTurn;
        public bool consumeWeaponAtEndTurn;
        public bool allWeaponsEquipped;
        public bool negateNextEnemyAttack;
        public bool liquidArmorToVigor;
        public int burningAuraRadius;
        public bool gainStrengthOnSelfDamage;
        public bool drawOnExhaust;
        public bool gainMoveOnStrengthOrToughness;
        public int armorOnExhaustCost;
        public bool retainArmorBetweenTurns;
        public bool warriorBurnEventThisTurn;
        public bool warriorFearEventThisTurn;
        public bool warriorBleedEventThisTurn;
        public bool warriorMoveEventThisTurn;
        public bool warriorExhaustEventThisTurn;
        public bool warriorFocusEventThisTurn;
        public bool warriorBurnFinisherUsedThisTurn;
        public bool warriorFearFinisherUsedThisTurn;
        public bool warriorBleedFinisherUsedThisTurn;
        public bool warriorMoveFinisherUsedThisTurn;
        public bool warriorExhaustFinisherUsedThisTurn;
        public bool warriorFocusFinisherUsedThisTurn;
        public int warriorBleedEventsThisBattle;
        public int warriorBleedEventsThisTurn;
        public int warriorStrengthPerTurn;
        public bool warriorBloodPactActive;
        public int warriorNextAttackDamageBonus;
        public int warriorNextAttackDamageBonusQueued;
        public bool warriorFocusEffectDoubleThisCard;
        public int warriorPendingEnergyNextTurn;
        public bool warriorPreparedBlade;
        public bool warriorScorchedEarthActive;
        public bool warriorSkirmishArmorOnMove;
        public int warriorBloodForgedBonus;
        public int warriorDelayedBleed;
        public int warriorDamageMultiplierThisTurn;
        public bool warriorInfernoHeart;
        public bool warriorDrawOnFearAdded;
        public bool warriorExtraFearFirstEachTurn;
        public bool warriorExtraFearUsedThisTurn;
        public bool warriorGainStrengthOnFearPlayed;
        public bool warriorArmorOnFearAdded;
        public bool warriorHealOnBleedGain;
        public bool warriorWindstepReady;
        public bool warriorFirstAttackKnockback;
        public bool warriorOpeningReach;
        public bool warriorLightGear;
        public bool warriorFearEcho;
        public bool warriorWindstepUsedThisTurn;
        public bool warriorFirstAttackCardUsedThisTurn;
        public bool warriorLightGearUsedThisTurn;
        public bool warriorFearEchoUsedThisTurn;
        public int blastBarrelDamage;
        public bool axeAppliesArmorBreak;
        public bool hammerDoubleArmorDamage;
        public bool swordAppliesBrittle;
        public int phaseMovement;
        public HexCardProfession profession;
        public HexDruidFormType druidForm;
        public int momentum;
        public int druidBonusArmorOnNextTransform;
        public int cardsPlayedThisTurn;
        public bool rooted;
        public bool isPlant;
        public HexAxialCoord coord;

        public HexBattleUnitState Clone()
        {
            return (HexBattleUnitState)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class HexRunState
    {
        public int maxHealth = 30;
        public int currentHealth = 30;
        public int gold = 0;
        public HexCardProfession profession = HexCardProfession.Warrior;
        public List<HexCardDefinition> deckDefinitions = new();
        public List<HexConsumableInstance> consumables = new();

        public HexRunState Clone()
        {
            return new HexRunState
            {
                maxHealth = maxHealth,
                currentHealth = currentHealth,
                gold = gold,
                profession = profession,
                deckDefinitions = new List<HexCardDefinition>(deckDefinitions),
                consumables = consumables.ConvertAll(item => new HexConsumableInstance
                {
                    runtimeId = item.runtimeId,
                    definitionId = item.definitionId,
                    remainingUses = item.remainingUses,
                }),
            };
        }
    }

    [Serializable]
    public sealed class HexMapNodeData
    {
        public string id;
        public int floorIndex;
        public int laneIndex;
        public HexMapNodeType nodeType;
        public Vector2 uiPosition;
        public readonly List<string> outgoingNodeIds = new();
    }

    [Serializable]
    public sealed class HexMapData
    {
        public readonly List<HexMapNodeData> nodes = new();
        public string startNodeId;
        public string bossNodeId;

        public HexMapNodeData GetNode(string nodeId)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].id == nodeId)
                    return nodes[i];
            }

            return null;
        }
    }

    public static class HexCardLibrary
    {
        private static string ExportDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "CardCreator", "exports"));
        private static string WarriorExportPath => Path.Combine(ExportDir, "warrior_cards.json");
        private static string PaladinExportPath => Path.Combine(ExportDir, "paladin_cards.json");
        private static string DruidExportPath => Path.Combine(ExportDir, "Druid_cards.json");
        private static readonly Regex KnockbackRegex = new(@"\u51fb\u98de\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex PullRegex = new(@"\u62c9\u8fd1\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex BleedRegex = new(@"\u6d41\u8840\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex VulnerableRegex = new(@"\u6613\u4f24\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex StunRegex = new(@"(?:\u51fb\u6655|\u7729\u6655)\s*(\d+)?", RegexOptions.Compiled);
        private static readonly Regex RetainRegex = new(@"\u4fdd\u7559", RegexOptions.Compiled);
        private static readonly Regex ExhaustRegex = new(@"消耗。", RegexOptions.Compiled);
        private static readonly Regex BurnRegex = new(@"\u71c3\u70e7\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex EntangleRegex = new(@"\u7f20\u7ed5\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex VoidRegex = new(@"\u865a\u65e0", RegexOptions.Compiled);
        private static readonly Regex WeakRegex = new(@"\u865a\u5f31\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex PhaseRegex = new(@"\u76f8\u4f4d", RegexOptions.Compiled);
        private static readonly Regex ExtendRegex = new(@"\u5ef6\u5c55", RegexOptions.Compiled);
        private static readonly Regex TransformRegex = new(@"\u53d8\u5f62\uff1a\s*(\u731b\u72b8|\u87fe\u870d|\u706b\u5c71\u9b23\u8725|\u706b\u7130\u9b23\u8725|\u5927\u738b\u82b1)", RegexOptions.Compiled);

        private static readonly HexCardDefinition Attack = new()
        {
            id = "attack_strike",
            displayName = "Attack",
            cardType = HexCardType.Attack,
            profession = HexCardProfession.Warrior,
            effectType = HexCardEffectType.Attack,
            targetType = HexCardTargetType.EnemyUnit,
            energyCost = 1,
            amount = 6,
            range = 1,
            castRange = 1,
            effectRadius = 0,
            priority = 1,
            rarity = "Starter",
            description = "Deal 6 damage.",
            color = new Color(0.77f, 0.3f, 0.25f, 1f),
        };

        private static readonly HexCardDefinition Defend = new()
        {
            id = "defend_guard",
            displayName = "Defend",
            cardType = HexCardType.Skill,
            profession = HexCardProfession.Warrior,
            effectType = HexCardEffectType.Defend,
            targetType = HexCardTargetType.Self,
            energyCost = 1,
            amount = 5,
            range = 0,
            castRange = 0,
            effectRadius = 0,
            priority = 2,
            rarity = "Starter",
            description = "Gain 5 armor.",
            color = new Color(0.27f, 0.52f, 0.82f, 1f),
        };

        private static readonly HexCardDefinition Daze = new()
        {
            id = "status_daze",
            displayName = "Daze",
            cardType = HexCardType.Status,
            profession = HexCardProfession.Common,
            effectType = HexCardEffectType.Defend,
            targetType = HexCardTargetType.Self,
            energyCost = 99,
            amount = 0,
            range = 0,
            castRange = 0,
            effectRadius = 0,
            priority = 99,
            rarity = "Common",
            description = "\u865a\u65e0",
            color = new Color(0.38f, 0.4f, 0.48f, 1f),
            isUnplayable = true,
        };

        private static readonly HexCardDefinition Wound = new()
        {
            id = "status_wound",
            displayName = "Wound",
            cardType = HexCardType.Status,
            profession = HexCardProfession.Common,
            effectType = HexCardEffectType.Defend,
            targetType = HexCardTargetType.Self,
            energyCost = 99,
            amount = 0,
            range = 0,
            castRange = 0,
            effectRadius = 0,
            priority = 99,
            rarity = "Common",
            description = "Unplayable.",
            color = new Color(0.45f, 0.2f, 0.2f, 1f),
            isUnplayable = true,
        };

        private static readonly HexCardDefinition HeavyAttack = new()
        {
            id = "attack_heavy",
            displayName = "Cleave",
            cardType = HexCardType.Attack,
            profession = HexCardProfession.Monster,
            effectType = HexCardEffectType.Attack,
            targetType = HexCardTargetType.EnemyUnit,
            energyCost = 2,
            amount = 10,
            range = 1,
            castRange = 1,
            effectRadius = 0,
            priority = 1,
            rarity = "Common",
            description = "Deal 10 damage.",
            color = new Color(0.82f, 0.45f, 0.2f, 1f),
        };

        private static readonly HexCardDefinition Brace = new()
        {
            id = "defend_brace",
            displayName = "Brace",
            cardType = HexCardType.Skill,
            profession = HexCardProfession.Monster,
            effectType = HexCardEffectType.Defend,
            targetType = HexCardTargetType.Self,
            energyCost = 0,
            amount = 3,
            range = 0,
            castRange = 0,
            effectRadius = 0,
            priority = 2,
            rarity = "Common",
            description = "Gain 3 armor.",
            color = new Color(0.35f, 0.66f, 0.88f, 1f),
        };

        private static readonly HexCardDefinition GuardUp = new()
        {
            id = "defend_guard_plus",
            displayName = "Fortify",
            cardType = HexCardType.Skill,
            profession = HexCardProfession.Monster,
            effectType = HexCardEffectType.Defend,
            targetType = HexCardTargetType.Self,
            energyCost = 2,
            amount = 9,
            range = 0,
            castRange = 0,
            effectRadius = 0,
            priority = 2,
            rarity = "Uncommon",
            description = "Gain 9 armor.",
            color = new Color(0.22f, 0.45f, 0.74f, 1f),
        };

        private static readonly HexCardDefinition QuickStrike = new()
        {
            id = "attack_quick",
            displayName = "Jab",
            cardType = HexCardType.Attack,
            profession = HexCardProfession.Monster,
            effectType = HexCardEffectType.Attack,
            targetType = HexCardTargetType.EnemyUnit,
            energyCost = 0,
            amount = 4,
            range = 1,
            castRange = 1,
            effectRadius = 0,
            priority = 1,
            rarity = "Common",
            description = "Deal 4 damage.",
            color = new Color(0.91f, 0.56f, 0.3f, 1f),
        };

        private static readonly HexCardDefinition GoblinStrike = new()
        {
            id = "enemy_goblin_strike",
            displayName = "打击",
            cardType = HexCardType.Attack,
            profession = HexCardProfession.Monster,
            effectType = HexCardEffectType.Attack,
            targetType = HexCardTargetType.EnemyUnit,
            energyCost = 0,
            amount = 5,
            range = 1,
            castRange = 1,
            effectRadius = 0,
            priority = 1,
            rarity = "Enemy",
            description = "对距离1的敌方单位造成5点伤害。",
            color = new Color(0.77f, 0.3f, 0.25f, 1f),
        };

        private static readonly HexCardDefinition GoblinApproach = new()
        {
            id = "enemy_goblin_approach",
            displayName = "移动",
            cardType = HexCardType.Action,
            profession = HexCardProfession.Monster,
            effectType = HexCardEffectType.Move,
            targetType = HexCardTargetType.EnemyUnit,
            energyCost = 0,
            amount = 1,
            range = 0,
            castRange = 0,
            effectRadius = 0,
            priority = 2,
            rarity = "Enemy",
            description = "按理想攻击距离相对目标移动1格。",
            color = new Color(0.48f, 0.62f, 0.28f, 1f),
        };

        private static readonly HexCardDefinition WarriorMoveForward = Card(
            "warrior_move_forward", "前进", HexCardType.Action, HexCardProfession.Warrior, HexCardEffectType.Move, HexCardTargetType.Tile,
            0, 2, 2, 0, "Starter", "移动2。", new Color(0.42f, 0.66f, 0.34f, 1f));

        private static readonly HexCardDefinition WarriorSidestep = Card(
            "warrior_sidestep", "侧步", HexCardType.Action, HexCardProfession.Warrior, HexCardEffectType.Move, HexCardTargetType.Tile,
            1, 1, 1, 0, "Common", "移动1，获得4格挡。", new Color(0.42f, 0.66f, 0.34f, 1f));

        private static readonly HexCardDefinition WarriorBreakPlatform = Card(
            "warrior_break_platform", "破障", HexCardType.Action, HexCardProfession.Warrior, HexCardEffectType.DestroyBarrier, HexCardTargetType.Tile,
            1, 1, 1, 0, "Uncommon", "移动1；破坏邻格高台。", new Color(0.55f, 0.48f, 0.28f, 1f));

        private static readonly HexCardDefinition WarriorBlazingStep = Card(
            "warrior_blazing_step", "炽燃步伐", HexCardType.Action, HexCardProfession.Warrior, HexCardEffectType.Move, HexCardTargetType.Tile,
            1, 1, 1, 0, "Uncommon", "移动1；落点邻格敌人+2燃烧。", new Color(0.72f, 0.32f, 0.18f, 1f));

        private static readonly HexCardDefinition WarriorRedStep = Card(
            "warrior_red_step", "赤步", HexCardType.Action, HexCardProfession.Warrior, HexCardEffectType.Move, HexCardTargetType.Tile,
            1, 1, 1, 0, "Common", "移动1；自身流血1；获得3格挡。", new Color(0.62f, 0.2f, 0.18f, 1f));

        private static readonly HexCardDefinition WarriorFrightenBack = Card(
            "warrior_frighten_back", "惊退", HexCardType.Action, HexCardProfession.Warrior, HexCardEffectType.Move, HexCardTargetType.Tile,
            0, 1, 1, 0, "Common", "移动1；敌方抽牌堆+1恐惧牌。", new Color(0.38f, 0.36f, 0.58f, 1f));

        private static readonly HexCardDefinition FearToken = Card(
            "status_fear_token", "恐惧", HexCardType.Status, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.Self,
            99, 0, 0, 0, "Token", "恐惧。抽到时无效果，进入弃牌堆。", new Color(0.25f, 0.2f, 0.34f, 1f), true, new[] { "恐惧" });

        private static readonly HexCardDefinition GoblinRoll = Card(
            "enemy_goblin_roll", "翻滚", HexCardType.Skill, HexCardProfession.Monster, HexCardEffectType.Move, HexCardTargetType.EnemyUnit,
            0, 1, 1, 0, "Enemy", "按理想攻击距离相对目标移动1格，获得5格挡。", new Color(0.35f, 0.62f, 0.42f, 1f));

        private static readonly HexCardDefinition SpearGoblinThrow = Card(
            "enemy_spear_goblin_throw", "投矛", HexCardType.Attack, HexCardProfession.Monster, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit,
            0, 4, 3, 0, "Enemy", "对距离2-3的敌方单位造成4点伤害。", new Color(0.77f, 0.42f, 0.24f, 1f));

        private static readonly HexCardDefinition SpearGoblinRetreat = Card(
            "enemy_spear_goblin_retreat", "移动", HexCardType.Action, HexCardProfession.Monster, HexCardEffectType.Move, HexCardTargetType.EnemyUnit,
            0, 1, 0, 0, "Enemy", "按理想攻击距离相对目标移动1格。", new Color(0.42f, 0.6f, 0.34f, 1f));

        private static readonly HexCardDefinition GoblinCaptainNet = Card(
            "enemy_goblin_captain_net", "网索", HexCardType.Skill, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.EnemyUnit,
            0, 1, 2, 0, "Enemy", "对距离2内目标施加1层束缚。", new Color(0.36f, 0.5f, 0.58f, 1f));

        private static readonly HexCardDefinition GoblinCaptainWarCry = Card(
            "enemy_goblin_captain_warcry", "战吼", HexCardType.Power, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.Self,
            0, 2, 0, 0, "Enemy", "获得2力量；若随从未达上限，召唤1只哥布林。", new Color(0.55f, 0.34f, 0.78f, 1f));

        private static readonly HexCardDefinition GoblinCaptainGuard = Card(
            "enemy_goblin_captain_guard", "格挡", HexCardType.Skill, HexCardProfession.Monster, HexCardEffectType.Defend, HexCardTargetType.Self,
            0, 8, 0, 0, "Enemy", "获得8点格挡。", new Color(0.27f, 0.52f, 0.82f, 1f));

        private static readonly HexCardDefinition ChieftainHeavyStrike = Card(
            "enemy_chieftain_heavy_strike", "重击", HexCardType.Attack, HexCardProfession.Monster, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit,
            0, 15, 1, 0, "Enemy", "邻格15伤。", new Color(0.82f, 0.32f, 0.22f, 1f));

        private static readonly HexCardDefinition ChieftainCharge = Card(
            "enemy_chieftain_charge", "冲撞", HexCardType.Attack, HexCardProfession.Monster, HexCardEffectType.MoveToward, HexCardTargetType.EnemyUnit,
            0, 1, 1, 0, "Enemy", "直线推进1；碰撞目标额外6伤。", new Color(0.82f, 0.45f, 0.2f, 1f));

        private static readonly HexCardDefinition ChieftainBrace = Card(
            "enemy_chieftain_brace", "稳固", HexCardType.Power, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.Self,
            0, 2, 0, 0, "Enemy", "获得2层稳固。", new Color(0.45f, 0.45f, 0.55f, 1f));

        private static readonly HexCardDefinition ChieftainDrum = Card(
            "enemy_chieftain_drum", "战鼓", HexCardType.Power, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.Self,
            0, 2, 0, 0, "Enemy", "获得2力量。", new Color(0.55f, 0.34f, 0.78f, 1f));

        private static readonly HexCardDefinition ChieftainQuake = Card(
            "enemy_chieftain_quake", "震地", HexCardType.Attack, HexCardProfession.Monster, HexCardEffectType.Attack, HexCardTargetType.Self,
            0, 5, 0, 2, "Enemy", "邻格2全体5伤，击退1；随机1格高台变为废墟木箱。", new Color(0.68f, 0.38f, 0.18f, 1f));

        private static readonly HexCardDefinition GoblinBottom = Card(
            "enemy_goblin_bottom", "越战越勇", HexCardType.Power, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.Self,
            0, 5, 0, 0, "Enemy", "底牌：获得5力量。", new Color(0.55f, 0.34f, 0.78f, 1f), false, new[] { "底牌" });

        private static readonly HexCardDefinition SpearGoblinBottom = Card(
            "enemy_spear_goblin_bottom", "越战越勇（弱）", HexCardType.Power, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.Self,
            0, 1, 0, 0, "Enemy", "底牌：获得1力量。", new Color(0.55f, 0.34f, 0.78f, 1f), false, new[] { "底牌" });

        private static readonly HexCardDefinition CaptainBottom = Card(
            "enemy_goblin_captain_bottom", "增援", HexCardType.Skill, HexCardProfession.Monster, HexCardEffectType.None, HexCardTargetType.Self,
            0, 1, 0, 0, "Enemy", "底牌：召唤1只哥布林；若已达上限，力量+1。", new Color(0.42f, 0.6f, 0.34f, 1f), false, new[] { "底牌" });

        private static readonly HexCardDefinition ChieftainBottom = Card(
            "enemy_chieftain_bottom", "落岩", HexCardType.Action, HexCardProfession.Monster, HexCardEffectType.PlaceRuin, HexCardTargetType.Self,
            0, 4, 1, 0, "Enemy", "底牌：距离1内随机空格放置废墟木箱；无格时力量+2。", new Color(0.55f, 0.48f, 0.28f, 1f), false, new[] { "底牌" });

        private static readonly HexCardDefinition TemporaryThrowingAxe = Card(
            "temp_throwing_axe", "投斧", HexCardType.Attack, HexCardProfession.Common, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit,
            1, 10, 3, 0, "Temporary", "本场临时牌。射程3，造成10伤。", new Color(0.82f, 0.58f, 0.2f, 1f), false, new[] { "临时" });

        private static readonly HexCardDefinition CommonDash = Card(
            "common_dash", "疾行", HexCardType.Action, HexCardProfession.Common, HexCardEffectType.Move, HexCardTargetType.Tile,
            0, 7, 0, 0, "Common", "移动最多7格。", new Color(0.28f, 0.72f, 0.86f, 1f));

        private static readonly IReadOnlyList<HexCardDefinition> WarriorDesignCards = CreateWarriorDesignCards();

        private static readonly IReadOnlyList<HexCardDefinition> EnemyCards = new[]
        {
            GoblinStrike,
            GoblinApproach,
            GoblinRoll,
            SpearGoblinThrow,
            SpearGoblinRetreat,
            GoblinCaptainNet,
            GoblinCaptainWarCry,
            GoblinCaptainGuard,
            ChieftainHeavyStrike,
            ChieftainCharge,
            ChieftainBrace,
            ChieftainDrum,
            ChieftainQuake,
            GoblinBottom,
            SpearGoblinBottom,
            CaptainBottom,
            ChieftainBottom,
        };

        private static readonly IReadOnlyList<HexCardDefinition> RewardPool = WarriorDesignCards;

        private static readonly IReadOnlyList<HexCardDefinition> CommonPool = new[]
        {
            Daze,
            Wound,
            CommonDash,
        };

        private static List<HexCardDefinition> s_loadedWarriorPool;
        private static List<HexCardDefinition> s_loadedPaladinPool;
        private static List<HexCardDefinition> s_loadedDruidPool;
        private static List<HexCardDefinition> s_loadedCommonPool;

        private const string CardDatabaseResourcePath = "HexCardDatabase";
        private static HexCardDatabaseSO s_database;
        private static bool s_databaseChecked;

        private static HexCardDatabaseSO Database
        {
            get
            {
                if (!s_databaseChecked)
                {
                    s_databaseChecked = true;
                    s_database = Resources.Load<HexCardDatabaseSO>(CardDatabaseResourcePath);
                }

                return s_database;
            }
        }

        public static HexCardDefinition GetAttack() => Attack;
        public static HexCardDefinition GetDefend() => Defend;
        public static HexCardDefinition GetDaze() => Daze;
        public static HexCardDefinition GetWound() => Wound;
        public static HexCardDefinition GetFearToken() => FearToken;
        public static HexCardDefinition GetTemporaryThrowingAxe() => TemporaryThrowingAxe;
        public static HexCardDefinition GetCommonDash() => CommonDash;
        public static HexCardDefinition GetGoblinStrike() => GoblinStrike;
        public static HexCardDefinition GetGoblinApproach() => GoblinApproach;
        public static IReadOnlyList<HexCardDefinition> GetEnemyCards() => EnemyCards;
        public static IReadOnlyList<HexCardDefinition> GetWarriorDesignCardsRaw() => WarriorDesignCards;
        public static IReadOnlyList<HexCardDefinition> GetRewardPool() => GetWarriorPool();
        public static IReadOnlyList<HexCardDefinition> GetCommonPool()
        {
            if (s_loadedCommonPool == null)
            {
                var fromDb = Database != null ? Database.BuildCommon() : null;
                s_loadedCommonPool = fromDb != null && fromDb.Count > 0 ? fromDb : new List<HexCardDefinition>();
                for (int i = 0; i < CommonPool.Count; i++)
                {
                    var builtIn = CommonPool[i];
                    if (builtIn != null && !s_loadedCommonPool.Any(card => card != null && card.id == builtIn.id))
                        s_loadedCommonPool.Add(builtIn);
                }
            }

            return s_loadedCommonPool;
        }

        public static IReadOnlyList<HexCardDefinition> GetWarriorPool()
        {
            if (s_loadedWarriorPool == null)
            {
                // Prefer hard-coded design table so stale HexCardDatabase.asset cannot hide new cards.
                if (WarriorDesignCards != null && WarriorDesignCards.Count > 0)
                    s_loadedWarriorPool = new List<HexCardDefinition>(WarriorDesignCards);
                else
                {
                    var fromDb = Database != null ? Database.BuildWarrior() : null;
                    s_loadedWarriorPool = fromDb != null && fromDb.Count > 0
                        ? fromDb
                        : new List<HexCardDefinition>();
                }
            }

            return s_loadedWarriorPool;
        }

        public static IReadOnlyList<HexCardDefinition> GetDruidPool()
        {
            if (s_loadedDruidPool == null)
            {
                var fromDb = Database != null ? Database.BuildDruid() : null;
                s_loadedDruidPool = fromDb != null && fromDb.Count > 0
                    ? fromDb
                    : LoadProfessionPoolFromExport(DruidExportPath, HexCardProfession.Druid, new List<HexCardDefinition>());
            }

            return s_loadedDruidPool;
        }

        public static IReadOnlyList<HexCardDefinition> GetPaladinPool()
        {
            if (s_loadedPaladinPool == null)
            {
                var fromDb = Database != null ? Database.BuildPaladin() : null;
                s_loadedPaladinPool = fromDb != null && fromDb.Count > 0
                    ? fromDb
                    : LoadProfessionPoolFromExport(PaladinExportPath, HexCardProfession.Paladin, new List<HexCardDefinition> { Attack, Defend });
            }

            return s_loadedPaladinPool;
        }

        public static List<HexCardDefinition> CreateStarterDeck(HexCardProfession profession = HexCardProfession.Warrior)
        {
            if (profession == HexCardProfession.Druid)
                return CreateDruidStarterDeck();
            if (profession == HexCardProfession.Paladin)
                return CreateProfessionStarterDeck(GetPaladinPool(), CreateWarriorStarterDeck());

            return CreateWarriorStarterDeck();
        }

        public static List<HexCardDefinition> CreateWarriorStarterDeck()
        {
            return new List<HexCardDefinition>
            {
                GetCardById("warrior_strike"), GetCardById("warrior_strike"), GetCardById("warrior_strike"), GetCardById("warrior_strike"),
                GetCardById("warrior_defend"), GetCardById("warrior_defend"), GetCardById("warrior_defend"), GetCardById("warrior_defend"),
                GetCardById("warrior_move_forward"),
            };
        }

        public static List<HexCardDefinition> CreateMonsterDeck()
        {
            return CreateGoblinDeck();
        }

        public static List<HexCardDefinition> CreateGoblinDeck()
        {
            return new List<HexCardDefinition>
            {
                GoblinStrike, GoblinStrike, GoblinStrike, GoblinStrike,
                GoblinApproach, GoblinApproach, GoblinApproach,
                GoblinRoll, GoblinRoll,
            };
        }

        public static List<HexCardDefinition> CreateSpearGoblinDeck()
        {
            return new List<HexCardDefinition>
            {
                SpearGoblinThrow, SpearGoblinThrow, SpearGoblinThrow, SpearGoblinThrow,
                GoblinApproach, GoblinApproach, GoblinApproach,
                GoblinApproach, GoblinApproach,
            };
        }

        public static List<HexCardDefinition> CreateGoblinCaptainDeck()
        {
            return new List<HexCardDefinition>
            {
                GoblinStrike, GoblinStrike, GoblinStrike,
                GoblinApproach, GoblinApproach, GoblinApproach,
                GoblinCaptainNet, GoblinCaptainNet,
                GoblinCaptainWarCry, GoblinCaptainWarCry,
                GoblinCaptainGuard, GoblinCaptainGuard,
            };
        }

        public static List<HexCardDefinition> CreateChieftainDeck()
        {
            return new List<HexCardDefinition>
            {
                ChieftainHeavyStrike, ChieftainHeavyStrike, ChieftainHeavyStrike, ChieftainHeavyStrike,
                ChieftainCharge, ChieftainCharge, ChieftainCharge,
                ChieftainBrace, ChieftainBrace,
                ChieftainDrum, ChieftainDrum,
                ChieftainQuake, ChieftainQuake,
                GoblinApproach, GoblinApproach,
            };
        }

        public static HexEnemyDefinition GetEnemyDefinition(string id)
        {
            return id switch
            {
                "goblin" => CreateEnemyDefinition("goblin", "哥布林", HexEnemyEncounterType.Normal, HexEnemyIntentPattern.ApproachStrike, 1, 1, 5, GoblinBottom, CreateGoblinDeck(), HexEnemyIntentSlotKind.Move, HexEnemyIntentSlotKind.Attack),
                "spear_goblin" => CreateEnemyDefinition("spear_goblin", "投矛哥布林", HexEnemyEncounterType.Normal, HexEnemyIntentPattern.Ranged, 2, 3, 1, SpearGoblinBottom, CreateSpearGoblinDeck(), HexEnemyIntentSlotKind.Move, HexEnemyIntentSlotKind.Attack),
                "goblin_captain" => CreateEnemyDefinition("goblin_captain", "哥布林队长", HexEnemyEncounterType.Elite, HexEnemyIntentPattern.ApproachStrike, 1, 1, 1, CaptainBottom, CreateGoblinCaptainDeck(), HexEnemyIntentSlotKind.Move, HexEnemyIntentSlotKind.Attack, HexEnemyIntentSlotKind.Free),
                "tribal_chieftain" => CreateEnemyDefinition("tribal_chieftain", "部落酋长", HexEnemyEncounterType.Boss, HexEnemyIntentPattern.ApproachStrike, 1, 1, 2, ChieftainBottom, CreateChieftainDeck(), HexEnemyIntentSlotKind.Move, HexEnemyIntentSlotKind.Attack, HexEnemyIntentSlotKind.Free, HexEnemyIntentSlotKind.Free),
                _ => CreateEnemyDefinition("goblin", "哥布林", HexEnemyEncounterType.Normal, HexEnemyIntentPattern.ApproachStrike, 1, 1, 5, GoblinBottom, CreateGoblinDeck(), HexEnemyIntentSlotKind.Move, HexEnemyIntentSlotKind.Attack),
            };
        }

        public static List<HexCardDefinition> CreateDruidStarterDeck()
        {
            string[] starterIds =
            {
                "C_03_001",
                "C_03_002",
                "C_03_003",
                "C_03_004",
                "C_03_005",
                "C_03_006",
                "C_03_007",
                "C_03_009",
                "C_03_010",
            };

            var deck = new List<HexCardDefinition>();
            for (int i = 0; i < starterIds.Length; i++)
            {
                var card = GetCardById(starterIds[i]);
                if (card != null)
                    deck.Add(card);
            }

            if (deck.Count == 0)
            {
                deck.AddRange(GetDruidPool());
                if (deck.Count > 9)
                    deck.RemoveRange(9, deck.Count - 9);
            }

            return deck;
        }

        private static List<HexCardDefinition> CreateProfessionStarterDeck(IReadOnlyList<HexCardDefinition> pool, List<HexCardDefinition> fallbackDeck)
        {
            var deck = new List<HexCardDefinition>();
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    var card = pool[i];
                    if (card != null && string.Equals(card.rarity, "Starter", StringComparison.OrdinalIgnoreCase))
                        deck.Add(card);
                }
            }

            if (deck.Count == 0)
                return fallbackDeck;

            return deck;
        }

        public static HexCardDefinition GetRandomRewardCard()
        {
            return GetRandomRewardCard(HexCardProfession.Warrior);
        }

        public static HexCardDefinition GetRandomRewardCard(HexCardProfession profession)
        {
            var card = DrawWeightedRewardCard(GetRewardCandidates(profession));
            return card ?? RewardPool[UnityEngine.Random.Range(0, RewardPool.Count)];
        }

        public static List<HexCardDefinition> GetRewardChoices(int count, HexCardProfession profession)
        {
            var available = GetRewardCandidates(profession);
            var results = new List<HexCardDefinition>(Mathf.Max(0, count));
            while (results.Count < count && available.Count > 0)
            {
                var card = DrawWeightedRewardCard(available);
                if (card == null)
                    break;

                results.Add(card);
                available.Remove(card);
            }

            return results;
        }

        public static HexCardDefinition GetCardById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            foreach (var pool in EnumerateSearchPools())
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].id == id)
                        return pool[i];
                }
            }

            return null;
        }

        public static HexCardDefinition GetCardByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            foreach (var pool in EnumerateSearchPools())
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].displayName == displayName)
                        return pool[i];
                }
            }

            return null;
        }

        public static HexDruidFormType GetDruidForm(HexCardDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.description))
                return HexDruidFormType.None;

            var match = TransformRegex.Match(definition.description);
            if (!match.Success)
                return HexDruidFormType.None;

            return match.Groups[1].Value switch
            {
                "猛犸" => HexDruidFormType.Mammoth,
                "蟾蜍" => HexDruidFormType.Toad,
                "火山鬣蜥" => HexDruidFormType.LavaLizard,
                "火焰鬣蜥" => HexDruidFormType.LavaLizard,
                "大王花" => HexDruidFormType.Rafflesia,
                _ => HexDruidFormType.None,
            };
        }

        public static IReadOnlyList<HexCardKeywordEffect> GetKeywordEffects(HexCardDefinition definition)
        {
            var effects = new List<HexCardKeywordEffect>();
            if (definition == null || string.IsNullOrWhiteSpace(definition.description))
                return effects;

            AddNumberedEffects(effects, definition.description, KnockbackRegex, HexCardKeywordType.Knockback);
            AddNumberedEffects(effects, definition.description, PullRegex, HexCardKeywordType.Pull);
            AddNumberedEffects(effects, definition.description, BleedRegex, HexCardKeywordType.Bleed);
            AddNumberedEffects(effects, definition.description, VulnerableRegex, HexCardKeywordType.Vulnerable);
            AddNumberedEffects(effects, definition.description, WeakRegex, HexCardKeywordType.Weak);
            AddNumberedEffects(effects, definition.description, BurnRegex, HexCardKeywordType.Burn);
            AddNumberedEffects(effects, definition.description, EntangleRegex, HexCardKeywordType.Entangle);

            foreach (Match match in StunRegex.Matches(definition.description))
            {
                int amount = 1;
                if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                    int.TryParse(match.Groups[1].Value, out amount);

                effects.Add(new HexCardKeywordEffect
                {
                    keywordType = HexCardKeywordType.Stun,
                    amount = Mathf.Max(1, amount),
                });
            }

            if (RetainRegex.IsMatch(definition.description))
                effects.Add(new HexCardKeywordEffect { keywordType = HexCardKeywordType.Retain, amount = 1 });

            if (ExhaustRegex.IsMatch(definition.description))
                effects.Add(new HexCardKeywordEffect { keywordType = HexCardKeywordType.Exhaust, amount = 1 });

            if (VoidRegex.IsMatch(definition.description))
                effects.Add(new HexCardKeywordEffect { keywordType = HexCardKeywordType.Void, amount = 1 });

            if (PhaseRegex.IsMatch(definition.description))
                effects.Add(new HexCardKeywordEffect { keywordType = HexCardKeywordType.Phase, amount = 1 });

            if (ExtendRegex.IsMatch(definition.description))
                effects.Add(new HexCardKeywordEffect { keywordType = HexCardKeywordType.Extend, amount = 1 });

            return effects;
        }

        public static bool HasKeyword(HexCardDefinition definition, HexCardKeywordType keywordType)
        {
            var effects = GetKeywordEffects(definition);
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].keywordType == keywordType)
                    return true;
            }

            return false;
        }

        public static bool ShouldExhaustAtEndOfTurn(HexCardDefinition definition)
        {
            if (definition == null)
                return false;

            if (HasKeyword(definition, HexCardKeywordType.Void))
                return true;

            if (definition.tags == null)
                return false;

            for (int i = 0; i < definition.tags.Length; i++)
            {
                if (string.Equals(definition.tags[i], "移出游戏", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void AddNumberedEffects(List<HexCardKeywordEffect> effects, string description, Regex regex, HexCardKeywordType keywordType)
        {
            foreach (Match match in regex.Matches(description))
            {
                if (!int.TryParse(match.Groups[1].Value, out int amount) || amount <= 0)
                    continue;

                effects.Add(new HexCardKeywordEffect
                {
                    keywordType = keywordType,
                    amount = amount,
                });
            }
        }

        private static IReadOnlyList<HexCardDefinition> CreateWarriorDesignCards()
        {
            Color attackColor = new(0.78f, 0.28f, 0.22f, 1f);
            Color skillColor = new(0.25f, 0.48f, 0.82f, 1f);
            Color actionColor = new(0.42f, 0.64f, 0.34f, 1f);
            Color powerColor = new(0.55f, 0.34f, 0.78f, 1f);
            Color exhaustColor = new(0.72f, 0.42f, 0.2f, 1f);
            Color focusColor = new(0.3f, 0.55f, 0.78f, 1f);
            Color moveColor = new(0.42f, 0.64f, 0.34f, 1f);
            Color fearColor = new(0.38f, 0.34f, 0.58f, 1f);

            return new List<HexCardDefinition>
            {
                // §1 基础
                W("warrior_strike", "打击", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit, 1, 6, 1, 0, "Starter", "6伤。", attackColor, "无"),
                W("warrior_defend", "防御", HexCardType.Skill, HexCardEffectType.Defend, HexCardTargetType.Self, 1, 5, 0, 0, "Starter", "5格挡。", skillColor, "无"),
                W("warrior_whirlwind", "旋风斩", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.Self, 1, 2, 0, 2, "Starter", "环形1：2伤，击退1。", attackColor, "无"),
                W("warrior_quick_step", "快步", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 1, 1, 1, 0, "Baseline", "首发。移动1。消耗。虚无。", actionColor, "无", "首发", "移出游戏"),
                W("warrior_close_step", "贴身步", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 1, 1, 3, 0, "Starter", "移动1，或移动到邻格敌人的任意邻格。", actionColor, "无", "草案"),

                // §2 过渡
                W("warrior_heavy_blow", "重击", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit, 1, 9, 1, 0, "Common", "9伤。", attackColor, "过渡"),
                W("warrior_cleave", "顺劈", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.Self, 1, 4, 1, 0, "Common", "对最多3个邻格敌人各4伤。", attackColor, "过渡"),
                W("warrior_dash_strike", "冲刺", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 3, 6, 1, 0, "Common", "命中无耗。移动1并攻击该方向敌人6伤。", attackColor, "过渡", "命中无耗"),
                W("warrior_pursuit", "追击", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 3, 10, 1, 0, "Uncommon", "命中无耗。10伤，随后后退1，弃1。", attackColor, "过渡", "命中无耗"),
                W("warrior_true_courage", "真勇", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 7, 0, 0, "Uncommon", "7格挡，抽1。", skillColor, "过渡"),
                W("warrior_disarming_stare", "缴械凝视", HexCardType.Action, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 0, 1, 1, 0, "Common", "邻格击退1。", actionColor, "过渡"),
                W("warrior_guillotine", "断头台", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 2, 2, 2, 0, "Uncommon", "移动2；落点邻格有敌人则击退1。", actionColor, "过渡"),
                W("warrior_battle_line", "战阵", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 2, 1, 0, 0, "Rare", "本场每回合开始获得1力量。", powerColor, "过渡"),
                W("warrior_immovable_mountain", "不动如山", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 3, 1, 0, 0, "Uncommon", "回合开始时格挡不重置。", powerColor, "过渡"),
                W("warrior_opening_stagger", "起手震慑", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 1, 1, 0, 0, "Uncommon", "每回合第一张攻击获得击退1。", powerColor, "过渡", "草案"),
                W("warrior_opening_reach", "先手延展", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 1, 1, 0, 0, "Uncommon", "每回合第一张攻击距离+1。", powerColor, "过渡", "草案"),
                W("warrior_leap_step", "跃迁步", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 1, 2, 2, 0, "Common", "可跨越1个敌人或残骸后移动2。", actionColor, "过渡", "草案"),
                W("warrior_blast_barrel", "爆破桶", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 1, 8, 2, 0, "Uncommon", "给目标放置炸药桶（受击时范围8伤击退1）。", skillColor, "过渡", "草案"),
                W("warrior_hook", "钩锁", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit, 1, 4, 2, 0, "Common", "4伤；目标拉拽1。", attackColor, "过渡", "草案"),
                W("warrior_break_slash", "破障斩", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit, 1, 8, 1, 0, "Uncommon", "8伤；破坏邻格残骸。", attackColor, "过渡", "草案"),
                W("warrior_rolling_siege", "滚石攻城", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.Direction, 2, 4, 1, 0, "Rare", "消耗。破坏邻格残骸，直线滚石：每格4伤。", attackColor, "过渡", "草案"),
                W("warrior_pierce_step", "穿垒步", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 1, 2, 2, 0, "Common", "穿越残骸至对侧。", actionColor, "过渡", "草案"),
                W("warrior_backwall_smash", "背障重击", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit, 2, 34, 1, 0, "Rare", "消耗。身后是障碍时34伤，否则10伤。", attackColor, "过渡", "草案"),
                W("warrior_dismantle_slash", "拆垒重斩", HexCardType.Attack, HexCardEffectType.Attack, HexCardTargetType.EnemyUnit, 2, 16, 1, 0, "Uncommon", "目标附近有残骸时16伤并破坏残骸，否则6伤。", attackColor, "过渡", "草案"),

                // §3.1 消耗
                W("warrior_warmup", "热身", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 2, 0, 0, "Common", "抽2，消耗手牌1。", exhaustColor, "消耗"),
                W("warrior_numb", "麻木", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 4, 0, 0, "Uncommon", "消耗。若本回合已打出消耗牌，获得4费。", exhaustColor, "消耗", "消耗"),
                W("warrior_simplify", "精简", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 2, 0, 0, "Rare", "消耗。从牌堆选2张消耗牌并打出。", exhaustColor, "消耗", "消耗"),
                W("warrior_ember_chaos", "烬乱", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.Self, 2, 3, 0, 2, "Rare", "消耗。消耗手牌全部；每张对环形2敌人3伤。", exhaustColor, "消耗", "消耗"),
                W("warrior_furnace_heart", "炉心", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 2, 1, 0, 0, "Rare", "每当你消耗一张牌，抽1。", powerColor, "消耗"),
                W("warrior_scorched_earth", "焦土协议", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 1, 1, 0, 0, "Uncommon", "回合结束时可消耗手牌1；若如此则下回合+1费。", powerColor, "消耗", "草案"),
                W("warrior_scrap_recycle", "废料回收", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 1, 0, 0, "Uncommon", "消耗。若本回合已打出消耗牌，从消耗堆将1张牌加入手牌。", exhaustColor, "消耗", "消耗"),
                W("warrior_fuel", "助燃", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 10, 0, 0, "Common", "消耗。若本回合已打出消耗牌，下一张攻击+10伤，否则+4伤。", exhaustColor, "消耗", "消耗"),

                // §3.2 集中
                W("warrior_build_up", "蓄势", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 2, 7, 0, 0, "Uncommon", "下一张攻击+7伤。", focusColor, "集中"),
                W("warrior_double_focus", "双集", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 2, 0, 0, "Rare", "下一张攻击+2伤；再下一张攻击+2伤。", focusColor, "集中", "草案"),
                W("warrior_combo_focus_slash", "连集斩", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 2, 2, 1, 0, "Rare", "2伤×4。", focusColor, "集中"),
                W("warrior_prepared_blade", "备刃", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 1, 1, 0, 0, "Uncommon", "每回合开始获得「下一张攻击+1伤」。", powerColor, "集中"),
                W("warrior_hilt_storm", "剑柄风暴", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 2, 1, 2, 0, "Uncommon", "1伤击退1，两次。", focusColor, "集中"),
                W("warrior_iaido", "见切", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 2, 12, 1, 0, "Common", "12伤；集中效果在这张卡上×2。", focusColor, "集中"),

                // §3.3 移动（含初始前进）
                W("warrior_move_forward", "前进", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 0, 2, 2, 0, "Starter", "移动2。", moveColor, "移动"),
                W("warrior_break_platform", "破障", HexCardType.Action, HexCardEffectType.DestroyBarrier, HexCardTargetType.Tile, 1, 1, 1, 0, "Uncommon", "消耗。移动1；破坏邻格障碍。", moveColor, "移动"),
                W("warrior_fortify", "筑垒", HexCardType.Action, HexCardEffectType.None, HexCardTargetType.Self, 1, 1, 1, 0, "Uncommon", "邻格空位生成临时障碍1回合。虚无。", moveColor, "移动", "移出游戏"),
                W("warrior_block_path", "封路", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Direction, 2, 1, 1, 0, "Rare", "指定直线1生成只存在两回合、生命值为1的残骸。", skillColor, "移动"),
                W("warrior_light_gear", "轻装", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 1, 1, 0, 0, "Uncommon", "下两个回合首次移动不消耗费用。", powerColor, "移动"),
                W("warrior_windstep_ready", "踏风预备", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 2, 0, 0, "Uncommon", "本回合每次位移获得2临时力量。", skillColor, "移动", "草案"),
                W("warrior_flash_step_slash", "疾步斩", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 1, 5, 1, 0, "Uncommon", "移动1后邻格5伤；本回合已触发位移时再5。", moveColor, "移动"),
                W("warrior_charge", "猛冲", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 2, 4, 1, 0, "Rare", "直线推进1；碰撞+4；本回合已触发位移时撞障碍再+8。", moveColor, "移动"),
                W("warrior_quake", "震地", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.Tile, 2, 4, 1, 1, "Rare", "移动1；邻格全体4伤；本回合已触发位移时+2/目标；随机1邻格障碍→残骸。", moveColor, "移动"),
                W("warrior_swap_guard", "换位守", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 1, 1, 1, 0, "Common", "移动1；本回合已触发位移时再+5格挡。", moveColor, "移动"),
                W("warrior_cover_lean", "掩体靠", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 8, 0, 0, "Uncommon", "8格挡；本回合已触发位移且邻格有障碍时再+8格挡。", skillColor, "移动"),
                W("warrior_clear_shield", "清障盾", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 6, 1, 0, "Common", "破坏邻格残骸；获得6格挡。", skillColor, "移动"),
                W("warrior_skirmish", "游斗", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 2, 2, 0, 0, "Rare", "每当你主动移动，获得2格挡。", powerColor, "移动"),

                // §3.4 塞牌
                W("warrior_vile_words", "污言", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 1, 1, 1, 0, "Common", "敌方抽牌堆+1恐惧牌；抽1。", fearColor, "塞牌", "打出后抽"),
                W("warrior_fear_howl", "塞啸", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 1, 2, 3, 0, "Uncommon", "消耗。敌方+2恐惧牌+击退1。", fearColor, "塞牌", "消耗"),
                W("warrior_contagion", "传染", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Tile, 1, 1, 1, 0, "Uncommon", "消耗。移动1；敌方+3恐惧牌。", fearColor, "塞牌", "消耗"),
                W("warrior_empty_city", "空城", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 2, 12, 0, 0, "Rare", "12格挡；敌方抽牌堆恐惧牌≥3时再抽1。", fearColor, "塞牌"),
                W("warrior_warcry_fear", "战吼", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.Self, 1, 7, 0, 0, "Uncommon", "7格挡；意图槽有恐惧时再移动2、5格挡。", fearColor, "塞牌"),
                W("warrior_frighten_back", "惊退", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 0, 1, 1, 0, "Common", "移动1；敌方+1恐惧牌。", fearColor, "塞牌"),
                W("warrior_screaming_raid", "惊啸突袭", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 1, 15, 2, 0, "Uncommon", "消耗意图槽1张恐惧牌；移动2、15伤。", fearColor, "塞牌"),
                W("warrior_fear_descends", "恐惧降临", HexCardType.Attack, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 2, 1, 1, 0, "Rare", "邻格击退1；意图槽有恐惧时击退3；撞地形+50。", fearColor, "塞牌", "Post-MVP"),
                W("warrior_omen", "噩兆", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 2, 1, 0, 0, "Rare", "每回合首次塞入恐惧牌时，额外再塞1。", powerColor, "塞牌", "草案"),
                W("warrior_mind_guard", "心防", HexCardType.Power, HexCardEffectType.None, HexCardTargetType.Self, 1, 3, 0, 0, "Uncommon", "每当敌方抽牌堆获得恐惧牌时，你获得3格挡。", powerColor, "塞牌", "草案"),
                W("warrior_intent_intercept", "意图截流", HexCardType.Skill, HexCardEffectType.None, HexCardTargetType.EnemyUnit, 2, 1, 2, 0, "Rare", "消耗。消耗敌人意图1张；敌方抽牌堆+2消耗掉的牌。", fearColor, "塞牌", "消耗", "草案"),
                W("warrior_evasion_plan", "规避预案", HexCardType.Action, HexCardEffectType.Move, HexCardTargetType.Tile, 1, 1, 2, 0, "Common", "移动1；4格挡；意图槽有恐惧时再移动1。", fearColor, "塞牌"),
            };
        }

        private static HexCardDefinition W(
            string id,
            string displayName,
            HexCardType cardType,
            HexCardEffectType effectType,
            HexCardTargetType targetType,
            int energyCost,
            int amount,
            int castRange,
            int effectRadius,
            string rarity,
            string description,
            Color color,
            params string[] tags)
        {
            return Card(id, displayName, cardType, HexCardProfession.Warrior, effectType, targetType, energyCost, amount, castRange, effectRadius, rarity, description, color, false, tags);
        }

        private static HexCardDefinition Card(
            string id,
            string displayName,
            HexCardType cardType,
            HexCardProfession profession,
            HexCardEffectType effectType,
            HexCardTargetType targetType,
            int energyCost,
            int amount,
            int castRange,
            int effectRadius,
            string rarity,
            string description,
            Color color,
            bool isUnplayable = false,
            string[] tags = null)
        {
            return new HexCardDefinition
            {
                id = id,
                displayName = displayName,
                cardType = cardType,
                profession = profession,
                effectType = effectType,
                targetType = targetType,
                energyCost = energyCost,
                amount = amount,
                range = castRange,
                castRange = castRange,
                effectRadius = effectRadius,
                priority = cardType == HexCardType.Attack ? 1 : cardType == HexCardType.Action ? 0 : 2,
                rarity = rarity,
                description = description,
                color = color,
                isUnplayable = isUnplayable,
                tags = tags,
            };
        }

        private static HexEnemyDefinition CreateEnemyDefinition(
            string id,
            string displayName,
            HexEnemyEncounterType encounterType,
            HexEnemyIntentPattern pattern,
            int attackMinRange,
            int attackMaxRange,
            int emptyDrawPileStrengthGain,
            HexCardDefinition bottomCard,
            List<HexCardDefinition> deck,
            params HexEnemyIntentSlotKind[] slots)
        {
            int minRange = Mathf.Max(1, attackMinRange);
            int maxRange = Mathf.Max(minRange, attackMaxRange);
            if (deck != null)
            {
                for (int i = 0; i < deck.Count; i++)
                {
                    var card = deck[i];
                    if (card == null || card.cardType != HexCardType.Attack)
                        continue;
                    maxRange = Mathf.Max(maxRange, Mathf.Max(1, card.castRange));
                }
            }

            return new HexEnemyDefinition
            {
                id = id,
                displayName = displayName,
                encounterType = encounterType,
                intentPattern = pattern,
                attackMinRange = minRange,
                attackMaxRange = maxRange,
                emptyDrawPileStrengthGain = Mathf.Max(0, emptyDrawPileStrengthGain),
                bottomCard = bottomCard,
                deckDefinitions = deck ?? new List<HexCardDefinition>(),
                intentSlots = slots != null ? new List<HexEnemyIntentSlotKind>(slots) : new List<HexEnemyIntentSlotKind>(),
            };
        }

        private static List<HexCardDefinition> LoadWarriorPoolFromExport()
        {
            var cards = LoadProfessionPoolFromExport(WarriorExportPath, HexCardProfession.Warrior, new List<HexCardDefinition> { Attack, Defend });
            MergeUniqueCards(cards, WarriorDesignCards);
            return cards;
        }

        private static void MergeUniqueCards(List<HexCardDefinition> cards, IEnumerable<HexCardDefinition> additions)
        {
            if (cards == null || additions == null)
                return;

            foreach (var addition in additions)
            {
                if (addition == null)
                    continue;

                int existingIndex = cards.FindIndex(card =>
                    card != null &&
                    (card.id == addition.id || card.displayName == addition.displayName));
                if (existingIndex >= 0)
                    cards[existingIndex] = addition;
                else
                    cards.Add(addition);
            }
        }

        private static List<HexCardDefinition> LoadProfessionPoolFromExport(string exportPath, HexCardProfession profession, List<HexCardDefinition> fallbackCards)
        {
            var cards = new List<HexCardDefinition>(fallbackCards);
            try
            {
                if (!File.Exists(exportPath))
                    return cards;

                string json = File.ReadAllText(exportPath);
                var exportFile = JsonUtility.FromJson<HexCardExportFile>(json);
                if (exportFile?.cards == null || exportFile.cards.Count == 0)
                    return cards;

                cards.Clear();
                for (int i = 0; i < exportFile.cards.Count; i++)
                {
                    var definition = BuildCardFromExport(exportFile.cards[i], i, profession);
                    if (definition != null)
                        cards.Add(definition);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load {profession} cards from export: {exception.Message}");
            }

            if (cards.Count == 0)
                cards.AddRange(fallbackCards);

            return cards;
        }

        private static HexCardDefinition BuildCardFromExport(HexCardExportData exportData, int index, HexCardProfession profession)
        {
            if (exportData == null || string.IsNullOrWhiteSpace(exportData.name))
                return null;

            string safeId = Regex.Replace(exportData.name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            if (string.IsNullOrEmpty(safeId))
                safeId = $"{profession.ToString().ToLowerInvariant()}_{index}";

            int castRange = exportData.cast_range > 0 || exportData.effect_radius > 0 || exportData.attack_range == 0
                ? Mathf.Max(0, exportData.cast_range)
                : Mathf.Max(0, exportData.attack_range);
            int effectRadius = Mathf.Max(0, exportData.effect_radius);
            bool hasExtendKeyword = !string.IsNullOrWhiteSpace(exportData.description) && ExtendRegex.IsMatch(exportData.description);
            if (hasExtendKeyword)
                castRange += 1;
            string definitionId = string.IsNullOrWhiteSpace(exportData.card_id) ? $"{profession.ToString().ToLowerInvariant()}_{safeId}_{index}" : exportData.card_id;

            return new HexCardDefinition
            {
                id = definitionId,
                displayName = exportData.name,
                cardType = ParseCardType(exportData.card_type),
                profession = profession,
                effectType = ParseEffectType(exportData.card_type, exportData.description),
                targetType = ParseTargetType(exportData),
                energyCost = ParseEnergyCost(exportData.cost),
                amount = ParseAmount(exportData.description),
                range = castRange,
                castRange = castRange,
                effectRadius = effectRadius,
                priority = ParseEffectType(exportData.card_type, exportData.description) == HexCardEffectType.Attack ? 1 : 2,
                rarity = string.IsNullOrWhiteSpace(exportData.rarity) ? "Common" : exportData.rarity,
                description = exportData.description,
                color = GetCardColor(ParseCardType(exportData.card_type)),
                isUnplayable = ParseCardType(exportData.card_type) == HexCardType.Status || ParseCardType(exportData.card_type) == HexCardType.Curse,
            };
        }

        private static HexCardTargetType ParseTargetType(HexCardExportData exportData)
        {
            string rawTargetType = string.IsNullOrWhiteSpace(exportData.target_type)
                ? string.Empty
                : exportData.target_type.Trim();

            if (string.Equals(rawTargetType, "Direction", StringComparison.OrdinalIgnoreCase))
                return HexCardTargetType.Direction;

            if (string.Equals(rawTargetType, "Tile", StringComparison.OrdinalIgnoreCase))
                return HexCardTargetType.Tile;

            if (string.Equals(rawTargetType, "Self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawTargetType, "None", StringComparison.OrdinalIgnoreCase))
                return HexCardTargetType.Self;

            if (string.Equals(rawTargetType, "Unit", StringComparison.OrdinalIgnoreCase) || exportData.is_directional)
                return HexCardTargetType.EnemyUnit;

            return HexCardTargetType.Self;
        }

        private static List<HexCardDefinition> GetRewardCandidates(HexCardProfession profession)
        {
            IReadOnlyList<HexCardDefinition> sourcePool = profession switch
            {
                HexCardProfession.Warrior => GetWarriorPool(),
                HexCardProfession.Paladin => GetPaladinPool(),
                HexCardProfession.Druid => GetDruidPool(),
                _ => GetRewardPool(),
            };
            var candidates = new List<HexCardDefinition>();
            for (int i = 0; i < sourcePool.Count; i++)
            {
                var card = sourcePool[i];
                if (card == null)
                    continue;
                if (card.profession == HexCardProfession.Monster)
                    continue;
                if (card.cardType == HexCardType.Status || card.cardType == HexCardType.Curse || card.cardType == HexCardType.Special)
                    continue;
                if (card.isUnplayable)
                    continue;
                if (string.Equals(card.rarity, "Starter", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(card.rarity, "Baseline", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(card.rarity, "Token", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(card.rarity, "Temporary", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasCardTag(card, "Post-MVP"))
                    continue;

                candidates.Add(card);
            }

            var commonPool = GetCommonPool();
            for (int i = 0; i < commonPool.Count; i++)
            {
                var card = commonPool[i];
                if (card == null || card.isUnplayable || card.cardType == HexCardType.Status || card.cardType == HexCardType.Curse || card.cardType == HexCardType.Special)
                    continue;
                if (!candidates.Any(existing => existing != null && existing.id == card.id))
                    candidates.Add(card);
            }

            return candidates;
        }

        private static bool HasCardTag(HexCardDefinition card, string tag)
        {
            if (card?.tags == null || string.IsNullOrWhiteSpace(tag))
                return false;

            for (int i = 0; i < card.tags.Length; i++)
            {
                if (string.Equals(card.tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<IReadOnlyList<HexCardDefinition>> EnumerateSearchPools()
        {
            yield return GetWarriorPool();
            yield return GetPaladinPool();
            yield return GetDruidPool();
            yield return GetRewardPool();
            yield return GetCommonPool();
        }

        private static HexCardDefinition DrawWeightedRewardCard(List<HexCardDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
                totalWeight += GetRewardWeight(candidates[i]);

            if (totalWeight <= 0.001f)
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];

            float roll = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= GetRewardWeight(candidates[i]);
                if (roll <= 0f)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private static float GetRewardWeight(HexCardDefinition card)
        {
            if (card == null)
                return 0f;

            return card.rarity switch
            {
                "Common" => 60f,
                "Uncommon" => 30f,
                "Rare" => 10f,
                _ => 20f,
            };
        }

        private static int ParseEnergyCost(string rawCost)
        {
            if (string.Equals(rawCost, "X", StringComparison.OrdinalIgnoreCase))
                return -1;

            if (int.TryParse(rawCost, out int cost))
                return Mathf.Max(0, cost);

            return 0;
        }

        private static int ParseAmount(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return 0;

            var match = Regex.Match(description, @"(\d+)\s*\u70b9\u4f24\u5bb3");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int attackAmount))
                return attackAmount;

            match = Regex.Match(description, @"(\d+)\s*\u70b9\u62a4\u7532");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int armorAmount))
                return armorAmount;

            return 0;
        }

        private static HexCardType ParseCardType(string rawCardType)
        {
            return rawCardType switch
            {
                "Attack" => HexCardType.Attack,
                "Skill" => HexCardType.Skill,
                "Power" => HexCardType.Power,
                "Status" => HexCardType.Status,
                "Curse" => HexCardType.Curse,
                "Action" => HexCardType.Action,
                "Move" => HexCardType.Action,
                "Special" => HexCardType.Special,
                _ => HexCardType.Skill,
            };
        }

        private static HexCardEffectType ParseEffectType(string rawCardType, string description)
        {
            if (rawCardType == "Attack")
                return HexCardEffectType.Attack;
            if (rawCardType == "Action" || rawCardType == "Move")
                return HexCardEffectType.Move;
            if (!string.IsNullOrWhiteSpace(description) && Regex.IsMatch(description, @"\u79fb\u52a8\s*\d+"))
                return HexCardEffectType.Move;

            return HexCardEffectType.Defend;
        }

        private static Color GetCardColor(HexCardType cardType)
        {
            return cardType switch
            {
                HexCardType.Attack => new Color(0.77f, 0.3f, 0.25f, 1f),
                HexCardType.Skill => new Color(0.27f, 0.52f, 0.82f, 1f),
                HexCardType.Power => new Color(0.55f, 0.34f, 0.78f, 1f),
                HexCardType.Status => new Color(0.38f, 0.4f, 0.48f, 1f),
                HexCardType.Curse => new Color(0.34f, 0.18f, 0.38f, 1f),
                HexCardType.Action => new Color(0.42f, 0.66f, 0.34f, 1f),
                HexCardType.Special => new Color(0.82f, 0.58f, 0.2f, 1f),
                _ => Color.white,
            };
        }
    }
}
