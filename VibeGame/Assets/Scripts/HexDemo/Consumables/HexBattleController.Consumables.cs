using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexDemo
{
    public sealed partial class HexBattleController
    {
        private sealed class TripwireState
        {
            public HexAxialCoord first;
            public HexAxialCoord second;
        }

        private HexRunState _consumableRunState;
        private HexConsumableInstance _pendingConsumable;
        private bool _pendingStealSkill;
        private HexAxialCoord? _pendingTripwireFirst;
        private readonly HashSet<HexAxialCoord> _bloodTrapCoords = new();
        private readonly List<TripwireState> _tripwires = new();
        private readonly Dictionary<HexAxialCoord, int> _strengthRitualTiles = new();

        private void InitializeConsumables(HexRunState runState)
        {
            _consumableRunState = runState ?? new HexRunState();
            _consumableRunState.consumables ??= new List<HexConsumableInstance>();
            _pendingConsumable = null;
            _pendingStealSkill = false;
            _pendingTripwireFirst = null;
            _bloodTrapCoords.Clear();
            _tripwires.Clear();
            _strengthRitualTiles.Clear();
        }

        public IReadOnlyList<HexConsumableInstance> GetConsumables()
        {
            return _consumableRunState?.consumables ?? (IReadOnlyList<HexConsumableInstance>)System.Array.Empty<HexConsumableInstance>();
        }

        public string GetConsumableTargetPrompt()
        {
            if (_pendingStealSkill)
                return "窃取：请选择一名有手牌的敌人";
            if (_pendingConsumable?.Definition == null)
                return string.Empty;
            if (_pendingConsumable.Definition.effectType == HexConsumableEffectType.Tripwire && _pendingTripwireFirst.HasValue)
                return "绊锁：请选择第二个端点";
            return $"{_pendingConsumable.Definition.displayName}：请选择目标";
        }

        public bool CanUseConsumables()
        {
            return !_battleFinished && !_busy && _currentTurn == HexBattleFaction.Player && _playerUnit != null && _playerUnit.IsAlive;
        }

        public bool CanSelectConsumables()
        {
            return !_battleFinished && _currentTurn == HexBattleFaction.Player && _playerUnit != null && _playerUnit.IsAlive;
        }

        private bool IsConsumableTargeting() => _pendingConsumable?.Definition != null || _pendingStealSkill;

        public void RequestUseConsumable(string runtimeId)
        {
            if (!CanUseConsumables() || _consumableRunState?.consumables == null)
                return;

            var item = _consumableRunState.consumables.FirstOrDefault(candidate => candidate != null && candidate.runtimeId == runtimeId);
            var definition = item?.Definition;
            if (definition == null || item.remainingUses <= 0)
                return;

            if (_pendingConsumable == item)
            {
                CancelConsumableTargeting();
                _ui?.Refresh();
                return;
            }

            CancelConsumableTargeting();
            if (definition.targetType == HexConsumableTargetType.Self)
            {
                if (ResolveSelfConsumable(item, definition))
                    ConsumeUse(item);
                _ui?.Refresh();
                return;
            }

            _pendingConsumable = item;
            UpdateConsumableTargetHighlights(definition);
            _ui?.Refresh();
        }

        public bool IsConsumableSelected(string runtimeId)
        {
            return !string.IsNullOrWhiteSpace(runtimeId) && _pendingConsumable?.runtimeId == runtimeId;
        }

        public void CancelConsumableTargeting()
        {
            _pendingConsumable = null;
            _pendingStealSkill = false;
            _pendingTripwireFirst = null;
            ClearRangeHighlights();
        }

        public void RequestUseFlyingSecretSkill()
        {
            if (!CanUseConsumables() || _playerUnit.State.flyingSecretTurns <= 0 || _playerUnit.State.energy < 1)
                return;

            _playerUnit.SpendEnergy(1);
            _playerUnit.ApplyPhase(1);
            AddTemporaryCardToHand(_playerUnit, HexCardLibrary.GetCommonDash());
            _ui?.Refresh();
        }

        public void RequestUseStealSecretSkill()
        {
            if (!CanUseConsumables() || _playerUnit.State.stealSecretTurns <= 0 || _playerUnit.State.energy < 1)
                return;

            CancelConsumableTargeting();
            _pendingStealSkill = true;
            _ui?.Refresh();
        }

        private bool TryHandlePendingConsumableTargetClick()
        {
            if (!Input.GetMouseButtonDown(0) || (!(_pendingConsumable?.Definition != null) && !_pendingStealSkill))
                return false;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return true;

            if (_pendingStealSkill)
            {
                if (!TryGetHoveredUnit(out var enemy) || enemy == null || enemy.State.faction != HexBattleFaction.Enemy || enemy.Deck.Hand.Count == 0)
                    return true;

                _playerUnit.SpendEnergy(1);
                var sourceCard = enemy.Deck.Hand[0]?.definition;
                if (sourceCard != null)
                {
                    var copiedDefinition = CloneTemporaryCard(sourceCard, "虚无 消耗");
                    AddTemporaryCardToHand(_playerUnit, copiedDefinition);
                }
                _pendingStealSkill = false;
                _ui?.Refresh();
                return true;
            }

            var item = _pendingConsumable;
            var definition = item.Definition;
            bool resolved = definition.targetType switch
            {
                HexConsumableTargetType.Enemy => TryResolveEnemyConsumable(item, definition),
                HexConsumableTargetType.EmptyTile => TryResolveTileConsumable(item, definition),
                HexConsumableTargetType.Structure => TryResolveStructureConsumable(item, definition),
                _ => false,
            };

            if (resolved && !(definition.effectType == HexConsumableEffectType.Tripwire && _pendingTripwireFirst.HasValue))
            {
                ConsumeUse(item);
                _pendingConsumable = null;
                ClearRangeHighlights();
            }

            _ui?.Refresh();
            return true;
        }

        private bool ResolveSelfConsumable(HexConsumableInstance item, HexConsumableDefinition definition)
        {
            switch (definition.effectType)
            {
                case HexConsumableEffectType.Strength:
                    _playerUnit.GainTemporaryStrength(
                        definition.amount,
                        HexTemporaryStrengthDuration.UntilEndOfTurn);
                    return true;
                case HexConsumableEffectType.Toughness:
                    _playerUnit.GainToughness(definition.amount);
                    _playerUnit.State.consumableTempToughness += definition.amount;
                    return true;
                case HexConsumableEffectType.Vampirism:
                    _playerUnit.State.vampirism += definition.amount;
                    return true;
                case HexConsumableEffectType.Energy:
                    _playerUnit.State.energy = Mathf.Min(_playerUnit.State.maxEnergy, _playerUnit.State.energy + definition.amount);
                    return true;
                case HexConsumableEffectType.Draw:
                    DrawCardsForUnit(_playerUnit, definition.amount, true);
                    return true;
                case HexConsumableEffectType.Coffee:
                    _playerUnit.State.consumableCoffeeAmount = Mathf.Max(_playerUnit.State.consumableCoffeeAmount, definition.amount);
                    _playerUnit.State.consumableCoffeeTurns += definition.duration;
                    return true;
                case HexConsumableEffectType.MaxHealth:
                    _playerUnit.State.maxHealth += definition.amount;
                    _playerUnit.State.currentHealth += definition.amount;
                    if (_consumableRunState != null)
                    {
                        _consumableRunState.maxHealth += definition.amount;
                        _consumableRunState.currentHealth += definition.amount;
                    }
                    _playerUnit.RefreshLabel();
                    return true;
                case HexConsumableEffectType.Armor:
                    GainArmorWithFeedback(_playerUnit, definition.amount);
                    return true;
                case HexConsumableEffectType.AttackBurn:
                    _playerUnit.State.consumableAttackBurnBonus += definition.amount;
                    return true;
                case HexConsumableEffectType.Wisdom:
                    _playerUnit.GainWisdom(definition.amount);
                    _playerUnit.State.consumableWisdomAmount += definition.amount;
                    _playerUnit.State.consumableWisdomTurns = Mathf.Max(_playerUnit.State.consumableWisdomTurns, definition.duration);
                    return true;
                case HexConsumableEffectType.EggTart:
                    _playerUnit.State.consumableEggTartTurns += definition.duration;
                    return true;
                case HexConsumableEffectType.Regeneration:
                    _playerUnit.State.regeneration += definition.amount;
                    return true;
                case HexConsumableEffectType.FlyingSecret:
                    _playerUnit.State.flyingSecretTurns = Mathf.Max(_playerUnit.State.flyingSecretTurns, definition.duration);
                    return true;
                case HexConsumableEffectType.StealSecret:
                    _playerUnit.State.stealSecretTurns = Mathf.Max(_playerUnit.State.stealSecretTurns, definition.duration);
                    return true;
                case HexConsumableEffectType.EvilPact:
                    ApplyEvilPact(_playerUnit);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryResolveEnemyConsumable(HexConsumableInstance item, HexConsumableDefinition definition)
        {
            if (!TryGetHoveredUnit(out var enemy) || enemy == null || enemy.State.faction != HexBattleFaction.Enemy)
                return false;
            if (!IsWithinConsumableRange(enemy.State.coord, definition.castRange))
                return false;

            switch (definition.effectType)
            {
                case HexConsumableEffectType.Poison:
                    enemy.State.poison += definition.amount;
                    enemy.RefreshLabel();
                    return true;
                case HexConsumableEffectType.Weak:
                    enemy.ApplyWeak(definition.amount);
                    return true;
                case HexConsumableEffectType.Transform:
                    enemy.ApplyStun(definition.amount);
                    return true;
                case HexConsumableEffectType.Alchemy:
                    var encounter = HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId)?.encounterType ?? HexEnemyEncounterType.Normal;
                    if (encounter != HexEnemyEncounterType.Normal)
                        return false;
                    int gold = Mathf.Max(0, enemy.State.currentHealth);
                    _consumableRunState.gold += gold;
                    ApplyDamageToUnit(enemy, enemy.State.currentHealth + enemy.State.armor, _playerUnit);
                    StartCoroutine(ResolveConsumableDeath(enemy));
                    return true;
                default:
                    return false;
            }
        }

        private bool TryResolveTileConsumable(HexConsumableInstance item, HexConsumableDefinition definition)
        {
            if (!TryGetHoveredTile(out var tile, out _) || tile == null || !TileCanEnter(tile) || IsOccupied(tile.coord, null))
                return false;
            if (!IsWithinConsumableRange(tile.coord, definition.castRange))
                return false;

            switch (definition.effectType)
            {
                case HexConsumableEffectType.StrengthRitual:
                    foreach (var coord in HexBattlePathing.GetCoordsInRange(tile.coord, definition.effectRadius))
                    {
                        if (!grid.TryGetTile(coord, out var ritualTile) || ritualTile == null)
                            continue;
                        _strengthRitualTiles[coord] = Mathf.Max(_strengthRitualTiles.TryGetValue(coord, out int old) ? old : 0, definition.duration);
                        ritualTile.AddOrRefreshEffect(HexTileEffectType.Custom, 1, definition.duration);
                        ritualTile.FlashClick();
                    }
                    return true;
                case HexConsumableEffectType.BloodTrap:
                    _bloodTrapCoords.Add(tile.coord);
                    tile.AddOrRefreshEffect(HexTileEffectType.Custom, 1, 999);
                    tile.FlashClick();
                    return true;
                case HexConsumableEffectType.Scarecrow:
                    return SpawnScarecrow(tile.coord, definition.amount);
                case HexConsumableEffectType.RocketBoots:
                    TeleportUnitTo(_playerUnit, tile.coord);
                    return true;
                case HexConsumableEffectType.Tripwire:
                    if (!_pendingTripwireFirst.HasValue)
                    {
                        _pendingTripwireFirst = tile.coord;
                        tile.AddOrRefreshEffect(HexTileEffectType.Custom, 1, 999);
                        tile.FlashClick();
                        return true;
                    }
                    if (_pendingTripwireFirst.Value.Equals(tile.coord))
                        return false;
                    _tripwires.Add(new TripwireState { first = _pendingTripwireFirst.Value, second = tile.coord });
                    tile.AddOrRefreshEffect(HexTileEffectType.Custom, 1, 999);
                    _pendingTripwireFirst = null;
                    tile.FlashClick();
                    return true;
                case HexConsumableEffectType.IronBall:
                    tile.SetProp("consumable_iron_ball", 999);
                    tile.FlashClick();
                    return true;
                default:
                    return false;
            }
        }

        private bool TryResolveStructureConsumable(HexConsumableInstance item, HexConsumableDefinition definition)
        {
            if (!TryGetHoveredTile(out var tile, out _) || tile == null || (!tile.HasBarrier && !tile.HasRuin))
                return false;
            if (!IsWithinConsumableRange(tile.coord, definition.castRange))
                return false;
            if (definition.effectType != HexConsumableEffectType.GrapplingHook)
                return false;

            var destinations = grid.GetNeighbors(tile.coord)
                .Where(coord => grid.TryGetTile(coord, out var candidate) && candidate != null && TileCanEnter(candidate) && !IsOccupied(coord, _playerUnit))
                .OrderBy(coord => HexAxialCoord.Distance(_playerUnit.State.coord, coord))
                .ToList();
            if (destinations.Count == 0)
                return false;
            var destination = destinations[0];
            if (!grid.TryGetTile(destination, out var destinationTile) || destinationTile == null || !TileCanEnter(destinationTile))
                return false;
            TeleportUnitTo(_playerUnit, destination);
            return true;
        }

        private void ResolveConsumableTurnStart(HexBattleUnit unit)
        {
            if (unit?.State == null || !unit.IsAlive)
                return;

            if (unit.State.poison > 0)
            {
                ApplyDamageToUnit(unit, unit.State.poison, null, HexDamageTags.Status);
                unit.State.poison = Mathf.Max(0, unit.State.poison - 1);
                if (!unit.IsAlive)
                    return;
            }

            if (unit.State.regeneration > 0)
            {
                unit.Heal(unit.State.regeneration);
                unit.State.regeneration = Mathf.Max(0, unit.State.regeneration - 1);
            }

            if (unit.State.consumableCoffeeTurns > 0)
            {
                unit.GainVigor(unit.State.consumableCoffeeAmount);
                unit.State.consumableCoffeeTurns--;
            }

            if (unit.State.consumableEggTartTurns > 0)
            {
                AddTemporaryCardToHand(unit, CloneTemporaryCard(HexCardLibrary.GetCardById("warrior_move_forward") ?? HexCardLibrary.GetCommonDash(), "虚无 消耗"));
                unit.State.consumableEggTartTurns--;
            }

            if (unit.State.flyingSecretTurns > 0)
                unit.State.flyingSecretTurns--;
            if (unit.State.stealSecretTurns > 0)
                unit.State.stealSecretTurns--;
            if (unit.State.consumableWisdomTurns > 0)
            {
                unit.State.consumableWisdomTurns--;
                if (unit.State.consumableWisdomTurns == 0 && unit.State.consumableWisdomAmount > 0)
                {
                    unit.State.wisdom = Mathf.Max(0, unit.State.wisdom - unit.State.consumableWisdomAmount);
                    unit.State.consumableWisdomAmount = 0;
                }
            }

            if (_strengthRitualTiles.ContainsKey(unit.State.coord))
            {
                unit.GainTemporaryStrength(3, HexTemporaryStrengthDuration.UntilEndOfTurn);
            }

            if (unit == _playerUnit)
                TickRitualDurations();
        }

        private void ResolveConsumableMovementTriggers(HexBattleUnit unit, IReadOnlyList<HexAxialCoord> path)
        {
            if (unit == null || path == null || path.Count < 2)
                return;

            for (int i = 1; i < path.Count; i++)
            {
                if (_bloodTrapCoords.Remove(path[i]))
                {
                    unit.ApplyBind(1);
                    unit.ApplyBleed(5);
                    if (grid.TryGetTile(path[i], out var trapTile))
                    {
                        trapTile.RemoveEffect(HexTileEffectType.Custom);
                        trapTile.FlashClick();
                    }
                }
            }

            for (int i = 0; i < _tripwires.Count; i++)
            {
                var line = GetTripwireLine(_tripwires[i]);
                if (path.Skip(1).Any(line.Contains))
                    ApplyDamageToUnit(unit, 2, null);
            }
        }

        private HexBattleUnit GetConsumableTauntTarget(HexBattleUnit enemy)
        {
            if (enemy == null)
                return null;
            var scarecrows = _units.Where(unit => unit != null && unit.IsAlive && unit.State.id.StartsWith("scarecrow_", System.StringComparison.Ordinal));
            var nearest = scarecrows.OrderBy(unit => HexAxialCoord.Distance(enemy.State.coord, unit.State.coord)).FirstOrDefault();
            if (nearest == null || _playerUnit == null || !_playerUnit.IsAlive)
                return nearest;
            return HexAxialCoord.Distance(enemy.State.coord, nearest.State.coord) <= HexAxialCoord.Distance(enemy.State.coord, _playerUnit.State.coord)
                ? nearest
                : null;
        }

        private IEnumerator ResolveIronBallHit(HexBattleUnit source, HexAxialCoord origin)
        {
            if (!grid.TryGetTile(origin, out var originTile) || originTile == null)
                yield break;

            originTile.ClearStructure();
            int direction = HexBattlePathing.GetPrimaryDirectionIndex(grid, source.State.coord, origin);
            HexAxialCoord current = origin;
            for (int step = 0; step < 5; step++)
            {
                var next = HexAxialCoord.Neighbor(current, direction);
                if (!grid.TryGetTile(next, out var nextTile) || nextTile == null)
                    break;
                var hitUnit = FindUnitAtCoord(next, null);
                if (hitUnit != null)
                {
                    ApplyDamageToUnit(hitUnit, 10, source);
                    break;
                }
                if (!TileCanEnter(nextTile))
                    break;
                current = next;
                nextTile.FlashClick();
                yield return new WaitForSeconds(0.04f);
            }

            if (grid.TryGetTile(current, out var finalTile) && finalTile != null && TileCanEnter(finalTile) && !IsOccupied(current, null))
                finalTile.SetProp("consumable_iron_ball", 999);
        }

        private void ConsumeUse(HexConsumableInstance item)
        {
            if (item == null || _consumableRunState?.consumables == null)
                return;
            item.remainingUses = Mathf.Max(0, item.remainingUses - 1);
            if (item.remainingUses == 0)
                _consumableRunState.consumables.Remove(item);
        }

        private void AddTemporaryCardToHand(HexBattleUnit unit, HexCardDefinition definition)
        {
            if (unit == null || definition == null)
                return;
            unit.Deck.AddToHand(definition);
            if (unit.Deck.Hand.Count > 0)
                unit.Deck.Hand[^1].exhaustWhenPlayed = true;
        }

        private static HexCardDefinition CloneTemporaryCard(HexCardDefinition source, string extraDescription)
        {
            if (source == null)
                return null;
            return new HexCardDefinition
            {
                id = $"temp_{source.id}_{System.Guid.NewGuid():N}",
                displayName = source.displayName,
                cardType = source.cardType,
                profession = HexCardProfession.Common,
                effectType = source.effectType,
                targetType = source.targetType,
                energyCost = source.energyCost,
                amount = source.amount,
                range = source.range,
                castRange = source.castRange,
                effectRadius = source.effectRadius,
                priority = source.priority,
                rarity = "Temporary",
                description = $"{source.description} {extraDescription}".Trim(),
                color = source.color,
                tags = new[] { "临时" },
            };
        }

        private static void ApplyEvilPact(HexBattleUnit unit)
        {
            if (unit == null)
                return;
            foreach (var card in unit.Deck.DrawPile.Concat(unit.Deck.DiscardPile).Concat(unit.Deck.Hand))
            {
                if (card == null)
                    continue;
                card.costsNoEnergyThisBattle = true;
                card.exhaustWhenPlayed = true;
            }
        }

        private bool SpawnScarecrow(HexAxialCoord coord, int health)
        {
            var root = new GameObject($"Scarecrow_{System.Guid.NewGuid():N}");
            root.transform.SetParent(_playerUnit.transform.parent != null ? _playerUnit.transform.parent : transform, false);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "ScarecrowVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            visual.transform.localScale = new Vector3(0.35f, 0.65f, 0.35f);
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Destroy(visualCollider);
            var visualRenderer = visual.GetComponent<Renderer>();
            if (visualRenderer != null)
                visualRenderer.material.color = new Color(0.72f, 0.56f, 0.24f, 1f);
            var unit = root.AddComponent<HexBattleUnit>();
            unit.Initialize(new HexBattleUnitState
            {
                id = $"scarecrow_{System.Guid.NewGuid():N}",
                displayName = "稻草人",
                faction = HexBattleFaction.Player,
                maxHealth = Mathf.Max(1, health),
                currentHealth = Mathf.Max(1, health),
                maxEnergy = 0,
                drawPerTurn = 0,
                maxMovePoints = 0,
                coord = coord,
            }, null, System.Array.Empty<HexCardDefinition>());
            unit.SnapTo(grid, unitYOffset);
            _units.Add(unit);
            return true;
        }

        private void TeleportUnitTo(HexBattleUnit unit, HexAxialCoord coord)
        {
            if (unit == null)
                return;
            unit.State.coord = coord;
            unit.SnapTo(grid, unitYOffset);
            if (grid.TryGetTile(coord, out var tile))
                tile.FlashClick();
        }

        private bool IsWithinConsumableRange(HexAxialCoord coord, int range)
        {
            return range < 0 || HexAxialCoord.Distance(_playerUnit.State.coord, coord) <= range;
        }

        private void UpdateConsumableTargetHighlights(HexConsumableDefinition definition)
        {
            ClearRangeHighlights();
            if (grid == null || definition == null)
                return;
            int range = definition.castRange < 0 ? 99 : definition.castRange;
            foreach (var coord in HexBattlePathing.GetCoordsInRange(_playerUnit.State.coord, range))
            {
                if (!grid.TryGetTile(coord, out var tile) || tile == null)
                    continue;
                bool targetable = definition.targetType switch
                {
                    HexConsumableTargetType.EmptyTile => TileCanEnter(tile) && !IsOccupied(coord, null),
                    HexConsumableTargetType.Structure => tile.HasBarrier || tile.HasRuin,
                    HexConsumableTargetType.Enemy => FindUnitAtCoord(coord, _playerUnit)?.State.faction == HexBattleFaction.Enemy,
                    _ => false,
                };
                tile.SetRangeIndicator(true, targetable);
            }
        }

        private HashSet<HexAxialCoord> GetTripwireLine(TripwireState tripwire)
        {
            var result = new HashSet<HexAxialCoord> { tripwire.first, tripwire.second };
            int distance = HexAxialCoord.Distance(tripwire.first, tripwire.second);
            foreach (var coord in HexBattlePathing.GetLineCoords(grid, tripwire.first, tripwire.second, distance))
                result.Add(coord);
            return result;
        }

        private void TickRitualDurations()
        {
            var coords = _strengthRitualTiles.Keys.ToList();
            for (int i = 0; i < coords.Count; i++)
            {
                int remaining = _strengthRitualTiles[coords[i]] - 1;
                if (remaining <= 0)
                    _strengthRitualTiles.Remove(coords[i]);
                else
                    _strengthRitualTiles[coords[i]] = remaining;
            }
        }

        private IEnumerator ResolveConsumableDeath(HexBattleUnit target)
        {
            yield return ResolveDeathsAndBattleEndRoutine();
        }
    }
}
