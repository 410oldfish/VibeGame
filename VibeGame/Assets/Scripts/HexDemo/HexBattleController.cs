using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexBattleController : MonoBehaviour
    {
        private sealed class ForcedMovementResult
        {
            public List<HexAxialCoord> path = new();
            public HexAxialCoord intendedDestination;
            public HexAxialCoord actualDestination;
            public bool collided;
        }

        private const int EnemyIntentDrawCount = 3;
        private const int WeaponSkillEnergyCost = 1;
        private const int WeaponSkillCooldown = 2;

        public HexGrid grid;
        public Camera rayCamera;
        public float unitYOffset = 0.03f;
        public float moveSpeed = 6.5f;
        public float stepStopDelay = 0.05f;
        public bool awardVictoryGold = true;
        public int victoryGoldAmount = 10;

        private readonly List<HexBattleUnit> _units = new();
        private HexBattleUnit _playerUnit;
        private readonly List<HexBattleUnit> _enemyUnits = new();
        private HexBattleFaction _currentTurn = HexBattleFaction.Player;
        private HexBattleUI _ui;
        private HexTile _hoveredTile;
        private bool _hoverHasColliderHit;
        private HexCardInstance _draggedCard;
        private bool _busy;
        private bool _pendingEndTurnRequest;
        private LineRenderer _targetArrow;
        private bool _battleFinished;

        public System.Action<bool, int, HexBattleUnit> BattleFinished;

        public void Initialize(HexGrid battleGrid, HexBattleUnit playerUnit, IReadOnlyList<HexBattleUnit> enemyUnits, Camera battleCamera)
        {
            grid = battleGrid;
            rayCamera = battleCamera != null ? battleCamera : Camera.main;

            _playerUnit = playerUnit;
            _playerUnit.ResetBattleState();
            _units.Clear();
            _units.Add(playerUnit);
            _enemyUnits.Clear();
            if (enemyUnits != null)
            {
                for (int i = 0; i < enemyUnits.Count; i++)
                {
                    if (enemyUnits[i] == null)
                        continue;

                    _enemyUnits.Add(enemyUnits[i]);
                    enemyUnits[i].ResetBattleState();
                    _units.Add(enemyUnits[i]);
                }
            }

            var uiGO = new GameObject("HexBattleUI_Root");
            uiGO.transform.SetParent(transform, false);
            _ui = uiGO.AddComponent<HexBattleUI>();
            _ui.Initialize(this);
            EnsureTargetArrow();

            BeginTurn(HexBattleFaction.Player);
        }

        private void OnDestroy()
        {
            _battleFinished = true;
            _busy = true;
            _draggedCard = null;
            _hoveredTile = null;
            BattleFinished = null;
            if (_targetArrow != null)
                Destroy(_targetArrow.gameObject);
            if (_ui != null)
                Destroy(_ui.gameObject);
        }

        private void Update()
        {
            if (grid == null || rayCamera == null)
                return;

            UpdateHoverFeedback();
            UpdateMovementHighlights();
            if (_busy || _draggedCard != null || _currentTurn != HexBattleFaction.Player)
                return;

            if (Input.GetMouseButtonDown(0))
                TryHandlePlayerMoveClick();
        }

        public IReadOnlyList<HexCardInstance> GetLocalHand()
        {
            return _playerUnit != null ? _playerUnit.Deck.Hand : System.Array.Empty<HexCardInstance>();
        }

        public IReadOnlyList<HexCardInstance> GetLocalDrawPile()
        {
            return _playerUnit != null ? _playerUnit.Deck.DrawPile : System.Array.Empty<HexCardInstance>();
        }

        public IReadOnlyList<HexCardInstance> GetLocalDiscardPile()
        {
            return _playerUnit != null ? _playerUnit.Deck.DiscardPile : System.Array.Empty<HexCardInstance>();
        }

        public int GetLocalCardCost(HexCardInstance card)
        {
            return _playerUnit != null ? _playerUnit.GetCardEnergyCost(card) : 0;
        }

        public string GetTurnSummary()
        {
            return _currentTurn == HexBattleFaction.Player ? "Player Turn" : "Enemy Turn";
        }

        public string GetStatusSummary()
        {
            var builder = new StringBuilder();
            builder.Append($"Hero   HP {_playerUnit.State.currentHealth}/{_playerUnit.State.maxHealth}  Armor {_playerUnit.State.armor}  Energy {_playerUnit.State.energy}/{_playerUnit.State.maxEnergy}  Move {_playerUnit.State.currentMovePoints}/{_playerUnit.State.maxMovePoints}");
            AppendStatusEffects(builder, _playerUnit);
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                builder.Append('\n');
                builder.Append($"Enemy {i + 1}  HP {enemy.State.currentHealth}/{enemy.State.maxHealth}  Armor {enemy.State.armor}  Energy {enemy.State.energy}/{enemy.State.maxEnergy}  Move {enemy.State.currentMovePoints}/{enemy.State.maxMovePoints}");
                AppendStatusEffects(builder, enemy);
                AppendEnemyIntent(builder, enemy);
            }

            return builder.ToString();
        }

        public string GetDeckSummary()
        {
            return $"Draw {_playerUnit.Deck.DrawPile.Count}   Hand {_playerUnit.Deck.Hand.Count}   Discard {_playerUnit.Deck.DiscardPile.Count}   Exhaust {_playerUnit.Deck.ExhaustPile.Count}";
        }

        public string GetResourceSummary()
        {
            return $"Energy  {_playerUnit.State.energy}/{_playerUnit.State.maxEnergy}\nMove    {_playerUnit.State.currentMovePoints}/{_playerUnit.State.maxMovePoints}\nPower   {_playerUnit.State.strength}";
        }

        public string GetWeaponSkillSummary()
        {
            if (_playerUnit != null && _playerUnit.State.profession == HexCardProfession.Druid)
                return GetDruidPassiveSummary(_playerUnit);

            return $"Weapon  {GetWeaponLabel(_playerUnit.State.weapon)}\nSkill   {WeaponSkillEnergyCost}E / CD {_playerUnit.State.skillCooldown}";
        }

        public bool CanLocalPlayerEndTurn()
        {
            return _currentTurn == HexBattleFaction.Player && _playerUnit != null && _playerUnit.IsAlive;
        }

        private static bool SubmitAuthoritativeCommand(HexNetworkCommandType commandType, string payloadJson)
        {
            var session = HexNetworkSessionController.EnsureExists();
            session.SubmitLocalCommand(commandType, payloadJson);
            return session.IsHostAuthority;
        }

        private static string ToPayload(HexAxialCoord coord)
        {
            return JsonUtility.ToJson(new HexCoordPayload { q = coord.q, r = coord.r });
        }

        private static string ToPayload(HexCardInstance card, HexAxialCoord targetCoord)
        {
            return JsonUtility.ToJson(new HexCardPlayPayload
            {
                runtimeId = card.runtimeId,
                cardId = card.definition != null ? card.definition.id : string.Empty,
                targetQ = targetCoord.q,
                targetR = targetCoord.r,
            });
        }

        public void RequestEndTurn()
        {
            if (!CanLocalPlayerEndTurn())
                return;

            if (_draggedCard != null)
            {
                SetTargetArrowActive(false);
                _draggedCard = null;
                ClearRangeHighlights();
                _ui.Refresh();
            }

            if (_busy)
            {
                _pendingEndTurnRequest = true;
                return;
            }

            if (!SubmitAuthoritativeCommand(HexNetworkCommandType.EndTurn, string.Empty))
                return;

            StartCoroutine(EndTurnRoutine());
        }

        public bool CanUseWeaponSkill(HexWeaponType weaponType)
        {
            if (_playerUnit != null && _playerUnit.State.profession == HexCardProfession.Druid)
                return false;

            int skillCost = _playerUnit != null && _playerUnit.State.weaponSkillFree ? 0 : WeaponSkillEnergyCost;
            return !_busy &&
                   _currentTurn == HexBattleFaction.Player &&
                   _draggedCard == null &&
                   _playerUnit != null &&
                   _playerUnit.CanUseWeaponSkill(skillCost) &&
                   !_playerUnit.State.cannotUseSkills;
        }

        public void RequestWeaponSkill(HexWeaponType weaponType)
        {
            if (!CanUseWeaponSkill(weaponType))
                return;

            var payload = JsonUtility.ToJson(new HexWeaponSkillPayload { weaponType = weaponType });
            if (!SubmitAuthoritativeCommand(HexNetworkCommandType.UseWeaponSkill, payload))
                return;

            int skillCost = _playerUnit.State.weaponSkillFree ? 0 : WeaponSkillEnergyCost;
            _playerUnit.SpendWeaponSkill(skillCost, WeaponSkillCooldown, weaponType);
            switch (weaponType)
            {
                case HexWeaponType.Sword:
                    _playerUnit.QueueNextAttackDraw(2);
                    break;
                case HexWeaponType.Axe:
                    _playerUnit.QueueNextAttackVulnerable(2);
                    break;
                case HexWeaponType.Hammer:
                    _playerUnit.GainStrength(2);
                    break;
            }

            _ui.Refresh();
        }

        public void BeginCardDrag(HexCardInstance card)
        {
            if (_busy || _currentTurn != HexBattleFaction.Player || !_playerUnit.IsAlive || card == null || card.definition == null || card.definition.isUnplayable)
                return;

            _draggedCard = card;
            UpdateRangeHighlights();
            SetTargetArrowActive(card.definition.targetType == HexCardTargetType.EnemyUnit ||
                card.definition.targetType == HexCardTargetType.Direction);
            UpdateDraggedCard(Vector2.zero);
        }

        public void UpdateDraggedCard(Vector2 screenPosition)
        {
            if (!this || grid == null)
                return;

            UpdateHoverFeedback();
            UpdateTargetArrow();
        }

        public bool EndCardDrag(Vector2 screenPosition)
        {
            if (!this || grid == null || _draggedCard == null)
                return false;

            bool played = TryPlayDraggedCard(screenPosition);
            SetTargetArrowActive(false);
            _draggedCard = null;
            ClearRangeHighlights();
            _ui.Refresh();
            return played;
        }

        private void TryHandlePlayerMoveClick()
        {
            if (_playerUnit == null || _playerUnit.State.rooted || _playerUnit.State.bind > 0)
                return;

            if (!TryGetHoveredTile(out var tile, out _))
                return;

            if (tile == null || IsMovementDestinationBlocked(tile.coord, _playerUnit))
                return;

            var path = BuildMovementPath(_playerUnit, tile.coord);
            int moveCost = GetMovementCost(_playerUnit, tile.coord, path);
            if (path == null || path.Count < 2 || moveCost > _playerUnit.State.currentMovePoints)
                return;

            if (!SubmitAuthoritativeCommand(HexNetworkCommandType.MoveUnit, ToPayload(tile.coord)))
                return;

            StartCoroutine(MoveUnitRoutine(_playerUnit, path, moveCost));
        }

        private bool TryPlayDraggedCard(Vector2 screenPosition)
        {
            if (_draggedCard == null || _draggedCard.definition == null || _draggedCard.definition.isUnplayable || !_playerUnit.CanPay(_draggedCard) || _busy)
                return false;

            if (_draggedCard.definition.id == "C_01_030" && !CanClashSucceed(_playerUnit))
                return false;

            if (_draggedCard.definition.targetType == HexCardTargetType.Self)
            {
                if (!SubmitAuthoritativeCommand(HexNetworkCommandType.PlayCard, ToPayload(_draggedCard, _playerUnit.State.coord)))
                    return true;

                StartCoroutine(ResolveCardRoutine(_playerUnit, _playerUnit, _draggedCard));
                return true;
            }

            if (_draggedCard.definition.targetType == HexCardTargetType.Direction)
            {
                if (!TryGetHoveredTile(out var hoveredTile, out _))
                    return false;

                if (hoveredTile.coord.Equals(_playerUnit.State.coord))
                    return false;

                var directionalTargets = GetDirectionalTargets(_playerUnit, hoveredTile.coord, _draggedCard.definition);
                if (directionalTargets.Count == 0)
                    return false;
                if (_draggedCard.definition.cardType == HexCardType.Attack &&
                    TryGetRequiredAttackTarget(_playerUnit, out var requiredDirectionalTarget) &&
                    !directionalTargets.Contains(requiredDirectionalTarget))
                    return false;

                if (!SubmitAuthoritativeCommand(HexNetworkCommandType.PlayCard, ToPayload(_draggedCard, hoveredTile.coord)))
                    return true;

                StartCoroutine(ResolveCardRoutine(_playerUnit, directionalTargets[0], _draggedCard, hoveredTile.coord));
                return true;
            }

            if (_draggedCard.definition.targetType == HexCardTargetType.Tile)
            {
                if (!TryGetHoveredTile(out var hoveredTile, out _))
                    return false;

                if (HexAxialCoord.Distance(_playerUnit.State.coord, hoveredTile.coord) > _draggedCard.definition.castRange)
                    return false;

                var areaTargets = GetEnemiesInArea(hoveredTile.coord, _draggedCard.definition.effectRadius, _playerUnit);
                if (_draggedCard.definition.cardType == HexCardType.Attack &&
                    TryGetRequiredAttackTarget(_playerUnit, out var requiredAreaTarget) &&
                    !areaTargets.Contains(requiredAreaTarget))
                    return false;
                var targetForResolution = areaTargets.Count > 0 ? areaTargets[0] : _playerUnit;
                if (!SubmitAuthoritativeCommand(HexNetworkCommandType.PlayCard, ToPayload(_draggedCard, hoveredTile.coord)))
                    return true;

                StartCoroutine(ResolveCardRoutine(_playerUnit, targetForResolution, _draggedCard, hoveredTile.coord));
                return true;
            }

            if (!TryGetHoveredUnit(out var targetUnit))
            {
                if (_draggedCard.definition.effectRadius <= 0)
                    return false;

                if (!TryGetHoveredTile(out var hoveredTile, out _))
                    return false;

                if (HexAxialCoord.Distance(_playerUnit.State.coord, hoveredTile.coord) > _draggedCard.definition.castRange)
                    return false;

                var areaTargets = GetEnemiesInArea(hoveredTile.coord, _draggedCard.definition.effectRadius, _playerUnit);
                if (areaTargets.Count == 0)
                    return false;
                if (_draggedCard.definition.cardType == HexCardType.Attack &&
                    TryGetRequiredAttackTarget(_playerUnit, out var requiredSplashTarget) &&
                    !areaTargets.Contains(requiredSplashTarget))
                    return false;

                if (!SubmitAuthoritativeCommand(HexNetworkCommandType.PlayCard, ToPayload(_draggedCard, hoveredTile.coord)))
                    return true;

                StartCoroutine(ResolveCardRoutine(_playerUnit, areaTargets[0], _draggedCard));
                return true;
            }

            if (targetUnit == null)
                return false;

            bool hostileTarget = targetUnit.State.faction != _playerUnit.State.faction;
            bool alliedPlantTarget = CanConvertArmorCardToPlantHealing(_playerUnit, targetUnit, _draggedCard.definition);
            if (!hostileTarget && !alliedPlantTarget)
                return false;
            if (_draggedCard.definition.cardType == HexCardType.Attack &&
                !CanAttackTarget(_playerUnit, targetUnit))
                return false;

            if (HexAxialCoord.Distance(_playerUnit.State.coord, targetUnit.State.coord) > _draggedCard.definition.castRange)
                return false;

            if (!SubmitAuthoritativeCommand(HexNetworkCommandType.PlayCard, ToPayload(_draggedCard, targetUnit.State.coord)))
                return true;

            StartCoroutine(ResolveCardRoutine(_playerUnit, targetUnit, _draggedCard));
            return true;
        }

        private IEnumerator ResolveCardRoutine(HexBattleUnit source, HexBattleUnit target, HexCardInstance card, HexAxialCoord? directionalCoord = null)
        {
            _busy = true;
            int energyCost = source.GetCardEnergyCost(card);
            HexAxialCoord targetedCoord = directionalCoord ?? (target != null ? target.State.coord : source.State.coord);
            source.SpendEnergy(energyCost);
            bool exhaustCard = card.exhaustWhenPlayed || HexCardLibrary.HasKeyword(card.definition, HexCardKeywordType.Exhaust);
            source.Deck.DiscardFromHand(card, exhaustCard);
            if (source.State.bleed > 0)
            {
                source.ApplyDamage(source.State.bleed);
                source.RefreshLabel();
                _ui.Refresh();
                if (!source.IsAlive)
                {
                    if (source == _playerUnit)
                    {
                        yield return source.PlayDeathAndCleanup();
                        yield return HandleBattleEnd(false);
                    }
                    else
                    {
                        yield return source.PlayDeathAndCleanup();
                        if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
                            yield return HandleBattleEnd(true);
                    }
                    yield break;
                }
            }

            if (source.State.armorOnAttackCardThisTurn > 0 && card.definition.cardType == HexCardType.Attack)
                source.GainArmor(source.State.armorOnAttackCardThisTurn);
            if (source.State.armorOnSkillCard > 0 && card.definition.cardType == HexCardType.Skill)
                source.GainArmor(source.State.armorOnSkillCard);

            ApplyDruidTransformFromCard(source, card.definition);

            bool handledByCustomLogic = false;
            yield return ResolveCustomCardRoutine(source, target, card, energyCost, targetedCoord, handled => handledByCustomLogic = handled);
            if (handledByCustomLogic)
            {
                source.RefreshLabel();
                target.RefreshLabel();
                _ui.Refresh();
                if (!target.IsAlive)
                {
                    if (target == _playerUnit)
                    {
                        yield return HandleBattleEnd(false);
                        yield break;
                    }

                    if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
                    {
                        yield return HandleBattleEnd(true);
                        yield break;
                    }
                }

                _busy = false;
                TryProcessPendingEndTurn();
                yield break;
            }

            switch (card.definition.effectType)
            {
                case HexCardEffectType.Attack:
                    if (card.definition.targetType == HexCardTargetType.Direction && directionalCoord.HasValue)
                    {
                        yield return ResolveDirectionalAttackRoutine(source, directionalCoord.Value, card);
                        break;
                    }

                    if (card.definition.targetType == HexCardTargetType.Tile)
                    {
                        yield return ResolveTileAttackRoutine(source, targetedCoord, card);
                        break;
                    }

                    int repeatCount = 1 + Mathf.Max(0, source.State.attackRepeatBonusThisTurn);
                    for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                    {
                        if (!target.IsAlive || !source.IsAlive)
                            break;

                        source.FaceTarget(target.transform.position);
                        source.PlayAttackAnimation();
                        yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));
                        target.FaceTarget(source.transform.position);
                        if (target.State.negateNextEnemyAttack && source.State.faction == HexBattleFaction.Enemy)
                        {
                            target.State.negateNextEnemyAttack = false;
                            continue;
                        }
                        int attackDamage = GetModifiedDamage(source, target, card.definition.amount + Mathf.Max(0, source.State.strength));
                        target.ApplyDamage(attackDamage);
                        source.State.damageDealtThisTurn += attackDamage;
                        if (target.IsAlive)
                        {
                            target.PlayHitAnimation();
                            yield return new WaitForSeconds(Mathf.Max(0.08f, target.GetHitDuration() * 0.85f));
                            if (target.State.thorns > 0 && source.IsAlive)
                                source.ApplyDamage(target.State.thorns);
                            yield return ApplyWeaponAttackEffectsRoutine(source, target);
                            yield return ApplyKeywordEffectsRoutine(source, target, card);
                        }
                        else
                        {
                            yield return target.PlayDeathAndCleanup();
                        }
                    }
                    break;
                case HexCardEffectType.Defend:
                    if (CanConvertArmorCardToPlantHealing(source, target, card.definition))
                        target.Heal(card.definition.amount);
                    else
                        source.GainArmor(card.definition.amount);
                    break;
            }

            ApplySelfKeywordEffects(source, card);

            if (card.definition.targetType == HexCardTargetType.Direction && directionalCoord.HasValue)
            {
                FlashDirectionalArea(directionalCoord.Value, source, card.definition);
                RefreshAliveUnitLabels();
                _ui.Refresh();

                if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
                {
                    yield return HandleBattleEnd(true);
                    yield break;
                }
            }
            else
            {
                if (grid.TryGetTile(target.State.coord, out var targetTile))
                    targetTile.FlashClick();

                source.RefreshLabel();
                target.RefreshLabel();
                _ui.Refresh();

                if (!target.IsAlive)
                {
                    if (target == _playerUnit)
                    {
                        yield return HandleBattleEnd(false);
                        yield break;
                    }

                    if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
                    {
                        yield return HandleBattleEnd(true);
                        yield break;
                    }
                }
            }

            _busy = false;
            TryProcessPendingEndTurn();
        }

        private IEnumerator ResolveDirectionalAttackRoutine(HexBattleUnit source, HexAxialCoord aimedCoord, HexCardInstance card)
        {
            var targets = GetDirectionalTargets(source, aimedCoord, card.definition);
            if (targets.Count == 0)
                yield break;

            int repeatCount = 1 + Mathf.Max(0, source.State.attackRepeatBonusThisTurn);
            Vector3 centerPoint = grid != null ? grid.AxialToWorld(aimedCoord) : source.transform.position + source.transform.forward;

            for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
            {
                if (!source.IsAlive)
                    yield break;

                targets = GetDirectionalTargets(source, aimedCoord, card.definition);
                if (targets.Count == 0)
                    yield break;

                source.FaceTarget(centerPoint);
                source.PlayAttackAnimation();
                yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));

                float longestImpactDuration = 0.08f;
                for (int i = 0; i < targets.Count; i++)
                {
                    var areaTarget = targets[i];
                    if (areaTarget == null || !areaTarget.IsAlive || !source.IsAlive)
                        continue;

                    areaTarget.FaceTarget(source.transform.position);
                    int attackDamage = GetModifiedDamage(source, areaTarget, card.definition.amount + Mathf.Max(0, source.State.strength));
                    ApplyAttackDamage(source, areaTarget, attackDamage);
                    source.State.damageDealtThisTurn += attackDamage;
                    bool survivedHit = areaTarget.IsAlive;
                    if (survivedHit)
                    {
                        areaTarget.PlayHitAnimation();
                        longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetHitDuration() * 0.85f);
                        if (areaTarget.State.thorns > 0 && source.IsAlive)
                            ApplyDamageToUnit(source, areaTarget.State.thorns, areaTarget);
                    }
                    else
                    {
                        longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetDeathDuration());
                    }

                    if (source.State.firstAttackBonusPending && source.State.firstAttackBurnAmount > 0)
                    {
                        areaTarget.ApplyBurn(source.State.firstAttackBurnAmount);
                        source.State.firstAttackBonusPending = false;
                    }

                    yield return ApplyWeaponAttackEffectsRoutine(source, areaTarget);
                    yield return ApplyKeywordEffectsRoutine(source, areaTarget, card);
                }

                yield return new WaitForSeconds(Mathf.Max(0.08f, longestImpactDuration));

                for (int i = 0; i < targets.Count; i++)
                {
                    var areaTarget = targets[i];
                    if (areaTarget == null || areaTarget.IsAlive)
                        continue;

                    yield return areaTarget.PlayDeathAndCleanup();
                }
            }
        }

        private IEnumerator ResolveTileAttackRoutine(HexBattleUnit source, HexAxialCoord centerCoord, HexCardInstance card)
        {
            var targets = GetEnemiesInArea(centerCoord, card.definition.effectRadius, source);
            if (targets.Count == 0)
                yield break;

            source.FaceTarget(grid != null ? grid.AxialToWorld(centerCoord) : source.transform.position + source.transform.forward);
            source.PlayAttackAnimation();
            yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));

            float longestImpactDuration = 0.08f;
            for (int i = 0; i < targets.Count; i++)
            {
                var areaTarget = targets[i];
                if (areaTarget == null || !areaTarget.IsAlive || !source.IsAlive)
                    continue;
                if (!CanAttackTarget(source, areaTarget))
                    continue;

                areaTarget.FaceTarget(source.transform.position);
                int attackDamage = GetModifiedDamage(source, areaTarget, card.definition.amount + Mathf.Max(0, source.State.strength));
                ApplyAttackDamage(source, areaTarget, attackDamage);
                source.State.damageDealtThisTurn += attackDamage;
                if (areaTarget.IsAlive)
                {
                    areaTarget.PlayHitAnimation();
                    longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetHitDuration() * 0.85f);
                    if (areaTarget.State.thorns > 0 && source.IsAlive)
                        ApplyDamageToUnit(source, areaTarget.State.thorns, areaTarget);
                }
                else
                {
                    longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetDeathDuration());
                }

                yield return ApplyKeywordEffectsRoutine(source, areaTarget, card);
            }

            yield return new WaitForSeconds(Mathf.Max(0.08f, longestImpactDuration));
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null && !targets[i].IsAlive)
                    yield return targets[i].PlayDeathAndCleanup();
            }
        }

        private void FlashDirectionalArea(HexAxialCoord aimedCoord, HexBattleUnit source, HexCardDefinition definition)
        {
            if (grid == null || source == null || definition == null)
                return;

            var coords = GetDirectionalAreaCoords(source.State.coord, aimedCoord, definition.castRange, definition.effectRadius);
            for (int i = 0; i < coords.Count; i++)
            {
                if (grid.TryGetTile(coords[i], out var tile))
                    tile.FlashClick();
            }
        }

        private void RefreshAliveUnitLabels()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive)
                    continue;

                unit.RefreshLabel();
            }
        }

        private IEnumerator ApplyKeywordEffectsRoutine(HexBattleUnit source, HexBattleUnit target, HexCardInstance card)
        {
            var keywordEffects = HexCardLibrary.GetKeywordEffects(card.definition);
            for (int i = 0; i < keywordEffects.Count; i++)
            {
                var keyword = keywordEffects[i];
                switch (keyword.keywordType)
                {
                    case HexCardKeywordType.Knockback:
                        yield return ApplyKnockbackRoutine(source, target, keyword.amount);
                        break;
                    case HexCardKeywordType.Pull:
                        yield return ApplyPullRoutine(source, target, keyword.amount);
                        break;
                    case HexCardKeywordType.Bleed:
                        target.ApplyBleed(keyword.amount);
                        break;
                    case HexCardKeywordType.Vulnerable:
                        target.ApplyVulnerable(keyword.amount);
                        break;
                    case HexCardKeywordType.Weak:
                        target.ApplyWeak(keyword.amount);
                        break;
                    case HexCardKeywordType.Stun:
                        target.ApplyStun(keyword.amount);
                        break;
                    case HexCardKeywordType.Burn:
                        target.ApplyBurn(keyword.amount);
                        break;
                    case HexCardKeywordType.Entangle:
                        target.ApplyEntangle(keyword.amount);
                        break;
                }
            }
        }

        private void ApplySelfKeywordEffects(HexBattleUnit source, HexCardInstance card)
        {
            if (source == null || card?.definition == null)
                return;

            var keywordEffects = HexCardLibrary.GetKeywordEffects(card.definition);
            for (int i = 0; i < keywordEffects.Count; i++)
            {
                var keyword = keywordEffects[i];
                if (keyword.keywordType != HexCardKeywordType.Phase)
                    continue;

                source.ApplyPhase(keyword.amount);
            }
        }

        private IEnumerator ApplyWeaponAttackEffectsRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            if (source == null || target == null)
                yield break;

            int nextAttackDraw = source.ConsumeNextAttackDraw();
            if (nextAttackDraw > 0)
                DrawCardsForUnit(source, nextAttackDraw);

            int nextAttackVulnerable = source.ConsumeNextAttackVulnerable();
            if (nextAttackVulnerable > 0 && target.IsAlive)
                target.ApplyVulnerable(nextAttackVulnerable);

            int passiveRepeat = source.State.weaponPassivesDoubleThisTurn ? 2 : 1;
            if (source.State.allWeaponsEquipped)
            {
                for (int i = 0; i < passiveRepeat; i++)
                {
                    if (target.IsAlive)
                        yield return ResolveSwordWaveRoutine(source, target);
                    if (target.IsAlive)
                        target.ApplyBleed(1);
                    if (target.IsAlive)
                        target.Deck.AddToDrawPile(HexCardLibrary.GetDaze());
                }
                yield break;
            }

            switch (source.State.weapon)
            {
                case HexWeaponType.Sword:
                    for (int i = 0; i < passiveRepeat; i++)
                        yield return ResolveSwordWaveRoutine(source, target);
                    break;
                case HexWeaponType.Axe:
                    for (int i = 0; i < passiveRepeat; i++)
                    {
                        if (target.IsAlive)
                            target.ApplyBleed(1);
                    }
                    break;
                case HexWeaponType.Hammer:
                    for (int i = 0; i < passiveRepeat; i++)
                    {
                        if (target.IsAlive)
                            target.Deck.AddToDrawPile(HexCardLibrary.GetDaze());
                    }
                    break;
            }
        }

        private IEnumerator ResolveSwordWaveRoutine(HexBattleUnit source, HexBattleUnit primaryTarget)
        {
            if (grid == null || source == null || primaryTarget == null)
                yield break;

            var line = HexBattlePathing.GetLineCoords(grid, source.State.coord, primaryTarget.State.coord, 4);
            for (int i = 0; i < line.Count; i++)
            {
                var unit = FindUnitAtCoord(line[i], source);
                if (unit == null || unit.State.faction == source.State.faction || !unit.IsAlive)
                    continue;

                unit.ApplyDamage(GetModifiedDamage(source, unit, 3));
                if (unit.IsAlive)
                    unit.PlayHitAnimation();
                else
                    yield return unit.PlayDeathAndCleanup();
            }
        }

        private IEnumerator ApplyKnockbackRoutine(HexBattleUnit source, HexBattleUnit target, int distance)
        {
            if (grid == null || source == null || target == null || distance <= 0)
                yield break;

            var movement = ResolveForcedMovement(source, target, distance, moveTowardSource: false);
            if (movement == null || movement.path.Count < 2)
                yield break;

            target.FaceTarget(grid.AxialToWorld(movement.actualDestination));
            yield return target.MoveAlongPath(grid, movement.path, unitYOffset, moveSpeed * 1.2f, 0.01f, coord => OnUnitEnteredTile(target, coord));
            ApplyForcedMovementCollisionEffects(source, target, movement);
            target.RefreshLabel();
            _ui.Refresh();
        }

        private IEnumerator ApplyPullRoutine(HexBattleUnit source, HexBattleUnit target, int distance)
        {
            if (grid == null || source == null || target == null || distance <= 0)
                yield break;

            var movement = ResolveForcedMovement(source, target, distance, moveTowardSource: true);
            if (movement == null || movement.path.Count < 2)
                yield break;

            target.FaceTarget(grid.AxialToWorld(movement.actualDestination));
            yield return target.MoveAlongPath(grid, movement.path, unitYOffset, moveSpeed * 1.2f, 0.01f, coord => OnUnitEnteredTile(target, coord));
            ApplyForcedMovementCollisionEffects(source, target, movement);
            target.RefreshLabel();
            _ui.Refresh();
        }

        private IEnumerator MoveUnitRoutine(HexBattleUnit unit, List<HexAxialCoord> path, int moveCost, HexAxialCoord? towardTargetCoord = null)
        {
            _busy = true;
            int movedDistance = Mathf.Max(0, moveCost);
            if (movedDistance > 0 && unit.State.entangle > 0)
            {
                ApplyDamageToUnit(unit, unit.State.entangle * movedDistance, unit);
                unit.RefreshLabel();
                _ui.Refresh();
                if (!unit.IsAlive)
                {
                    yield return unit.PlayDeathAndCleanup();
                    if (unit == _playerUnit)
                        yield return HandleBattleEnd(false);
                    else if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
                        yield return HandleBattleEnd(true);
                    yield break;
                }
            }

            bool jumpMovement = IsToadJumpMovement(unit);
            bool temporaryPhaseMovement = HasLavaLizardMovementPhase(unit);
            if (temporaryPhaseMovement)
                unit.State.phaseMovement += 1;

            try
            {
                yield return unit.MoveAlongPath(
                    grid,
                    path,
                    unitYOffset,
                    moveSpeed,
                    stepStopDelay,
                    coord => OnUnitEnteredTile(unit, coord),
                    jumpMovement);
            }
            finally
            {
                if (temporaryPhaseMovement)
                    unit.State.phaseMovement = Mathf.Max(0, unit.State.phaseMovement - 1);
            }

            unit.SpendMovePoints(moveCost);
            HandlePostMovementPassives(unit, path, towardTargetCoord, movedDistance);
            _busy = false;
            UpdateMovementHighlights();
            _ui.Refresh();
            TryProcessPendingEndTurn();
        }

        private IEnumerator EndTurnRoutine()
        {
            _pendingEndTurnRequest = false;
            _busy = true;
            ClearRangeHighlights();
            ClearMovementHighlights();
            ApplyDruidEndTurnPassives(GetCurrentUnit());
            GetCurrentUnit().EndTurn();
            yield return new WaitForSeconds(0.15f);
            BeginTurn(_currentTurn == HexBattleFaction.Player ? HexBattleFaction.Enemy : HexBattleFaction.Player);
            _busy = false;
            TryProcessPendingEndTurn();
        }

        private void BeginTurn(HexBattleFaction faction)
        {
            if (_battleFinished)
                return;

            _currentTurn = faction;
            if (_currentTurn == HexBattleFaction.Player)
            {
                PrepareEnemyIntents();
                _playerUnit.BeginTurn();
                ApplyDruidBeginTurnPassives(_playerUnit);
                ApplyBurningAura(_playerUnit);
            }
            else
            {
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    if (_enemyUnits[i] != null && _enemyUnits[i].IsAlive)
                    {
                        _enemyUnits[i].BeginTurn();
                        ApplyDruidBeginTurnPassives(_enemyUnits[i]);
                        ApplyBurningAura(_enemyUnits[i]);
                        if (!_enemyUnits[i].IsAlive)
                            StartCoroutine(_enemyUnits[i].PlayDeathAndCleanup());
                    }
                }

                if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
                {
                    StartCoroutine(HandleBattleEnd(true));
                    return;
                }
            }

            StartCoroutine(ResolveTurnStartRoutine(faction));
        }

        private IEnumerator ResolveTurnStartRoutine(HexBattleFaction faction)
        {
            _busy = true;
            if (faction == HexBattleFaction.Player)
            {
                yield return ResolveUnitTurnStartStatuses(_playerUnit);
                _ui.Refresh();
                if (!_playerUnit.IsAlive)
                {
                    yield return HandlePlayerDefeatFromStatus();
                    yield break;
                }

                if (!_playerUnit.CanActThisTurn)
                {
                    _busy = false;
                    StartCoroutine(AutoPassStunnedPlayerTurn());
                    yield break;
                }

                UpdateMovementHighlights();
                _ui.Refresh();
                _busy = false;
                yield break;
            }

            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                yield return ResolveUnitTurnStartStatuses(enemy);
                if (!enemy.IsAlive)
                    yield return enemy.PlayDeathAndCleanup();
            }

            if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
            {
                yield return HandleBattleEnd(true);
                yield break;
            }

            UpdateMovementHighlights();
            _ui.Refresh();
            StartCoroutine(RunEnemyTurn());
        }

        private IEnumerator RunEnemyTurn()
        {
            yield return new WaitForSeconds(0.45f);
            if (_playerUnit == null || !_playerUnit.IsAlive)
                yield break;

            for (int enemyIndex = 0; enemyIndex < _enemyUnits.Count; enemyIndex++)
            {
                var enemy = _enemyUnits[enemyIndex];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                if (!enemy.CanActThisTurn)
                {
                    enemy.EndTurn();
                    yield return new WaitForSeconds(0.1f);
                    continue;
                }

                var primaryTarget = GetPrimaryEnemyTarget(enemy);
                if (primaryTarget == null || !primaryTarget.IsAlive)
                    primaryTarget = _playerUnit;
                if (primaryTarget == null || !primaryTarget.IsAlive)
                    yield break;

                if (enemy.Deck.Hand.Count == 0)
                    DrawEnemyIntentCards(enemy);

                var intentCards = enemy.Deck.Hand.ToList();
                for (int cardIndex = 0; cardIndex < intentCards.Count; cardIndex++)
                {
                    var card = intentCards[cardIndex];
                    if (card == null || !enemy.Deck.Hand.Contains(card))
                        continue;

                    yield return ResolveEnemyIntentCard(enemy, card);
                    if (_battleFinished || _playerUnit == null || !_playerUnit.IsAlive || !enemy.IsAlive)
                        yield break;

                    yield return new WaitForSeconds(0.1f);
                }

                enemy.EndTurn();
                if (_battleFinished || _playerUnit == null || !_playerUnit.IsAlive)
                    yield break;
            }

            ApplyEnemyTurnEndPlayerEffects();

            yield return new WaitForSeconds(0.2f);
            BeginTurn(HexBattleFaction.Player);
        }

        private void PrepareEnemyIntents()
        {
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                enemy.Deck.DiscardHand();
                DrawEnemyIntentCards(enemy);
                enemy.RefreshLabel();
            }
        }

        private void DrawEnemyIntentCards(HexBattleUnit enemy)
        {
            if (enemy == null)
                return;

            for (int i = 0; i < EnemyIntentDrawCount; i++)
            {
                var card = enemy.Deck.DrawCard(out bool emptiedDrawPile);
                if (card == null)
                    break;

                if (emptiedDrawPile)
                    TriggerEnemyDrawPileEmptiedEffect(enemy);
            }
        }

        private void TriggerEnemyDrawPileEmptiedEffect(HexBattleUnit enemy)
        {
            if (enemy == null || enemy.State == null || !enemy.IsAlive)
                return;

            int strengthGain = Mathf.Max(0, enemy.State.emptyDrawPileStrengthGain);
            if (strengthGain <= 0)
                return;

            enemy.GainStrength(strengthGain);
            enemy.RefreshLabel();
            Debug.Log($"{enemy.State.displayName} played a special empty-deck card: Strength +{strengthGain}.");
        }

        private IEnumerator ResolveEnemyIntentCard(HexBattleUnit enemy, HexCardInstance card)
        {
            bool resolved = false;
            if (enemy == null || card?.definition == null || !enemy.IsAlive)
                yield break;

            var primaryTarget = GetPrimaryEnemyTarget(enemy);
            if (primaryTarget == null || !primaryTarget.IsAlive)
                primaryTarget = _playerUnit;

            if (card.definition.effectType == HexCardEffectType.MoveToward)
            {
                if (primaryTarget != null && primaryTarget.IsAlive)
                {
                    int maxSteps = Mathf.Max(1, card.definition.amount);
                    var path = FindBestApproachPath(enemy, primaryTarget.State.coord, 1);
                    if (path != null && path.Count >= 2)
                    {
                        int takeCount = Mathf.Min(path.Count, maxSteps + 1);
                        var trimmed = path.Take(takeCount).ToList();
                        yield return MoveUnitRoutine(enemy, trimmed, 0, primaryTarget.State.coord);
                        resolved = true;
                    }
                }
            }
            else if (card.definition.effectType == HexCardEffectType.Attack)
            {
                if (primaryTarget != null &&
                    primaryTarget.IsAlive &&
                    HexAxialCoord.Distance(enemy.State.coord, primaryTarget.State.coord) <= card.definition.castRange &&
                    CanAttackTarget(enemy, primaryTarget))
                {
                    yield return ResolveCardRoutine(enemy, primaryTarget, card);
                    resolved = true;
                }
            }
            else if (card.definition.effectType == HexCardEffectType.Defend)
            {
                yield return ResolveCardRoutine(enemy, enemy, card);
                resolved = true;
            }

            if (!resolved && enemy.Deck.Hand.Contains(card))
            {
                DiscardOrExhaustCard(enemy, card, false);
                enemy.RefreshLabel();
                _ui.Refresh();
            }
        }

        private IEnumerator TryEnemyMove(HexBattleUnit enemy, HexBattleUnit player)
        {
            if (enemy.State.currentMovePoints <= 0 || enemy.State.rooted || enemy.State.bind > 0)
                yield break;

            if (HexAxialCoord.Distance(enemy.State.coord, player.State.coord) <= enemy.State.attackRange)
                yield break;

            List<HexAxialCoord> bestPath = null;
            foreach (var neighbor in grid.GetNeighbors(player.State.coord))
            {
                if (IsMovementBlocked(neighbor, enemy))
                    continue;

                var path = HexBattlePathing.FindPath(grid, enemy.State.coord, neighbor, coord => IsMovementBlocked(coord, enemy));
                if (path == null || path.Count < 2)
                    continue;

                if (bestPath == null || path.Count < bestPath.Count)
                    bestPath = path;
            }

            if (bestPath == null)
                yield break;

            int maxSteps = Mathf.Min(enemy.State.currentMovePoints, bestPath.Count - 1);
            var trimmed = bestPath.Take(maxSteps + 1).ToList();
            yield return MoveUnitRoutine(enemy, trimmed, trimmed.Count - 1, _playerUnit != null ? (HexAxialCoord?)_playerUnit.State.coord : null);
        }

        private IEnumerator HandleBattleEnd(bool playerWon)
        {
            if (_battleFinished)
                yield break;

            _battleFinished = true;
            _busy = true;
            yield return new WaitForSeconds(0.25f);
            _ui.Refresh();
            int goldReward = playerWon && awardVictoryGold ? victoryGoldAmount : 0;
            BattleFinished?.Invoke(playerWon, goldReward, _playerUnit);
        }

        private IEnumerator AutoPassStunnedPlayerTurn()
        {
            _busy = true;
            yield return new WaitForSeconds(0.45f);
            if (_battleFinished)
                yield break;

            StartCoroutine(EndTurnRoutine());
        }

        private void TryProcessPendingEndTurn()
        {
            if (!_pendingEndTurnRequest || _busy || _battleFinished)
                return;
            if (_currentTurn != HexBattleFaction.Player || _playerUnit == null || !_playerUnit.IsAlive)
                return;

            StartCoroutine(EndTurnRoutine());
        }

        private IEnumerator HandlePlayerDefeatFromStatus()
        {
            yield return _playerUnit.PlayDeathAndCleanup();
            yield return HandleBattleEnd(false);
        }

        private IEnumerator ResolveCustomCardRoutine(HexBattleUnit source, HexBattleUnit target, HexCardInstance card, int energySpent, HexAxialCoord targetedCoord, System.Action<bool> setHandled)
        {
            bool handled = true;
            switch (card.definition.id)
            {
                case "C_01_001":
                    yield return ResolveWhirlwindRoutine(source);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_004":
                    yield return ResolveDirectAttackRoutine(source, target, 6);
                    UpgradeOneStarterStrike(source);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_012":
                    yield return ResolveNimbleStrikeRoutine(source, target);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_026":
                    yield return ResolveHarvestRoutine(source, target);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_035":
                    yield return ResolveSpinningBladesRoutine(source);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_036":
                    yield return ResolveCutRoutine(source, target);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_041":
                    yield return ResolveBattleCryRoutine(source);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_044":
                    yield return ResolveTrashCleanupRoutine(source, target);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_017":
                    source.State.negateNextEnemyAttack = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_030":
                    if (CanClashSucceed(source))
                        source.GainInvincible(1);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_047":
                    source.State.burningAuraRadius = Mathf.Max(source.State.burningAuraRadius, 2);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_054":
                    source.State.liquidArmorToVigor = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_058":
                    yield return ResolveArsonRoutine(source, target, card);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_059":
                    source.State.weaponPassivesDoubleThisTurn = true;
                    source.State.consumeWeaponAtEndTurn = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_024":
                    yield return ResolveDirectAttackRoutine(source, target, 8);
                    if (target.IsAlive)
                        target.State.disarm = Mathf.Max(target.State.disarm, 1);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_045":
                    source.State.energy += GetHighestCardCostInHand(source);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_048":
                    source.State.gainStrengthOnSelfDamage = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_049":
                    source.State.firstAttackBurnAmount = Mathf.Max(source.State.firstAttackBurnAmount, 1);
                    source.State.firstAttackBonusPending = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_051":
                    source.State.drawOnExhaust = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_053":
                    source.State.gainMoveOnStrengthOrToughness = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_056":
                    source.State.armorOnExhaustCost += 1;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_01_062":
                    source.State.axeAppliesArmorBreak = true;
                    source.State.hammerDoubleArmorDamage = true;
                    source.State.swordAppliesBrittle = true;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_001":
                    yield return ResolveMoveAdjacentAndAttackRoutine(source, target, 5, knockback: 1);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_002":
                    ApplyTileEffectArea(targetedCoord, 1, HexTileEffectType.Poisoned, 3, 3);
                    AddGeneratedCardsToHand(source, GetToadResourceCard(), 2);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_003":
                    source.ApplyBurn(2);
                    yield return ResolveDirectionalDashRoutine(source, targetedCoord, card.definition.castRange, 4);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_005":
                    source.GainArmor(4);
                    source.State.druidBonusArmorOnNextTransform += 3;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_006":
                    source.GainArmor(3);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_007":
                    source.GainArmor(4);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_010":
                    bool wasMammoth = source.State.druidForm == HexDruidFormType.Mammoth;
                    source.GainArmor(6);
                    source.GainMomentum(1, 2);
                    if (wasMammoth)
                        source.GainStrength(1);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_025":
                    if (target != null && target != source)
                        target.ApplyBleed(3);
                    setHandled?.Invoke(true);
                    yield break;
            }

            switch (card.definition.displayName)
            {
                case "旋风斩":
                    yield return ResolveWhirlwindRoutine(source);
                    break;
                case "预备打击":
                    yield return ResolveDirectAttackRoutine(source, target, 8, onHit: _ => source.GainVigor(8));
                    break;
                case "重锤":
                    yield return ResolveDirectAttackRoutine(source, target, 30);
                    if (target.IsAlive)
                        target.ApplyStun(1);
                    break;
                case "狂暴锤击":
                    yield return ResolveRepeatedHammerRoutine(source, target);
                    break;
                case "狼牙棒":
                    yield return ResolveDirectAttackRoutine(source, target, 12, bleed: 1, vulnerable: 1, knockback: 1);
                    break;
                case "百变打击":
                    yield return ResolveAllWeaponStrikeRoutine(source, target);
                    break;
                case "活动肌肉":
                    source.GainStrength(source.Deck.Hand.Count(instance => instance.definition.cardType == HexCardType.Attack));
                    break;
                case "裂劈":
                    yield return ResolveDirectAttackRoutine(source, target, 7, bleed: Mathf.Max(0, energySpent));
                    break;
                case "剑舞":
                    yield return ResolveRepeatedByTargetHandRoutine(source, target, 7);
                    break;
                case "棒击":
                    yield return ResolveDirectAttackRoutine(source, target, 8, onHit: dealt => source.GainArmor(dealt), addDaze: 1);
                    break;
                case "压制":
                    yield return ResolveDirectAttackRoutine(source, target, 13, weak: 2);
                    break;
                case "戳刺":
                    yield return ResolveDirectAttackRoutine(source, target, 8, bleed: target.State.vulnerable);
                    break;
                case "本垒打":
                    yield return ResolveDirectAttackRoutine(source, target, 22, knockback: 5);
                    break;
                case "毁灭":
                    yield return ResolveDirectAttackRoutine(source, target, 28 + Mathf.Max(0, source.State.strength * 2));
                    break;
                case "喂食":
                    yield return ResolveFeedingStrikeRoutine(source, target);
                    break;
                case "刃甲":
                    source.GainThorns(12);
                    break;
                case "攻守兼备":
                    source.State.armorOnAttackCardThisTurn += 3;
                    break;
                case "武装":
                    source.GainArmor(4);
                    UpgradeRandomCard(source);
                    break;
                case "防御姿态":
                    source.State.weapon = HexWeaponType.None;
                    source.GainToughness(6);
                    break;
                case "整备":
                    source.GainArmor(8);
                    source.State.skillCooldown = 0;
                    DrawCardsForUnit(source, 1);
                    break;
                case "轻装上阵":
                    DiscountSkillCardsInHand(source, -1, true);
                    break;
                case "鲜血护盾":
                    source.ApplyDamage(3);
                    source.GainArmor(15);
                    break;
                case "百变护甲":
                    source.State.armorOnSkillCard += 4;
                    break;
                case "退避":
                    ExhaustRandomHandCard(source);
                    source.State.currentMovePoints += 2;
                    source.GainArmor(8);
                    break;
                case "燃烧契约":
                    source.ApplyBurn(1);
                    DrawCardsForUnit(source, 3);
                    break;
                case "放血":
                    source.ApplyDamage(source.Deck.Hand.Count);
                    source.State.energy += source.Deck.Hand.Count;
                    break;
                case "嘲讽":
                    source.GainArmor(8);
                    break;
                case "钝击":
                    yield return ResolveDirectAttackRoutine(source, target, 18, addDaze: 3);
                    break;
                case "火焰疗法":
                    RemoveOneNegativeStatus(source);
                    source.GainStrength(1);
                    source.ApplyBurn(1);
                    break;
                case "战斗专注":
                    DrawCardsForUnit(source, 3);
                    source.State.drawDisabledThisTurn = true;
                    break;
                case "双持":
                    source.State.attackRepeatBonusThisTurn += 1;
                    break;
                case "血祭":
                    source.ApplyBleed(2);
                    source.State.energy += 2;
                    source.State.currentMovePoints += 3;
                    DrawCardsForUnit(source, 3);
                    break;
                case "嗜血":
                    source.GainStrength(1);
                    break;
                case "炎刃":
                    source.State.firstAttackBurnAmount = Mathf.Max(source.State.firstAttackBurnAmount, 1);
                    break;
                case "称手兵器":
                    source.State.weaponSkillFree = true;
                    break;
                case "新生":
                    DrawCardsForUnit(source, 1);
                    break;
                case "狂战":
                    source.State.extraEnergyPerTurn += 1;
                    source.State.extraMovePerTurn += 1;
                    break;
                case "体能训练":
                    source.State.currentMovePoints += 1;
                    break;
                case "无惧苦痛":
                    source.GainArmor(energySpent);
                    break;
                case "愤怒":
                    yield return ResolveDirectAttackRoutine(source, target, source.Deck.Hand.Count(instance => instance.definition.cardType == HexCardType.Attack));
                    card.definition.amount += 6;
                    break;
                case "无敌斩":
                    DrawCardsCostFree(source, Mathf.Max(0, energySpent) + 2);
                    break;
                case "破碎":
                    if (source.State.weapon != HexWeaponType.None || source.State.allWeaponsEquipped)
                    {
                        yield return ResolveDirectAttackRoutine(source, target, 16);
                        source.State.weapon = HexWeaponType.None;
                        source.State.allWeaponsEquipped = false;
                    }
                    break;
                case "破舰者":
                    source.GainStrength(1);
                    break;
                case "三刀流":
                    source.State.allWeaponsEquipped = true;
                    source.State.cannotUseSkills = true;
                    break;
                default:
                    handled = false;
                    break;
            }

            setHandled?.Invoke(handled);
        }

        private IEnumerator ResolveWhirlwindRoutine(HexBattleUnit source)
        {
            source.PlayAttackAnimation();
            yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                enemy.ApplyDamage(GetModifiedDamage(source, enemy, 6));
                enemy.ApplyBleed(1);
                if (enemy.IsAlive)
                    enemy.PlayHitAnimation();
                else
                    yield return enemy.PlayDeathAndCleanup();
            }
        }

        private IEnumerator ResolveRepeatedHammerRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            int repeatCount = Mathf.Clamp(source.State.strength, 0, 3) + 1;
            for (int i = 0; i < repeatCount; i++)
            {
                if (!target.IsAlive || !source.IsAlive)
                    break;

                yield return ResolveDirectAttackRoutine(source, target, 8, knockback: 1);
            }
        }

        private IEnumerator ResolveAllWeaponStrikeRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            var originalWeapon = source.State.weapon;
            var weaponList = new List<HexWeaponType> { HexWeaponType.Sword, HexWeaponType.Axe, HexWeaponType.Hammer };
            for (int i = 0; i < weaponList.Count; i++)
            {
                if (!target.IsAlive || !source.IsAlive)
                    break;

                source.State.weapon = weaponList[i];
                yield return ResolveDirectAttackRoutine(source, target, 8);
            }

            source.State.weapon = originalWeapon;
        }

        private IEnumerator ResolveRepeatedByTargetHandRoutine(HexBattleUnit source, HexBattleUnit target, int baseDamage)
        {
            int repeatCount = Mathf.Max(1, target.Deck.Hand.Count);
            for (int i = 0; i < repeatCount; i++)
            {
                if (!target.IsAlive || !source.IsAlive)
                    break;

                yield return ResolveDirectAttackRoutine(source, target, baseDamage);
            }
        }

        private IEnumerator ResolveHarvestRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            HexAxialCoord center = target != null ? target.State.coord : source.State.coord;
            var targets = GetEnemiesInArea(center, 2, source);
            int totalHealing = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var enemy = targets[i];
                if (!enemy.IsAlive)
                    continue;

                int beforeHealth = enemy.State.currentHealth;
                yield return ResolveDirectAttackRoutine(source, enemy, 3);
                int dealt = Mathf.Max(0, beforeHealth - enemy.State.currentHealth);
                totalHealing += dealt;
            }

            if (totalHealing > 0 && source.IsAlive)
                source.State.currentHealth = Mathf.Min(source.State.maxHealth, source.State.currentHealth + totalHealing);
        }

        private IEnumerator ResolveTrashCleanupRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            HexAxialCoord center = target != null ? target.State.coord : source.State.coord;
            var initialTargets = GetEnemiesInArea(center, 3, source);
            int repeatCount = Mathf.Max(1, initialTargets.Count);
            for (int repeat = 0; repeat < repeatCount; repeat++)
            {
                var targets = GetEnemiesInArea(center, 3, source);
                if (targets.Count == 0 || !source.IsAlive)
                    yield break;

                Vector3 centerPoint = grid != null ? grid.AxialToWorld(center) : targets[0].transform.position;
                source.FaceTarget(centerPoint);
                source.PlayAttackAnimation();
                yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));

                for (int i = 0; i < targets.Count; i++)
                {
                    var areaTarget = targets[i];
                    if (areaTarget == null || !areaTarget.IsAlive || !source.IsAlive)
                        continue;

                    areaTarget.FaceTarget(source.transform.position);
                    int dealt = GetModifiedDamage(source, areaTarget, 6 + Mathf.Max(0, source.State.strength));
                    ApplyAttackDamage(source, areaTarget, dealt);
                    source.State.damageDealtThisTurn += dealt;

                    bool survived = areaTarget.IsAlive;
                    if (survived)
                    {
                        areaTarget.PlayHitAnimation();
                        if (areaTarget.State.thorns > 0 && source.IsAlive)
                            ApplyDamageToUnit(source, areaTarget.State.thorns, areaTarget);
                    }
                }

                float longestHitDuration = 0.08f;
                for (int i = 0; i < targets.Count; i++)
                {
                    var areaTarget = targets[i];
                    if (areaTarget == null)
                        continue;

                    longestHitDuration = Mathf.Max(longestHitDuration, areaTarget.IsAlive
                        ? areaTarget.GetHitDuration() * 0.85f
                        : areaTarget.GetDeathDuration());
                }

                yield return new WaitForSeconds(Mathf.Max(0.08f, longestHitDuration));

                for (int i = 0; i < targets.Count; i++)
                {
                    var areaTarget = targets[i];
                    if (areaTarget == null || areaTarget.IsAlive)
                        continue;

                    yield return areaTarget.PlayDeathAndCleanup();
                }
            }
        }

        private IEnumerator ResolveSpinningBladesRoutine(HexBattleUnit source)
        {
            for (int i = 0; i < 3; i++)
            {
                var aliveEnemies = _enemyUnits.Where(enemy => enemy != null && enemy.IsAlive).ToList();
                if (aliveEnemies.Count == 0 || !source.IsAlive)
                    yield break;

                var enemy = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
                yield return ResolveDirectAttackRoutine(source, enemy, 2, vulnerable: 1);
            }
        }

        private IEnumerator ResolveCutRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            for (int i = 0; i < 2; i++)
            {
                if (!source.IsAlive || !target.IsAlive)
                    break;

                yield return ResolveDirectAttackRoutine(source, target, 5);
            }

            if (target.IsAlive)
                target.Deck.AddToDrawPile(HexCardLibrary.GetWound());
        }

        private IEnumerator ResolveBattleCryRoutine(HexBattleUnit source)
        {
            source.PlayAttackAnimation();
            yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.35f));
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                enemy.ApplyWeak(1);
            }
        }

        private IEnumerator ResolveNimbleStrikeRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            yield return ResolveDirectAttackRoutine(source, target, 4);
            if (!target.IsAlive)
                yield break;

            int pendingDraw = source.State.nextAttackDrawCards;
            int pendingVulnerable = source.State.nextAttackApplyVulnerable;
            if (pendingDraw > 0)
                source.QueueNextAttackDraw(pendingDraw);
            if (pendingVulnerable > 0)
                source.QueueNextAttackVulnerable(pendingVulnerable);

            switch (source.State.weapon)
            {
                case HexWeaponType.Sword:
                    source.QueueNextAttackDraw(2);
                    break;
                case HexWeaponType.Axe:
                    source.QueueNextAttackVulnerable(2);
                    break;
                case HexWeaponType.Hammer:
                    source.GainStrength(2);
                    break;
            }
        }

        private IEnumerator ResolveMoveAdjacentAndAttackRoutine(HexBattleUnit source, HexBattleUnit target, int damage, int knockback = 0)
        {
            if (source == null || target == null)
                yield break;

            var path = FindBestApproachPath(source, target.State.coord, 1);
            if (path != null && path.Count >= 2)
                yield return MoveUnitRoutine(source, path, 0, target.State.coord);

            if (!source.IsAlive || !target.IsAlive)
                yield break;

            yield return ResolveDirectAttackRoutine(source, target, damage, knockback: knockback);
        }

        private IEnumerator ResolveDirectionalDashRoutine(HexBattleUnit source, HexAxialCoord aimedCoord, int maxDistance, int passThroughDamage)
        {
            if (source == null || grid == null || aimedCoord.Equals(source.State.coord))
                yield break;

            int directionIndex = HexBattlePathing.GetPrimaryDirectionIndex(grid, source.State.coord, aimedCoord);
            var path = BuildDirectionalMovementPath(source, directionIndex, maxDistance);
            if (path == null || path.Count < 2)
                yield break;

            var hitEnemies = new HashSet<HexBattleUnit>();
            for (int i = 1; i < path.Count; i++)
            {
                var enemy = FindUnitAtCoord(path[i], source);
                if (enemy != null && enemy.State.faction != source.State.faction)
                    hitEnemies.Add(enemy);
            }

            yield return MoveUnitRoutine(source, path, 0, aimedCoord);

            foreach (var enemy in hitEnemies)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                ApplyAttackDamage(source, enemy, GetModifiedDamage(source, enemy, passThroughDamage + Mathf.Max(0, source.State.strength)));
                if (enemy.IsAlive)
                    enemy.PlayHitAnimation();
                else
                    yield return enemy.PlayDeathAndCleanup();
            }
        }

        private IEnumerator ResolveArsonRoutine(HexBattleUnit source, HexBattleUnit target, HexCardInstance card)
        {
            var cardsToExhaust = source.Deck.Hand.Where(instance => instance != card).ToList();
            int repeatCount = cardsToExhaust.Count;
            for (int i = 0; i < cardsToExhaust.Count; i++)
                DiscardOrExhaustCard(source, cardsToExhaust[i], true);

            for (int i = 0; i < repeatCount; i++)
            {
                if (!source.IsAlive || !target.IsAlive)
                    break;

                yield return ResolveDirectAttackRoutine(source, target, 7);
                if (target.IsAlive)
                    target.ApplyBurn(1);
            }
        }

        private IEnumerator ResolveFeedingStrikeRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            int damage = target.State.maxHealth < source.State.maxHealth ? 20 : 10;
            bool targetWasAlive = target.IsAlive;
            yield return ResolveDirectAttackRoutine(source, target, damage);
            if (targetWasAlive && !target.IsAlive)
            {
                source.State.maxHealth += 3;
                source.State.currentHealth = Mathf.Min(source.State.maxHealth, source.State.currentHealth + 3);
            }
        }

        private static void UpgradeOneStarterStrike(HexBattleUnit unit)
        {
            if (unit == null)
                return;

            var cards = unit.Deck.DrawPile.Concat(unit.Deck.DiscardPile).Concat(unit.Deck.Hand).ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.definition.id != "attack_strike")
                    continue;

                card.upgraded = true;
                return;
            }
        }

        private IEnumerator ResolveDirectAttackRoutine(
            HexBattleUnit source,
            HexBattleUnit target,
            int baseDamage,
            int bleed = 0,
            int weak = 0,
            int vulnerable = 0,
            int knockback = 0,
            int addDaze = 0,
            System.Action<int> onHit = null)
        {
            int repeatCount = 1 + Mathf.Max(0, source.State.attackRepeatBonusThisTurn);
            for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
            {
                if (!target.IsAlive || !source.IsAlive)
                    break;

                source.FaceTarget(target.transform.position);
                source.PlayAttackAnimation();
                yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));
                target.FaceTarget(source.transform.position);

                int dealt = GetModifiedDamage(source, target, baseDamage + Mathf.Max(0, source.State.strength));
                ApplyAttackDamage(source, target, dealt);
                onHit?.Invoke(dealt);
                bool targetSurvivedHit = target.IsAlive;
                if (targetSurvivedHit)
                {
                    target.PlayHitAnimation();
                    yield return new WaitForSeconds(Mathf.Max(0.08f, target.GetHitDuration() * 0.85f));
                    if (target.State.thorns > 0 && source.IsAlive)
                        ApplyDamageToUnit(source, target.State.thorns, target);
                }

                if (bleed > 0)
                    target.ApplyBleed(bleed);
                if (weak > 0)
                    target.ApplyWeak(weak);
                if (vulnerable > 0)
                    target.ApplyVulnerable(vulnerable);
                if (addDaze > 0)
                {
                    for (int i = 0; i < addDaze; i++)
                        target.Deck.AddToDrawPile(HexCardLibrary.GetDaze());
                }
                if (knockback > 0)
                    yield return ApplyKnockbackRoutine(source, target, knockback);

                if (!target.IsAlive)
                    yield return target.PlayDeathAndCleanup();
            }
        }

        private static void UpgradeRandomCard(HexBattleUnit unit)
        {
            if (unit == null)
                return;

            var cards = unit.Deck.DrawPile.Concat(unit.Deck.DiscardPile).Concat(unit.Deck.Hand).ToList();
            if (cards.Count == 0)
                return;

            cards[Random.Range(0, cards.Count)].upgraded = true;
        }

        private static void DiscountSkillCardsInHand(HexBattleUnit unit, int costModifier, bool exhaustWhenPlayed)
        {
            if (unit == null)
                return;

            for (int i = 0; i < unit.Deck.Hand.Count; i++)
            {
                var card = unit.Deck.Hand[i];
                if (card.definition.cardType != HexCardType.Skill)
                    continue;

                card.temporaryCostModifier += costModifier;
                card.exhaustWhenPlayed |= exhaustWhenPlayed;
            }
        }

        private void ExhaustRandomHandCard(HexBattleUnit unit)
        {
            if (unit == null || unit.Deck.Hand.Count == 0)
                return;

            int index = Random.Range(0, unit.Deck.Hand.Count);
            DiscardOrExhaustCard(unit, unit.Deck.Hand[index], true);
        }

        private static void RemoveOneNegativeStatus(HexBattleUnit unit)
        {
            if (unit == null)
                return;

            if (unit.State.bleed > 0)
                unit.State.bleed = Mathf.Max(0, unit.State.bleed - 1);
            else if (unit.State.burn > 0)
                unit.State.burn = Mathf.Max(0, unit.State.burn - 1);
            else if (unit.State.entangle > 0)
                unit.State.entangle = Mathf.Max(0, unit.State.entangle - 1);
            else if (unit.State.vulnerable > 0)
                unit.State.vulnerable = Mathf.Max(0, unit.State.vulnerable - 1);
            else if (unit.State.weak > 0)
                unit.State.weak = Mathf.Max(0, unit.State.weak - 1);
        }

        private void DrawCardsCostFree(HexBattleUnit unit, int count)
        {
            if (unit == null || count <= 0)
                return;

            int beforeCount = unit.Deck.Hand.Count;
            DrawCardsForUnit(unit, count, true);
            for (int i = beforeCount; i < unit.Deck.Hand.Count; i++)
                unit.Deck.Hand[i].costsNoEnergyThisTurn = true;
        }

        private void ApplyBurningAura(HexBattleUnit source)
        {
            if (source == null || !source.IsAlive || source.State.burningAuraRadius <= 0)
                return;

            var targets = GetEnemiesInArea(source.State.coord, source.State.burningAuraRadius, source);
            for (int i = 0; i < targets.Count; i++)
                targets[i].ApplyBurn(1);
        }

        private void ApplyEnemyTurnEndPlayerEffects()
        {
            if (_playerUnit == null || !_playerUnit.IsAlive)
                return;

            if (_playerUnit.State.liquidArmorToVigor && _playerUnit.State.armor > 0)
            {
                _playerUnit.GainVigor(_playerUnit.State.armor);
                _playerUnit.State.armor = 0;
            }
        }

        private static bool IsDruid(HexBattleUnit unit)
        {
            return unit != null && unit.State != null && unit.State.profession == HexCardProfession.Druid;
        }

        private static bool IsToadJumpMovement(HexBattleUnit unit)
        {
            return IsDruid(unit) && unit.State.druidForm == HexDruidFormType.Toad;
        }

        private static bool HasLavaLizardMovementPhase(HexBattleUnit unit)
        {
            return IsDruid(unit) && unit.State.druidForm == HexDruidFormType.LavaLizard;
        }

        private bool CanIgnoreOccupiedTilesWhileMoving(HexBattleUnit unit)
        {
            return unit != null &&
                   unit.State != null &&
                   (unit.State.phaseMovement > 0 || HasLavaLizardMovementPhase(unit));
        }

        private void ApplyDruidTransformFromCard(HexBattleUnit unit, HexCardDefinition definition)
        {
            if (!IsDruid(unit) || definition == null)
                return;

            var form = HexCardLibrary.GetDruidForm(definition);
            if (form == HexDruidFormType.None)
                return;

            unit.State.druidForm = form;
            unit.State.rooted = form == HexDruidFormType.Rafflesia;
            if (unit.State.druidBonusArmorOnNextTransform > 0)
            {
                unit.GainArmor(unit.State.druidBonusArmorOnNextTransform);
                unit.State.druidBonusArmorOnNextTransform = 0;
            }
        }

        private void ApplyDruidBeginTurnPassives(HexBattleUnit unit)
        {
            if (!IsDruid(unit) || !unit.IsAlive)
                return;

            if (unit.State.druidForm == HexDruidFormType.LavaLizard)
                unit.ApplyBurn(1);
        }

        private void ApplyDruidEndTurnPassives(HexBattleUnit unit)
        {
            if (!IsDruid(unit) || unit == null)
                return;

            switch (unit.State.druidForm)
            {
                case HexDruidFormType.Toad:
                    AddGeneratedCardToHand(unit, GetToadResourceCard());
                    break;
                case HexDruidFormType.Rafflesia:
                    if (unit.State.currentMovePoints > 0)
                    {
                        unit.GainArmor(unit.State.currentMovePoints);
                        unit.State.currentMovePoints = 0;
                    }
                    break;
            }
        }

        private void HandlePostMovementPassives(HexBattleUnit unit, IReadOnlyList<HexAxialCoord> path, HexAxialCoord? towardTargetCoord, int movedDistance)
        {
            if (!IsDruid(unit) || path == null || path.Count < 2 || movedDistance <= 0)
                return;

            if (towardTargetCoord.HasValue &&
                unit.State.druidForm == HexDruidFormType.Mammoth &&
                IsPathMovingTowardTarget(path, towardTargetCoord.Value))
            {
                unit.GainMomentum(1, 2);
            }
        }

        private void ApplyForcedMovementCollisionEffects(HexBattleUnit source, HexBattleUnit target, ForcedMovementResult movement)
        {
            if (source == null || target == null || movement == null || !movement.collided)
                return;

            if (IsDruid(source) && source.State.druidForm == HexDruidFormType.Mammoth && source.State.strength > 0)
                ApplyDamageToUnit(target, source.State.strength, source);
        }

        private HexCardDefinition GetToadResourceCard()
        {
            return HexCardLibrary.GetCardById("C_03_025") ?? HexCardLibrary.GetCardById("C_03_032");
        }

        private void AddGeneratedCardsToHand(HexBattleUnit unit, HexCardDefinition definition, int count)
        {
            if (unit == null || definition == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
                unit.Deck.AddToHand(definition);
        }

        private static void AddGeneratedCardToHand(HexBattleUnit unit, HexCardDefinition definition)
        {
            if (unit == null || definition == null)
                return;

            unit.Deck.AddToHand(definition);
        }

        private List<HexAxialCoord> FindBestApproachPath(HexBattleUnit movingUnit, HexAxialCoord targetCoord, int desiredDistance)
        {
            if (grid == null || movingUnit == null)
                return null;

            List<HexAxialCoord> bestPath = null;
            float bestDistanceScore = float.PositiveInfinity;
            foreach (var candidate in grid.Tiles.Keys.OrderBy(coord => coord.q).ThenBy(coord => coord.r))
            {
                if (candidate.Equals(movingUnit.State.coord))
                    continue;
                if (HexAxialCoord.Distance(candidate, targetCoord) != desiredDistance)
                    continue;
                if (IsMovementDestinationBlocked(candidate, movingUnit))
                    continue;

                List<HexAxialCoord> path = IsToadJumpMovement(movingUnit)
                    ? new List<HexAxialCoord> { movingUnit.State.coord, candidate }
                    : HexBattlePathing.FindPath(grid, movingUnit.State.coord, candidate, coord => IsMovementBlocked(coord, movingUnit));
                if (path == null || path.Count < 2)
                    continue;

                float score = path.Count + GetStraightLineDistance(candidate, targetCoord);
                if (bestPath == null || score < bestDistanceScore)
                {
                    bestPath = path;
                    bestDistanceScore = score;
                }
            }

            return bestPath;
        }

        private List<HexAxialCoord> BuildDirectionalMovementPath(HexBattleUnit movingUnit, int directionIndex, int maxDistance)
        {
            var path = new List<HexAxialCoord> { movingUnit.State.coord };
            HexAxialCoord current = movingUnit.State.coord;
            for (int step = 0; step < maxDistance; step++)
            {
                HexAxialCoord next = HexAxialCoord.Neighbor(current, directionIndex);
                if (grid == null || !grid.IsCoordInside(next) || HasSceneObstacleAtCoord(next, movingUnit))
                    break;

                path.Add(next);
                current = next;
            }

            return path;
        }

        private void ApplyTileEffectArea(HexAxialCoord centerCoord, int radius, HexTileEffectType effectType, int stacks, int duration)
        {
            if (grid == null)
                return;

            foreach (var coord in HexBattlePathing.GetCoordsInRange(centerCoord, radius))
            {
                if (!grid.TryGetTile(coord, out var tile) || tile == null)
                    continue;

                tile.AddOrRefreshEffect(effectType, stacks, duration);
                tile.FlashClick();
            }
        }

        private bool CanConvertArmorCardToPlantHealing(HexBattleUnit source, HexBattleUnit target, HexCardDefinition definition)
        {
            return source != null &&
                   target != null &&
                   definition != null &&
                   source.State.druidForm == HexDruidFormType.Rafflesia &&
                   definition.effectType == HexCardEffectType.Defend &&
                   target.State.faction == source.State.faction &&
                   target.State.isPlant;
        }

        private string GetDruidPassiveSummary(HexBattleUnit unit)
        {
            string formLabel = unit.State.druidForm switch
            {
                HexDruidFormType.Mammoth => "Mammoth",
                HexDruidFormType.Toad => "Toad",
                HexDruidFormType.LavaLizard => "Lizard",
                HexDruidFormType.Rafflesia => "Rafflesia",
                _ => "None",
            };

            string passiveLabel = unit.State.druidForm switch
            {
                HexDruidFormType.Mammoth => $"Momentum {unit.State.momentum}/2",
                HexDruidFormType.Toad => "Jump Move + Toxic Sac",
                HexDruidFormType.LavaLizard => $"Burn Immune  Burn {unit.State.burn}",
                HexDruidFormType.Rafflesia => "Rooted  Armor -> Plant Heal",
                _ => "Passive Form",
            };

            return $"Form  {formLabel}\nTrait  {passiveLabel}";
        }

        private bool CanClashSucceed(HexBattleUnit source)
        {
            if (source == null)
                return false;

            return source.State.damageDealtThisTurn > EstimateEnemyPlannedDamage();
        }

        private void OnUnitEnteredTile(HexBattleUnit unit, HexAxialCoord coord)
        {
            if (unit == null || grid == null)
                return;

            if (HasLavaLizardMovementPhase(unit) && unit.State.burn > 0)
            {
                var passedEnemy = FindUnitAtCoord(coord, unit);
                if (passedEnemy != null && passedEnemy.State.faction != unit.State.faction)
                    passedEnemy.ApplyBurn(unit.State.burn);
            }

            if (!grid.TryGetTile(coord, out var tile) || tile == null)
                return;

            ApplyTileEffectsToUnit(tile, unit);
        }

        private void ApplyTileEffectsToUnit(HexTile tile, HexBattleUnit unit)
        {
            if (tile == null || unit == null)
                return;

            var effects = tile.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                switch (effect.effectType)
                {
                    case HexTileEffectType.Burning:
                        unit.ApplyBurn(effect.stacks);
                        break;
                    case HexTileEffectType.Poisoned:
                        unit.ApplyBleed(effect.stacks);
                        break;
                    case HexTileEffectType.Entangled:
                        unit.ApplyEntangle(effect.stacks);
                        break;
                }
            }

            unit.RefreshLabel();
        }

        private int EstimateEnemyPlannedDamage()
        {
            int total = 0;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                for (int cardIndex = 0; cardIndex < enemy.Deck.Hand.Count; cardIndex++)
                {
                    var card = enemy.Deck.Hand[cardIndex];
                    if (card.definition.effectType == HexCardEffectType.Attack)
                        total += Mathf.Max(0, card.definition.amount + enemy.State.strength);
                }
            }

            return total;
        }

        private void DrawCardsForUnit(HexBattleUnit unit, int count, bool ignoreDrawBlock = false)
        {
            if (unit == null || count <= 0)
                return;
            if (!ignoreDrawBlock && unit.State.drawDisabledThisTurn)
                return;

            unit.Deck.DrawCards(count);
        }

        private void DiscardOrExhaustCard(HexBattleUnit unit, HexCardInstance card, bool exhaust)
        {
            if (unit == null || card == null)
                return;

            int exhaustedCost = unit.GetCardEnergyCost(card);
            card.temporaryCostModifier = 0;
            card.costsNoEnergyThisTurn = false;
            unit.Deck.DiscardFromHand(card, exhaust);
            if (!exhaust)
                return;

            if (unit.State.drawOnExhaust)
                DrawCardsForUnit(unit, 1);
            if (unit.State.armorOnExhaustCost > 0)
                unit.GainArmor(exhaustedCost * unit.State.armorOnExhaustCost);
        }

        private void ApplyDamageToUnit(HexBattleUnit target, int amount, HexBattleUnit source)
        {
            if (target == null || amount <= 0)
                return;

            int beforeHealth = target.State.currentHealth;
            target.ApplyDamage(amount);
            int healthLost = Mathf.Max(0, beforeHealth - target.State.currentHealth);
            if (healthLost > 0 && target == source && target.State.gainStrengthOnSelfDamage)
                target.GainStrength(1);
        }

        private void ApplyAttackDamage(HexBattleUnit source, HexBattleUnit target, int amount)
        {
            if (target == null || amount <= 0)
                return;

            bool axeArmorBreak = source != null && source.State.axeAppliesArmorBreak &&
                (source.State.weapon == HexWeaponType.Axe || source.State.allWeaponsEquipped);
            bool swordBrittle = source != null && source.State.swordAppliesBrittle &&
                (source.State.weapon == HexWeaponType.Sword || source.State.allWeaponsEquipped);
            bool hammerArmorCrush = source != null && source.State.hammerDoubleArmorDamage &&
                (source.State.weapon == HexWeaponType.Hammer || source.State.allWeaponsEquipped);

            if (axeArmorBreak)
                target.State.armorBreak += 1;
            if (swordBrittle)
                target.State.brittle += 1;
            if (hammerArmorCrush && target.State.armor > 0)
                target.State.armor = Mathf.Max(0, target.State.armor - Mathf.Min(target.State.armor, amount));

            if (target.State.deflect > 0)
            {
                amount = Mathf.CeilToInt(amount * 0.75f);
                target.State.deflect = Mathf.Max(0, target.State.deflect - 1);
            }
            if (target.State.block > 0)
                amount = Mathf.Max(0, amount - target.State.block);

            target.ApplyDamage(amount);
        }

        private bool CanAttackTarget(HexBattleUnit source, HexBattleUnit candidate)
        {
            if (source == null || candidate == null || !candidate.IsAlive)
                return false;

            if (!TryGetRequiredAttackTarget(source, out var requiredTarget))
                return true;

            return candidate == requiredTarget;
        }

        private bool TryGetRequiredAttackTarget(HexBattleUnit source, out HexBattleUnit requiredTarget)
        {
            requiredTarget = null;
            if (source?.State == null || source.State.tauntActiveThisTurn <= 0 || !source.State.hasTauntSource)
                return false;

            var tauntSource = FindUnitAtCoord(source.State.tauntSourceCoord, source);
            if (tauntSource == null || !tauntSource.IsAlive || tauntSource.State.faction == source.State.faction)
                return false;

            requiredTarget = tauntSource;
            return true;
        }

        private HexBattleUnit GetPrimaryEnemyTarget(HexBattleUnit enemy)
        {
            if (TryGetRequiredAttackTarget(enemy, out var requiredTarget))
                return requiredTarget;

            return _playerUnit;
        }

        private IEnumerator ResolveUnitTurnStartStatuses(HexBattleUnit unit)
        {
            if (unit == null || !unit.IsAlive || unit.State == null)
                yield break;

            if (unit.State.allure > 0)
                yield return ResolveAllureRoutine(unit);

            int confusionCount = unit.State.confusion;
            unit.State.confusion = 0;
            for (int i = 0; i < confusionCount; i++)
            {
                if (unit == null || !unit.IsAlive || unit.Deck.Hand.Count == 0)
                    yield break;

                if (!TryChooseConfusionPlay(unit, out var randomCard, out var randomTarget, out var randomCoord))
                    break;

                yield return ResolveCardRoutine(unit, randomTarget, randomCard, randomCoord);
                if (_battleFinished)
                    yield break;
            }

            unit.RefreshLabel();
            _ui.Refresh();
        }

        private IEnumerator ResolveAllureRoutine(HexBattleUnit unit)
        {
            if (unit == null || unit.State == null)
                yield break;

            int totalMove = unit.State.currentMovePoints;
            if (totalMove <= 0 || !unit.State.hasAllureSource)
            {
                unit.ClearAllure();
                yield break;
            }

            if (!unit.State.rooted && unit.State.bind <= 0)
            {
                var path = FindBestApproachPath(unit, unit.State.allureSourceCoord, 1);
                if (path != null && path.Count >= 2)
                {
                    int maxSteps = Mathf.Min(totalMove, path.Count - 1);
                    var trimmed = path.Take(maxSteps + 1).ToList();
                    yield return MoveUnitRoutine(unit, trimmed, maxSteps, unit.State.allureSourceCoord);
                }
            }

            unit.SpendMovePoints(unit.State.currentMovePoints);
            unit.ClearAllure();
        }

        private bool TryChooseConfusionPlay(HexBattleUnit unit, out HexCardInstance chosenCard, out HexBattleUnit target, out HexAxialCoord? coord)
        {
            chosenCard = null;
            target = null;
            coord = null;
            if (unit == null)
                return false;

            var playableCards = unit.Deck.Hand
                .Where(card => card != null && card.definition != null && !card.definition.isUnplayable)
                .Where(unit.CanPay)
                .OrderBy(_ => Random.value)
                .ToList();
            for (int i = 0; i < playableCards.Count; i++)
            {
                var card = playableCards[i];
                if (TryGetAutoplayTarget(unit, card, out target, out coord))
                {
                    chosenCard = card;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetAutoplayTarget(HexBattleUnit source, HexCardInstance card, out HexBattleUnit target, out HexAxialCoord? coord)
        {
            target = null;
            coord = null;
            if (source == null || card?.definition == null)
                return false;

            if (card.definition.targetType == HexCardTargetType.Self || card.definition.effectType == HexCardEffectType.Defend)
            {
                target = source;
                return true;
            }

            var preferredTarget = GetAutoAttackTarget(source);
            if (preferredTarget == null || !preferredTarget.IsAlive)
                return false;

            if (card.definition.targetType == HexCardTargetType.Direction)
            {
                var directionalTargets = GetDirectionalTargets(source, preferredTarget.State.coord, card.definition);
                if (directionalTargets.Count == 0)
                    return false;
                if (card.definition.cardType == HexCardType.Attack && !CanAttackTarget(source, preferredTarget))
                    return false;

                target = directionalTargets[0];
                coord = preferredTarget.State.coord;
                return true;
            }

            if (card.definition.targetType == HexCardTargetType.Tile)
            {
                if (HexAxialCoord.Distance(source.State.coord, preferredTarget.State.coord) > card.definition.castRange)
                    return false;

                var areaTargets = GetEnemiesInArea(preferredTarget.State.coord, card.definition.effectRadius, source);
                if (card.definition.cardType == HexCardType.Attack && !areaTargets.Contains(preferredTarget))
                    return false;

                target = areaTargets.Count > 0 ? areaTargets[0] : preferredTarget;
                coord = preferredTarget.State.coord;
                return true;
            }

            if (card.definition.effectRadius > 0)
            {
                if (HexAxialCoord.Distance(source.State.coord, preferredTarget.State.coord) > card.definition.castRange)
                    return false;

                var areaTargets = GetEnemiesInArea(preferredTarget.State.coord, card.definition.effectRadius, source);
                if (areaTargets.Count == 0)
                    return false;
                if (card.definition.cardType == HexCardType.Attack && !areaTargets.Contains(preferredTarget))
                    return false;

                target = areaTargets[0];
                return true;
            }

            if (HexAxialCoord.Distance(source.State.coord, preferredTarget.State.coord) > card.definition.castRange)
                return false;
            if (card.definition.cardType == HexCardType.Attack && !CanAttackTarget(source, preferredTarget))
                return false;

            target = preferredTarget;
            return true;
        }

        private HexBattleUnit GetAutoAttackTarget(HexBattleUnit source)
        {
            if (source == null)
                return null;

            if (TryGetRequiredAttackTarget(source, out var requiredTarget))
                return requiredTarget;

            return _units
                .Where(unit => unit != null && unit.IsAlive && unit != source && unit.State.faction != source.State.faction)
                .OrderBy(unit => HexAxialCoord.Distance(source.State.coord, unit.State.coord))
                .FirstOrDefault();
        }

        private static int GetHighestCardCostInHand(HexBattleUnit unit)
        {
            if (unit == null || unit.Deck.Hand.Count == 0)
                return 0;

            int highest = 0;
            for (int i = 0; i < unit.Deck.Hand.Count; i++)
                highest = Mathf.Max(highest, unit.GetCardEnergyCost(unit.Deck.Hand[i]));
            return highest;
        }

        private static int GetModifiedDamage(HexBattleUnit source, HexBattleUnit target, int baseDamage)
        {
            int result = baseDamage;
            if (source != null && source.State != null)
            {
                if (source.State.weak > 0)
                    result = Mathf.FloorToInt(result * 0.75f);
                if (source.State.vigor > 0)
                {
                    result += source.State.vigor;
                    source.State.vigor = 0;
                }
            }

            if (target != null && target.State != null && target.State.vulnerable > 0)
                result = Mathf.CeilToInt(result * 1.25f);

            if (source != null && source.State != null && source.State.momentum > 0)
            {
                result = Mathf.CeilToInt(result * 1.5f);
                source.State.momentum = Mathf.Max(0, source.State.momentum - 1);
            }

            return Mathf.Max(0, result);
        }

        private static void AppendStatusEffects(StringBuilder builder, HexBattleUnit unit)
        {
            if (unit == null || unit.State == null)
                return;

            if (unit.State.strength > 0)
                builder.Append($"  Strength {unit.State.strength}");
            if (unit.State.toughness > 0)
                builder.Append($"  Toughness {unit.State.toughness}");
            if (unit.State.vigor > 0)
                builder.Append($"  Vigor {unit.State.vigor}");
            if (unit.State.bleed > 0)
                builder.Append($"  Bleed {unit.State.bleed}");
            if (unit.State.vulnerable > 0)
                builder.Append($"  Vulnerable {unit.State.vulnerable}");
            if (unit.State.weak > 0)
                builder.Append($"  Weak {unit.State.weak}");
            if (unit.State.stun > 0)
                builder.Append($"  Stun {unit.State.stun}");
            if (unit.State.blind > 0)
                builder.Append($"  Blind {unit.State.blind}");
            if (unit.State.nausea > 0)
                builder.Append($"  Nausea {unit.State.nausea}");
            if (unit.State.curse > 0)
                builder.Append($"  Curse {unit.State.curse}");
            if (unit.State.allure > 0)
                builder.Append($"  Allure {unit.State.allure}");
            if (unit.State.taunt > 0 || unit.State.tauntActiveThisTurn > 0)
                builder.Append($"  Taunt {Mathf.Max(unit.State.taunt, unit.State.tauntActiveThisTurn)}");
            if (unit.State.confusion > 0)
                builder.Append($"  Confusion {unit.State.confusion}");
            if (unit.State.burn > 0)
                builder.Append($"  Burn {unit.State.burn}");
            if (unit.State.entangle > 0)
                builder.Append($"  Entangle {unit.State.entangle}");
            if (unit.State.cold > 0)
                builder.Append($"  Cold {unit.State.cold}");
            if (unit.State.fatigue > 0)
                builder.Append($"  Fatigue {unit.State.fatigue}");
            if (unit.State.paralysis > 0 || unit.State.paralysisActiveThisTurn > 0)
                builder.Append($"  Paralysis {Mathf.Max(unit.State.paralysis, unit.State.paralysisActiveThisTurn)}");
            if (unit.State.slow > 0)
                builder.Append($"  Slow {unit.State.slow}");
            if (unit.State.frozen > 0)
                builder.Append($"  Frozen {unit.State.frozen}");
            if (unit.State.bind > 0)
                builder.Append($"  Bind {unit.State.bind}");
            if (unit.State.agility > 0)
                builder.Append($"  Agility {unit.State.agility}");
            if (unit.State.wisdom > 0)
                builder.Append($"  Wisdom {unit.State.wisdom}");
            if (unit.State.humility > 0)
                builder.Append($"  Humility {unit.State.humility}");
            if (unit.State.luck > 0)
                builder.Append($"  Luck {unit.State.luck}");
            if (unit.State.momentum > 0)
                builder.Append($"  Momentum {unit.State.momentum}");
            if (unit.State.phaseMovement > 0)
                builder.Append($"  Phase {unit.State.phaseMovement}");
            if (unit.State.rooted)
                builder.Append("  Rooted");
            if (unit.State.armorBreak > 0)
                builder.Append($"  ArmorBreak {unit.State.armorBreak}");
            if (unit.State.brittle > 0)
                builder.Append($"  Brittle {unit.State.brittle}");
            if (unit.State.disarm > 0)
                builder.Append($"  Disarm {unit.State.disarm}");
            if (unit.State.holyShield > 0)
                builder.Append($"  HolyShield {unit.State.holyShield}");
            if (unit.State.immunity > 0)
                builder.Append($"  Immunity {unit.State.immunity}");
            if (unit.State.invincible > 0)
                builder.Append($"  Invincible {unit.State.invincible}");
            if (unit.State.deflect > 0)
                builder.Append($"  Deflect {unit.State.deflect}");
            if (unit.State.block > 0)
                builder.Append($"  Block {unit.State.block}");
            if (unit.State.thorns > 0)
                builder.Append($"  Thorns {unit.State.thorns}");
            if (unit.State.druidForm != HexDruidFormType.None)
                builder.Append($"  Form {unit.State.druidForm}");
        }

        private static void AppendEnemyIntent(StringBuilder builder, HexBattleUnit enemy)
        {
            if (enemy == null || enemy.State == null || enemy.State.faction != HexBattleFaction.Enemy)
                return;
            if (enemy.Deck.Hand.Count == 0)
                return;

            builder.Append("  Intent ");
            for (int i = 0; i < enemy.Deck.Hand.Count; i++)
            {
                if (i > 0)
                    builder.Append(" > ");

                var definition = enemy.Deck.Hand[i]?.definition;
                builder.Append(definition != null ? definition.displayName : "?");
            }
        }

        private HexBattleUnit GetCurrentUnit()
        {
            if (_currentTurn == HexBattleFaction.Player)
                return _playerUnit;

            return _enemyUnits.FirstOrDefault(enemy => enemy != null && enemy.IsAlive);
        }

        private bool IsOccupied(HexAxialCoord coord, HexBattleUnit ignoreUnit = null)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit == ignoreUnit)
                    continue;

                if (unit.State.coord.Equals(coord))
                    return true;
            }

            return false;
        }

        private bool IsMovementDestinationBlocked(HexAxialCoord coord, HexBattleUnit movingUnit)
        {
            if (grid == null || movingUnit == null || !grid.IsCoordInside(coord))
                return true;

            if (IsToadJumpMovement(movingUnit))
                return HasSceneObstacleAtCoord(coord, movingUnit) || IsOccupied(coord, movingUnit);

            return IsMovementBlocked(coord, movingUnit);
        }

        private List<HexAxialCoord> BuildMovementPath(HexBattleUnit unit, HexAxialCoord destination)
        {
            if (grid == null || unit == null)
                return null;

            if (IsToadJumpMovement(unit))
            {
                int distance = HexAxialCoord.Distance(unit.State.coord, destination);
                if (distance <= 0 || distance > unit.State.currentMovePoints)
                    return null;

                if (IsMovementDestinationBlocked(destination, unit))
                    return null;

                return new List<HexAxialCoord> { unit.State.coord, destination };
            }

            return HexBattlePathing.FindPath(grid, unit.State.coord, destination, coord => IsMovementBlocked(coord, unit));
        }

        private int GetMovementCost(HexBattleUnit unit, HexAxialCoord destination, List<HexAxialCoord> path)
        {
            if (unit == null || path == null || path.Count < 2)
                return 0;

            return IsToadJumpMovement(unit)
                ? HexAxialCoord.Distance(unit.State.coord, destination)
                : path.Count - 1;
        }

        private bool IsMovementBlocked(HexAxialCoord coord, HexBattleUnit movingUnit)
        {
            if (CanIgnoreOccupiedTilesWhileMoving(movingUnit))
                return false;

            return IsOccupied(coord, movingUnit);
        }

        private bool IsForcedMovementBlocked(HexAxialCoord coord, HexBattleUnit movingUnit)
        {
            return IsOccupied(coord, movingUnit) || HasSceneObstacleAtCoord(coord, movingUnit);
        }

        private bool HasSceneObstacleAtCoord(HexAxialCoord coord, HexBattleUnit movingUnit)
        {
            if (grid == null || !grid.IsCoordInside(coord))
                return true;

            Vector3 center = grid.GetTileSurfaceWorld(coord) + Vector3.up * 0.9f;
            var colliders = Physics.OverlapSphere(center, 0.28f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;

                if (movingUnit != null && collider.transform.IsChildOf(movingUnit.transform))
                    continue;

                if (collider.GetComponentInParent<HexTile>() != null)
                    continue;

                if (collider.GetComponentInParent<HexBattleUnit>() != null)
                    continue;

                return true;
            }

            return false;
        }

        private ForcedMovementResult ResolveForcedMovement(HexBattleUnit source, HexBattleUnit target, int distance, bool moveTowardSource)
        {
            if (grid == null || source == null || target == null || distance <= 0)
                return null;

            HexAxialCoord start = target.State.coord;
            int directionIndex = moveTowardSource
                ? HexBattlePathing.GetPrimaryDirectionIndex(grid, start, source.State.coord)
                : HexBattlePathing.GetPrimaryDirectionIndex(grid, source.State.coord, start);

            HexAxialCoord intendedDestination = GetForcedMovementIntendedDestination(start, directionIndex, distance);
            var reachableCosts = GetForcedMovementReachableCosts(start, distance, target);
            HexAxialCoord actualDestination = SelectBestForcedMovementDestination(start, intendedDestination, directionIndex, reachableCosts);
            var path = BuildForcedMovementPath(start, actualDestination, target);

            return new ForcedMovementResult
            {
                path = path ?? new List<HexAxialCoord> { start },
                intendedDestination = intendedDestination,
                actualDestination = actualDestination,
                collided = !actualDestination.Equals(intendedDestination),
            };
        }

        private HexAxialCoord GetForcedMovementIntendedDestination(HexAxialCoord start, int directionIndex, int distance)
        {
            HexAxialCoord current = start;
            for (int step = 0; step < distance; step++)
                current = HexAxialCoord.Neighbor(current, directionIndex);

            return current;
        }

        private Dictionary<HexAxialCoord, int> GetForcedMovementReachableCosts(HexAxialCoord start, int maxSteps, HexBattleUnit movingUnit)
        {
            var result = new Dictionary<HexAxialCoord, int> { [start] = 0 };
            var frontier = new Queue<HexAxialCoord>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                int currentCost = result[current];
                if (currentCost >= maxSteps)
                    continue;

                foreach (var neighbor in grid.GetNeighbors(current))
                {
                    if (IsForcedMovementBlocked(neighbor, movingUnit))
                        continue;

                    int nextCost = currentCost + 1;
                    if (result.TryGetValue(neighbor, out int existingCost) && existingCost <= nextCost)
                        continue;

                    result[neighbor] = nextCost;
                    frontier.Enqueue(neighbor);
                }
            }

            return result;
        }

        private HexAxialCoord SelectBestForcedMovementDestination(
            HexAxialCoord start,
            HexAxialCoord intendedDestination,
            int directionIndex,
            IReadOnlyDictionary<HexAxialCoord, int> reachableCosts)
        {
            if (reachableCosts == null || reachableCosts.Count == 0)
                return start;

            Vector3 intendedDirection = grid.AxialToWorld(HexAxialCoord.Neighbor(start, directionIndex)) - grid.AxialToWorld(start);
            intendedDirection.y = 0f;
            if (intendedDirection.sqrMagnitude > 0.0001f)
                intendedDirection.Normalize();

            HexAxialCoord bestCoord = start;
            float bestDistanceToTarget = float.PositiveInfinity;
            float bestAlignment = float.NegativeInfinity;
            int bestCost = -1;

            foreach (var kvp in reachableCosts)
            {
                var candidate = kvp.Key;
                int cost = kvp.Value;
                if (cost <= 0)
                    continue;

                float distanceToTarget = GetStraightLineDistance(candidate, intendedDestination);
                Vector3 candidateDirection = grid.AxialToWorld(candidate) - grid.AxialToWorld(start);
                candidateDirection.y = 0f;
                float alignment = candidateDirection.sqrMagnitude > 0.0001f && intendedDirection.sqrMagnitude > 0.0001f
                    ? Vector3.Dot(candidateDirection.normalized, intendedDirection)
                    : 0f;

                bool isBetter = distanceToTarget < bestDistanceToTarget - 0.001f ||
                    (Mathf.Abs(distanceToTarget - bestDistanceToTarget) <= 0.001f && alignment > bestAlignment + 0.001f) ||
                    (Mathf.Abs(distanceToTarget - bestDistanceToTarget) <= 0.001f && Mathf.Abs(alignment - bestAlignment) <= 0.001f && cost > bestCost);

                if (!isBetter)
                    continue;

                bestCoord = candidate;
                bestDistanceToTarget = distanceToTarget;
                bestAlignment = alignment;
                bestCost = cost;
            }

            return bestCoord;
        }

        private List<HexAxialCoord> BuildForcedMovementPath(HexAxialCoord start, HexAxialCoord destination, HexBattleUnit movingUnit)
        {
            if (destination.Equals(start))
                return new List<HexAxialCoord> { start };

            return HexBattlePathing.FindPath(grid, start, destination, coord => IsForcedMovementBlocked(coord, movingUnit));
        }

        private bool IsPathMovingTowardTarget(IReadOnlyList<HexAxialCoord> path, HexAxialCoord targetCoord)
        {
            if (grid == null || path == null || path.Count < 2)
                return false;

            float previousDistance = GetStraightLineDistance(path[0], targetCoord);
            for (int i = 1; i < path.Count; i++)
            {
                float currentDistance = GetStraightLineDistance(path[i], targetCoord);
                if (currentDistance >= previousDistance)
                    return false;

                previousDistance = currentDistance;
            }

            return true;
        }

        private float GetStraightLineDistance(HexAxialCoord from, HexAxialCoord to)
        {
            if (grid == null)
                return float.PositiveInfinity;

            Vector3 fromWorld = grid.AxialToWorld(from);
            Vector3 toWorld = grid.AxialToWorld(to);
            fromWorld.y = 0f;
            toWorld.y = 0f;
            return Vector3.Distance(fromWorld, toWorld);
        }

        private HexBattleUnit FindUnitAtCoord(HexAxialCoord coord, HexBattleUnit ignoreUnit = null)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit == ignoreUnit)
                    continue;

                if (unit.State.coord.Equals(coord))
                    return unit;
            }

            return null;
        }

        private IEnumerable<HexAxialCoord> GetCastOriginCoords(HexBattleUnit source, HexCardDefinition definition)
        {
            if (source == null || definition == null)
                yield break;

            if (definition.targetType == HexCardTargetType.Self ||
                definition.targetType == HexCardTargetType.Direction ||
                definition.targetType == HexCardTargetType.Tile ||
                definition.castRange <= 0)
            {
                yield return source.State.coord;
                yield break;
            }

            foreach (var coord in HexBattlePathing.GetCoordsInRange(source.State.coord, definition.castRange))
                yield return coord;
        }

        private bool HasEnemyInArea(HexAxialCoord center, int radius)
        {
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                if (HexAxialCoord.Distance(center, enemy.State.coord) <= radius)
                    return true;
            }

            return false;
        }

        private List<HexBattleUnit> GetEnemiesInArea(HexAxialCoord center, int radius, HexBattleUnit source)
        {
            var result = new List<HexBattleUnit>();
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit == source)
                    continue;

                if (unit.State.faction == source.State.faction)
                    continue;

                if (HexAxialCoord.Distance(center, unit.State.coord) <= radius)
                    result.Add(unit);
            }

            return result;
        }

        private List<HexBattleUnit> GetDirectionalTargets(HexBattleUnit source, HexAxialCoord aimedCoord, HexCardDefinition definition)
        {
            var result = new List<HexBattleUnit>();
            if (source == null || definition == null)
                return result;

            var coveredCoords = new HashSet<HexAxialCoord>(GetDirectionalAreaCoords(source.State.coord, aimedCoord, definition.castRange, definition.effectRadius));
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit == source)
                    continue;

                if (unit.State.faction == source.State.faction)
                    continue;

                if (coveredCoords.Contains(unit.State.coord))
                    result.Add(unit);
            }

            return result;
        }

        private List<HexAxialCoord> GetDirectionalAreaCoords(HexAxialCoord origin, HexAxialCoord aimedCoord, int length, int width)
        {
            if (grid == null || length <= 0 || aimedCoord.Equals(origin))
                return new List<HexAxialCoord>();

            int directionIndex = HexBattlePathing.GetPrimaryDirectionIndex(grid, origin, aimedCoord);
            return GetDirectionalAreaCoordsByDirection(origin, directionIndex, length, width);
        }

        private List<HexAxialCoord> GetDirectionalAreaCoordsByDirection(HexAxialCoord origin, int directionIndex, int length, int width)
        {
            var result = new List<HexAxialCoord>();
            var seen = new HashSet<HexAxialCoord>();
            if (grid == null || length <= 0)
                return result;

            HexAxialCoord current = origin;
            for (int step = 0; step < length; step++)
            {
                current = HexAxialCoord.Neighbor(current, directionIndex);
                if (!grid.IsCoordInside(current))
                    break;

                if (seen.Add(current))
                    result.Add(current);

                if (width <= 0)
                    continue;

                foreach (var coord in HexBattlePathing.GetCoordsInRange(current, width))
                {
                    if (!grid.IsCoordInside(coord) || !seen.Add(coord))
                        continue;

                    result.Add(coord);
                }
            }

            return result;
        }

        private void UpdateHoverFeedback()
        {
            if (_draggedCard != null && _draggedCard.definition.targetType == HexCardTargetType.EnemyUnit &&
                TryGetHoveredUnit(out var hoveredUnit) && hoveredUnit != null)
            {
                if (grid.TryGetTile(hoveredUnit.State.coord, out var hoveredUnitTile))
                    SetHoveredTile(hoveredUnitTile, true);
                return;
            }

            if (!TryGetHoveredTile(out var hoveredTile, out bool hasColliderHit))
            {
                SetHoveredTile(null, false);
                return;
            }

            SetHoveredTile(hoveredTile, hasColliderHit);
        }

        private void SetHoveredTile(HexTile tile, bool hasColliderHit)
        {
            if (_hoveredTile == tile && _hoverHasColliderHit == hasColliderHit)
                return;

            if (_hoveredTile != null && _hoveredTile != tile)
                _hoveredTile.SetHoverState(false, false);

            _hoveredTile = tile;
            _hoverHasColliderHit = hasColliderHit;

            if (_hoveredTile != null)
                _hoveredTile.SetHoverState(true, hasColliderHit);
        }

        private bool TryGetHoveredTile(out HexTile tile, out bool hasColliderHit)
        {
            tile = null;
            hasColliderHit = false;
            if (grid == null || rayCamera == null)
                return false;

            Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < hits.Length; i++)
                {
                    var candidate = hits[i].collider.GetComponentInParent<HexTile>();
                    if (candidate == null || hits[i].distance >= bestDistance)
                        continue;

                    bestDistance = hits[i].distance;
                    tile = candidate;
                    hasColliderHit = true;
                }
            }

            if (tile != null)
                return true;

            if (!TryGetCoordFromGroundPlane(ray, out var coord))
                return false;

            return grid.TryGetTile(coord, out tile);
        }

        private bool TryGetHoveredUnit(out HexBattleUnit unit)
        {
            unit = null;
            if (grid == null || rayCamera == null)
                return false;

            Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < hits.Length; i++)
                {
                    var candidate = hits[i].collider.GetComponentInParent<HexBattleUnit>();
                    if (candidate == null || !candidate.IsAlive || hits[i].distance >= bestDistance)
                        continue;

                    bestDistance = hits[i].distance;
                    unit = candidate;
                }
            }

            if (unit != null)
                return true;

            if (!TryGetHoveredTile(out var tile, out _))
                return false;

            unit = _units.FirstOrDefault(candidate => candidate.IsAlive && candidate.State.coord.Equals(tile.coord));
            return unit != null;
        }

        private bool TryGetCoordFromGroundPlane(Ray ray, out HexAxialCoord coord)
        {
            coord = default;
            if (grid == null)
                return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, grid.tileY, 0f));
            if (!plane.Raycast(ray, out float enter))
                return false;

            var worldPoint = ray.GetPoint(enter);
            coord = HexBattlePathing.WorldToAxial(grid, worldPoint);
            return grid.IsCoordInside(coord);
        }

        private void UpdateRangeHighlights()
        {
            ClearRangeHighlights();
            ClearMovementHighlights();
            if (_draggedCard == null)
                return;

            if (_draggedCard.definition.targetType == HexCardTargetType.Self)
            {
                if (grid.TryGetTile(_playerUnit.State.coord, out var selfTile))
                    selfTile.SetRangeIndicator(true, true);
                return;
            }

            if (_draggedCard.definition.targetType == HexCardTargetType.Direction)
            {
                var previewCoords = new HashSet<HexAxialCoord>();
                var targetableCoords = new HashSet<HexAxialCoord>();
                for (int directionIndex = 0; directionIndex < HexAxialCoord.Directions.Length; directionIndex++)
                {
                    var areaCoords = GetDirectionalAreaCoordsByDirection(
                        _playerUnit.State.coord,
                        directionIndex,
                        _draggedCard.definition.castRange,
                        _draggedCard.definition.effectRadius);

                    for (int i = 0; i < areaCoords.Count; i++)
                    {
                        var coord = areaCoords[i];
                        previewCoords.Add(coord);

                        var unit = FindUnitAtCoord(coord, _playerUnit);
                        if (unit != null && unit.State.faction != _playerUnit.State.faction)
                            targetableCoords.Add(coord);
                    }
                }

                foreach (var coord in previewCoords)
                {
                    if (!grid.TryGetTile(coord, out var tile))
                        continue;

                    tile.SetRangeIndicator(true, targetableCoords.Contains(coord));
                }
                return;
            }

            if (_draggedCard.definition.targetType == HexCardTargetType.Tile)
            {
                foreach (var coord in HexBattlePathing.GetCoordsInRange(_playerUnit.State.coord, _draggedCard.definition.castRange))
                {
                    if (!grid.TryGetTile(coord, out var tile))
                        continue;

                    bool targetable = _draggedCard.definition.effectRadius > 0
                        ? HasEnemyInArea(coord, _draggedCard.definition.effectRadius)
                        : true;
                    tile.SetRangeIndicator(true, targetable);
                }
                return;
            }

            foreach (var coord in GetCastOriginCoords(_playerUnit, _draggedCard.definition))
            {
                if (!grid.TryGetTile(coord, out var tile))
                    continue;

                bool targetable = _draggedCard.definition.effectRadius > 0
                    ? HasEnemyInArea(coord, _draggedCard.definition.effectRadius)
                    : _enemyUnits.Any(enemy => enemy != null && enemy.IsAlive && enemy.State.coord.Equals(coord));
                tile.SetRangeIndicator(true, targetable);
            }
        }

        private void ClearRangeHighlights()
        {
            foreach (var tile in grid.Tiles.Values)
                tile.SetRangeIndicator(false, false);
        }

        private void UpdateMovementHighlights()
        {
            if (grid == null)
                return;

            if (_draggedCard != null || _currentTurn != HexBattleFaction.Player || _busy || !_playerUnit.IsAlive)
            {
                ClearMovementHighlights();
                return;
            }

            if (_playerUnit.State.rooted || _playerUnit.State.bind > 0)
            {
                foreach (var tile in grid.Tiles.Values)
                    tile.SetMoveIndicator(tile.coord.Equals(_playerUnit.State.coord), tile.coord.Equals(_playerUnit.State.coord));

                foreach (var tile in grid.Tiles.Values)
                    tile.SetPathPreview(false, false, false);
                return;
            }

            var reachable = GetReachableCosts(_playerUnit);
            bool showGlobalReachable = _hoveredTile == null || _hoveredTile.coord.Equals(_playerUnit.State.coord);
            foreach (var tile in grid.Tiles.Values)
            {
                if (tile.coord.Equals(_playerUnit.State.coord))
                {
                    tile.SetMoveIndicator(true, true);
                    continue;
                }

                if (!showGlobalReachable)
                {
                    tile.SetMoveIndicator(false, false);
                    continue;
                }

                bool canReach = reachable.TryGetValue(tile.coord, out int cost) &&
                    cost <= _playerUnit.State.currentMovePoints &&
                    !IsOccupied(tile.coord, _playerUnit);
                tile.SetMoveIndicator(canReach, canReach);
            }

            if (_hoveredTile != null && !_hoveredTile.coord.Equals(_playerUnit.State.coord))
                ApplyMovementPreview(reachable, _hoveredTile.coord);
            else
            {
                foreach (var tile in grid.Tiles.Values)
                    tile.SetPathPreview(false, false, false);
            }
        }

        private void ClearMovementHighlights()
        {
            if (grid == null)
                return;

            foreach (var tile in grid.Tiles.Values)
            {
                tile.SetMoveIndicator(false, false);
                tile.SetPathPreview(false, false, false);
            }
        }

        private void ApplyMovementPreview(Dictionary<HexAxialCoord, int> reachable, HexAxialCoord hoveredCoord)
        {
            foreach (var tile in grid.Tiles.Values)
                tile.SetPathPreview(false, false, false);

            if (IsMovementDestinationBlocked(hoveredCoord, _playerUnit))
            {
                if (grid.TryGetTile(hoveredCoord, out var occupiedTile))
                    occupiedTile.SetPathPreview(true, true, true);
                return;
            }

            var path = BuildMovementPath(_playerUnit, hoveredCoord);
            bool canReach = path != null &&
                            path.Count >= 2 &&
                            reachable.TryGetValue(hoveredCoord, out int hoveredCost) &&
                            hoveredCost <= _playerUnit.State.currentMovePoints;

            if (!canReach)
            {
                if (grid.TryGetTile(hoveredCoord, out var invalidTile))
                    invalidTile.SetPathPreview(true, true, true);
                return;
            }

            for (int i = 1; i < path.Count; i++)
            {
                if (!grid.TryGetTile(path[i], out var pathTile))
                    continue;

                bool isTarget = i == path.Count - 1;
                pathTile.SetPathPreview(true, isTarget, false);
            }
        }

        private Dictionary<HexAxialCoord, int> GetReachableCosts(HexBattleUnit unit)
        {
            if (unit == null)
                return new Dictionary<HexAxialCoord, int>();

            if (unit.State.rooted || unit.State.bind > 0)
                return new Dictionary<HexAxialCoord, int> { [unit.State.coord] = 0 };

            if (IsToadJumpMovement(unit))
            {
                var jumped = new Dictionary<HexAxialCoord, int> { [unit.State.coord] = 0 };
                foreach (var tile in grid.Tiles.Values)
                {
                    if (tile.coord.Equals(unit.State.coord))
                        continue;

                    if (IsMovementDestinationBlocked(tile.coord, unit))
                        continue;

                    int distance = HexAxialCoord.Distance(unit.State.coord, tile.coord);
                    if (distance <= unit.State.currentMovePoints)
                        jumped[tile.coord] = distance;
                }

                return jumped;
            }

            var result = new Dictionary<HexAxialCoord, int> { [unit.State.coord] = 0 };
            var frontier = new Queue<HexAxialCoord>();
            frontier.Enqueue(unit.State.coord);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                int currentCost = result[current];
                if (currentCost >= unit.State.currentMovePoints)
                    continue;

                foreach (var neighbor in grid.GetNeighbors(current))
                {
                    if (!grid.IsCoordInside(neighbor) || IsMovementDestinationBlocked(neighbor, unit))
                        continue;

                    int nextCost = currentCost + 1;
                    if (result.TryGetValue(neighbor, out int oldCost) && oldCost <= nextCost)
                        continue;

                    result[neighbor] = nextCost;
                    frontier.Enqueue(neighbor);
                }
            }

            return result;
        }

        private void EnsureTargetArrow()
        {
            var arrowGO = new GameObject("Battle_Target_Arrow");
            arrowGO.transform.SetParent(transform, false);
            _targetArrow = arrowGO.AddComponent<LineRenderer>();
            _targetArrow.useWorldSpace = true;
            _targetArrow.positionCount = 2;
            _targetArrow.widthMultiplier = 0.08f;
            _targetArrow.numCapVertices = 3;
            _targetArrow.numCornerVertices = 3;
            _targetArrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _targetArrow.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("UI/Default");
            _targetArrow.sharedMaterial = new Material(shader) { color = new Color(1f, 0.42f, 0.2f, 0.95f) };
            _targetArrow.enabled = false;
        }

        private void SetTargetArrowActive(bool active)
        {
            if (_targetArrow != null)
                _targetArrow.enabled = active;
        }

        private void UpdateTargetArrow()
        {
            if (_targetArrow == null || !_targetArrow.enabled || _playerUnit == null)
                return;

            Vector3 origin = _playerUnit.GetTargetPoint();
            Vector3 target = GetArrowTargetPoint();
            _targetArrow.SetPosition(0, origin);
            _targetArrow.SetPosition(1, target);
        }

        private Vector3 GetArrowTargetPoint()
        {
            if (TryGetHoveredUnit(out var unit) && unit != null && unit != _playerUnit)
                return unit.GetTargetPoint();

            Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, grid.tileY + 0.9f, 0f));
            if (plane.Raycast(ray, out float enter))
                return ray.GetPoint(enter);

            return _playerUnit.GetTargetPoint() + _playerUnit.transform.forward * 1.5f;
        }

        private static string GetWeaponLabel(HexWeaponType weaponType)
        {
            return weaponType switch
            {
                HexWeaponType.Sword => "Sword",
                HexWeaponType.Axe => "Axe",
                HexWeaponType.Hammer => "Hammer",
                _ => "None",
            };
        }
    }
}
