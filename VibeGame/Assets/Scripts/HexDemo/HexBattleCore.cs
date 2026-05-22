using System;
using System.Collections.Generic;
using System.IO;
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
        Special = 5,
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

    [Serializable]
    public sealed class HexCardInstance
    {
        public string runtimeId;
        public HexCardDefinition definition;
        public bool upgraded;
        public int temporaryCostModifier;
        public bool costsNoEnergyThisTurn;
        public bool exhaustWhenPlayed;

        public HexCardInstance(HexCardDefinition definition)
        {
            runtimeId = Guid.NewGuid().ToString("N");
            this.definition = definition;
            upgraded = definition != null && definition.upgraded;
        }
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
                RefillDrawPileIfNeeded();
                if (_drawPile.Count == 0)
                    return;

                var nextCard = _drawPile[^1];
                _drawPile.RemoveAt(_drawPile.Count - 1);
                _hand.Add(nextCard);
            }
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
        public HexWeaponType weapon;
        public bool drawDisabledThisTurn;
        public int attackRepeatBonusThisTurn;
        public int damageDealtThisTurn;
        public int armorOnAttackCardThisTurn;
        public int armorOnSkillCard;
        public int firstAttackBurnAmount;
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

        public HexRunState Clone()
        {
            return new HexRunState
            {
                maxHealth = maxHealth,
                currentHealth = currentHealth,
                gold = gold,
                profession = profession,
                deckDefinitions = new List<HexCardDefinition>(deckDefinitions),
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
        private const string WarriorExportPath = "F:/VibeGame/CardCreator/exports/warrior_cards.json";
        private const string PaladinExportPath = "F:/VibeGame/CardCreator/exports/paladin_cards.json";
        private const string DruidExportPath = "F:/VibeGame/CardCreator/exports/Druid_cards.json";
        private static readonly Regex KnockbackRegex = new(@"\u51fb\u98de\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex PullRegex = new(@"\u62c9\u8fd1\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex BleedRegex = new(@"\u6d41\u8840\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex VulnerableRegex = new(@"\u6613\u4f24\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex StunRegex = new(@"(?:\u51fb\u6655|\u7729\u6655)\s*(\d+)?", RegexOptions.Compiled);
        private static readonly Regex RetainRegex = new(@"\u4fdd\u7559", RegexOptions.Compiled);
        private static readonly Regex ExhaustRegex = new(@"\u6d88\u8017", RegexOptions.Compiled);
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

        private static readonly IReadOnlyList<HexCardDefinition> RewardPool = new[]
        {
            Attack,
            Defend,
            HeavyAttack,
            Brace,
            GuardUp,
            QuickStrike,
        };

        private static readonly IReadOnlyList<HexCardDefinition> CommonPool = new[]
        {
            Daze,
            Wound,
        };

        private static List<HexCardDefinition> s_loadedWarriorPool;
        private static List<HexCardDefinition> s_loadedPaladinPool;
        private static List<HexCardDefinition> s_loadedDruidPool;

        public static HexCardDefinition GetAttack() => Attack;
        public static HexCardDefinition GetDefend() => Defend;
        public static HexCardDefinition GetDaze() => Daze;
        public static HexCardDefinition GetWound() => Wound;
        public static IReadOnlyList<HexCardDefinition> GetRewardPool() => RewardPool;
        public static IReadOnlyList<HexCardDefinition> GetCommonPool() => CommonPool;
        public static IReadOnlyList<HexCardDefinition> GetWarriorPool()
        {
            if (s_loadedWarriorPool == null)
                s_loadedWarriorPool = LoadWarriorPoolFromExport();

            return s_loadedWarriorPool;
        }

        public static IReadOnlyList<HexCardDefinition> GetDruidPool()
        {
            if (s_loadedDruidPool == null)
                s_loadedDruidPool = LoadProfessionPoolFromExport(DruidExportPath, HexCardProfession.Druid, new List<HexCardDefinition>());

            return s_loadedDruidPool;
        }

        public static IReadOnlyList<HexCardDefinition> GetPaladinPool()
        {
            if (s_loadedPaladinPool == null)
                s_loadedPaladinPool = LoadProfessionPoolFromExport(PaladinExportPath, HexCardProfession.Paladin, new List<HexCardDefinition> { Attack, Defend });

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
            var whirlwind = GetCardById("C_01_001") ?? GetCardByName("旋风斩");
            return new List<HexCardDefinition>
            {
                Attack, Attack, Attack, Attack, Attack,
                Defend, Defend, Defend, Defend,
                whirlwind ?? Attack,
            };
        }

        public static List<HexCardDefinition> CreateMonsterDeck()
        {
            return new List<HexCardDefinition>
            {
                Attack, Attack, Attack, Attack, Attack,
                Defend, Defend, Defend, Defend,
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

        private static List<HexCardDefinition> LoadWarriorPoolFromExport()
        {
            return LoadProfessionPoolFromExport(WarriorExportPath, HexCardProfession.Warrior, new List<HexCardDefinition> { Attack, Defend });
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
                effectType = ParseEffectType(exportData.card_type),
                targetType = ParseTargetType(exportData),
                energyCost = ParseEnergyCost(exportData.cost),
                amount = ParseAmount(exportData.description),
                range = castRange,
                castRange = castRange,
                effectRadius = effectRadius,
                priority = ParseEffectType(exportData.card_type) == HexCardEffectType.Attack ? 1 : 2,
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

                candidates.Add(card);
            }

            return candidates;
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
                "Special" => HexCardType.Special,
                _ => HexCardType.Skill,
            };
        }

        private static HexCardEffectType ParseEffectType(string rawCardType)
        {
            return rawCardType == "Attack" ? HexCardEffectType.Attack : HexCardEffectType.Defend;
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
                HexCardType.Special => new Color(0.82f, 0.58f, 0.2f, 1f),
                _ => Color.white,
            };
        }
    }
}
