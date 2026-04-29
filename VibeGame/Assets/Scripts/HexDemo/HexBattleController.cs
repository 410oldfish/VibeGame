using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexBattleController : MonoBehaviour
    {
        public HexGrid grid;
        public Camera rayCamera;
        public float unitYOffset = 0.03f;
        public float moveSpeed = 6.5f;
        public float stepStopDelay = 0.05f;

        private readonly List<HexBattleUnit> _units = new();
        private HexBattleUnit _playerUnit;
        private HexBattleUnit _enemyUnit;
        private HexBattleFaction _currentTurn = HexBattleFaction.Player;
        private HexBattleUI _ui;
        private HexTile _hoveredTile;
        private bool _hoverHasColliderHit;
        private HexCardInstance _draggedCard;
        private bool _busy;
        private LineRenderer _targetArrow;

        public void Initialize(HexGrid battleGrid, HexBattleUnit playerUnit, HexBattleUnit enemyUnit, Camera battleCamera)
        {
            grid = battleGrid;
            rayCamera = battleCamera != null ? battleCamera : Camera.main;

            _playerUnit = playerUnit;
            _enemyUnit = enemyUnit;
            _units.Clear();
            _units.Add(playerUnit);
            _units.Add(enemyUnit);

            var uiGO = new GameObject("HexBattleUI_Root");
            _ui = uiGO.AddComponent<HexBattleUI>();
            _ui.Initialize(this);
            EnsureTargetArrow();

            BeginTurn(HexBattleFaction.Player);
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

        public string GetTurnSummary()
        {
            return _currentTurn == HexBattleFaction.Player ? "Player Turn" : "Enemy Turn";
        }

        public string GetStatusSummary()
        {
            return $"Hero   HP {_playerUnit.State.currentHealth}/{_playerUnit.State.maxHealth}  Armor {_playerUnit.State.armor}  Energy {_playerUnit.State.energy}/{_playerUnit.State.maxEnergy}  Move {_playerUnit.State.currentMovePoints}/{_playerUnit.State.maxMovePoints}\n" +
                   $"Enemy  HP {_enemyUnit.State.currentHealth}/{_enemyUnit.State.maxHealth}  Armor {_enemyUnit.State.armor}  Energy {_enemyUnit.State.energy}/{_enemyUnit.State.maxEnergy}  Move {_enemyUnit.State.currentMovePoints}/{_enemyUnit.State.maxMovePoints}";
        }

        public string GetDeckSummary()
        {
            return $"Draw {_playerUnit.Deck.DrawPile.Count}   Hand {_playerUnit.Deck.Hand.Count}   Discard {_playerUnit.Deck.DiscardPile.Count}";
        }

        public bool CanLocalPlayerEndTurn()
        {
            return !_busy && _currentTurn == HexBattleFaction.Player && _draggedCard == null && _playerUnit.IsAlive;
        }

        public void RequestEndTurn()
        {
            if (!CanLocalPlayerEndTurn())
                return;

            StartCoroutine(EndTurnRoutine());
        }

        public void BeginCardDrag(HexCardInstance card)
        {
            if (_busy || _currentTurn != HexBattleFaction.Player || !_playerUnit.IsAlive)
                return;

            _draggedCard = card;
            UpdateRangeHighlights();
            SetTargetArrowActive(card.definition.targetType == HexCardTargetType.EnemyUnit);
            UpdateDraggedCard(Vector2.zero);
        }

        public void UpdateDraggedCard(Vector2 screenPosition)
        {
            UpdateHoverFeedback();
            UpdateTargetArrow();
        }

        public bool EndCardDrag(Vector2 screenPosition)
        {
            if (_draggedCard == null)
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
            if (!TryGetHoveredTile(out var tile, out _))
                return;

            if (tile == null || IsOccupied(tile.coord, _playerUnit))
                return;

            var path = HexBattlePathing.FindPath(grid, _playerUnit.State.coord, tile.coord, coord => IsOccupied(coord, _playerUnit));
            int moveCost = path != null ? path.Count - 1 : 0;
            if (path == null || path.Count < 2 || moveCost > _playerUnit.State.currentMovePoints)
                return;

            StartCoroutine(MoveUnitRoutine(_playerUnit, path, moveCost));
        }

        private bool TryPlayDraggedCard(Vector2 screenPosition)
        {
            if (_draggedCard == null || !_playerUnit.CanPay(_draggedCard))
                return false;

            if (_draggedCard.definition.targetType == HexCardTargetType.Self)
            {
                ResolveCard(_playerUnit, _playerUnit, _draggedCard);
                return true;
            }

            if (!TryGetHoveredUnit(out var targetUnit))
                return false;

            if (targetUnit == null || targetUnit.State.faction == _playerUnit.State.faction)
                return false;

            if (HexAxialCoord.Distance(_playerUnit.State.coord, targetUnit.State.coord) > _draggedCard.definition.range)
                return false;

            ResolveCard(_playerUnit, targetUnit, _draggedCard);
            return true;
        }

        private void ResolveCard(HexBattleUnit source, HexBattleUnit target, HexCardInstance card)
        {
            source.SpendEnergy(card.definition.energyCost);
            source.Deck.DiscardFromHand(card);

            switch (card.definition.effectType)
            {
                case HexCardEffectType.Attack:
                    target.ApplyDamage(card.definition.amount);
                    break;
                case HexCardEffectType.Defend:
                    source.GainArmor(card.definition.amount);
                    break;
            }

            if (grid.TryGetTile(target.State.coord, out var targetTile))
                targetTile.FlashClick();

            source.RefreshLabel();
            target.RefreshLabel();
            _ui.Refresh();

            if (!target.IsAlive)
                StartCoroutine(HandleBattleEnd());
        }

        private IEnumerator MoveUnitRoutine(HexBattleUnit unit, List<HexAxialCoord> path, int moveCost)
        {
            _busy = true;
            yield return unit.MoveAlongPath(grid, path, unitYOffset, moveSpeed, stepStopDelay);
            unit.SpendMovePoints(moveCost);
            _busy = false;
            UpdateMovementHighlights();
            _ui.Refresh();
        }

        private IEnumerator EndTurnRoutine()
        {
            _busy = true;
            ClearRangeHighlights();
            ClearMovementHighlights();
            GetCurrentUnit().EndTurn();
            yield return new WaitForSeconds(0.15f);
            BeginTurn(_currentTurn == HexBattleFaction.Player ? HexBattleFaction.Enemy : HexBattleFaction.Player);
            _busy = false;
        }

        private void BeginTurn(HexBattleFaction faction)
        {
            _currentTurn = faction;
            var currentUnit = GetCurrentUnit();
            currentUnit.BeginTurn();
            UpdateMovementHighlights();
            _ui.Refresh();

            if (_currentTurn == HexBattleFaction.Enemy)
                StartCoroutine(RunEnemyTurn());
        }

        private IEnumerator RunEnemyTurn()
        {
            yield return new WaitForSeconds(0.45f);
            var enemy = _enemyUnit;
            var player = _playerUnit;

            if (!enemy.IsAlive || !player.IsAlive)
                yield break;

            yield return TryEnemyMove(enemy, player);

            bool playedAny = true;
            while (playedAny && enemy.State.energy > 0 && enemy.Deck.Hand.Count > 0)
            {
                playedAny = false;
                var orderedCards = enemy.Deck.Hand
                    .Where(enemy.CanPay)
                    .OrderBy(card => card.definition.priority)
                    .ThenBy(card => card.definition.energyCost)
                    .ToList();

                foreach (var card in orderedCards)
                {
                    bool cardPlayed = false;
                    if (card.definition.effectType == HexCardEffectType.Attack)
                    {
                        if (HexAxialCoord.Distance(enemy.State.coord, player.State.coord) <= card.definition.range)
                        {
                            ResolveCard(enemy, player, card);
                            cardPlayed = true;
                        }
                    }
                    else if (card.definition.effectType == HexCardEffectType.Defend)
                    {
                        ResolveCard(enemy, enemy, card);
                        cardPlayed = true;
                    }

                    if (cardPlayed)
                    {
                        playedAny = true;
                        yield return new WaitForSeconds(0.45f);
                        if (!player.IsAlive)
                            yield break;
                        break;
                    }
                }
            }

            enemy.EndTurn();
            yield return new WaitForSeconds(0.2f);
            BeginTurn(HexBattleFaction.Player);
        }

        private IEnumerator TryEnemyMove(HexBattleUnit enemy, HexBattleUnit player)
        {
            if (enemy.State.currentMovePoints <= 0)
                yield break;

            if (HexAxialCoord.Distance(enemy.State.coord, player.State.coord) <= enemy.State.attackRange)
                yield break;

            List<HexAxialCoord> bestPath = null;
            foreach (var neighbor in grid.GetNeighbors(player.State.coord))
            {
                if (IsOccupied(neighbor, enemy))
                    continue;

                var path = HexBattlePathing.FindPath(grid, enemy.State.coord, neighbor, coord => IsOccupied(coord, enemy));
                if (path == null || path.Count < 2)
                    continue;

                if (bestPath == null || path.Count < bestPath.Count)
                    bestPath = path;
            }

            if (bestPath == null)
                yield break;

            int maxSteps = Mathf.Min(enemy.State.currentMovePoints, bestPath.Count - 1);
            var trimmed = bestPath.Take(maxSteps + 1).ToList();
            yield return MoveUnitRoutine(enemy, trimmed, trimmed.Count - 1);
        }

        private IEnumerator HandleBattleEnd()
        {
            _busy = true;
            yield return new WaitForSeconds(0.25f);
            _ui.Refresh();
        }

        private HexBattleUnit GetCurrentUnit()
        {
            return _currentTurn == HexBattleFaction.Player ? _playerUnit : _enemyUnit;
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

            foreach (var coord in HexBattlePathing.GetCoordsInRange(_playerUnit.State.coord, _draggedCard.definition.range))
            {
                if (!grid.TryGetTile(coord, out var tile))
                    continue;

                bool targetable = _enemyUnit.IsAlive && _enemyUnit.State.coord.Equals(coord);
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

            var reachable = GetReachableCosts(_playerUnit);
            foreach (var tile in grid.Tiles.Values)
            {
                if (tile.coord.Equals(_playerUnit.State.coord))
                {
                    tile.SetMoveIndicator(true, true);
                    continue;
                }

                bool canReach = reachable.TryGetValue(tile.coord, out int cost) &&
                                cost <= _playerUnit.State.currentMovePoints &&
                                !IsOccupied(tile.coord, _playerUnit);
                tile.SetMoveIndicator(canReach, canReach);
            }

            if (_hoveredTile != null && !_hoveredTile.coord.Equals(_playerUnit.State.coord))
            {
                bool canReach = reachable.TryGetValue(_hoveredTile.coord, out int hoveredCost) &&
                                hoveredCost <= _playerUnit.State.currentMovePoints &&
                                !IsOccupied(_hoveredTile.coord, _playerUnit);
                if (!canReach)
                    _hoveredTile.SetMoveIndicator(true, false);
            }
        }

        private void ClearMovementHighlights()
        {
            if (grid == null)
                return;

            foreach (var tile in grid.Tiles.Values)
                tile.SetMoveIndicator(false, false);
        }

        private Dictionary<HexAxialCoord, int> GetReachableCosts(HexBattleUnit unit)
        {
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
                    if (!grid.IsCoordInside(neighbor) || IsOccupied(neighbor, unit))
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
    }
}
