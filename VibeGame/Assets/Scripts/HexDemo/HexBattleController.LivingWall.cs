using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexDemo
{
    public sealed partial class HexBattleController
    {
        private void RegisterLivingWallSpecialHandlers()
        {
            _enemySpecialHandlers["enemy_wall_advance"] = ResolveLivingWallCard;
            _enemySpecialHandlers["enemy_wall_spike"] = ResolveLivingWallCard;
            _enemySpecialHandlers["enemy_wall_reform"] = ResolveLivingWallCard;
            _enemySpecialHandlers["enemy_wall_fortify"] = ResolveLivingWallCard;
        }

        private IEnumerator ResolveLivingWallCard(HexBattleUnit wall, HexBattleUnit _, HexCardInstance card)
        {
            if (wall == null || !wall.IsAlive || !wall.IsLivingWall || card?.definition == null)
                yield break;

            wall.SetLivingWallIntentPreview(null, false);

            switch (card.definition.id)
            {
                case "enemy_wall_advance":
                    yield return ResolveLivingWallAdvance(wall);
                    break;
                case "enemy_wall_spike":
                    yield return ResolveLivingWallSpike(wall);
                    break;
                case "enemy_wall_reform":
                    wall.State.livingWall.reformPending = true;
                    break;
                case "enemy_wall_fortify":
                    GainArmorWithFeedback(wall, wall.FootprintSize * HexLivingWallRules.ArmorPerCell);
                    wall.State.livingWall.movementLocked = true;
                    break;
            }
        }

        private void UpdateLivingWallIntentPreview(HexBattleUnit wall, HexCardInstance card)
        {
            if (wall == null || !wall.IsLivingWall || card?.definition == null || grid == null)
            {
                wall?.SetLivingWallIntentPreview(null, false);
                return;
            }
            if (card.definition.id != "enemy_wall_advance" && card.definition.id != "enemy_wall_spike")
            {
                wall.SetLivingWallIntentPreview(null, false);
                return;
            }

            HexBattleUnit directionTarget = GetLivingWallDirectionTarget(wall);
            if (directionTarget == null)
            {
                wall.SetLivingWallIntentPreview(null, false);
                return;
            }
            int direction = HexBattlePathing.GetPrimaryDirectionIndex(grid, wall.State.coord, directionTarget.State.coord);
            var occupied = new HashSet<HexAxialCoord>(wall.OccupiedCoords);
            var front = new HashSet<HexAxialCoord>(occupied.Where(coord => !occupied.Contains(HexAxialCoord.Neighbor(coord, direction))));
            bool danger = false;
            if (card.definition.id == "enemy_wall_advance")
            {
                HexBattleUnit pair = GetLivingWallPair(wall);
                if (pair != null)
                {
                    HexAxialCoord destinationCore = HexAxialCoord.Neighbor(wall.State.coord, direction);
                    danger = FootprintsAreAdjacent(
                        BuildFootprintCoords(destinationCore, wall.State.livingWall.footprintOffsets),
                        pair.OccupiedCoords);
                }
            }
            wall.SetLivingWallIntentPreview(front, danger);
        }

        private IEnumerator ResolveLivingWallTurnStarts()
        {
            var walls = GetLivingWalls()
                .OrderBy(GetLivingWallExecutionGroup)
                .ThenBy(unit => unit.State.livingWall.spawnOrder)
                .ThenBy(unit => unit.State.id, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < walls.Count; i++)
                walls[i].State.livingWall.movementLocked = false;

            RepairLivingWallPairs();
            var handled = new HashSet<HexBattleUnit>();
            for (int i = 0; i < walls.Count; i++)
            {
                HexBattleUnit wall = walls[i];
                if (!wall.IsAlive || !wall.State.livingWall.reformPending || !handled.Add(wall))
                    continue;

                HexBattleUnit pair = GetLivingWallPair(wall);
                bool pairIsReforming = pair != null &&
                                        pair.IsAlive &&
                                        pair.State.livingWall.reformPending &&
                                        !handled.Contains(pair);
                if (pairIsReforming)
                {
                    handled.Add(pair);
                    TryResolvePairedLivingWallReform(wall, pair);
                    FinishLivingWallReform(pair);
                }
                else
                {
                    TryResolveLivingWallReform(wall);
                }

                FinishLivingWallReform(wall);
                _ui?.Refresh();
                yield return new WaitForSeconds(0.08f);
            }
        }

        private void FinishLivingWallReform(HexBattleUnit wall)
        {
            if (wall?.State?.livingWall == null)
                return;
            wall.State.livingWall.reformPending = false;
            _enemyIntentSlots.Remove(wall);
            wall.RefreshLabel();
        }

        private static int GetLivingWallExecutionGroup(HexBattleUnit wall) =>
            wall?.State?.livingWall?.isOffspring == true ? 1 : 0;

        private List<HexBattleUnit> GetLivingWalls()
        {
            return _enemyUnits
                .Where(unit => unit != null && unit.IsAlive && unit.IsLivingWall)
                .ToList();
        }

        private void RepairLivingWallPairs()
        {
            var walls = GetLivingWalls();
            var byId = walls.ToDictionary(unit => unit.State.id, StringComparer.Ordinal);
            for (int i = 0; i < walls.Count; i++)
            {
                var state = walls[i].State.livingWall;
                if (string.IsNullOrWhiteSpace(state.pairedWallId) ||
                    !byId.TryGetValue(state.pairedWallId, out var pair) ||
                    pair == walls[i] ||
                    pair.State.livingWall.pairedWallId != walls[i].State.id)
                    state.pairedWallId = string.Empty;
            }

            var unpaired = walls
                .Where(unit => string.IsNullOrWhiteSpace(unit.State.livingWall.pairedWallId))
                .OrderBy(GetLivingWallExecutionGroup)
                .ThenBy(unit => unit.State.livingWall.spawnOrder)
                .ThenBy(unit => unit.State.id, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < unpaired.Count; i++)
            {
                HexBattleUnit wall = unpaired[i];
                if (!string.IsNullOrWhiteSpace(wall.State.livingWall.pairedWallId))
                    continue;

                HexBattleUnit pair = unpaired
                    .Where(candidate => candidate != wall && string.IsNullOrWhiteSpace(candidate.State.livingWall.pairedWallId))
                    .OrderBy(candidate => GetUnitDistance(wall, candidate))
                    .ThenBy(candidate => candidate.State.livingWall.spawnOrder)
                    .ThenBy(candidate => candidate.State.id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (pair == null)
                    continue;

                wall.State.livingWall.pairedWallId = pair.State.id;
                pair.State.livingWall.pairedWallId = wall.State.id;
            }
        }

        private HexBattleUnit GetLivingWallPair(HexBattleUnit wall)
        {
            if (wall?.State?.livingWall == null || string.IsNullOrWhiteSpace(wall.State.livingWall.pairedWallId))
                return null;

            HexBattleUnit pair = _enemyUnits.FirstOrDefault(unit =>
                unit != null && unit.IsAlive && unit.IsLivingWall && unit.State.id == wall.State.livingWall.pairedWallId);
            if (pair != null)
                return pair;

            wall.State.livingWall.pairedWallId = string.Empty;
            return null;
        }

        private HexBattleUnit GetLivingWallDirectionTarget(HexBattleUnit wall) =>
            GetLivingWallPair(wall) ?? _playerUnit;

        private IEnumerator ResolveLivingWallAdvance(HexBattleUnit wall)
        {
            if (grid == null || wall.State.livingWall.movementLocked)
                yield break;

            HexBattleUnit directionTarget = GetLivingWallDirectionTarget(wall);
            if (directionTarget == null || !directionTarget.IsAlive)
                yield break;

            int direction = HexBattlePathing.GetPrimaryDirectionIndex(grid, wall.State.coord, directionTarget.State.coord);
            HexAxialCoord destinationCore = HexAxialCoord.Neighbor(wall.State.coord, direction);
            var destinationCoords = BuildFootprintCoords(destinationCore, wall.State.livingWall.footprintOffsets);
            HexBattleUnit pair = GetLivingWallPair(wall);
            if (!IsLivingWallStaticDestinationLegal(wall, destinationCoords, pair))
                yield break;

            var pushedUnits = new List<HexBattleUnit>();
            for (int i = 0; i < destinationCoords.Count; i++)
            {
                HexBattleUnit occupant = FindUnitAtCoord(destinationCoords[i], wall);
                if (occupant == null || occupant == pair || pushedUnits.Contains(occupant))
                    continue;
                if (occupant.State.faction == wall.State.faction)
                    yield break;
                pushedUnits.Add(occupant);
            }

            bool closesPair = pair != null && FootprintsAreAdjacent(destinationCoords, pair.OccupiedCoords);
            var reserved = new HashSet<HexAxialCoord>(destinationCoords);
            bool blocked = false;
            for (int i = 0; i < pushedUnits.Count; i++)
            {
                HexBattleUnit target = pushedUnits[i];
                ForcedMovementResult movement = ResolveForcedMovementInDirection(target, direction, reserved, wall);
                if (movement == null || movement.path.Count <= 1)
                {
                    blocked = true;
                    if (closesPair && target.IsAlive)
                    {
                        ApplyDamageToUnit(target, HexLivingWallRules.SqueezeDamage, wall);
                        if (!target.IsAlive)
                            StartCoroutine(target.PlayDeathAndCleanup());
                    }
                    continue;
                }

                yield return MoveUnitRoutine(target, movement.path, 0);
            }

            if (blocked)
                yield break;

            yield return MoveUnitRoutine(wall, new List<HexAxialCoord> { wall.State.coord, destinationCore }, 0, directionTarget.State.coord);
        }

        private bool IsLivingWallStaticDestinationLegal(
            HexBattleUnit wall,
            IReadOnlyList<HexAxialCoord> destinationCoords,
            HexBattleUnit allowedPair)
        {
            if (destinationCoords == null || destinationCoords.Count == 0)
                return false;

            for (int i = 0; i < destinationCoords.Count; i++)
            {
                HexAxialCoord coord = destinationCoords[i];
                if (!IsLivingWallTerrainLegal(coord))
                    return false;
                HexBattleUnit occupyingWall = FindLivingWallAtCoord(coord, wall);
                if (occupyingWall != null && occupyingWall != allowedPair)
                    return false;
                if (occupyingWall == allowedPair)
                    return false;
            }
            return true;
        }

        private IEnumerator ResolveLivingWallSpike(HexBattleUnit wall)
        {
            if (grid == null)
                yield break;

            HexBattleUnit directionTarget = GetLivingWallDirectionTarget(wall);
            if (directionTarget == null || !directionTarget.IsAlive)
                yield break;

            int direction = HexBattlePathing.GetPrimaryDirectionIndex(grid, wall.State.coord, directionTarget.State.coord);
            var occupied = new HashSet<HexAxialCoord>(wall.OccupiedCoords);
            var targets = new HashSet<HexBattleUnit>();
            foreach (HexAxialCoord segment in occupied)
            {
                HexAxialCoord front = HexAxialCoord.Neighbor(segment, direction);
                if (occupied.Contains(front))
                    continue;
                HexBattleUnit target = FindUnitAtCoord(front, wall);
                if (target != null && target.IsAlive && target.State.faction != wall.State.faction)
                    targets.Add(target);
            }

            foreach (HexBattleUnit target in targets.OrderBy(unit => unit.State.id, StringComparer.Ordinal))
                yield return ResolveDirectAttackRoutine(wall, target, HexLivingWallRules.SpikeDamage);
        }

        private bool TryResolveLivingWallReform(HexBattleUnit wall)
        {
            if (grid == null || wall == null || !wall.IsAlive || _playerUnit == null || !_playerUnit.IsAlive)
                return false;

            HexBattleUnit pair = GetLivingWallPair(wall);
            if (pair == null)
                return false;

            int directionToPair = HexBattlePathing.GetPrimaryDirectionIndex(
                grid,
                _playerUnit.State.coord,
                pair.State.coord);
            int oppositeDirection = (directionToPair + 3) % HexAxialCoord.Directions.Length;
            HexAxialCoord destination = _playerUnit.State.coord;
            for (int step = 0; step < 3; step++)
                destination = HexAxialCoord.Neighbor(destination, oppositeDirection);

            if (!IsWholeFootprintLegal(wall, destination, wall.State.livingWall.footprintOffsets))
                return false;

            ApplyLivingWallReform(wall, destination, pair);
            return true;
        }

        private bool TryResolvePairedLivingWallReform(HexBattleUnit first, HexBattleUnit second)
        {
            if (grid == null || first == null || second == null ||
                !first.IsAlive || !second.IsAlive || _playerUnit == null || !_playerUnit.IsAlive)
                return false;

            var ignoredWalls = new HashSet<HexBattleUnit> { first, second };
            List<HexAxialCoord> candidates = GetLivingWallReformCandidates(first, second);
            for (int i = 0; i < candidates.Count; i++)
            {
                HexAxialCoord firstCore = candidates[i];
                HexAxialCoord secondCore = HexLivingWallRules.Rotate180(_playerUnit.State.coord, firstCore);
                if (!IsWholeFootprintLegal(first, firstCore, first.State.livingWall.footprintOffsets, ignoredWalls) ||
                    !IsWholeFootprintLegal(second, secondCore, second.State.livingWall.footprintOffsets, ignoredWalls))
                    continue;

                var firstCoords = new HashSet<HexAxialCoord>(
                    BuildFootprintCoords(firstCore, first.State.livingWall.footprintOffsets));
                var secondCoords = BuildFootprintCoords(secondCore, second.State.livingWall.footprintOffsets);
                if (secondCoords.Any(firstCoords.Contains))
                    continue;

                first.State.coord = firstCore;
                second.State.coord = secondCore;
                first.SnapTo(grid, unitYOffset);
                second.SnapTo(grid, unitYOffset);
                TryGrowLivingWall(first, second);
                TryGrowLivingWall(second, first);
                first.RefreshLivingWallView();
                second.RefreshLivingWallView();
                return true;
            }

            return TryResolveSeparatedSingleReform(first, second);
        }

        private bool TryResolveSeparatedSingleReform(HexBattleUnit wall, HexBattleUnit priorityTarget)
        {
            List<HexAxialCoord> candidates = GetLivingWallReformCandidates(wall, priorityTarget);
            for (int i = 0; i < candidates.Count; i++)
            {
                HexAxialCoord core = candidates[i];
                IReadOnlyList<HexAxialCoord> coords = BuildFootprintCoords(
                    core,
                    wall.State.livingWall.footprintOffsets);
                if (!IsWholeFootprintLegal(wall, core, wall.State.livingWall.footprintOffsets) ||
                    !HasLivingWallClearance(coords, wall, 2))
                    continue;

                ApplyLivingWallReform(wall, core, priorityTarget, 2);
                return true;
            }
            return false;
        }

        private List<HexAxialCoord> GetLivingWallReformCandidates(
            HexBattleUnit wall,
            HexBattleUnit priorityTarget)
        {
            IReadOnlyList<HexAxialCoord> offsets = wall.State.livingWall.footprintOffsets;
            return HexBattlePathing.GetCoordsInRange(_playerUnit.State.coord, 3)
                .Where(coord => HexAxialCoord.Distance(_playerUnit.State.coord, coord) == 3)
                .OrderBy(coord => GetFootprintDistance(coord, offsets, priorityTarget))
                .ThenBy(coord => coord.q)
                .ThenBy(coord => coord.r)
                .ToList();
        }

        private void ApplyLivingWallReform(
            HexBattleUnit wall,
            HexAxialCoord destination,
            HexBattleUnit priorityTarget,
            int minimumOtherWallDistance = 1)
        {
            wall.State.coord = destination;
            wall.SnapTo(grid, unitYOffset);
            TryGrowLivingWall(wall, priorityTarget, minimumOtherWallDistance);
            wall.RefreshLivingWallView();
        }

        private bool TryGrowLivingWall(
            HexBattleUnit wall,
            HexBattleUnit priorityTarget,
            int minimumOtherWallDistance = 1)
        {
            var offsets = wall.State.livingWall.footprintOffsets;
            if (offsets == null || offsets.Count >= HexLivingWallRules.MaxFootprintSize)
                return false;

            var occupied = new HashSet<HexAxialCoord>(wall.OccupiedCoords);
            var candidates = new HashSet<HexAxialCoord>();
            foreach (HexAxialCoord coord in occupied)
                for (int direction = 0; direction < HexAxialCoord.Directions.Length; direction++)
                {
                    HexAxialCoord candidate = HexAxialCoord.Neighbor(coord, direction);
                    if (!occupied.Contains(candidate) &&
                        IsWholeFootprintCellLegal(wall, candidate) &&
                        HasLivingWallClearance(new[] { candidate }, wall, minimumOtherWallDistance))
                        candidates.Add(candidate);
                }
            if (candidates.Count == 0)
                return false;

            HexAxialCoord chosen = candidates
                .OrderBy(coord => priorityTarget != null ? GetDistanceToUnit(coord, priorityTarget) : 0)
                .ThenBy(coord => coord.q)
                .ThenBy(coord => coord.r)
                .First();
            offsets.Add(new HexAxialCoord(chosen.q - wall.State.coord.q, chosen.r - wall.State.coord.r));
            return true;
        }

        private bool TrySummonLivingWallOffspring(HexBattleUnit source)
        {
            if (source == null || grid == null || _playerUnit == null)
                return false;
            int offspringCount = GetLivingWalls().Count(unit => unit.State.livingWall.isOffspring);
            if (offspringCount >= HexLivingWallRules.MaxOffspring)
                return false;

            HexEnemyDefinition definition = HexCardLibrary.GetEnemyDefinition("living_wall");
            if (definition == null)
                return false;

            var candidates = HexBattlePathing.GetCoordsInRange(source.State.coord, 2)
                .Where(coord => HexAxialCoord.Distance(source.State.coord, coord) >= 1)
                .Select(coord => new
                {
                    core = coord,
                    offsets = HexLivingWallRules.CreateInitialOffsets(grid, coord, _playerUnit.State.coord),
                })
                .Where(candidate => IsWholeFootprintLegal(null, candidate.core, candidate.offsets))
                .OrderBy(candidate => HexAxialCoord.Distance(source.State.coord, candidate.core))
                .ThenBy(candidate => candidate.core.q)
                .ThenBy(candidate => candidate.core.r)
                .ToList();
            if (candidates.Count == 0)
                return false;

            int spawnOrder = GetLivingWalls().Select(unit => unit.State.livingWall.spawnOrder).DefaultIfEmpty(0).Max() + 1;
            string id = $"living_wall_offspring_{spawnOrder}";
            var root = new GameObject(id);
            root.transform.SetParent(source.transform.parent != null ? source.transform.parent : transform, false);
            var offspring = root.AddComponent<HexBattleUnit>();
            offspring.Initialize(new HexBattleUnitState
            {
                id = id,
                displayName = "子代活墙壁",
                enemyDefinitionId = definition.id,
                faction = HexBattleFaction.Enemy,
                maxHealth = HexLivingWallRules.OffspringHealth,
                currentHealth = HexLivingWallRules.OffspringHealth,
                attackRange = 1,
                enemyAttackMinRange = 1,
                enemyAttackMaxRange = 1,
                isSummonedEnemy = true,
                summonOwnerId = source.State.id,
                coord = candidates[0].core,
                livingWall = new HexLivingWallRuntimeState
                {
                    isOffspring = true,
                    spawnOrder = spawnOrder,
                    footprintOffsets = candidates[0].offsets,
                },
            }, null, definition.deckDefinitions);
            offspring.SnapTo(grid, unitYOffset);
            offspring.AttachLivingWallView(grid);
            EnsureEnemyDefinition(offspring);
            _enemyUnits.Add(offspring);
            _units.Add(offspring);
            RepairLivingWallPairs();
            offspring.RefreshLabel();
            return true;
        }

        private bool ApplyLivingWallBreak(HexBattleUnit wall)
        {
            if (wall == null || !wall.IsAlive || !wall.IsLivingWall)
                return false;

            if (wall.State.livingWall.isOffspring)
            {
                wall.State.currentHealth = 0;
                wall.RefreshLabel();
                StartCoroutine(wall.PlayDeathAndCleanup());
                return true;
            }

            ApplyDamageToUnit(wall, HexLivingWallRules.GetBreakDamage(wall.State.maxHealth), _playerUnit);
            if (!wall.IsAlive)
                StartCoroutine(wall.PlayDeathAndCleanup());
            wall.RefreshLabel();
            return true;
        }

        private ForcedMovementResult ResolveLivingWallForcedMovement(
            HexBattleUnit source,
            HexBattleUnit wall,
            int distance,
            bool moveTowardSource)
        {
            HexAxialCoord start = wall.State.coord;
            if (wall.State.livingWall.movementLocked || wall.State.toughness > 0 || wall.State.cannotBeKnockedBackThisTurn)
                return StationaryForcedMovement(start);

            int direction = moveTowardSource
                ? HexBattlePathing.GetPrimaryDirectionIndex(grid, start, source.State.coord)
                : HexBattlePathing.GetPrimaryDirectionIndex(grid, source.State.coord, start);
            HexAxialCoord intended = GetForcedMovementIntendedDestination(start, direction, distance);
            var costs = new Dictionary<HexAxialCoord, int> { [start] = 0 };
            var cameFrom = new Dictionary<HexAxialCoord, HexAxialCoord>();
            var queue = new Queue<HexAxialCoord>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                HexAxialCoord current = queue.Dequeue();
                if (costs[current] >= distance)
                    continue;
                for (int directionIndex = 0; directionIndex < HexAxialCoord.Directions.Length; directionIndex++)
                {
                    HexAxialCoord next = HexAxialCoord.Neighbor(current, directionIndex);
                    if (costs.ContainsKey(next) || !IsWholeFootprintLegal(wall, next, wall.State.livingWall.footprintOffsets))
                        continue;
                    costs[next] = costs[current] + 1;
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            HexAxialCoord actual = SelectBestForcedMovementDestination(start, intended, direction, costs);
            return new ForcedMovementResult
            {
                path = ReconstructCorePath(start, actual, cameFrom),
                intendedDestination = intended,
                actualDestination = actual,
                collided = !actual.Equals(intended),
            };
        }

        private ForcedMovementResult ResolveForcedMovementInDirection(
            HexBattleUnit target,
            int direction,
            ISet<HexAxialCoord> reservedCoords,
            HexBattleUnit ignoredWall)
        {
            HexAxialCoord start = target.State.coord;
            if (target.State.toughness > 0 || target.State.cannotBeKnockedBackThisTurn)
                return StationaryForcedMovement(start);

            HexAxialCoord intended = HexAxialCoord.Neighbor(start, direction);
            var candidates = new Dictionary<HexAxialCoord, int> { [start] = 0 };
            for (int directionIndex = 0; directionIndex < HexAxialCoord.Directions.Length; directionIndex++)
            {
                HexAxialCoord candidate = HexAxialCoord.Neighbor(start, directionIndex);
                if (reservedCoords.Contains(candidate) || IsForcedDestinationBlocked(candidate, target, ignoredWall))
                    continue;
                candidates[candidate] = 1;
            }

            HexAxialCoord actual = SelectBestForcedMovementDestination(start, intended, direction, candidates);
            return new ForcedMovementResult
            {
                path = actual.Equals(start)
                    ? new List<HexAxialCoord> { start }
                    : new List<HexAxialCoord> { start, actual },
                intendedDestination = intended,
                actualDestination = actual,
                collided = !actual.Equals(intended),
            };
        }

        private bool IsForcedDestinationBlocked(HexAxialCoord coord, HexBattleUnit movingUnit, HexBattleUnit ignoredWall)
        {
            if (grid == null || !grid.IsCoordInside(coord))
                return true;
            if (grid.TryGetTile(coord, out var tile) && tile != null && !TileCanEnter(tile))
                return true;
            for (int i = 0; i < _units.Count; i++)
            {
                HexBattleUnit unit = _units[i];
                if (unit == null || !unit.IsAlive || unit == movingUnit || unit == ignoredWall)
                    continue;
                if (unit.Occupies(coord))
                    return true;
            }
            return HasSceneObstacleAtCoord(coord, movingUnit);
        }

        private static ForcedMovementResult StationaryForcedMovement(HexAxialCoord coord) => new()
        {
            path = new List<HexAxialCoord> { coord },
            intendedDestination = coord,
            actualDestination = coord,
            collided = true,
        };

        private static List<HexAxialCoord> ReconstructCorePath(
            HexAxialCoord start,
            HexAxialCoord destination,
            IReadOnlyDictionary<HexAxialCoord, HexAxialCoord> cameFrom)
        {
            var path = new List<HexAxialCoord> { destination };
            HexAxialCoord current = destination;
            while (!current.Equals(start) && cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        private bool IsWholeFootprintLegal(
            HexBattleUnit wall,
            HexAxialCoord core,
            IReadOnlyList<HexAxialCoord> offsets,
            ISet<HexBattleUnit> ignoredUnits = null)
        {
            var coords = BuildFootprintCoords(core, offsets);
            for (int i = 0; i < coords.Count; i++)
            {
                if (!IsLivingWallTerrainLegal(coords[i]))
                    return false;
                for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
                {
                    HexBattleUnit unit = _units[unitIndex];
                    if (unit == null || !unit.IsAlive || unit == wall || ignoredUnits?.Contains(unit) == true)
                        continue;
                    if (unit.Occupies(coords[i]))
                        return false;
                }
            }
            return true;
        }

        private bool HasLivingWallClearance(
            IReadOnlyList<HexAxialCoord> candidateCoords,
            HexBattleUnit movingWall,
            int minimumDistance)
        {
            if (candidateCoords == null || minimumDistance <= 0)
                return true;

            List<HexBattleUnit> otherWalls = GetLivingWalls()
                .Where(other => other != movingWall)
                .ToList();
            for (int candidateIndex = 0; candidateIndex < candidateCoords.Count; candidateIndex++)
                for (int wallIndex = 0; wallIndex < otherWalls.Count; wallIndex++)
                {
                    IReadOnlyList<HexAxialCoord> occupied = otherWalls[wallIndex].OccupiedCoords;
                    for (int occupiedIndex = 0; occupiedIndex < occupied.Count; occupiedIndex++)
                        if (HexAxialCoord.Distance(candidateCoords[candidateIndex], occupied[occupiedIndex]) < minimumDistance)
                            return false;
                }
            return true;
        }

        private bool IsWholeFootprintCellLegal(HexBattleUnit wall, HexAxialCoord coord)
        {
            if (!IsLivingWallTerrainLegal(coord))
                return false;
            for (int i = 0; i < _units.Count; i++)
            {
                HexBattleUnit unit = _units[i];
                if (unit == null || !unit.IsAlive || unit == wall)
                    continue;
                if (unit.Occupies(coord))
                    return false;
            }
            return true;
        }

        private bool IsLivingWallTerrainLegal(HexAxialCoord coord)
        {
            if (grid == null || !grid.IsCoordInside(coord) || !grid.TryGetTile(coord, out var tile) || tile == null)
                return false;
            return TileCanEnter(tile) && TilePickupType(tile) == HexTerrainPickupType.None;
        }

        private static List<HexAxialCoord> BuildFootprintCoords(
            HexAxialCoord core,
            IReadOnlyList<HexAxialCoord> offsets)
        {
            var result = new List<HexAxialCoord>();
            if (offsets == null || offsets.Count == 0)
            {
                result.Add(core);
                return result;
            }
            for (int i = 0; i < offsets.Count; i++)
                result.Add(HexLivingWallRules.ToWorldCoord(core, offsets[i]));
            return result;
        }

        private static bool FootprintsAreAdjacent(
            IReadOnlyList<HexAxialCoord> first,
            IReadOnlyList<HexAxialCoord> second)
        {
            if (first == null || second == null)
                return false;
            var secondSet = new HashSet<HexAxialCoord>(second);
            for (int i = 0; i < first.Count; i++)
                for (int direction = 0; direction < HexAxialCoord.Directions.Length; direction++)
                    if (secondSet.Contains(HexAxialCoord.Neighbor(first[i], direction)))
                        return true;
            return false;
        }

        private int GetFootprintDistance(
            HexAxialCoord core,
            IReadOnlyList<HexAxialCoord> offsets,
            HexBattleUnit target)
        {
            if (target == null)
                return 0;
            int best = int.MaxValue;
            var coords = BuildFootprintCoords(core, offsets);
            for (int i = 0; i < coords.Count; i++)
                best = Mathf.Min(best, GetDistanceToUnit(coords[i], target));
            return best;
        }

        private int GetUnitDistance(HexBattleUnit first, HexBattleUnit second)
        {
            if (first == null || second == null)
                return int.MaxValue;
            int best = int.MaxValue;
            IReadOnlyList<HexAxialCoord> firstCoords = first.OccupiedCoords;
            IReadOnlyList<HexAxialCoord> secondCoords = second.OccupiedCoords;
            for (int i = 0; i < firstCoords.Count; i++)
                for (int j = 0; j < secondCoords.Count; j++)
                    best = Mathf.Min(best, HexAxialCoord.Distance(firstCoords[i], secondCoords[j]));
            return best;
        }

        private int GetDistanceToUnit(HexAxialCoord coord, HexBattleUnit unit)
        {
            if (unit == null)
                return int.MaxValue;
            int best = int.MaxValue;
            IReadOnlyList<HexAxialCoord> occupied = unit.OccupiedCoords;
            for (int i = 0; i < occupied.Count; i++)
                best = Mathf.Min(best, HexAxialCoord.Distance(coord, occupied[i]));
            return best;
        }

        private HexBattleUnit FindLivingWallAtCoord(HexAxialCoord coord, HexBattleUnit ignoreUnit = null)
        {
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                HexBattleUnit unit = _enemyUnits[i];
                if (unit == null || !unit.IsAlive || unit == ignoreUnit || !unit.IsLivingWall)
                    continue;
                if (unit.Occupies(coord))
                    return unit;
            }
            return null;
        }
    }
}
