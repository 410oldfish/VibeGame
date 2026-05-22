using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class HexBattleUnit : MonoBehaviour
    {
        private enum StatusIconKind
        {
            Strength,
            Toughness,
            Agility,
            Wisdom,
            Humility,
            Luck,
            Vigor,
            Momentum,
            HolyShield,
            Immunity,
            Invincible,
            Deflect,
            Block,
            Thorns,
            Bleed,
            Vulnerable,
            Weak,
            Stun,
            Blind,
            Nausea,
            Curse,
            Allure,
            Taunt,
            Confusion,
            Burn,
            Cold,
            Fatigue,
            Paralysis,
            Slow,
            Frozen,
            Entangle,
            Bind,
            Phase,
            Rooted,
            ArmorBreak,
            Brittle,
            Disarm,
        }

        private sealed class StatusBadgeView
        {
            public GameObject root;
            public Image icon;
            public TextMeshProUGUI countText;
        }

        private const string MovingParameter = "IsMoving";
        private const string AttackTrigger = "Attack";
        private const string HitTrigger = "Hit";
        private const string DeathTrigger = "Death";
        private const float DefaultAttackDuration = 0.8f;
        private const float DefaultHitDuration = 0.45f;
        private const float DefaultDeathDuration = 1.1f;

        public HexBattleUnitState State { get; private set; }
        public HexDeckState Deck { get; } = new();
        public bool IsAlive => State != null && State.currentHealth > 0;

        private Animator _animator;
        private CapsuleCollider _targetCollider;
        private Canvas _healthCanvas;
        private Slider _healthSlider;
        private Image _healthFill;
        private Image _armorFill;
        private TextMeshProUGUI _healthText;
        private Image _armorIcon;
        private TextMeshProUGUI _armorText;
        private RectTransform _statusGrid;
        private readonly List<StatusBadgeView> _statusBadges = new();
        private Camera _mainCamera;
        private static Sprite s_armorIconSprite;
        private static readonly Dictionary<StatusIconKind, Sprite> s_statusIconSprites = new();
        private Renderer[] _modelRenderers = System.Array.Empty<Renderer>();
        private bool _deathVisualConsumed;
        public bool CanActThisTurn { get; private set; } = true;

        public void Initialize(HexBattleUnitState state, Animator animator, IEnumerable<HexCardDefinition> startingDeck)
        {
            State = state;
            _animator = animator;
            ResetBattleState();
            PrepareDeckForBattle(startingDeck);
            EnsureTargetCollider();
            EnsureHealthBar();
            CacheModelRenderers();
            RefreshHealthBar();
        }

        public void ResetBattleState()
        {
            if (State == null)
                return;

            State.armor = 0;
            State.bleed = 0;
            State.vulnerable = 0;
            State.weak = 0;
            State.stun = 0;
            State.blind = 0;
            State.nausea = 0;
            State.curse = 0;
            State.allure = 0;
            State.hasAllureSource = false;
            State.allureSourceCoord = default;
            State.taunt = 0;
            State.tauntActiveThisTurn = 0;
            State.hasTauntSource = false;
            State.tauntSourceCoord = default;
            State.confusion = 0;
            State.bind = 0;
            State.burn = 0;
            State.entangle = 0;
            State.armorBreak = 0;
            State.brittle = 0;
            State.disarm = 0;
            State.cold = 0;
            State.fatigue = 0;
            State.paralysis = 0;
            State.paralysisActiveThisTurn = 0;
            State.slow = 0;
            State.frozen = 0;
            State.strength = 0;
            State.toughness = 0;
            State.agility = 0;
            State.wisdom = 0;
            State.humility = 0;
            State.luck = 0;
            State.vigor = 0;
            State.holyShield = 0;
            State.immunity = 0;
            State.invincible = 0;
            State.deflect = 0;
            State.block = 0;
            State.thorns = 0;
            State.skillCooldown = 0;
            State.nextAttackDrawCards = 0;
            State.nextAttackApplyVulnerable = 0;
            State.energy = 0;
            State.currentMovePoints = 0;
            State.weapon = HexWeaponType.None;
            State.drawDisabledThisTurn = false;
            State.attackRepeatBonusThisTurn = 0;
            State.damageDealtThisTurn = 0;
            State.armorOnAttackCardThisTurn = 0;
            State.armorOnSkillCard = 0;
            State.firstAttackBurnAmount = 0;
            State.firstAttackBonusPending = false;
            State.weaponSkillFree = false;
            State.extraEnergyPerTurn = 0;
            State.extraMovePerTurn = 0;
            State.cannotUseSkills = false;
            State.weaponPassivesDoubleThisTurn = false;
            State.consumeWeaponAtEndTurn = false;
            State.allWeaponsEquipped = false;
            State.negateNextEnemyAttack = false;
            State.liquidArmorToVigor = false;
            State.burningAuraRadius = 0;
            State.gainStrengthOnSelfDamage = false;
            State.drawOnExhaust = false;
            State.gainMoveOnStrengthOrToughness = false;
            State.armorOnExhaustCost = 0;
            State.axeAppliesArmorBreak = false;
            State.hammerDoubleArmorDamage = false;
            State.swordAppliesBrittle = false;
            State.phaseMovement = 0;
            State.druidForm = HexDruidFormType.None;
            State.momentum = 0;
            State.druidBonusArmorOnNextTransform = 0;
            State.cardsPlayedThisTurn = 0;
            State.rooted = false;
            State.isPlant = false;
            _deathVisualConsumed = false;
            CanActThisTurn = true;
        }

        public void PrepareDeckForBattle(IEnumerable<HexCardDefinition> startingDeck)
        {
            Deck.ClearBattleState();
            Deck.LoadStartingDeck(startingDeck);
        }

        public void BeginTurn()
        {
            CanActThisTurn = true;
            if (State.skillCooldown > 0)
                State.skillCooldown = Mathf.Max(0, State.skillCooldown - 1);

            if (State.burn > 0)
            {
                if (State.druidForm != HexDruidFormType.LavaLizard)
                    ApplyDamage(State.burn);
                State.burn /= 2;
            }

            if (!IsAlive)
            {
                CanActThisTurn = false;
                return;
            }

            if (State.frozen > 0)
            {
                State.energy = 0;
                State.currentMovePoints = 0;
                CanActThisTurn = false;
                RefreshHealthBar();
                return;
            }

            if (State.stun > 0)
            {
                State.stun = Mathf.Max(0, State.stun - 1);
                State.energy = 0;
                State.currentMovePoints = 0;
                CanActThisTurn = false;
                return;
            }

            State.energy = State.maxEnergy;
            State.energy += Mathf.Max(0, State.extraEnergyPerTurn);
            if (State.fatigue > 0)
            {
                State.energy = Mathf.Max(0, State.energy - State.fatigue);
                State.fatigue = 0;
            }
            State.currentMovePoints = State.maxMovePoints + Mathf.Max(0, State.extraMovePerTurn);
            if (State.slow > 0)
            {
                State.currentMovePoints = Mathf.Max(0, State.currentMovePoints - State.slow);
                State.slow = 0;
            }
            State.attackRepeatBonusThisTurn = 0;
            State.damageDealtThisTurn = 0;
            State.armorOnAttackCardThisTurn = 0;
            State.drawDisabledThisTurn = false;
            State.weaponPassivesDoubleThisTurn = false;
            State.firstAttackBonusPending = State.firstAttackBurnAmount > 0;
            State.cardsPlayedThisTurn = 0;
            State.paralysisActiveThisTurn = State.paralysis;
            State.paralysis = 0;
            State.tauntActiveThisTurn = State.taunt;
            State.taunt = 0;
            if (!State.drawDisabledThisTurn)
                Deck.DrawCards(State.drawPerTurn + Mathf.Max(0, State.wisdom));

            ApplyColdToHand();
            ApplyLuckToHand();
        }

        public void EndTurn()
        {
            var extraRetainedIds = new HashSet<string>();
            if (State.humility > 0)
            {
                for (int i = Deck.Hand.Count - 1; i >= 0 && extraRetainedIds.Count < State.humility; i--)
                {
                    var card = Deck.Hand[i];
                    if (card == null || HexCardLibrary.HasKeyword(card.definition, HexCardKeywordType.Retain))
                        continue;

                    extraRetainedIds.Add(card.runtimeId);
                }
            }

            Deck.DiscardHand(
                card => HexCardLibrary.HasKeyword(card.definition, HexCardKeywordType.Retain) || extraRetainedIds.Contains(card.runtimeId),
                card => HexCardLibrary.HasKeyword(card.definition, HexCardKeywordType.Void));
            for (int i = 0; i < Deck.Hand.Count; i++)
            {
                Deck.Hand[i].temporaryCostModifier = 0;
                Deck.Hand[i].costsNoEnergyThisTurn = false;
            }
            if (State.vulnerable > 0)
                State.vulnerable = Mathf.Max(0, State.vulnerable - 1);
            if (State.weak > 0)
                State.weak = Mathf.Max(0, State.weak - 1);
            if (State.entangle > 0)
                State.entangle = Mathf.Max(0, State.entangle - 1);
            if (State.immunity > 0)
                State.immunity = Mathf.Max(0, State.immunity - 1);
            if (State.invincible > 0)
                State.invincible = Mathf.Max(0, State.invincible - 1);
            if (State.frozen > 0)
                State.frozen = Mathf.Max(0, State.frozen - 1);
            if (State.disarm > 0)
                State.disarm = Mathf.Max(0, State.disarm - 1);
            if (State.bind > 0)
                State.bind = Mathf.Max(0, State.bind - 1);
            if (State.phaseMovement > 0)
                State.phaseMovement = Mathf.Max(0, State.phaseMovement - 1);
            State.paralysisActiveThisTurn = 0;
            State.tauntActiveThisTurn = 0;
            if (State.consumeWeaponAtEndTurn)
            {
                State.weapon = HexWeaponType.None;
                State.allWeaponsEquipped = false;
                State.consumeWeaponAtEndTurn = false;
            }
            CanActThisTurn = true;
        }

        public bool CanPay(HexCardInstance card)
        {
            if (card == null || card.definition == null)
                return false;
            if (State.disarm > 0 && card.definition.cardType == HexCardType.Attack)
                return false;
            if (State.frozen > 0)
                return false;
            return State.energy >= GetCardEnergyCost(card);
        }

        public void SpendEnergy(int amount)
        {
            State.energy = Mathf.Max(0, State.energy - amount);
        }

        public void GainArmor(int amount)
        {
            int finalAmount = amount + State.toughness;
            if (State.brittle > 0)
                finalAmount = Mathf.FloorToInt(finalAmount * 0.5f);
            State.armor += Mathf.Max(0, finalAmount);
            RefreshHealthBar();
        }

        public void GainStrength(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.strength += Mathf.Max(0, amount);
            if (State.gainMoveOnStrengthOrToughness && amount > 0)
                State.currentMovePoints += amount;
            RefreshHealthBar();
        }

        public void GainToughness(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.toughness += Mathf.Max(0, amount);
            if (State.gainMoveOnStrengthOrToughness && amount > 0)
                State.currentMovePoints += amount;
            RefreshHealthBar();
        }

        public void GainVigor(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.vigor += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainMomentum(int amount, int maxStacks = 2)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.momentum = Mathf.Clamp(State.momentum + Mathf.Max(0, amount), 0, Mathf.Max(0, maxStacks));
            RefreshHealthBar();
        }

        public void GainInvincible(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.invincible += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainThorns(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.thorns += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
                return;

            State.currentHealth = Mathf.Min(State.maxHealth, State.currentHealth + amount);
            RefreshHealthBar();
        }

        public void SpendMovePoints(int amount)
        {
            State.currentMovePoints = Mathf.Max(0, State.currentMovePoints - Mathf.Max(0, amount));
        }

        public int ApplyDamage(int amount)
        {
            if (State.frozen > 0 || State.invincible > 0)
            {
                RefreshHealthBar();
                return 0;
            }

            if (State.holyShield > 0)
            {
                State.holyShield = Mathf.Max(0, State.holyShield - 1);
                RefreshHealthBar();
                return 0;
            }

            int remaining = Mathf.Max(0, amount);
            int effectiveArmor = State.armorBreak > 0 ? Mathf.CeilToInt(State.armor * 0.5f) : State.armor;
            int absorbed = Mathf.Min(effectiveArmor, remaining);
            State.armor -= absorbed;
            remaining -= absorbed;
            State.currentHealth = Mathf.Max(0, State.currentHealth - remaining);
            RefreshHealthBar();
            return remaining;
        }

        public void ApplyBleed(int amount)
        {
            State.bleed += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyVulnerable(int amount)
        {
            State.vulnerable += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyWeak(int amount)
        {
            State.weak += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyStun(int amount)
        {
            State.stun += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyBurn(int amount)
        {
            State.burn += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyEntangle(int amount)
        {
            State.entangle += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyPhase(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.phaseMovement += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public int GetCardEnergyCost(HexCardInstance card)
        {
            if (card == null || card.definition == null)
                return 0;

            if (card.costsNoEnergyThisTurn)
                return 0;

            int cost = card.definition.energyCost < 0 ? State.energy : card.definition.energyCost;
            cost += card.temporaryCostModifier;
            if (State.cardsPlayedThisTurn == 0 && State.agility > 0)
                cost -= State.agility;
            if (State.paralysisActiveThisTurn > 0)
                cost += State.paralysisActiveThisTurn;
            return Mathf.Max(0, cost);
        }

        public void NotifyCardPlayed()
        {
            State.cardsPlayedThisTurn += 1;
        }

        public void ApplyBlind(int amount)
        {
            State.blind += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyNausea(int amount)
        {
            State.nausea += AdjustNegativeAmount(amount, applyNauseaBonus: false, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyCurse(int amount)
        {
            State.curse += AdjustNegativeAmount(amount, applyNauseaBonus: false, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyAllure(int amount, HexAxialCoord sourceCoord)
        {
            int applied = AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            if (applied <= 0)
                return;

            State.allure += applied;
            State.hasAllureSource = true;
            State.allureSourceCoord = sourceCoord;
            RefreshHealthBar();
        }

        public void ClearAllure()
        {
            State.allure = 0;
            State.hasAllureSource = false;
        }

        public void ApplyTaunt(int amount, HexAxialCoord sourceCoord)
        {
            int applied = AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            if (applied <= 0)
                return;

            State.taunt += applied;
            State.hasTauntSource = true;
            State.tauntSourceCoord = sourceCoord;
            RefreshHealthBar();
        }

        public void ClearTaunt()
        {
            State.taunt = 0;
            State.tauntActiveThisTurn = 0;
            State.hasTauntSource = false;
        }

        public void ApplyConfusion(int amount)
        {
            State.confusion += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyBind(int amount)
        {
            State.bind += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyCorruption(int amount)
        {
            int applied = AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            if (applied <= 0)
                return;

            for (int i = 0; i < applied; i++)
            {
                switch (Random.Range(0, 12))
                {
                    case 0:
                        ApplyBleed(1);
                        break;
                    case 1:
                        ApplyVulnerable(1);
                        break;
                    case 2:
                        ApplyWeak(1);
                        break;
                    case 3:
                        ApplyBlind(1);
                        break;
                    case 4:
                        ApplyNausea(1);
                        break;
                    case 5:
                        ApplyCurse(1);
                        break;
                    case 6:
                        ApplyBurn(1);
                        break;
                    case 7:
                        ApplyEntangle(1);
                        break;
                    case 8:
                        ApplyCold(1);
                        break;
                    case 9:
                        ApplyFatigue(1);
                        break;
                    case 10:
                        ApplyParalysis(1);
                        break;
                    default:
                        ApplySlow(1);
                        break;
                }
            }

            RefreshHealthBar();
        }

        public void ApplyCold(int amount)
        {
            State.cold += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyFatigue(int amount)
        {
            State.fatigue += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyParalysis(int amount)
        {
            State.paralysis += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplySlow(int amount)
        {
            State.slow += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void ApplyFrozen(int amount)
        {
            State.frozen += AdjustNegativeAmount(amount, applyNauseaBonus: true, allowWhenImmune: false, allowWhenFrozen: false);
            RefreshHealthBar();
        }

        public void GainAgility(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.agility += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainWisdom(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.wisdom += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainHumility(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.humility += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainLuck(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.luck += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainHolyShield(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.holyShield += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainImmunity(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.immunity += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainDeflect(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.deflect += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        public void GainBlock(int amount)
        {
            if (!CanReceivePositiveStatus())
                return;

            State.block += Mathf.Max(0, amount);
            RefreshHealthBar();
        }

        private bool CanReceivePositiveStatus()
        {
            return State != null && State.curse <= 0;
        }

        private int AdjustNegativeAmount(int amount, bool applyNauseaBonus, bool allowWhenImmune, bool allowWhenFrozen)
        {
            int adjusted = Mathf.Max(0, amount);
            if (adjusted <= 0 || State == null)
                return 0;
            if (!allowWhenFrozen && State.frozen > 0)
                return 0;
            if (!allowWhenImmune && State.immunity > 0)
                return 0;
            if (applyNauseaBonus && State.nausea > 0)
                adjusted *= 2;
            return adjusted;
        }

        private void ApplyColdToHand()
        {
            if (State == null || State.cold <= 0 || Deck.Hand.Count == 0)
                return;

            var candidates = new List<HexCardInstance>(Deck.Hand);
            for (int i = 0; i < State.cold && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                candidates[index].temporaryCostModifier += 1;
                candidates.RemoveAt(index);
            }

            State.cold = 0;
        }

        private void ApplyLuckToHand()
        {
            if (State == null || State.luck <= 0 || Deck.Hand.Count == 0)
                return;

            var candidates = new List<HexCardInstance>(Deck.Hand);
            for (int i = 0; i < State.luck && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                candidates[index].costsNoEnergyThisTurn = true;
                candidates.RemoveAt(index);
            }
        }

        public bool CanUseWeaponSkill(int energyCost)
        {
            return IsAlive && CanActThisTurn && State.energy >= energyCost && State.skillCooldown <= 0;
        }

        public void SpendWeaponSkill(int energyCost, int cooldown, HexWeaponType weaponType)
        {
            SpendEnergy(energyCost);
            State.skillCooldown = Mathf.Max(0, cooldown);
            State.weapon = weaponType;
        }

        public void QueueNextAttackDraw(int amount)
        {
            State.nextAttackDrawCards += Mathf.Max(0, amount);
        }

        public void QueueNextAttackVulnerable(int amount)
        {
            State.nextAttackApplyVulnerable += Mathf.Max(0, amount);
        }

        public int ConsumeNextAttackDraw()
        {
            int amount = State.nextAttackDrawCards;
            State.nextAttackDrawCards = 0;
            return amount;
        }

        public int ConsumeNextAttackVulnerable()
        {
            int amount = State.nextAttackApplyVulnerable;
            State.nextAttackApplyVulnerable = 0;
            return amount;
        }

        public IEnumerator MoveAlongPath(HexGrid grid, List<HexAxialCoord> path, float unitYOffset, float moveSpeed, float stopDelay, System.Action<HexAxialCoord> onStepReached = null, bool jumpMovement = false)
        {
            if (path == null || path.Count < 2)
                yield break;

            SetMoving(true);
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 startPos = grid.AxialToWorld(path[i - 1]) + Vector3.up * unitYOffset;
                Vector3 endPos = grid.AxialToWorld(path[i]) + Vector3.up * unitYOffset;
                FaceDirection(endPos - startPos);

                float t = 0f;
                float distance = Vector3.Distance(startPos, endPos);
                float duration = distance / Mathf.Max(0.01f, moveSpeed);
                transform.position = startPos;

                while (t < 1f)
                {
                    t += Time.deltaTime / duration;
                    float easedT = Mathf.SmoothStep(0f, 1f, t);
                    Vector3 position = Vector3.Lerp(startPos, endPos, easedT);
                    if (jumpMovement)
                    {
                        float jumpArc = Mathf.Sin(easedT * Mathf.PI) * Mathf.Max(0.35f, distance * 0.12f);
                        position.y += jumpArc;
                    }

                    transform.position = position;
                    yield return null;
                }

                transform.position = endPos;
                State.coord = path[i];
                onStepReached?.Invoke(State.coord);

                if (stopDelay > 0f)
                    yield return new WaitForSeconds(stopDelay);
            }

            SetMoving(false);
        }

        public void SnapTo(HexGrid grid, float unitYOffset)
        {
            transform.position = grid.AxialToWorld(State.coord) + Vector3.up * unitYOffset;
            RefreshHealthBar();
        }

        public void RefreshLabel()
        {
        }

        public Vector3 GetTargetPoint()
        {
            return transform.position + Vector3.up * 1.2f;
        }

        public void FaceTarget(Vector3 worldPoint)
        {
            FaceDirection(worldPoint - transform.position);
        }

        public void PlayAttackAnimation()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;

            _animator.ResetTrigger(HitTrigger);
            _animator.SetBool(MovingParameter, false);
            _animator.SetTrigger(AttackTrigger);
        }

        public void PlayHitAnimation()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;

            _animator.ResetTrigger(AttackTrigger);
            _animator.SetBool(MovingParameter, false);
            _animator.SetTrigger(HitTrigger);
        }

        public void PlayDeathAnimation()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return;

            _animator.ResetTrigger(AttackTrigger);
            _animator.ResetTrigger(HitTrigger);
            _animator.SetBool(MovingParameter, false);
            _animator.SetTrigger(DeathTrigger);
        }

        public float GetAttackDuration()
        {
            return GetClipDuration(DefaultAttackDuration, "attack", "slash", "melee", "sword");
        }

        public float GetHitDuration()
        {
            return GetClipDuration(DefaultHitDuration, "hit", "hurt", "damage", "impact", "stagger");
        }

        public float GetDeathDuration()
        {
            return GetClipDuration(DefaultDeathDuration, "death", "die", "dead", "defeat", "knockout");
        }

        public IEnumerator PlayDeathAndCleanup()
        {
            if (_deathVisualConsumed)
                yield break;

            _deathVisualConsumed = true;
            if (_targetCollider != null)
                _targetCollider.enabled = false;

            PlayDeathAnimation();
            float duration = GetDeathDuration();
            yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

            if (_healthCanvas != null)
                _healthCanvas.gameObject.SetActive(false);

            for (int i = 0; i < _modelRenderers.Length; i++)
            {
                if (_modelRenderers[i] != null)
                    _modelRenderers[i].enabled = false;
            }

            gameObject.SetActive(false);
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void SetMoving(bool isMoving)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetBool(MovingParameter, isMoving);
        }

        private void CacheModelRenderers()
        {
            _modelRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private float GetClipDuration(float fallback, params string[] terms)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return fallback;

            var clips = _animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;

                string clipName = clips[i].name.ToLowerInvariant();
                for (int termIndex = 0; termIndex < terms.Length; termIndex++)
                {
                    if (clipName.Contains(terms[termIndex]))
                        return clips[i].length;
                }
            }

            return fallback;
        }

        private void LateUpdate()
        {
            if (_healthCanvas == null)
                return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera != null)
                _healthCanvas.transform.forward = _mainCamera.transform.forward;

            RefreshHealthBar();
        }

        private void EnsureTargetCollider()
        {
            _targetCollider = GetComponent<CapsuleCollider>();
            if (_targetCollider != null)
            {
                _targetCollider.isTrigger = false;
                _targetCollider.center = new Vector3(0f, 1f, 0f);
                _targetCollider.radius = 0.45f;
                _targetCollider.height = 2.1f;
                return;
            }

            _targetCollider = gameObject.AddComponent<CapsuleCollider>();
            _targetCollider.isTrigger = false;
            _targetCollider.center = new Vector3(0f, 1f, 0f);
            _targetCollider.radius = 0.45f;
            _targetCollider.height = 2.1f;
        }

        private void EnsureHealthBar()
        {
            var canvasGO = new GameObject("HealthBar", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            canvasGO.transform.localScale = Vector3.one * 0.01f;

            _healthCanvas = canvasGO.GetComponent<Canvas>();
            _healthCanvas.renderMode = RenderMode.WorldSpace;
            _healthCanvas.worldCamera = Camera.main;

            var canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(136f, 60f);

            var sliderGO = new GameObject("HealthSlider", typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(canvasGO.transform, false);
            var sliderRect = sliderGO.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0f);
            sliderRect.anchorMax = new Vector2(0.5f, 0f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, 8f);
            sliderRect.sizeDelta = new Vector2(120f, 16f);
            _healthSlider = sliderGO.GetComponent<Slider>();
            _healthSlider.minValue = 0f;
            _healthSlider.maxValue = 1f;
            _healthSlider.interactable = false;
            _healthSlider.transition = Selectable.Transition.None;
            _healthSlider.direction = Slider.Direction.LeftToRight;
            _healthSlider.handleRect = null;

            var backgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundGO.transform.SetParent(sliderGO.transform, false);
            var backgroundRect = backgroundGO.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var sliderBackground = backgroundGO.GetComponent<Image>();
            sliderBackground.color = new Color(0.18f, 0.12f, 0.12f, 0.92f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            var fillBackgroundGO = new GameObject("FillBackground", typeof(RectTransform), typeof(Image));
            fillBackgroundGO.transform.SetParent(fillArea.transform, false);
            var fillBackgroundRect = fillBackgroundGO.GetComponent<RectTransform>();
            fillBackgroundRect.anchorMin = Vector2.zero;
            fillBackgroundRect.anchorMax = Vector2.one;
            fillBackgroundRect.offsetMin = Vector2.zero;
            fillBackgroundRect.offsetMax = Vector2.zero;
            fillBackgroundGO.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.75f);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(fillArea.transform, false);
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            _healthFill = fillGO.GetComponent<Image>();
            _healthFill.color = new Color(0.8f, 0.18f, 0.18f, 1f);
            _healthSlider.fillRect = fillRect;
            _healthSlider.targetGraphic = _healthFill;

            var armorArea = new GameObject("ArmorArea", typeof(RectTransform));
            armorArea.transform.SetParent(sliderGO.transform, false);
            var armorAreaRect = armorArea.GetComponent<RectTransform>();
            armorAreaRect.anchorMin = Vector2.zero;
            armorAreaRect.anchorMax = Vector2.one;
            armorAreaRect.offsetMin = new Vector2(2f, 2f);
            armorAreaRect.offsetMax = new Vector2(-2f, -2f);

            var armorFillGO = new GameObject("ArmorFill", typeof(RectTransform), typeof(Image));
            armorFillGO.transform.SetParent(armorArea.transform, false);
            var armorFillRect = armorFillGO.GetComponent<RectTransform>();
            armorFillRect.anchorMin = new Vector2(0f, 0f);
            armorFillRect.anchorMax = new Vector2(0f, 1f);
            armorFillRect.pivot = new Vector2(0f, 0.5f);
            armorFillRect.anchoredPosition = Vector2.zero;
            armorFillRect.sizeDelta = Vector2.zero;
            _armorFill = armorFillGO.GetComponent<Image>();
            _armorFill.color = new Color(0.22f, 0.7f, 1f, 0.72f);
            _armorFill.type = Image.Type.Simple;

            var textGO = new GameObject("HealthText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(sliderGO.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(116f, 16f);
            _healthText = textGO.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(_healthText);
            _healthText.fontSize = 12f;
            _healthText.fontStyle = FontStyles.Bold;
            _healthText.alignment = TextAlignmentOptions.Center;
            _healthText.color = new Color(0.06f, 0.06f, 0.06f, 1f);

            var armorBadgeGO = new GameObject("ArmorBadge", typeof(RectTransform));
            armorBadgeGO.transform.SetParent(canvasGO.transform, false);
            var armorBadgeRect = armorBadgeGO.GetComponent<RectTransform>();
            armorBadgeRect.anchorMin = new Vector2(0f, 0.5f);
            armorBadgeRect.anchorMax = new Vector2(0f, 0.5f);
            armorBadgeRect.pivot = new Vector2(1f, 0.5f);
            armorBadgeRect.anchoredPosition = new Vector2(-8f, 0f);
            armorBadgeRect.sizeDelta = new Vector2(76f, 32f);

            var armorIconGO = new GameObject("ArmorIcon", typeof(RectTransform), typeof(Image));
            armorIconGO.transform.SetParent(armorBadgeGO.transform, false);
            var armorIconRect = armorIconGO.GetComponent<RectTransform>();
            armorIconRect.anchorMin = new Vector2(0f, 0.5f);
            armorIconRect.anchorMax = new Vector2(0f, 0.5f);
            armorIconRect.pivot = new Vector2(0f, 0.5f);
            armorIconRect.anchoredPosition = Vector2.zero;
            armorIconRect.sizeDelta = new Vector2(32f, 32f);
            _armorIcon = armorIconGO.GetComponent<Image>();
            _armorIcon.sprite = GetArmorIconSprite();
            _armorIcon.preserveAspect = true;
            _armorIcon.color = Color.white;

            var armorTextGO = new GameObject("ArmorText", typeof(RectTransform), typeof(TextMeshProUGUI));
            armorTextGO.transform.SetParent(armorBadgeGO.transform, false);
            var armorTextRect = armorTextGO.GetComponent<RectTransform>();
            armorTextRect.anchorMin = new Vector2(0f, 0.5f);
            armorTextRect.anchorMax = new Vector2(0f, 0.5f);
            armorTextRect.pivot = new Vector2(0f, 0.5f);
            armorTextRect.anchoredPosition = new Vector2(34f, 0f);
            armorTextRect.sizeDelta = new Vector2(40f, 28f);
            _armorText = armorTextGO.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(_armorText);
            _armorText.fontSize = 12f;
            _armorText.fontStyle = FontStyles.Bold;
            _armorText.alignment = TextAlignmentOptions.Left;
            _armorText.color = new Color(0.08f, 0.18f, 0.3f, 1f);

            var statusGridGO = new GameObject("StatusGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            statusGridGO.transform.SetParent(canvasGO.transform, false);
            _statusGrid = statusGridGO.GetComponent<RectTransform>();
            _statusGrid.anchorMin = new Vector2(0.5f, 1f);
            _statusGrid.anchorMax = new Vector2(0.5f, 1f);
            _statusGrid.pivot = new Vector2(0.5f, 1f);
            _statusGrid.anchoredPosition = new Vector2(0f, -2f);
            _statusGrid.sizeDelta = new Vector2(132f, 38f);

            var statusLayout = statusGridGO.GetComponent<GridLayoutGroup>();
            statusLayout.cellSize = new Vector2(20f, 20f);
            statusLayout.spacing = new Vector2(2f, 2f);
            statusLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            statusLayout.constraintCount = 6;
            statusLayout.childAlignment = TextAnchor.UpperCenter;
        }

        private static Sprite GetArmorIconSprite()
        {
            if (s_armorIconSprite != null)
                return s_armorIconSprite;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ArmorShieldIcon"
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var fill = new Color(0.23f, 0.67f, 0.98f, 1f);
            var outline = new Color(0.83f, 0.95f, 1f, 1f);

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            Vector2[] polygon =
            {
                new Vector2(16f, 29f),
                new Vector2(26f, 25f),
                new Vector2(24f, 13f),
                new Vector2(16f, 4f),
                new Vector2(8f, 13f),
                new Vector2(6f, 25f),
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = PointInPolygon(p, polygon);
                    if (!inside)
                        continue;

                    bool border = false;
                    for (int i = 0; i < polygon.Length; i++)
                    {
                        var a = polygon[i];
                        var b = polygon[(i + 1) % polygon.Length];
                        if (DistanceToSegment(p, a, b) <= 1.3f)
                        {
                            border = true;
                            break;
                        }
                    }

                    pixels[y * size + x] = border ? outline : fill;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            s_armorIconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return s_armorIconSprite;
        }

        private static Sprite GetStatusIconSprite(StatusIconKind kind)
        {
            if (s_statusIconSprites.TryGetValue(kind, out var sprite) && sprite != null)
                return sprite;

            sprite = CreateStatusIconSprite(kind);
            s_statusIconSprites[kind] = sprite;
            return sprite;
        }

        private static Sprite CreateStatusIconSprite(StatusIconKind kind)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Status_{kind}"
            };

            var pixels = new Color[size * size];
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            Color bg = new Color(0.14f, 0.16f, 0.2f, 0.96f);
            Color border = new Color(0.85f, 0.9f, 0.96f, 1f);
            Color fg = Color.white;

            switch (kind)
            {
                case StatusIconKind.Strength:
                    bg = new Color(0.72f, 0.26f, 0.18f, 0.96f);
                    fg = new Color(1f, 0.95f, 0.88f, 1f);
                    break;
                case StatusIconKind.Toughness:
                    bg = new Color(0.19f, 0.42f, 0.78f, 0.96f);
                    fg = new Color(0.92f, 0.98f, 1f, 1f);
                    break;
                case StatusIconKind.Agility:
                    bg = new Color(0.12f, 0.64f, 0.74f, 0.96f);
                    fg = new Color(0.92f, 1f, 1f, 1f);
                    break;
                case StatusIconKind.Wisdom:
                    bg = new Color(0.34f, 0.28f, 0.74f, 0.96f);
                    fg = new Color(0.96f, 0.94f, 1f, 1f);
                    break;
                case StatusIconKind.Humility:
                    bg = new Color(0.35f, 0.58f, 0.78f, 0.96f);
                    fg = new Color(0.94f, 0.98f, 1f, 1f);
                    break;
                case StatusIconKind.Luck:
                    bg = new Color(0.18f, 0.68f, 0.48f, 0.96f);
                    fg = new Color(0.96f, 1f, 0.94f, 1f);
                    break;
                case StatusIconKind.Vigor:
                    bg = new Color(0.16f, 0.64f, 0.38f, 0.96f);
                    fg = new Color(0.94f, 1f, 0.95f, 1f);
                    break;
                case StatusIconKind.Momentum:
                    bg = new Color(0.84f, 0.52f, 0.14f, 0.96f);
                    fg = new Color(1f, 0.97f, 0.88f, 1f);
                    break;
                case StatusIconKind.HolyShield:
                    bg = new Color(0.96f, 0.78f, 0.2f, 0.96f);
                    fg = new Color(0.42f, 0.3f, 0.04f, 1f);
                    border = new Color(1f, 0.95f, 0.62f, 1f);
                    break;
                case StatusIconKind.Immunity:
                    bg = new Color(0.22f, 0.7f, 0.78f, 0.96f);
                    fg = new Color(0.93f, 1f, 1f, 1f);
                    break;
                case StatusIconKind.Invincible:
                    bg = new Color(0.66f, 0.48f, 0.14f, 0.96f);
                    fg = new Color(1f, 0.96f, 0.78f, 1f);
                    break;
                case StatusIconKind.Deflect:
                    bg = new Color(0.22f, 0.48f, 0.7f, 0.96f);
                    fg = new Color(0.94f, 0.98f, 1f, 1f);
                    break;
                case StatusIconKind.Block:
                    bg = new Color(0.24f, 0.56f, 0.92f, 0.96f);
                    fg = new Color(0.96f, 0.99f, 1f, 1f);
                    break;
                case StatusIconKind.Thorns:
                    bg = new Color(0.22f, 0.56f, 0.24f, 0.96f);
                    fg = new Color(0.95f, 1f, 0.95f, 1f);
                    break;
                case StatusIconKind.Bleed:
                    bg = new Color(0.7f, 0.12f, 0.16f, 0.96f);
                    fg = new Color(1f, 0.93f, 0.94f, 1f);
                    break;
                case StatusIconKind.Vulnerable:
                    bg = new Color(0.76f, 0.44f, 0.1f, 0.96f);
                    fg = new Color(1f, 0.95f, 0.88f, 1f);
                    break;
                case StatusIconKind.Weak:
                    bg = new Color(0.43f, 0.36f, 0.68f, 0.96f);
                    fg = new Color(0.95f, 0.94f, 1f, 1f);
                    break;
                case StatusIconKind.Stun:
                    bg = new Color(0.86f, 0.76f, 0.16f, 0.96f);
                    fg = new Color(0.34f, 0.26f, 0.06f, 1f);
                    border = new Color(1f, 0.95f, 0.62f, 1f);
                    break;
                case StatusIconKind.Blind:
                    bg = new Color(0.26f, 0.26f, 0.3f, 0.96f);
                    fg = new Color(0.96f, 0.96f, 1f, 1f);
                    break;
                case StatusIconKind.Nausea:
                    bg = new Color(0.38f, 0.58f, 0.16f, 0.96f);
                    fg = new Color(0.96f, 1f, 0.92f, 1f);
                    break;
                case StatusIconKind.Curse:
                    bg = new Color(0.4f, 0.16f, 0.54f, 0.96f);
                    fg = new Color(0.97f, 0.92f, 1f, 1f);
                    break;
                case StatusIconKind.Allure:
                    bg = new Color(0.86f, 0.38f, 0.56f, 0.96f);
                    fg = new Color(1f, 0.95f, 0.97f, 1f);
                    break;
                case StatusIconKind.Taunt:
                    bg = new Color(0.78f, 0.3f, 0.14f, 0.96f);
                    fg = new Color(1f, 0.94f, 0.88f, 1f);
                    break;
                case StatusIconKind.Confusion:
                    bg = new Color(0.5f, 0.36f, 0.72f, 0.96f);
                    fg = new Color(0.97f, 0.95f, 1f, 1f);
                    break;
                case StatusIconKind.Burn:
                    bg = new Color(0.88f, 0.38f, 0.08f, 0.96f);
                    fg = new Color(1f, 0.96f, 0.85f, 1f);
                    break;
                case StatusIconKind.Cold:
                    bg = new Color(0.22f, 0.58f, 0.86f, 0.96f);
                    fg = new Color(0.94f, 0.99f, 1f, 1f);
                    break;
                case StatusIconKind.Fatigue:
                    bg = new Color(0.48f, 0.34f, 0.2f, 0.96f);
                    fg = new Color(1f, 0.96f, 0.9f, 1f);
                    break;
                case StatusIconKind.Paralysis:
                    bg = new Color(0.88f, 0.72f, 0.18f, 0.96f);
                    fg = new Color(0.34f, 0.26f, 0.08f, 1f);
                    break;
                case StatusIconKind.Slow:
                    bg = new Color(0.34f, 0.54f, 0.78f, 0.96f);
                    fg = new Color(0.94f, 0.98f, 1f, 1f);
                    break;
                case StatusIconKind.Frozen:
                    bg = new Color(0.66f, 0.9f, 0.98f, 0.96f);
                    fg = new Color(0.12f, 0.34f, 0.52f, 1f);
                    border = new Color(0.9f, 0.98f, 1f, 1f);
                    break;
                case StatusIconKind.Entangle:
                    bg = new Color(0.19f, 0.5f, 0.24f, 0.96f);
                    fg = new Color(0.92f, 1f, 0.92f, 1f);
                    break;
                case StatusIconKind.Bind:
                    bg = new Color(0.2f, 0.42f, 0.22f, 0.96f);
                    fg = new Color(0.92f, 1f, 0.92f, 1f);
                    break;
                case StatusIconKind.Phase:
                    bg = new Color(0.22f, 0.7f, 0.68f, 0.96f);
                    fg = new Color(0.94f, 1f, 1f, 1f);
                    break;
                case StatusIconKind.Rooted:
                    bg = new Color(0.34f, 0.46f, 0.16f, 0.96f);
                    fg = new Color(0.94f, 1f, 0.92f, 1f);
                    break;
                case StatusIconKind.ArmorBreak:
                    bg = new Color(0.52f, 0.22f, 0.1f, 0.96f);
                    fg = new Color(1f, 0.95f, 0.9f, 1f);
                    break;
                case StatusIconKind.Brittle:
                    bg = new Color(0.44f, 0.44f, 0.5f, 0.96f);
                    fg = new Color(0.96f, 0.96f, 1f, 1f);
                    break;
                case StatusIconKind.Disarm:
                    bg = new Color(0.58f, 0.2f, 0.2f, 0.96f);
                    fg = new Color(1f, 0.94f, 0.94f, 1f);
                    break;
            }

            FillCircle(pixels, size, new Vector2(16f, 16f), 14.5f, bg);
            DrawCircleOutline(pixels, size, new Vector2(16f, 16f), 14.5f, 1.2f, border);

            switch (kind)
            {
                case StatusIconKind.Strength:
                    DrawSwordSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Toughness:
                    DrawShieldSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Agility:
                    DrawSparkSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Wisdom:
                    DrawStarSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Humility:
                    DrawShieldSymbol(pixels, size, fg);
                    DrawDownSlashSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Luck:
                    DrawStarSymbol(pixels, size, fg);
                    DrawPlusSymbol(pixels, size, bg * 0.5f + Color.black * 0.5f);
                    break;
                case StatusIconKind.Vigor:
                    DrawPlusSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Momentum:
                    DrawSparkSymbol(pixels, size, fg);
                    DrawLine(pixels, size, new Vector2(9f, 21f), new Vector2(23f, 11f), 2.2f, fg);
                    break;
                case StatusIconKind.HolyShield:
                    DrawShieldSymbol(pixels, size, fg);
                    DrawStarSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Immunity:
                    DrawShieldSymbol(pixels, size, fg);
                    DrawPlusSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Invincible:
                    DrawStarSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Deflect:
                    DrawShieldSymbol(pixels, size, fg);
                    DrawCrossBarSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Block:
                    DrawShieldSymbol(pixels, size, fg);
                    DrawPlusSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Thorns:
                    DrawThornsSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Bleed:
                    DrawDropSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Vulnerable:
                    DrawShieldSymbol(pixels, size, fg);
                    DrawCrackSymbol(pixels, size, bg * 0.7f + Color.black * 0.3f);
                    break;
                case StatusIconKind.Weak:
                    DrawDownSlashSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Stun:
                    DrawSparkSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Blind:
                    DrawCrossBarSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Nausea:
                    DrawDropSymbol(pixels, size, fg);
                    DrawDownSlashSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Curse:
                    DrawCrystalSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Allure:
                    DrawDropSymbol(pixels, size, fg);
                    DrawPlusSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Taunt:
                    DrawSwordSymbol(pixels, size, fg);
                    DrawSparkSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Confusion:
                    DrawStarSymbol(pixels, size, fg);
                    DrawCrossBarSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Burn:
                    DrawFlameSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Cold:
                    DrawCrystalSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Fatigue:
                    DrawDownSlashSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Paralysis:
                    DrawSparkSymbol(pixels, size, fg);
                    DrawCrossBarSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Slow:
                    DrawDownSlashSymbol(pixels, size, fg);
                    DrawLine(pixels, size, new Vector2(10f, 24f), new Vector2(22f, 24f), 2f, fg);
                    break;
                case StatusIconKind.Frozen:
                    DrawCrystalSymbol(pixels, size, fg);
                    DrawStarSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Entangle:
                    DrawVineSymbol(pixels, size, fg);
                    break;
                case StatusIconKind.Bind:
                    DrawVineSymbol(pixels, size, fg);
                    DrawCrossBarSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.Phase:
                    DrawSparkSymbol(pixels, size, fg);
                    DrawLine(pixels, size, new Vector2(9f, 12f), new Vector2(23f, 20f), 2f, fg);
                    break;
                case StatusIconKind.Rooted:
                    DrawVineSymbol(pixels, size, fg);
                    DrawShieldSymbol(pixels, size, bg * 0.55f + Color.black * 0.45f);
                    break;
                case StatusIconKind.ArmorBreak:
                    DrawShieldSymbol(pixels, size, fg);
                    DrawBreakLineSymbol(pixels, size, bg * 0.7f + Color.black * 0.3f);
                    break;
                case StatusIconKind.Brittle:
                    DrawCrystalSymbol(pixels, size, fg);
                    DrawBreakLineSymbol(pixels, size, bg * 0.65f + Color.black * 0.35f);
                    break;
                case StatusIconKind.Disarm:
                    DrawSwordSymbol(pixels, size, fg);
                    DrawCrossBarSymbol(pixels, size, bg * 0.7f + Color.black * 0.3f);
                    break;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                bool intersect = ((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                                 (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x);
                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(point - a, ab) / ab.sqrMagnitude;
            t = Mathf.Clamp01(t);
            Vector2 projection = a + ab * t;
            return Vector2.Distance(point, projection);
        }

        private void RefreshHealthBar()
        {
            if (_healthFill == null || State == null || State.maxHealth <= 0)
                return;

            float hp01 = Mathf.Clamp01((float)State.currentHealth / State.maxHealth);
            float armor01 = Mathf.Clamp01((float)State.armor / State.maxHealth);
            if (_healthSlider != null)
                _healthSlider.normalizedValue = hp01;
            _healthFill.color = Color.Lerp(new Color(0.85f, 0.18f, 0.18f, 1f), new Color(0.2f, 0.86f, 0.28f, 1f), hp01);
            if (_healthText != null)
                _healthText.text = $"{State.currentHealth}/{State.maxHealth}";
            if (_armorFill != null)
            {
                var armorRect = _armorFill.rectTransform;
                armorRect.anchorMax = new Vector2(armor01, 1f);
                _armorFill.enabled = State.armor > 0;
            }
            if (_armorIcon != null)
                _armorIcon.enabled = State.armor > 0;
            if (_armorText != null)
            {
                _armorText.enabled = State.armor > 0;
                _armorText.text = State.armor.ToString();
            }

            RefreshStatusBadges();
        }

        private void RefreshStatusBadges()
        {
            if (_statusGrid == null || State == null)
                return;

            var entries = new List<(StatusIconKind kind, int amount)>(32);
            AddStatusEntry(entries, StatusIconKind.Strength, State.strength);
            AddStatusEntry(entries, StatusIconKind.Toughness, State.toughness);
            AddStatusEntry(entries, StatusIconKind.Agility, State.agility);
            AddStatusEntry(entries, StatusIconKind.Wisdom, State.wisdom);
            AddStatusEntry(entries, StatusIconKind.Humility, State.humility);
            AddStatusEntry(entries, StatusIconKind.Luck, State.luck);
            AddStatusEntry(entries, StatusIconKind.Vigor, State.vigor);
            AddStatusEntry(entries, StatusIconKind.Momentum, State.momentum);
            AddStatusEntry(entries, StatusIconKind.HolyShield, State.holyShield);
            AddStatusEntry(entries, StatusIconKind.Immunity, State.immunity);
            AddStatusEntry(entries, StatusIconKind.Invincible, State.invincible);
            AddStatusEntry(entries, StatusIconKind.Deflect, State.deflect);
            AddStatusEntry(entries, StatusIconKind.Block, State.block);
            AddStatusEntry(entries, StatusIconKind.Thorns, State.thorns);
            AddStatusEntry(entries, StatusIconKind.Bleed, State.bleed);
            AddStatusEntry(entries, StatusIconKind.Vulnerable, State.vulnerable);
            AddStatusEntry(entries, StatusIconKind.Weak, State.weak);
            AddStatusEntry(entries, StatusIconKind.Stun, State.stun);
            AddStatusEntry(entries, StatusIconKind.Blind, State.blind);
            AddStatusEntry(entries, StatusIconKind.Nausea, State.nausea);
            AddStatusEntry(entries, StatusIconKind.Curse, State.curse);
            AddStatusEntry(entries, StatusIconKind.Allure, State.allure);
            AddStatusEntry(entries, StatusIconKind.Taunt, Mathf.Max(State.taunt, State.tauntActiveThisTurn));
            AddStatusEntry(entries, StatusIconKind.Confusion, State.confusion);
            AddStatusEntry(entries, StatusIconKind.Burn, State.burn);
            AddStatusEntry(entries, StatusIconKind.Cold, State.cold);
            AddStatusEntry(entries, StatusIconKind.Fatigue, State.fatigue);
            AddStatusEntry(entries, StatusIconKind.Paralysis, Mathf.Max(State.paralysis, State.paralysisActiveThisTurn));
            AddStatusEntry(entries, StatusIconKind.Slow, State.slow);
            AddStatusEntry(entries, StatusIconKind.Frozen, State.frozen);
            AddStatusEntry(entries, StatusIconKind.Entangle, State.entangle);
            AddStatusEntry(entries, StatusIconKind.Bind, State.bind);
            AddStatusEntry(entries, StatusIconKind.Phase, State.phaseMovement);
            AddStatusEntry(entries, StatusIconKind.Rooted, State.rooted ? 1 : 0);
            AddStatusEntry(entries, StatusIconKind.ArmorBreak, State.armorBreak);
            AddStatusEntry(entries, StatusIconKind.Brittle, State.brittle);
            AddStatusEntry(entries, StatusIconKind.Disarm, State.disarm);

            EnsureStatusBadgeCount(entries.Count);
            for (int i = 0; i < _statusBadges.Count; i++)
            {
                bool active = i < entries.Count;
                _statusBadges[i].root.SetActive(active);
                if (!active)
                    continue;

                var entry = entries[i];
                _statusBadges[i].icon.sprite = GetStatusIconSprite(entry.kind);
                _statusBadges[i].countText.text = entry.amount.ToString();
            }
        }

        private static void AddStatusEntry(List<(StatusIconKind kind, int amount)> entries, StatusIconKind kind, int amount)
        {
            if (amount > 0)
                entries.Add((kind, amount));
        }

        private void EnsureStatusBadgeCount(int count)
        {
            while (_statusBadges.Count < count)
                _statusBadges.Add(CreateStatusBadge(_statusBadges.Count));
        }

        private StatusBadgeView CreateStatusBadge(int index)
        {
            var root = new GameObject($"Status_{index}", typeof(RectTransform));
            root.transform.SetParent(_statusGrid, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(20f, 20f);

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(root.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var icon = iconGO.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.color = Color.white;

            var countGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGO.transform.SetParent(root.transform, false);
            var countRect = countGO.GetComponent<RectTransform>();
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(1f, 0f);
            countRect.offsetMax = new Vector2(-1f, -1f);
            var countText = countGO.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(countText);
            countText.fontSize = 8f;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.BottomRight;
            countText.color = new Color(0.03f, 0.03f, 0.03f, 1f);
            countText.textWrappingMode = TextWrappingModes.NoWrap;

            return new StatusBadgeView
            {
                root = root,
                icon = icon,
                countText = countText
            };
        }

        private static void FillCircle(Color[] pixels, int size, Vector2 center, float radius, Color color)
        {
            float radiusSq = radius * radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - center;
                    if (delta.sqrMagnitude <= radiusSq)
                        pixels[y * size + x] = color;
                }
            }
        }

        private static void DrawCircleOutline(Color[] pixels, int size, Vector2 center, float radius, float thickness, Color color)
        {
            float minSq = (radius - thickness) * (radius - thickness);
            float maxSq = radius * radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float sq = delta.sqrMagnitude;
                    if (sq >= minSq && sq <= maxSq)
                        pixels[y * size + x] = color;
                }
            }
        }

        private static void FillPolygon(Color[] pixels, int size, IReadOnlyList<Vector2> polygon, Color color)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), polygon))
                        pixels[y * size + x] = color;
                }
            }
        }

        private static void DrawLine(Color[] pixels, int size, Vector2 a, Vector2 b, float thickness, Color color)
        {
            float maxDistance = thickness * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b) <= maxDistance)
                        pixels[y * size + x] = color;
                }
            }
        }

        private static void DrawSwordSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(16f, 8f), new Vector2(16f, 24f), 3f, color);
            DrawLine(pixels, size, new Vector2(11f, 10f), new Vector2(21f, 10f), 2.5f, color);
            DrawLine(pixels, size, new Vector2(16f, 24f), new Vector2(12f, 28f), 2f, color);
            DrawLine(pixels, size, new Vector2(16f, 24f), new Vector2(20f, 28f), 2f, color);
            FillPolygon(pixels, size, new[]
            {
                new Vector2(16f, 4f),
                new Vector2(19f, 8f),
                new Vector2(13f, 8f),
            }, color);
        }

        private static void DrawShieldSymbol(Color[] pixels, int size, Color color)
        {
            FillPolygon(pixels, size, new[]
            {
                new Vector2(16f, 26f),
                new Vector2(23f, 22f),
                new Vector2(21f, 13f),
                new Vector2(16f, 7f),
                new Vector2(11f, 13f),
                new Vector2(9f, 22f),
            }, color);
        }

        private static void DrawPlusSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(16f, 9f), new Vector2(16f, 23f), 4f, color);
            DrawLine(pixels, size, new Vector2(9f, 16f), new Vector2(23f, 16f), 4f, color);
        }

        private static void DrawStarSymbol(Color[] pixels, int size, Color color)
        {
            FillPolygon(pixels, size, new[]
            {
                new Vector2(16f, 27f),
                new Vector2(19f, 20f),
                new Vector2(26f, 20f),
                new Vector2(21f, 15f),
                new Vector2(23f, 8f),
                new Vector2(16f, 12f),
                new Vector2(9f, 8f),
                new Vector2(11f, 15f),
                new Vector2(6f, 20f),
                new Vector2(13f, 20f),
            }, color);
        }

        private static void DrawThornsSymbol(Color[] pixels, int size, Color color)
        {
            FillPolygon(pixels, size, new[]
            {
                new Vector2(8f, 9f),
                new Vector2(14f, 23f),
                new Vector2(4f, 21f),
            }, color);
            FillPolygon(pixels, size, new[]
            {
                new Vector2(16f, 6f),
                new Vector2(20f, 23f),
                new Vector2(12f, 23f),
            }, color);
            FillPolygon(pixels, size, new[]
            {
                new Vector2(24f, 9f),
                new Vector2(28f, 21f),
                new Vector2(18f, 23f),
            }, color);
        }

        private static void DrawDropSymbol(Color[] pixels, int size, Color color)
        {
            FillPolygon(pixels, size, new[]
            {
                new Vector2(16f, 27f),
                new Vector2(23f, 18f),
                new Vector2(19f, 8f),
                new Vector2(13f, 8f),
                new Vector2(9f, 18f),
            }, color);
        }

        private static void DrawDownSlashSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(10f, 22f), new Vector2(22f, 10f), 4f, color);
            DrawLine(pixels, size, new Vector2(10f, 10f), new Vector2(22f, 10f), 2f, color);
            DrawLine(pixels, size, new Vector2(10f, 22f), new Vector2(10f, 14f), 2f, color);
        }

        private static void DrawSparkSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(16f, 7f), new Vector2(16f, 25f), 2.5f, color);
            DrawLine(pixels, size, new Vector2(7f, 16f), new Vector2(25f, 16f), 2.5f, color);
            DrawLine(pixels, size, new Vector2(10f, 10f), new Vector2(22f, 22f), 2f, color);
            DrawLine(pixels, size, new Vector2(22f, 10f), new Vector2(10f, 22f), 2f, color);
        }

        private static void DrawFlameSymbol(Color[] pixels, int size, Color color)
        {
            FillPolygon(pixels, size, new[]
            {
                new Vector2(16f, 27f),
                new Vector2(22f, 20f),
                new Vector2(20f, 10f),
                new Vector2(17f, 13f),
                new Vector2(15f, 7f),
                new Vector2(12f, 14f),
                new Vector2(9f, 11f),
                new Vector2(10f, 20f),
            }, color);
        }

        private static void DrawVineSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(9f, 9f), new Vector2(23f, 23f), 2.5f, color);
            DrawLine(pixels, size, new Vector2(9f, 23f), new Vector2(23f, 9f), 2.5f, color);
            DrawLine(pixels, size, new Vector2(8f, 16f), new Vector2(14f, 16f), 2f, color);
            DrawLine(pixels, size, new Vector2(18f, 16f), new Vector2(24f, 16f), 2f, color);
        }

        private static void DrawCrackSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(18f, 25f), new Vector2(14f, 18f), 2f, color);
            DrawLine(pixels, size, new Vector2(14f, 18f), new Vector2(18f, 14f), 2f, color);
            DrawLine(pixels, size, new Vector2(18f, 14f), new Vector2(13f, 8f), 2f, color);
        }

        private static void DrawBreakLineSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(11f, 24f), new Vector2(15f, 18f), 2f, color);
            DrawLine(pixels, size, new Vector2(15f, 18f), new Vector2(13f, 14f), 2f, color);
            DrawLine(pixels, size, new Vector2(13f, 14f), new Vector2(19f, 8f), 2f, color);
        }

        private static void DrawCrystalSymbol(Color[] pixels, int size, Color color)
        {
            FillPolygon(pixels, size, new[]
            {
                new Vector2(16f, 26f),
                new Vector2(23f, 18f),
                new Vector2(20f, 8f),
                new Vector2(12f, 8f),
                new Vector2(9f, 18f),
            }, color);
        }

        private static void DrawCrossBarSymbol(Color[] pixels, int size, Color color)
        {
            DrawLine(pixels, size, new Vector2(10f, 23f), new Vector2(23f, 10f), 2.2f, color);
            DrawLine(pixels, size, new Vector2(11f, 10f), new Vector2(22f, 21f), 2.2f, color);
        }
    }
}
