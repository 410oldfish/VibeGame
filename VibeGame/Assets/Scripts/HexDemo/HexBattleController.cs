using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StringComparer = System.StringComparer;
using TEngine;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexCardPlayLogEntry
    {
        public string turnOwner;
        public string sourceName;
        public string targetName;
        public string cardName;
    }

    public sealed partial class HexBattleController : MonoBehaviour
    {
        private delegate IEnumerator EnemySpecialHandler(HexBattleUnit enemy, HexBattleUnit target, HexCardInstance card);

        private sealed class ForcedMovementResult
        {
            public List<HexAxialCoord> path = new();
            public HexAxialCoord intendedDestination;
            public HexAxialCoord actualDestination;
            public bool collided;
        }

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
        private bool? _lastBattlePlayerWon;
        private bool _updateRegistered;
        private readonly List<HexCardPlayLogEntry> _playLog = new();
        private readonly Dictionary<HexBattleUnit, List<HexEnemyIntentSlot>> _enemyIntentSlots = new();
        private readonly Dictionary<string, EnemySpecialHandler> _enemySpecialHandlers = new(StringComparer.OrdinalIgnoreCase);
        private HexCardDefinition _lastPlayerMirrorCard;
        private HexBattleUnit _activeAttackPassiveSource;
        private HexCardInstance _activeAttackPassiveCard;

        public System.Action<bool, int, HexBattleUnit> BattleFinished;

        public void Initialize(HexGrid battleGrid, HexBattleUnit playerUnit, IReadOnlyList<HexBattleUnit> enemyUnits, Camera battleCamera, HexRunState runState = null)
        {
            grid = battleGrid;
            rayCamera = battleCamera != null ? battleCamera : Camera.main;
            _battleFinished = false;
            _lastBattlePlayerWon = null;

            _playerUnit = playerUnit;
            InitializeConsumables(runState);
            _playerUnit.ResetBattleState();
            RegisterEnemySpecialHandlers();
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
                    EnsureEnemyDefinition(enemyUnits[i]);
                    _units.Add(enemyUnits[i]);
                }
            }

            var uiGO = new GameObject("HexBattleUI_Root");
            uiGO.transform.SetParent(transform, false);
            _ui = uiGO.AddComponent<HexBattleUI>();
            _ui.Initialize(this);
            EnsureTargetArrow();
            RegisterUpdate();
            GameEvent.Send(HexGameEvents.BattleStarted, this);

            BeginTurn(HexBattleFaction.Player);
        }

        private void RegisterEnemySpecialHandlers()
        {
            _enemySpecialHandlers.Clear();
            string[] ids =
            {
                "enemy_goblin_roll", "enemy_spear_goblin_cover_retreat", "enemy_spear_goblin_volley",
                "enemy_goblin_captain_net", "enemy_goblin_captain_warcry", "enemy_goblin_captain_rally",
                "enemy_goblin_captain_shield_wall", "enemy_chieftain_charge", "enemy_chieftain_quake",
                "enemy_chieftain_brace", "enemy_chieftain_drum", "enemy_vine_entangle", "enemy_vine_snare",
                "enemy_vine_spread", "enemy_vine_spore_sac", "enemy_wall_root_stab", "enemy_wall_crush",
                "enemy_wall_grow", "enemy_wall_regenerate", "enemy_gargoyle_dive", "enemy_gargoyle_stone_skin",
                "enemy_gargoyle_guard", "enemy_gargoyle_gaze", "enemy_gargoyle_rockfall",
                "enemy_hellhound_chain_bite", "enemy_hellhound_flame_fang", "enemy_hellhound_charge",
                "enemy_hellhound_lick_fire", "enemy_hellhound_instinct", "enemy_hellhound_ember",
                "enemy_mimic_frenzy", "enemy_mimic_pounce", "enemy_mimic_reveal", "enemy_mimic_sticky",
                "enemy_mimic_greed", "enemy_mind_flayer_steal", "enemy_mind_flayer_blast",
                "enemy_mind_flayer_tentacles", "enemy_mind_flayer_obscure",
                "enemy_orc_charge",
            };
            for (int i = 0; i < ids.Length; i++)
                _enemySpecialHandlers[ids[i]] = ResolveRegisteredEnemySpecialCard;
            RegisterLivingWallSpecialHandlers();
        }

        private void EnsureEnemyDefinition(HexBattleUnit enemy)
        {
            if (enemy?.State == null)
                return;

            string definitionId = string.IsNullOrWhiteSpace(enemy.State.enemyDefinitionId)
                ? InferEnemyDefinitionId(enemy.State.displayName)
                : enemy.State.enemyDefinitionId;
            var definition = HexCardLibrary.GetEnemyDefinition(definitionId);
            if (definition == null)
                return;

            enemy.State.enemyDefinitionId = definition.id;
            if (string.IsNullOrWhiteSpace(enemy.State.displayName))
                enemy.State.displayName = definition.displayName;
            enemy.State.emptyDrawPileStrengthGain = definition.emptyDrawPileStrengthGain;
            GetEnemyIdealAttackRange(definition, out int minRange, out int maxRange);
            enemy.State.enemyAttackMinRange = minRange;
            enemy.State.enemyAttackMaxRange = maxRange;
            enemy.State.attackRange = Mathf.Max(enemy.State.attackRange, maxRange);
        }

        private static void GetEnemyIdealAttackRange(HexEnemyDefinition definition, out int minRange, out int maxRange)
        {
            minRange = Mathf.Max(1, definition?.attackMinRange ?? 1);
            maxRange = Mathf.Max(minRange, definition?.attackMaxRange ?? minRange);
            if (definition?.deckDefinitions == null)
                return;

            for (int i = 0; i < definition.deckDefinitions.Count; i++)
            {
                var card = definition.deckDefinitions[i];
                if (card == null || card.cardType != HexCardType.Attack)
                    continue;
                maxRange = Mathf.Max(maxRange, Mathf.Max(1, card.castRange));
            }
        }

        private void GetEnemyIdealAttackRange(HexBattleUnit enemy, out int minRange, out int maxRange)
        {
            minRange = Mathf.Max(1, enemy?.State?.enemyAttackMinRange ?? 1);
            maxRange = Mathf.Max(minRange, enemy?.State?.enemyAttackMaxRange ?? minRange);
            if (enemy?.State == null)
                return;

            var definition = HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId);
            if (definition == null)
                return;

            GetEnemyIdealAttackRange(definition, out minRange, out maxRange);
        }

        private static string InferEnemyDefinitionId(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "goblin";

            string normalized = displayName.ToLowerInvariant();
            if (normalized.Contains("spear") || displayName.Contains("投矛"))
                return "spear_goblin";
            if (normalized.Contains("captain") || displayName.Contains("队长"))
                return "goblin_captain";
            if (normalized.Contains("chieftain") || normalized.Contains("boss") || displayName.Contains("酋长"))
                return "tribal_chieftain";

            return "goblin";
        }

        private void OnDestroy()
        {
            _battleFinished = true;
            _busy = true;
            _draggedCard = null;
            _hoveredTile = null;
            BattleFinished = null;
            UnregisterUpdate();
            if (_targetArrow != null)
                Destroy(_targetArrow.gameObject);
            if (_ui != null)
                Destroy(_ui.gameObject);
        }

        private void Tick()
        {
            if (grid == null || rayCamera == null)
                return;

            if (Input.GetMouseButtonDown(1) && _ui != null && _ui.IsBlockingWorldClick())
            {
                _ui.CloseTopModal();
                return;
            }

            UpdateHoverFeedback();
            UpdateOrcChargeIntentPreviews();
            if (!IsConsumableTargeting())
                UpdateMovementHighlights();
            if (_busy || _draggedCard != null || _currentTurn != HexBattleFaction.Player)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (TryHandlePendingConsumableTargetClick())
                    return;
                if (TryHandleEnemyHandClick())
                    return;
                TryHandleTerrainDetailClick();
            }
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

        public IReadOnlyList<HexCardInstance> GetLocalExhaustPile()
        {
            return _playerUnit != null ? _playerUnit.Deck.ExhaustPile : System.Array.Empty<HexCardInstance>();
        }

        public IReadOnlyList<HexCardPlayLogEntry> GetPlayLog()
        {
            return _playLog;
        }

        public IReadOnlyList<HexCardInstance> GetEnemyHand(HexBattleUnit enemy)
        {
            if (enemy == null || enemy.State == null || enemy.State.faction != HexBattleFaction.Enemy)
                return System.Array.Empty<HexCardInstance>();

            if (_enemyIntentSlots.TryGetValue(enemy, out var slots) && slots != null && slots.Count > 0)
                return slots.Where(slot => slot?.card != null).Select(slot => slot.card).ToList();

            return enemy.Deck.Hand;
        }

        public int GetLocalCardCost(HexCardInstance card)
        {
            return _playerUnit != null ? _playerUnit.GetCardEnergyCost(card) : 0;
        }

        public string GetTurnSummary()
        {
            return _currentTurn == HexBattleFaction.Player ? "玩家回合" : "敌方回合";
        }

        public BattleHudSnapshot GetBattleHudSnapshot()
        {
            var snapshot = new BattleHudSnapshot
            {
                phaseLabel = GetTurnSummary(),
                canEndTurn = CanLocalPlayerEndTurn(),
            };

            if (_playerUnit?.State != null)
            {
                snapshot.player.displayName = "战士";
                snapshot.player.currentHealth = _playerUnit.State.currentHealth;
                snapshot.player.maxHealth = _playerUnit.State.maxHealth;
                snapshot.player.armor = _playerUnit.State.armor;
                snapshot.player.energy = _playerUnit.State.energy;
                snapshot.player.maxEnergy = _playerUnit.State.maxEnergy;
                snapshot.player.power = _playerUnit.State.strength;
                snapshot.player.statuses = HexBattleStatusDisplay.BuildMvpStatusEntries(_playerUnit.State);

                snapshot.piles.draw = _playerUnit.Deck.DrawPile.Count;
                snapshot.piles.hand = _playerUnit.Deck.Hand.Count;
                snapshot.piles.discard = _playerUnit.Deck.DiscardPile.Count;
                snapshot.piles.exhaust = _playerUnit.Deck.ExhaustPile.Count;
            }

            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                var enemyView = new BattleUnitHudView
                {
                    enemyIndex = i,
                    displayName = enemy.State?.displayName ?? $"敌人 {i + 1}",
                    currentHealth = enemy.State.currentHealth,
                    maxHealth = enemy.State.maxHealth,
                    armor = enemy.State.armor,
                    statuses = HexBattleStatusDisplay.BuildMvpStatusEntries(enemy.State),
                    intentOrderHint = BuildIntentOrderHint(enemy),
                };

                if (_enemyIntentSlots.TryGetValue(enemy, out var slots) && slots != null)
                {
                    var ordered = GetEnemyIntentExecutionOrder(enemy);
                    for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                    {
                        var slot = slots[slotIndex];
                        bool isEmpty = slot?.card == null || slot.card.definition == null;
                        int order = ordered.FindIndex(s => ReferenceEquals(s, slot));
                        enemyView.intentSlots.Add(new BattleIntentSlotView
                        {
                            slotKind = slot.slotKind,
                            slotLabel = HexBattleStatusDisplay.GetIntentSlotLabel(slot.slotKind),
                            cardName = isEmpty ? string.Empty : (enemy.State.enemyHiddenIntentSlotIndex == slotIndex ? "?" : slot.card.definition.displayName),
                            cardCost = isEmpty ? 0 : Mathf.Max(0, slot.card.definition.energyCost),
                            isEmpty = isEmpty,
                            executionOrder = order >= 0 ? order + 1 : slotIndex + 1,
                        });
                    }
                }

                snapshot.enemies.Add(enemyView);
            }

            return snapshot;
        }

        private string BuildIntentOrderHint(HexBattleUnit enemy)
        {
            if (enemy?.State == null)
                return string.Empty;

            var definition = HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId);
            if (definition == null || !_enemyIntentSlots.TryGetValue(enemy, out var slots) || slots == null || slots.Count == 0)
                return string.Empty;

            if (enemy.State.livingWall?.reformPending == true)
                return "重聚准备：下个自身回合开始时传送并成长";
            if (definition.intentPattern == HexEnemyIntentPattern.PairedLivingWall)
            {
                var pair = GetLivingWallPair(enemy);
                return pair != null
                    ? $"对向墙：{pair.State.displayName}"
                    : "无配对墙：改以玩家为目标";
            }

            var orderedSlots = GetEnemyIntentExecutionOrder(enemy);
            string orderHint = BuildMoveAttackOrderHint(
                orderedSlots,
                definition.intentPattern != HexEnemyIntentPattern.ApproachStrike &&
                definition.intentPattern != HexEnemyIntentPattern.Ranged);
            if (definition.intentPattern == HexEnemyIntentPattern.LineCharge &&
                slots.Any(slot => slot?.card?.definition?.id == "enemy_orc_charge"))
            {
                if (TryBuildOrcChargePreview(enemy, out _, out HexAxialCoord knockbackDestination))
                {
                    int damage = enemy.State.orcChargeEmpowered
                        ? HexOrcWarriorRules.EmpoweredChargeDamage
                        : HexOrcWarriorRules.BaseChargeDamage;
                    int knockback = enemy.State.orcChargeEmpowered
                        ? HexOrcWarriorRules.EmpoweredKnockback
                        : HexOrcWarriorRules.BaseKnockback;
                    return JoinIntentHints(
                        orderHint,
                        $"直线冲锋：{damage}伤，击退{knockback}至 {knockbackDestination}");
                }

                return JoinIntentHints(orderHint, "直线冲锋无合法命中：执行时改为逼近1格");
            }

            return orderHint;
        }

        private static string BuildMoveAttackOrderHint(
            IReadOnlyList<HexEnemyIntentSlot> orderedSlots,
            bool fixedOrder)
        {
            if (orderedSlots == null)
                return string.Empty;

            int attackIndex = -1;
            int moveIndex = -1;
            for (int i = 0; i < orderedSlots.Count; i++)
            {
                if (orderedSlots[i]?.slotKind == HexEnemyIntentSlotKind.Attack && attackIndex < 0)
                    attackIndex = i;
                else if (orderedSlots[i]?.slotKind == HexEnemyIntentSlotKind.Move && moveIndex < 0)
                    moveIndex = i;
            }

            if (attackIndex < 0 || moveIndex < 0)
                return string.Empty;

            string sequence = attackIndex < moveIndex ? "先攻后移" : "先移后攻";
            return fixedOrder ? $"固定顺序：{sequence}" : $"若保持当前距离：{sequence}";
        }

        private static string JoinIntentHints(string orderHint, string detail)
        {
            return string.IsNullOrWhiteSpace(orderHint) ? detail : $"{orderHint}；{detail}";
        }

        public string GetStatusSummary()
        {
            var builder = new StringBuilder();
            builder.Append($"Hero   HP {_playerUnit.State.currentHealth}/{_playerUnit.State.maxHealth}  Armor {_playerUnit.State.armor}  Energy {_playerUnit.State.energy}/{_playerUnit.State.maxEnergy}");
            AppendStatusEffects(builder, _playerUnit);
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                builder.Append('\n');
                builder.Append($"Enemy {i + 1}  HP {enemy.State.currentHealth}/{enemy.State.maxHealth}  Armor {enemy.State.armor}");
                AppendStatusEffects(builder, enemy);
                AppendEnemyIntent(builder, enemy);
            }

            return builder.ToString();
        }

        public string GetDeckSummary()
        {
            if (_playerUnit == null)
                return string.Empty;

            var piles = GetBattleHudSnapshot().piles;
            return $"抽牌 {piles.draw}  手牌 {piles.hand}  弃牌 {piles.discard}  消耗 {piles.exhaust}";
        }

        public string GetResourceSummary()
        {
            if (_playerUnit?.State == null)
                return string.Empty;

            return $"能量 {_playerUnit.State.energy}/{_playerUnit.State.maxEnergy}\n力量 {_playerUnit.State.strength}";
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

        private bool TryHandleEnemyHandClick()
        {
            if (_ui != null && _ui.IsBlockingWorldClick())
                return true;

            if (!TryGetHoveredUnit(out var unit) || unit == null || unit.State.faction != HexBattleFaction.Enemy)
            {
                _ui?.CloseEnemyHandPopup();
                return false;
            }

            _ui?.CloseTerrainDetailPopup();
            _ui?.OpenEnemyHandPopup(unit, Input.mousePosition);
            return true;
        }

        private bool TryHandleTerrainDetailClick()
        {
            if (_ui != null && _ui.IsBlockingWorldClick())
                return true;

            if (!TryGetHoveredTile(out var tile, out _) || tile == null)
            {
                _ui?.CloseTerrainDetailPopup();
                return false;
            }

            bool shouldShow = tile.Controller != null
                ? tile.Controller.ShouldShowDetail()
                : (tile.HasBarrier || tile.HasRuin || tile.zone != HexTerrainZoneType.Normal);
            if (!shouldShow)
            {
                _ui?.CloseTerrainDetailPopup();
                return false;
            }

            _ui?.OpenTerrainDetailPopup(tile, Input.mousePosition);
            return true;
        }

        private bool TryPlayDraggedCard(Vector2 screenPosition)
        {
            if (_draggedCard == null || _draggedCard.definition == null || _draggedCard.definition.isUnplayable || !_playerUnit.CanPay(_draggedCard) || _busy)
                return false;
            if (!KeywordTriggerEngine.CanPlay(_playerUnit, _draggedCard))
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
                if (RequiresTraversableTileTarget(_draggedCard.definition) && !CanUseAsMovementTarget(hoveredTile))
                    return false;

                if (IsTileActionCard(_draggedCard.definition))
                {
                    if (!CanResolveTileAction(_playerUnit, _draggedCard.definition, hoveredTile.coord))
                        return false;

                    if (!SubmitAuthoritativeCommand(HexNetworkCommandType.PlayCard, ToPayload(_draggedCard, hoveredTile.coord)))
                        return true;

                    StartCoroutine(ResolveCardRoutine(_playerUnit, _playerUnit, _draggedCard, hoveredTile.coord));
                    return true;
                }

                if (HexAxialCoord.Distance(_playerUnit.State.coord, hoveredTile.coord) > _draggedCard.definition.castRange + GetWarriorFirstAttackRangeBonus(_draggedCard))
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
                if (_draggedCard.definition.cardType == HexCardType.Attack &&
                    _draggedCard.definition.effectRadius <= 0 &&
                    TryGetHoveredTile(out var hoveredRuinTile, out _) &&
                    CanAttackRuinTile(_playerUnit, _draggedCard.definition, hoveredRuinTile))
                {
                    Debug.Log(
                        $"[RuinAttack] PlayCard branch card={_draggedCard.definition.id} coord=({hoveredRuinTile.coord.q},{hoveredRuinTile.coord.r}) " +
                        $"hasRuin={TileHasRuin(hoveredRuinTile)} propId={hoveredRuinTile.propId} hp={TileStructureHp(hoveredRuinTile)}");

                    if (!SubmitAuthoritativeCommand(HexNetworkCommandType.PlayCard, ToPayload(_draggedCard, hoveredRuinTile.coord)))
                        return true;

                    StartCoroutine(ResolveCardRoutine(_playerUnit, _playerUnit, _draggedCard, hoveredRuinTile.coord));
                    return true;
                }

                if (_draggedCard.definition.effectRadius <= 0)
                    return false;

                if (!TryGetHoveredTile(out var hoveredTile, out _))
                    return false;

                if (HexAxialCoord.Distance(_playerUnit.State.coord, hoveredTile.coord) > _draggedCard.definition.castRange + GetWarriorFirstAttackRangeBonus(_draggedCard))
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

            if (GetUnitDistance(_playerUnit, targetUnit) > _draggedCard.definition.castRange + GetWarriorFirstAttackRangeBonus(_draggedCard))
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
            bool exhaustCard = KeywordTriggerEngine.ShouldExhaustOnPlay(source, card);
            source.Deck.DiscardFromHand(card, exhaustCard);
            // Defer exhaust-event mark until after resolve so cards like 麻木/助燃 need a prior exhaust.
            card.ResetActionFlags();
            RecordCardPlay(source, target, card, targetedCoord);
            source.NotifyCardPlayed();
            if (source == _playerUnit &&
                (card.definition.cardType == HexCardType.Attack || card.definition.cardType == HexCardType.Skill))
                _lastPlayerMirrorCard = card.definition;
            if (source == _playerUnit)
                ExhaustHandCardsTriggeredByPlay(source, card);
            _ui?.ShowPlayedCard(source, card);
            if (source.State.bleed > 0)
            {
                ApplyDamageWithFeedback(
                    source,
                    source.State.bleed,
                    source,
                    HexDamageTags.Status | HexDamageTags.SelfDamage);
                source.RefreshLabel();
                _ui.Refresh();
                if (!source.IsAlive)
                {
                    yield return ResolveDeathsAndBattleEndRoutine();
                    yield break;
                }
            }

            if (source.State.armorOnAttackCardThisTurn > 0 && card.definition.cardType == HexCardType.Attack)
                GainArmorWithFeedback(source, source.State.armorOnAttackCardThisTurn);
            if (source.State.armorOnSkillCard > 0 && card.definition.cardType == HexCardType.Skill)
                GainArmorWithFeedback(source, source.State.armorOnSkillCard);

            ApplyDruidTransformFromCard(source, card.definition);

            bool isRuinDirectAttack = IsRuinDirectAttackPlay(source, target, card, directionalCoord, targetedCoord);
            Debug.Log(
                $"[RuinAttack] ResolveCardRoutine card={card.definition.id} source={GetUnitDisplayName(source)} " +
                $"target={GetUnitDisplayName(target)} targeted=({targetedCoord.q},{targetedCoord.r}) isRuinDirect={isRuinDirectAttack}");

            if (isRuinDirectAttack)
            {
                Debug.Log("[RuinAttack] Short-circuit before CustomCardRoutine → ResolveRuinTargetAttackRoutine");
                yield return ResolveRuinTargetAttackRoutine(source, targetedCoord, card);
                if (exhaustCard)
                    NotifyWarriorExhaust(source);
                if (grid.TryGetTile(targetedCoord, out var ruinTile))
                    ruinTile.FlashClick();
                source.RefreshLabel();
                _ui.Refresh();
                _busy = false;
                TryProcessPendingEndTurn();
                yield break;
            }

            bool handledByCustomLogic = false;
            BeginAttackPassiveContext(source, card);
            yield return ResolveCustomCardRoutine(source, target, card, energyCost, targetedCoord, handled => handledByCustomLogic = handled);
            if (handledByCustomLogic)
            {
                EndAttackPassiveContext();
                if (_battleFinished)
                    yield break;
                if (exhaustCard)
                    NotifyWarriorExhaust(source);
                yield return ApplyWarriorFirstAttackCardEffects(source, target, card);
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

            bool resolvedRuinTargetAttack = false;
            switch (card.definition.effectType)
            {
                case HexCardEffectType.Attack:
                    if (card.definition.targetType == HexCardTargetType.EnemyUnit &&
                        directionalCoord.HasValue &&
                        target == source &&
                        ResolveRuinAttackTarget(source, card.definition, directionalCoord.Value) &&
                        grid.TryGetTile(directionalCoord.Value, out var ruinFallbackTile) &&
                        TileHasRuin(ruinFallbackTile))
                    {
                        Debug.Log("[RuinAttack] Fallback Attack branch → ResolveRuinTargetAttackRoutine");
                        resolvedRuinTargetAttack = true;
                        yield return ResolveRuinTargetAttackRoutine(source, directionalCoord.Value, card);
                        break;
                    }

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
                        ApplyAttackDamage(
                            source,
                            target,
                            card.EffectiveAmount + Mathf.Max(0, source.State.strength));
                        if (target.IsAlive)
                        {
                            target.PlayHitAnimation();
                            yield return new WaitForSeconds(Mathf.Max(0.08f, target.GetHitDuration() * 0.85f));
                            yield return ApplyWeaponAttackEffectsRoutine(source, target);
                            if (target.IsAlive && source.IsAlive)
                                yield return ApplyKeywordEffectsRoutine(source, target, card);
                            if (target.IsAlive && target.State.thorns > 0 && source.IsAlive)
                                ApplyDamageToUnit(source, target.State.thorns, target, HexDamageTags.Reaction);
                        }
                        yield return ResolveDeathsAndBattleEndRoutine();
                        if (_battleFinished)
                            yield break;
                    }
                    break;
                case HexCardEffectType.Defend:
                    if (CanConvertArmorCardToPlantHealing(source, target, card.definition))
                        target.Heal(card.EffectiveAmount);
                    else
                        GainArmorWithFeedback(source, card.EffectiveAmount);
                    break;
                case HexCardEffectType.Move:
                    yield return ResolveCardMoveRoutine(source, targetedCoord, Mathf.Max(1, card.EffectiveAmount));
                    break;
                case HexCardEffectType.MoveAway:
                    yield return ResolveRetreatRoutine(source, target, Mathf.Max(1, card.EffectiveAmount));
                    break;
                case HexCardEffectType.DestroyBarrier:
                    DestroyBarrierAt(targetedCoord);
                    break;
                case HexCardEffectType.PlaceRuin:
                    PlaceRuinNear(source, Mathf.Max(1, card.definition.castRange), Mathf.Max(1, card.EffectiveAmount));
                    break;
            }

            ApplySelfKeywordEffects(source, card);
            EndAttackPassiveContext();

            if (resolvedRuinTargetAttack)
            {
                if (grid.TryGetTile(targetedCoord, out var ruinTile))
                    ruinTile.FlashClick();
                source.RefreshLabel();
                _ui.Refresh();
            }
            else if (card.definition.targetType == HexCardTargetType.Direction && directionalCoord.HasValue)
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

            if (exhaustCard)
                NotifyWarriorExhaust(source);

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
                int totalHealthLost = 0;
                int totalThornsDamage = 0;
                HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
                int baseDamage = card.EffectiveAmount + Mathf.Max(0, source.State.strength);
                for (int i = 0; i < targets.Count; i++)
                {
                    var areaTarget = targets[i];
                    if (areaTarget == null || !areaTarget.IsAlive || !source.IsAlive)
                        continue;

                    areaTarget.FaceTarget(source.transform.position);
                    HexDamageResult damageResult = ApplyAttackDamage(source, areaTarget, baseDamage, snapshot);
                    totalHealthLost += damageResult.healthLost;
                    bool survivedHit = areaTarget.IsAlive;
                    if (survivedHit)
                    {
                        areaTarget.PlayHitAnimation();
                        longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetHitDuration() * 0.85f);
                        totalThornsDamage += Mathf.Max(0, areaTarget.State.thorns);
                    }
                    else
                    {
                        longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetDeathDuration());
                    }

                    if (areaTarget.IsAlive && source.State.firstAttackBonusPending && source.State.firstAttackBurnAmount > 0)
                    {
                        areaTarget.ApplyBurn(source.State.firstAttackBurnAmount);
                        source.State.firstAttackBonusPending = false;
                    }

                    if (areaTarget.IsAlive)
                    {
                        yield return ApplyWeaponAttackEffectsRoutine(source, areaTarget);
                        yield return ApplyKeywordEffectsRoutine(source, areaTarget, card);
                    }
                }

                CompleteAttackDamageBatch(source, totalHealthLost);
                if (totalThornsDamage > 0 && source.IsAlive)
                    ApplyDamageToUnit(source, totalThornsDamage, null, HexDamageTags.Reaction);

                DamageRuinsInCoords(
                    GetDirectionalAreaCoords(source.State.coord, aimedCoord, card.definition.castRange, card.definition.effectRadius),
                    HexDamageResolver.PreviewModifiedDamage(snapshot, null, baseDamage),
                    targets);

                yield return new WaitForSeconds(Mathf.Max(0.08f, longestImpactDuration));
                yield return ResolveDeathsAndBattleEndRoutine();
                if (_battleFinished || !source.IsAlive)
                    yield break;
            }
        }

        private IEnumerator ResolveTileAttackRoutine(HexBattleUnit source, HexAxialCoord centerCoord, HexCardInstance card)
        {
            var targets = GetEnemiesInArea(centerCoord, card.definition.effectRadius, source);
            var affectedCoords = HexBattlePathing.GetCoordsInRange(centerCoord, Mathf.Max(0, card.definition.effectRadius)).ToList();
            if (targets.Count == 0 && !HasRuinInCoords(affectedCoords))
                yield break;

            source.FaceTarget(grid != null ? grid.AxialToWorld(centerCoord) : source.transform.position + source.transform.forward);
            source.PlayAttackAnimation();
            yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));

            float longestImpactDuration = 0.08f;
            int totalHealthLost = 0;
            int totalThornsDamage = 0;
            HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
            int baseDamage = card.EffectiveAmount + Mathf.Max(0, source.State.strength);
            for (int i = 0; i < targets.Count; i++)
            {
                var areaTarget = targets[i];
                if (areaTarget == null || !areaTarget.IsAlive || !source.IsAlive)
                    continue;
                if (!CanAttackTarget(source, areaTarget))
                    continue;

                areaTarget.FaceTarget(source.transform.position);
                HexDamageResult damageResult = ApplyAttackDamage(source, areaTarget, baseDamage, snapshot);
                totalHealthLost += damageResult.healthLost;
                if (areaTarget.IsAlive)
                {
                    areaTarget.PlayHitAnimation();
                    longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetHitDuration() * 0.85f);
                    totalThornsDamage += Mathf.Max(0, areaTarget.State.thorns);
                }
                else
                {
                    longestImpactDuration = Mathf.Max(longestImpactDuration, areaTarget.GetDeathDuration());
                }

                if (areaTarget.IsAlive)
                    yield return ApplyKeywordEffectsRoutine(source, areaTarget, card);
            }

            CompleteAttackDamageBatch(source, totalHealthLost);
            if (totalThornsDamage > 0 && source.IsAlive)
                ApplyDamageToUnit(source, totalThornsDamage, null, HexDamageTags.Reaction);

            DamageRuinsInCoords(
                affectedCoords,
                HexDamageResolver.PreviewModifiedDamage(snapshot, null, baseDamage),
                targets);

            yield return new WaitForSeconds(Mathf.Max(0.08f, longestImpactDuration));
            yield return ResolveDeathsAndBattleEndRoutine();
        }

        private IEnumerator ResolveRuinTargetAttackRoutine(HexBattleUnit source, HexAxialCoord coord, HexCardInstance card)
        {
            if (source == null || card?.definition == null || grid == null)
            {
                Debug.LogWarning("[RuinAttack] ResolveRuinTargetAttackRoutine aborted: null source/card/grid");
                yield break;
            }
            if (!grid.TryGetTile(coord, out var tile) || tile == null || !TileHasRuin(tile))
            {
                Debug.LogWarning(
                    $"[RuinAttack] ResolveRuinTargetAttackRoutine aborted: no ruin at ({coord.q},{coord.r}) " +
                    $"tileNull={tile == null} hasRuin={(tile != null && TileHasRuin(tile))}");
                yield break;
            }

            source.FaceTarget(grid.AxialToWorld(coord));
            source.PlayAttackAnimation();
            yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));

            HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
            int damage = HexDamageResolver.PreviewModifiedDamage(
                snapshot,
                null,
                card.EffectiveAmount + Mathf.Max(0, source.State.strength));
            int hpBefore = TileStructureHp(tile);
            string attackedPropId = tile.propId;
            bool applied = tile.DamageStructure(damage, out bool destroyed);
            int hpAfter = TileStructureHp(tile);
            Debug.Log(
                $"[RuinAttack] DamageStructure card={card.definition.id} coord=({coord.q},{coord.r}) " +
                $"propId={tile.propId} damage={damage} applied={applied} destroyed={destroyed} hp={hpBefore}->{hpAfter}");
            if (!applied)
                Debug.LogWarning($"[RuinAttack] DamageStructure returned false at ({coord.q},{coord.r})");

            tile.FlashClick();
            if (destroyed)
                Debug.Log($"[RuinAttack] Ruin at {coord.q},{coord.r} destroyed by direct attack.");
            if (applied && attackedPropId == "consumable_iron_ball")
                yield return ResolveIronBallHit(source, coord);
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
                if (target == null || !target.IsAlive)
                    yield break;

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

        private void BeginAttackPassiveContext(HexBattleUnit source, HexCardInstance card)
        {
            if (source?.State == null || card?.definition?.cardType != HexCardType.Attack)
            {
                _activeAttackPassiveSource = null;
                _activeAttackPassiveCard = null;
                return;
            }

            _activeAttackPassiveSource = source;
            _activeAttackPassiveCard = card;
        }

        private void EndAttackPassiveContext()
        {
            _activeAttackPassiveSource = null;
            _activeAttackPassiveCard = null;
        }

        public HexBattleUnitState GetLocalPlayerState() => _playerUnit?.State;

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
            HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
            int totalHealthLost = 0;
            int totalThornsDamage = 0;
            for (int i = 0; i < line.Count; i++)
            {
                var unit = FindUnitAtCoord(line[i], source);
                if (unit == null || unit.State.faction == source.State.faction || !unit.IsAlive)
                    continue;

                HexDamageResult result = ApplyAttackDamage(source, unit, 3, snapshot);
                totalHealthLost += result.healthLost;
                if (unit.IsAlive)
                {
                    unit.PlayHitAnimation();
                    totalThornsDamage += Mathf.Max(0, unit.State.thorns);
                }
            }

            CompleteAttackDamageBatch(source, totalHealthLost);
            if (totalThornsDamage > 0 && source.IsAlive)
                ApplyDamageToUnit(source, totalThornsDamage, null, HexDamageTags.Reaction);
            yield return ResolveDeathsAndBattleEndRoutine();
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

        private IEnumerator MoveUnitRoutine(
            HexBattleUnit unit,
            List<HexAxialCoord> path,
            int moveCost,
            HexAxialCoord? towardTargetCoord = null)
        {
            if (IsLivingWallMovementPathBlocked(path, unit))
                yield break;

            _busy = true;
            int movedDistance = path != null ? Mathf.Max(0, path.Count - 1) : Mathf.Max(0, moveCost);
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
            if (!unit.IsAlive)
            {
                yield return ResolveDeathsAndBattleEndRoutine();
                yield break;
            }
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
            ApplyWarriorEndTurnPassives(GetCurrentUnit());
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
                ExpireTemporaryObstacles();
                PrepareEnemyIntents();
                _playerUnit.BeginTurn();
                if (_playerUnit.IsAlive)
                    ResolveConsumableTurnStart(_playerUnit);
                if (_playerUnit.IsAlive)
                    ApplyWarriorBeginTurnPassives(_playerUnit);
                if (_playerUnit.IsAlive)
                    ApplyDruidBeginTurnPassives(_playerUnit);
                if (_playerUnit.IsAlive)
                    ApplyBurningAura(_playerUnit);
            }
            else
            {
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    if (_enemyUnits[i] != null && _enemyUnits[i].IsAlive)
                    {
                        _enemyUnits[i].BeginTurn();
                        if (_enemyUnits[i].IsAlive)
                            ResolveConsumableTurnStart(_enemyUnits[i]);
                        if (_enemyUnits[i].IsAlive)
                            ApplyDruidBeginTurnPassives(_enemyUnits[i]);
                        if (_enemyUnits[i].IsAlive)
                            ApplyBurningAura(_enemyUnits[i]);
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
                    yield return ResolveDeathsAndBattleEndRoutine();
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
                    yield return ResolveDeathsAndBattleEndRoutine();
            }

            if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
            {
                yield return HandleBattleEnd(true);
                yield break;
            }

            yield return ResolveLivingWallTurnStarts();
            yield return ResolveMindTentaclePhase();

            UpdateMovementHighlights();
            _ui.Refresh();
            StartCoroutine(RunEnemyTurn());
        }

        private IEnumerator ResolveMindTentaclePhase()
        {
            if (_playerUnit == null || !_playerUnit.IsAlive)
                yield break;
            int successfulPulls = 0;
            var tentacles = _enemyUnits
                .Where(unit => unit != null && unit.IsAlive && unit.State.enemyDefinitionId == "mind_tentacle")
                .OrderBy(unit => unit.State.id, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < tentacles.Count && successfulPulls < 3; i++)
            {
                var pull = ResolveForcedMovement(tentacles[i], _playerUnit, 1, true);
                if (pull == null || pull.path.Count <= 1)
                    continue;
                yield return MoveUnitRoutine(_playerUnit, pull.path, 0);
                successfulPulls++;
            }
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

                if (!_enemyIntentSlots.TryGetValue(enemy, out var currentSlots) || currentSlots == null || currentSlots.Count == 0)
                    DrawEnemyIntentCards(enemy);

                var intentSlots = GetEnemyIntentExecutionOrder(enemy);
                for (int cardIndex = 0; cardIndex < intentSlots.Count; cardIndex++)
                {
                    var card = intentSlots[cardIndex]?.card;
                    if (card == null || !enemy.Deck.Hand.Contains(card))
                        continue;

                    yield return ResolveEnemyIntentCard(enemy, card);
                    yield return ResolveDeathsAndBattleEndRoutine();
                    if (_battleFinished || _playerUnit == null || !_playerUnit.IsAlive)
                        yield break;
                    if (!enemy.IsAlive)
                        break;

                    yield return new WaitForSeconds(0.1f);
                }

                _enemyIntentSlots.Remove(enemy);
                if (!enemy.IsAlive)
                    continue;
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

                TryApplyEnemyPhaseTwo(enemy);
                enemy.Deck.DiscardHand();
                DrawEnemyIntentCards(enemy);
                enemy.RefreshLabel();
            }
        }

        private void DrawEnemyIntentCards(HexBattleUnit enemy)
        {
            if (enemy == null)
                return;

            EnsureEnemyDefinition(enemy);
            var definition = HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId);
            if (definition == null)
            {
                Debug.LogError($"Cannot draw intents for unknown enemy: {enemy.State.enemyDefinitionId}");
                _enemyIntentSlots.Remove(enemy);
                return;
            }
            if (enemy.State.livingWall?.reformPending == true)
            {
                enemy.Deck.DiscardHand();
                enemy.SetLivingWallIntentPreview(null, false);
                _enemyIntentSlots[enemy] = new List<HexEnemyIntentSlot>
                {
                    new() { slotKind = HexEnemyIntentSlotKind.Free, card = null },
                };
                return;
            }
            var slots = new List<HexEnemyIntentSlot>();
            enemy.Deck.DiscardHand();
            enemy.State.enemyHiddenIntentSlotIndex = -1;

            for (int i = 0; i < definition.intentSlots.Count; i++)
            {
                var slotKind = definition.intentSlots[i];
                HexCardInstance card = null;
                bool emptiedDrawPile = false;
                for (int attempts = 0; attempts < 8; attempts++)
                {
                    card = DrawCardForIntentSlot(enemy, slotKind, out emptiedDrawPile);
                    if (card == null || !IsFearToken(card))
                        break;

                    OnEnemyFearTokenDrawn(enemy);
                    DiscardOrExhaustCard(enemy, card, false);
                    card = null;
                }

                if (card == null)
                    break;

                slots.Add(new HexEnemyIntentSlot
                {
                    slotKind = slotKind,
                    card = card,
                });

                if (emptiedDrawPile)
                    TriggerEnemyDrawPileEmptiedEffect(enemy, definition);
            }

            _enemyIntentSlots[enemy] = slots;
            if (slots.Any(slot => slot?.card?.definition?.id == "enemy_mind_flayer_obscure") && slots.Count > 1)
                enemy.State.enemyHiddenIntentSlotIndex = Random.Range(0, slots.Count);
            if (enemy.IsLivingWall)
                UpdateLivingWallIntentPreview(enemy, slots.FirstOrDefault(slot => slot?.card != null)?.card);
        }

        private void TryApplyEnemyPhaseTwo(HexBattleUnit enemy)
        {
            if (enemy?.State == null || enemy.State.enemyPhaseTwoApplied || enemy.State.maxHealth <= 0)
                return;

            var definition = HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId);
            if (definition == null || definition.phaseTwoHealthRatio <= 0f || definition.phaseTwoDeckDefinitions == null || definition.phaseTwoDeckDefinitions.Count == 0)
                return;
            if ((float)enemy.State.currentHealth / enemy.State.maxHealth > definition.phaseTwoHealthRatio)
                return;

            enemy.State.enemyPhaseTwoApplied = true;
            enemy.PrepareDeckForBattle(definition.phaseTwoDeckDefinitions);
            Debug.Log($"{enemy.State.displayName} entered phase 2.");
        }

        private void ApplyWarriorBeginTurnPassives(HexBattleUnit unit)
        {
            if (unit == null || unit.State == null || unit.State.profession != HexCardProfession.Warrior)
                return;

            if (unit.State.warriorStrengthPerTurn > 0)
                unit.GainStrength(unit.State.warriorStrengthPerTurn);

            if (unit.State.warriorPreparedBlade)
                ApplyWarriorFocusBonus(unit, 1);

            if (unit.State.warriorInfernoHeart)
            {
                for (int i = 0; i < _enemyUnits.Count; i++)
                {
                    var enemy = _enemyUnits[i];
                    if (enemy == null || !enemy.IsAlive || enemy.State.burn <= 0)
                        continue;
                    if (GetUnitDistance(unit, enemy) <= 1)
                        ApplyWarriorBurn(unit, enemy, 1);
                }
            }

            var quickStep = HexCardLibrary.GetCardById("warrior_quick_step");
            if (quickStep != null && !unit.Deck.Hand.Any(card => card?.definition?.id == quickStep.id))
                unit.Deck.AddToHand(quickStep);
        }

        private void ApplyWarriorEndTurnPassives(HexBattleUnit unit)
        {
            if (unit == null || unit.State == null || unit.State.profession != HexCardProfession.Warrior)
                return;

            if (!unit.State.warriorScorchedEarthActive || unit.Deck.Hand.Count <= 0)
                return;

            ExhaustRandomHandCard(unit);
            unit.State.warriorPendingEnergyNextTurn += 1;
        }

        private HexCardInstance DrawCardForIntentSlot(HexBattleUnit enemy, HexEnemyIntentSlotKind slotKind, out bool emptiedDrawPile)
        {
            emptiedDrawPile = false;
            if (enemy == null)
                return null;

            if (string.Equals(enemy.State?.enemyDefinitionId, HexEncounterGenerator.OrcWarriorId, System.StringComparison.Ordinal))
                return DrawOrcIntentCard(enemy, slotKind, out emptiedDrawPile);

            System.Predicate<HexCardDefinition> predicate = slotKind switch
            {
                HexEnemyIntentSlotKind.Move => IsEnemyMoveCard,
                HexEnemyIntentSlotKind.Attack => definition => definition != null && definition.cardType == HexCardType.Attack,
                _ => null,
            };

            System.Predicate<HexCardDefinition> allowedByCadence = definition =>
                definition != null &&
                (definition.id != "enemy_goblin_captain_warcry" || enemy.State.enemyTurnIndex - enemy.State.enemyLastWarcryTurn > 1);
            if (predicate == null)
                predicate = allowedByCadence;
            else
            {
                var slotPredicate = predicate;
                predicate = definition => slotPredicate(definition) && allowedByCadence(definition);
            }

            if (predicate != null)
            {
                var matched = enemy.Deck.DrawFirstMatchingToHand(predicate, out emptiedDrawPile);
                if (matched != null)
                    return matched;
            }

            return enemy.Deck.DrawRandomToHand(out emptiedDrawPile);
        }

        private HexCardInstance DrawOrcIntentCard(
            HexBattleUnit enemy,
            HexEnemyIntentSlotKind slotKind,
            out bool emptiedDrawPile)
        {
            emptiedDrawPile = false;
            HexCardInstance card;
            if (slotKind == HexEnemyIntentSlotKind.Move)
            {
                card = enemy.Deck.DrawFirstMatchingToHand(definition => definition?.id == "enemy_orc_approach", out emptiedDrawPile);
                if (card != null)
                    return card;
                card = enemy.Deck.DrawFirstMatchingToHand(definition => definition?.id == "enemy_orc_stance", out emptiedDrawPile);
                if (card != null)
                    return card;
                return enemy.Deck.DrawFirstMatchingToHand(definition => definition?.cardType != HexCardType.Attack, out emptiedDrawPile)
                       ?? enemy.Deck.DrawRandomToHand(out emptiedDrawPile);
            }

            if (slotKind == HexEnemyIntentSlotKind.Attack)
            {
                var target = GetPrimaryEnemyTarget(enemy);
                bool canCharge = target != null && HexOrcWarriorRules.TryBuildChargePath(
                    enemy.State.coord,
                    target.State.coord,
                    coord => IsChargeObstacle(coord, enemy) || IsOccupied(coord, enemy),
                    out _,
                    out _);
                bool adjacent = target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1;
                string preferredId = canCharge || !adjacent ? "enemy_orc_charge" : "enemy_orc_heavy_slash";
                card = enemy.Deck.DrawFirstMatchingToHand(definition => definition?.id == preferredId, out emptiedDrawPile);
                if (card != null)
                    return card;
                return enemy.Deck.DrawFirstMatchingToHand(definition => definition?.cardType == HexCardType.Attack, out emptiedDrawPile)
                       ?? enemy.Deck.DrawRandomToHand(out emptiedDrawPile);
            }

            return enemy.Deck.DrawRandomToHand(out emptiedDrawPile);
        }

        private List<HexEnemyIntentSlot> GetEnemyIntentExecutionOrder(HexBattleUnit enemy)
        {
            if (enemy == null || !_enemyIntentSlots.TryGetValue(enemy, out var slots) || slots == null)
                return new List<HexEnemyIntentSlot>();

            var definition = HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId);
            if (definition == null)
                return slots.ToList();
            var target = GetPrimaryEnemyTarget(enemy);
            bool targetInRange = target != null && IsInEnemyAttackRange(enemy, target, definition);
            if (definition.intentPattern == HexEnemyIntentPattern.Ranged)
            {
                return targetInRange
                    ? slots.OrderBy(slot => slot.slotKind == HexEnemyIntentSlotKind.Attack ? 0 : slot.slotKind == HexEnemyIntentSlotKind.Move ? 1 : 2).ToList()
                    : slots.OrderBy(slot => slot.slotKind == HexEnemyIntentSlotKind.Move ? 0 : slot.slotKind == HexEnemyIntentSlotKind.Attack ? 1 : 2).ToList();
            }

            if (definition.intentPattern == HexEnemyIntentPattern.ApproachStrike)
            {
                return targetInRange
                    ? slots.OrderBy(slot => slot.slotKind == HexEnemyIntentSlotKind.Attack ? 0 : slot.slotKind == HexEnemyIntentSlotKind.Move ? 1 : 2).ToList()
                    : slots.OrderBy(slot => slot.slotKind == HexEnemyIntentSlotKind.Move ? 0 : slot.slotKind == HexEnemyIntentSlotKind.Attack ? 1 : 2).ToList();
            }

            return slots.ToList();
        }

        private void TriggerEnemyDrawPileEmptiedEffect(HexBattleUnit enemy, HexEnemyDefinition definition = null)
        {
            if (enemy == null || enemy.State == null || !enemy.IsAlive)
                return;

            definition ??= HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId);
            if (definition?.bottomCard != null)
            {
                string bottomId = definition.bottomCard.id;
                if (bottomId == "enemy_skeleton_bottom")
                {
                    enemy.State.currentHealth = Mathf.Max(1, enemy.State.currentHealth - 4);
                    enemy.State.pendingStrengthNextTurn += 2;
                    enemy.RefreshLabel();
                    return;
                }
                if (bottomId == "enemy_orc_bottom")
                {
                    enemy.State.orcChargeEmpowered = true;
                    enemy.RefreshLabel();
                    Debug.Log($"{enemy.State.displayName} triggered bottom card {definition.bottomCard.displayName}.");
                    return;
                }
                if (bottomId == "enemy_vine_bottom")
                {
                    GetPrimaryEnemyTarget(enemy)?.ApplyBind(2);
                    return;
                }
                if (bottomId == "enemy_wall_bottom")
                {
                    TrySummonLivingWallOffspring(enemy);
                    return;
                }
                if (bottomId == "enemy_gargoyle_bottom")
                {
                    if (!TryPlaceBarrierNear(enemy, 1)) enemy.GainStrength(2);
                    return;
                }
                if (bottomId == "enemy_hellhound_bottom")
                {
                    var target = GetPrimaryEnemyTarget(enemy);
                    if (target != null)
                    {
                        ApplyAttackDamage(enemy, target, 5 + Mathf.Max(0, enemy.State.strength));
                        if (target.IsAlive)
                        {
                            target.ApplyBurn(2);
                            if (target.State.thorns > 0 && enemy.IsAlive)
                                ApplyDamageToUnit(enemy, target.State.thorns, target, HexDamageTags.Reaction);
                        }
                        StartCoroutine(ResolveDeathsAndBattleEndRoutine());
                    }
                    enemy.GainStrength(1);
                    return;
                }
                if (bottomId == "enemy_mimic_bottom")
                {
                    if (!PlaceRuinNear(enemy, 1, 4))
                    {
                        var target = GetPrimaryEnemyTarget(enemy);
                        if (target != null)
                        {
                            ApplyAttackDamage(enemy, target, 6);
                            if (target.IsAlive && target.State.thorns > 0 && enemy.IsAlive)
                                ApplyDamageToUnit(enemy, target.State.thorns, target, HexDamageTags.Reaction);
                            StartCoroutine(ResolveDeathsAndBattleEndRoutine());
                        }
                    }
                    return;
                }
                if (bottomId == "enemy_mind_flayer_bottom")
                {
                    var target = GetPrimaryEnemyTarget(enemy);
                    if (target != null)
                        for (int i = 0; i < 2; i++) target.Deck.AddToDiscardPile(HexCardLibrary.GetDaze());
                    GainArmorWithFeedback(enemy, 5);
                    return;
                }
                switch (definition.bottomCard.effectType)
                {
                    case HexCardEffectType.PlaceRuin:
                        if (PlaceRuinNear(enemy, Mathf.Max(1, definition.bottomCard.castRange), Mathf.Max(1, definition.bottomCard.amount)))
                        {
                            enemy.RefreshLabel();
                            Debug.Log($"{enemy.State.displayName} triggered bottom card {definition.bottomCard.displayName}.");
                            return;
                        }
                        break;
                }

                if (definition.bottomCard.id == "enemy_goblin_captain_bottom" && TrySummonGoblinMinion(enemy))
                {
                    enemy.RefreshLabel();
                    Debug.Log($"{enemy.State.displayName} triggered bottom card {definition.bottomCard.displayName}.");
                    return;
                }
            }

            int strengthGain = Mathf.Max(0, definition?.emptyDrawPileStrengthGain ?? enemy.State.emptyDrawPileStrengthGain);
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

            if (_enemySpecialHandlers.TryGetValue(card.definition.id, out var specialHandler))
            {
                yield return specialHandler(enemy, primaryTarget, card);
                resolved = true;
            }
            else if (card.definition.id == "enemy_chieftain_charge")
            {
                if (primaryTarget != null && primaryTarget.IsAlive)
                {
                    int maxSteps = Mathf.Max(1, card.EffectiveAmount);
                    var path = FindBestApproachPath(enemy, primaryTarget.State.coord, 1);
                    if (path != null && path.Count >= 2)
                    {
                        int takeCount = Mathf.Min(path.Count, maxSteps + 1);
                        var trimmed = path.Take(takeCount).ToList();
                        yield return MoveUnitRoutine(enemy, trimmed, 0, primaryTarget.State.coord);
                        if (HexAxialCoord.Distance(enemy.State.coord, primaryTarget.State.coord) <= 1)
                            yield return ResolveDirectAttackRoutine(enemy, primaryTarget, 6);
                        resolved = true;
                    }
                }
            }
            else if (IsEnemyMoveCard(card.definition))
            {
                if (primaryTarget != null && primaryTarget.IsAlive)
                {
                    yield return ResolveEnemyIdealRangeMoveRoutine(enemy, primaryTarget, Mathf.Max(1, card.EffectiveAmount));
                    if (card.definition.id == "enemy_goblin_roll")
                        GainArmorWithFeedback(enemy, 5);
                    resolved = true;
                }
            }
            else if (card.definition.effectType == HexCardEffectType.Attack)
            {
                if (card.definition.id == "enemy_chieftain_quake")
                {
                    yield return ResolveChieftainQuakeRoutine(enemy, card);
                    resolved = true;
                }
                else if (primaryTarget != null &&
                    primaryTarget.IsAlive &&
                    IsInEnemyAttackRange(enemy, primaryTarget, HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId), card.definition) &&
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
            else if (card.definition.id == "enemy_goblin_captain_net")
            {
                if (primaryTarget != null && primaryTarget.IsAlive && HexAxialCoord.Distance(enemy.State.coord, primaryTarget.State.coord) <= card.definition.castRange)
                    primaryTarget.ApplyBind(Mathf.Max(1, card.EffectiveAmount));
                resolved = true;
            }
            else if (card.definition.id == "enemy_goblin_captain_warcry")
            {
                enemy.GainStrength(Mathf.Max(1, card.EffectiveAmount));
                TrySummonGoblinMinion(enemy);
                resolved = true;
            }
            else if (card.definition.id == "enemy_chieftain_brace")
            {
                GainArmorWithFeedback(enemy, Mathf.Max(1, card.EffectiveAmount) * 3);
                resolved = true;
            }
            else if (card.definition.id == "enemy_chieftain_drum")
            {
                enemy.GainStrength(Mathf.Max(1, card.EffectiveAmount));
                resolved = true;
            }

            if (!resolved && enemy.Deck.Hand.Contains(card))
            {
                DiscardOrExhaustCard(enemy, card, false);
                enemy.RefreshLabel();
                _ui.Refresh();
            }
            else if (resolved && enemy.Deck.Hand.Contains(card))
            {
                DiscardOrExhaustCard(enemy, card, false);
                enemy.RefreshLabel();
                _ui.Refresh();
            }
        }

        private IEnumerator ResolveRegisteredEnemySpecialCard(HexBattleUnit enemy, HexBattleUnit target, HexCardInstance card)
        {
            string id = card.definition.id;
            switch (id)
            {
                case "enemy_goblin_roll":
                    if (target != null)
                        yield return ResolveEnemyIdealRangeMoveRoutine(enemy, target, 1);
                    GainArmorWithFeedback(enemy, 5);
                    break;
                case "enemy_spear_goblin_cover_retreat":
                    if (target != null)
                        yield return ResolveRetreatRoutine(enemy, target, 1);
                    GainArmorWithFeedback(enemy, HasAdjacentStructure(enemy, HexTerrainStructureType.Ruin) ? 7 : 4);
                    break;
                case "enemy_spear_goblin_volley":
                    if (target != null && IsInEnemyAttackRange(enemy, target, HexCardLibrary.GetEnemyDefinition(enemy.State.enemyDefinitionId), card.definition))
                        yield return ResolveDirectAttackRoutine(enemy, target, 3);
                    break;
                case "enemy_goblin_captain_net":
                case "enemy_gargoyle_gaze":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= Mathf.Max(1, card.definition.castRange))
                        target.ApplyBind(1);
                    break;
                case "enemy_goblin_captain_warcry":
                    enemy.GainStrength(2);
                    if (enemy.State.enemyTurnIndex - enemy.State.enemyLastWarcryTurn > 1)
                    {
                        TrySummonGoblinMinion(enemy);
                        enemy.State.enemyLastWarcryTurn = enemy.State.enemyTurnIndex;
                    }
                    break;
                case "enemy_goblin_captain_rally":
                    if (!TrySummonGoblinMinion(enemy))
                        enemy.GainStrength(1);
                    break;
                case "enemy_goblin_captain_shield_wall":
                    GainArmorWithFeedback(enemy, 12);
                    enemy.State.cannotBeKnockedBackThisTurn = true;
                    break;
                case "enemy_chieftain_charge":
                    yield return ResolveEnemyChargeRoutine(enemy, target, 1, 6, true);
                    break;
                case "enemy_orc_charge":
                    yield return ResolveOrcChargeRoutine(enemy, target);
                    break;
                case "enemy_chieftain_quake":
                    yield return ResolveChieftainQuakeRoutine(enemy, card);
                    break;
                case "enemy_chieftain_brace":
                    enemy.GainToughness(2);
                    break;
                case "enemy_chieftain_drum":
                    enemy.GainStrength(2);
                    break;
                case "enemy_vine_entangle":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                    {
                        yield return ResolveDirectAttackRoutine(enemy, target, 3);
                        if (target.IsAlive) target.ApplyBind(1);
                    }
                    break;
                case "enemy_vine_snare":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 2)
                    {
                        bool alreadyBound = target.State.bind > 0;
                        target.ApplyBind(1);
                        if (alreadyBound)
                        {
                            var pull = ResolveForcedMovement(enemy, target, 1, true);
                            if (pull != null && pull.path.Count > 1)
                                yield return MoveUnitRoutine(target, pull.path, 0);
                        }
                    }
                    break;
                case "enemy_vine_spread":
                    enemy.State.enemySpreadActiveThisTurn = true;
                    break;
                case "enemy_vine_spore_sac":
                    PlaceRuinNear(enemy, 1, 3);
                    break;
                case "enemy_wall_root_stab":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                        yield return ResolveDirectAttackRoutine(enemy, target, 4 + (target.State.bind > 0 ? 3 : 0));
                    break;
                case "enemy_wall_crush":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                        yield return ResolveDirectAttackRoutine(enemy, target, 3, knockback: 1);
                    break;
                case "enemy_wall_grow":
                case "enemy_gargoyle_rockfall":
                    TryPlaceBarrierNear(enemy, 1);
                    break;
                case "enemy_wall_regenerate":
                    enemy.Heal(3);
                    break;
                case "enemy_gargoyle_dive":
                    yield return ResolveEnemyChargeRoutine(enemy, target, 2, 8, false);
                    break;
                case "enemy_gargoyle_stone_skin":
                    GainArmorWithFeedback(enemy, HasAdjacentStructure(enemy, HexTerrainStructureType.Barrier) ? 12 : 8);
                    break;
                case "enemy_gargoyle_guard":
                    enemy.State.enemyDamageReductionActive = true;
                    break;
                case "enemy_hellhound_chain_bite":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                        for (int i = 0; i < 3 && target.IsAlive; i++)
                            yield return ResolveDirectAttackRoutine(enemy, target, 3);
                    break;
                case "enemy_hellhound_flame_fang":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                    {
                        yield return ResolveDirectAttackRoutine(enemy, target, 4);
                        if (target.IsAlive) target.ApplyBurn(2);
                    }
                    break;
                case "enemy_hellhound_charge":
                    yield return ResolveEnemyChargeRoutine(enemy, target, 2, 6, false);
                    break;
                case "enemy_hellhound_lick_fire":
                    enemy.Heal(5);
                    enemy.ApplyBurn(1);
                    break;
                case "enemy_hellhound_instinct":
                    enemy.State.enemyIgnitionPassive = true;
                    break;
                case "enemy_hellhound_ember":
                    PlaceTemporaryObstaclesAround(enemy, 1, 2);
                    break;
                case "enemy_mimic_frenzy":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                        for (int i = 0; i < 2 && target.IsAlive; i++)
                            yield return ResolveDirectAttackRoutine(enemy, target, 4);
                    break;
                case "enemy_mimic_pounce":
                    yield return ResolveEnemyChargeRoutine(enemy, target, 2, 7, false);
                    break;
                case "enemy_mimic_reveal":
                    enemy.GainStrength(2);
                    break;
                case "enemy_mimic_sticky":
                    if (target != null && HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                    {
                        yield return ResolveDirectAttackRoutine(enemy, target, 3);
                        if (target.IsAlive) target.ApplyBind(1);
                    }
                    break;
                case "enemy_mimic_greed":
                    enemy.State.enemySpreadActiveThisTurn = true;
                    break;
                case "enemy_mind_flayer_steal":
                    if (target != null && _lastPlayerMirrorCard != null)
                    {
                        if (_lastPlayerMirrorCard.cardType == HexCardType.Attack)
                            yield return ResolveDirectAttackRoutine(enemy, target, Mathf.Max(0, _lastPlayerMirrorCard.amount));
                        else if (_lastPlayerMirrorCard.effectType == HexCardEffectType.Defend)
                            GainArmorWithFeedback(enemy, Mathf.Max(0, _lastPlayerMirrorCard.amount));
                    }
                    break;
                case "enemy_mind_flayer_blast":
                    if (target != null)
                    {
                        yield return ResolveDirectAttackRoutine(enemy, target, 20);
                        for (int i = 0; i < 3; i++) target.Deck.AddToDiscardPile(HexCardLibrary.GetDaze());
                    }
                    break;
                case "enemy_mind_flayer_tentacles":
                    int summoned = 0;
                    while (summoned < 2 && TrySummonEnemy(enemy, "mind_tentacle", 8, 4)) summoned++;
                    if (summoned < 2) GainArmorWithFeedback(enemy, (2 - summoned) * 3);
                    break;
                case "enemy_mind_flayer_obscure":
                    break;
            }

            enemy.RefreshLabel();
            _ui?.Refresh();
        }

        private IEnumerator ResolveEnemyChargeRoutine(HexBattleUnit enemy, HexBattleUnit target, int maxSteps, int damage, bool stunOnBlocked)
        {
            if (grid == null || enemy == null || target == null || !target.IsAlive)
                yield break;

            int directionIndex = HexBattlePathing.GetPrimaryDirectionIndex(grid, enemy.State.coord, target.State.coord);
            var path = new List<HexAxialCoord> { enemy.State.coord };
            HexAxialCoord current = enemy.State.coord;
            bool hitObstacle = false;
            for (int step = 0; step < Mathf.Max(1, maxSteps); step++)
            {
                HexAxialCoord next = HexAxialCoord.Neighbor(current, directionIndex);
                if (next.Equals(target.State.coord))
                    break;

                if (IsChargeObstacle(next, enemy))
                {
                    hitObstacle = true;
                    break;
                }

                if (IsOccupied(next, enemy))
                    break;

                path.Add(next);
                current = next;
            }

            if (path.Count > 1)
                yield return MoveUnitRoutine(enemy, path, 0, target.State.coord);

            if (HexAxialCoord.Distance(enemy.State.coord, target.State.coord) <= 1)
                yield return ResolveDirectAttackRoutine(enemy, target, damage);

            if (stunOnBlocked && hitObstacle)
                enemy.ApplyStun(1);
        }

        private IEnumerator ResolveOrcChargeRoutine(HexBattleUnit enemy, HexBattleUnit target)
        {
            if (enemy == null || target == null || !enemy.IsAlive || !target.IsAlive)
                yield break;

            bool validCharge = HexOrcWarriorRules.TryBuildChargePath(
                enemy.State.coord,
                target.State.coord,
                coord => IsChargeObstacle(coord, enemy) || IsOccupied(coord, enemy),
                out _,
                out List<HexAxialCoord> movementPath);
            if (!validCharge)
            {
                yield return MoveTowardTargetRoutine(enemy, target, 1);
                enemy.GetComponent<HexOrcChargePreviewView>()?.Clear();
                yield break;
            }

            if (movementPath.Count > 1)
                yield return MoveUnitRoutine(enemy, movementPath, 0, target.State.coord);

            bool empowered = enemy.State.orcChargeEmpowered;
            int damage = empowered
                ? HexOrcWarriorRules.EmpoweredChargeDamage
                : HexOrcWarriorRules.BaseChargeDamage;
            int knockback = empowered
                ? HexOrcWarriorRules.EmpoweredKnockback
                : HexOrcWarriorRules.BaseKnockback;
            enemy.State.orcChargeEmpowered = false;
            yield return ResolveDirectAttackRoutine(enemy, target, damage, knockback: knockback);
            enemy.GetComponent<HexOrcChargePreviewView>()?.Clear();
        }

        private void UpdateOrcChargeIntentPreviews()
        {
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                HexBattleUnit enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive ||
                    !string.Equals(enemy.State?.enemyDefinitionId, HexEncounterGenerator.OrcWarriorId, System.StringComparison.Ordinal))
                    continue;

                var view = enemy.GetComponent<HexOrcChargePreviewView>();
                bool hasChargeIntent = _enemyIntentSlots.TryGetValue(enemy, out var slots) &&
                                       slots != null &&
                                       slots.Any(slot => slot?.card?.definition?.id == "enemy_orc_charge");
                if (!hasChargeIntent || !TryBuildOrcChargePreview(enemy, out List<HexAxialCoord> previewCoords, out _))
                {
                    view?.Clear();
                    continue;
                }

                view ??= enemy.gameObject.AddComponent<HexOrcChargePreviewView>();
                view.SetPreview(grid, previewCoords, enemy.State.orcChargeEmpowered);
            }
        }

        private bool TryBuildOrcChargePreview(
            HexBattleUnit enemy,
            out List<HexAxialCoord> previewCoords,
            out HexAxialCoord knockbackDestination)
        {
            previewCoords = new List<HexAxialCoord>();
            knockbackDestination = enemy?.State?.coord ?? default;
            HexBattleUnit target = GetPrimaryEnemyTarget(enemy);
            if (enemy == null || target == null || !target.IsAlive ||
                !HexOrcWarriorRules.TryBuildChargePath(
                    enemy.State.coord,
                    target.State.coord,
                    coord => IsChargeObstacle(coord, enemy) || IsOccupied(coord, enemy),
                    out _,
                    out List<HexAxialCoord> movementPath))
                return false;

            previewCoords.AddRange(movementPath);
            if (previewCoords.Count == 0 || !previewCoords[^1].Equals(target.State.coord))
                previewCoords.Add(target.State.coord);

            int knockback = enemy.State.orcChargeEmpowered
                ? HexOrcWarriorRules.EmpoweredKnockback
                : HexOrcWarriorRules.BaseKnockback;
            ForcedMovementResult forcedMovement = ResolveForcedMovement(enemy, target, knockback, false);
            if (forcedMovement != null)
            {
                knockbackDestination = forcedMovement.actualDestination;
                if (!previewCoords[^1].Equals(knockbackDestination))
                    previewCoords.Add(knockbackDestination);
            }
            else
            {
                knockbackDestination = target.State.coord;
            }

            return true;
        }

        private bool IsChargeObstacle(HexAxialCoord coord, HexBattleUnit movingUnit)
        {
            if (grid == null || !grid.IsCoordInside(coord))
                return true;

            if (grid.TryGetTile(coord, out var tile) && tile != null && !TileCanEnter(tile))
                return true;

            return HasSceneObstacleAtCoord(coord, movingUnit);
        }

        private bool HasAdjacentStructure(HexBattleUnit unit, HexTerrainStructureType type)
        {
            if (unit == null || grid == null)
                return false;
            return grid.GetNeighbors(unit.State.coord).Any(coord =>
                grid.TryGetTile(coord, out var tile) && tile != null && tile.structureType == type);
        }

        private bool TryPlaceBarrierNear(HexBattleUnit source, int radius)
        {
            if (source == null || grid == null)
                return false;
            var candidates = HexBattlePathing.GetCoordsInRange(source.State.coord, Mathf.Max(1, radius))
                .Where(coord => !coord.Equals(source.State.coord) && grid.TryGetTile(coord, out var tile) && tile != null && TileCanEnter(tile) && !IsOccupied(coord, source))
                .OrderBy(_ => Random.value).ToList();
            if (candidates.Count == 0 || !grid.TryGetTile(candidates[0], out var chosen) || chosen == null)
                return false;
            chosen.SetStructure(HexTerrainStructureType.Barrier);
            chosen.FlashClick();
            return true;
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

                var path = HexBattlePathing.FindPath(
                    grid,
                    enemy.State.coord,
                    neighbor,
                    coord => IsMovementBlocked(coord, enemy),
                    (from, to) => IsLivingWallMovementTransitionBlocked(from, to, enemy));
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
            _lastBattlePlayerWon = playerWon;
            _busy = true;
            yield return new WaitForSeconds(0.25f);
            _ui.Refresh();
            int goldReward = playerWon && awardVictoryGold ? victoryGoldAmount : 0;
            GameEvent.Send(HexGameEvents.BattleFinished, playerWon, goldReward, _playerUnit);
            BattleFinished?.Invoke(playerWon, goldReward, _playerUnit);
        }

        private IEnumerator ResolveDeathsAndBattleEndRoutine()
        {
            if (_battleFinished)
                yield break;

            var deadUnits = _units
                .Where(unit => unit != null && !unit.IsAlive)
                .Distinct()
                .ToList();
            for (int i = 0; i < deadUnits.Count; i++)
                yield return deadUnits[i].PlayDeathAndCleanup();

            if (_playerUnit == null || !_playerUnit.IsAlive)
            {
                yield return HandleBattleEnd(false);
                yield break;
            }

            if (_enemyUnits.All(enemy => enemy == null || !enemy.IsAlive))
                yield return HandleBattleEnd(true);
        }

        private void RegisterUpdate()
        {
            if (_updateRegistered)
                return;

            HexGameModule.Update.AddUpdateListener(Tick);
            _updateRegistered = true;
        }

        private void UnregisterUpdate()
        {
            if (!_updateRegistered)
                return;

            HexGameModule.Update.RemoveUpdateListener(Tick);
            _updateRegistered = false;
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

        private IEnumerator ResolveWarriorDesignCardRoutine(HexBattleUnit source, HexBattleUnit target, HexCardInstance card, int energySpent, HexAxialCoord targetedCoord)
        {
            string id = card.definition.id;
            switch (id)
            {
                case "warrior_strike":
                    yield return ResolveDirectAttackRoutine(source, target, 6);
                    yield break;
                case "warrior_defend":
                    GainArmorWithFeedback(source, 5);
                    yield break;
                case "warrior_whirlwind":
                    yield return ResolveWarriorAreaAttackRoutine(source, source.State.coord, 2, 2, knockback: 1);
                    yield break;
                case "warrior_burning":
                    ApplyWarriorBurnToArea(source, source.State.coord, 2, 1);
                    yield break;
                case "warrior_quick_step":
                case "warrior_move_forward":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, Mathf.Max(1, card.EffectiveAmount));
                    yield break;
                case "warrior_heavy_blow":
                    yield return ResolveDirectAttackRoutine(source, target, 9);
                    yield break;
                case "warrior_cleave":
                    yield return ResolveWarriorAdjacentMultiAttackRoutine(source, 3, 4);
                    yield break;
                case "warrior_dash_strike":
                    yield return MoveTowardTargetRoutine(source, target, 1);
                    yield return ResolveDirectAttackRoutine(source, target, 6);
                    RefundEnergyOnHit(source, target, card, energySpent);
                    yield break;
                case "warrior_pursuit":
                    yield return ResolveDirectAttackRoutine(source, target, 10);
                    RefundEnergyOnHit(source, target, card, energySpent);
                    yield return ResolveRetreatRoutine(source, target, 1);
                    if (source.Deck.Hand.Count > 0)
                        DiscardOrExhaustCard(source, source.Deck.Hand[Random.Range(0, source.Deck.Hand.Count)], false);
                    yield break;
                case "warrior_battle_cry_transition":
                    source.GainStrength(2);
                    RecycleOneExhaustedCard(source);
                    yield break;
                case "warrior_ember":
                    ExhaustRandomHandCard(source);
                    DrawCardsForUnit(source, 1, true);
                    yield break;
                case "warrior_warmup":
                    DrawCardsForUnit(source, 2, true);
                    ExhaustRandomHandCard(source);
                    yield break;
                case "warrior_iron_wall":
                    GainArmorWithFeedback(source, 10);
                    yield break;
                case "warrior_true_courage":
                    GainArmorWithFeedback(source, 7);
                    DrawCardsForUnit(source, 1, true);
                    yield break;
                case "warrior_armor_break_setup":
                    ApplyWarriorFocusBonus(source, 3);
                    DrawCardsForUnit(source, 1, true);
                    yield break;
                case "warrior_numb":
                    if (source.State.warriorExhaustEventThisTurn)
                        source.State.energy += ScaleWarriorChainValue(source, "exhaust", 4);
                    yield break;
                case "warrior_simplify":
                    yield return ResolveWarriorSimplifyRoutine(source);
                    yield break;
                case "warrior_ember_chaos":
                    yield return ResolveEmberChaosRoutine(source);
                    yield break;
                case "warrior_furnace_heart":
                    source.State.drawOnExhaust = true;
                    yield break;
                case "warrior_scorched_earth":
                    source.State.warriorScorchedEarthActive = true;
                    yield break;
                case "warrior_scrap_recycle":
                    if (source.State.warriorExhaustEventThisTurn)
                    {
                        for (int i = 0; i < source.Deck.ExhaustPile.Count; i++)
                        {
                            if (ReferenceEquals(source.Deck.ExhaustPile[i], card))
                                continue;
                            source.Deck.TakeFromExhaustPileToHand(i);
                            break;
                        }
                    }
                    yield break;
                case "warrior_fuel":
                    ApplyWarriorFocusBonus(source, source.State.warriorExhaustEventThisTurn ? 10 : 4);
                    yield break;
                case "warrior_build_up":
                    ApplyWarriorFocusBonus(source, 7);
                    yield break;
                case "warrior_double_focus":
                    ApplyWarriorFocusBonus(source, 2, queueSecondHit: true);
                    yield break;
                case "warrior_combo_focus_slash":
                {
                    int hitDamage = ScaleWarriorChainValue(source, "focus", 2);
                    for (int i = 0; i < 4; i++)
                        yield return ResolveDirectAttackRoutine(source, target, hitDamage);
                    yield break;
                }
                case "warrior_prepared_blade":
                    source.State.warriorPreparedBlade = true;
                    yield break;
                case "warrior_hilt_storm":
                {
                    int hiltDamage = ScaleWarriorChainValue(source, "focus", 1);
                    for (int i = 0; i < 2; i++)
                    {
                        yield return ResolveDirectAttackRoutine(source, target, hiltDamage);
                        if (target != null && target.IsAlive)
                            yield return ApplyKnockbackRoutine(source, target, 1);
                    }
                    yield break;
                }
                case "warrior_iaido":
                    source.State.warriorFocusEffectDoubleThisCard = true;
                    yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "focus", 12));
                    yield break;
                case "warrior_sidestep":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    GainArmorWithFeedback(source, 4);
                    yield break;
                case "warrior_guillotine":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 2);
                    yield return KnockbackAdjacentEnemiesRoutine(source, 1);
                    yield break;
                case "warrior_disarming_stare":
                    yield return ApplyKnockbackRoutine(source, target, 1);
                    yield break;
                case "warrior_battle_line":
                    source.State.warriorStrengthPerTurn += 1;
                    yield break;
                case "warrior_immovable_mountain":
                    source.State.retainArmorBetweenTurns = true;
                    yield break;
                case "warrior_triple_slash":
                    for (int i = 0; i < 3; i++)
                        yield return ResolveDirectAttackRoutine(source, target, 2);
                    yield break;

                case "warrior_burning_mark":
                    ApplyWarriorBurn(source, target, 3);
                    yield break;
                case "warrior_fire_tongue":
                    ApplyWarriorBurnToArea(source, source.State.coord, 1, 1);
                    yield break;
                case "warrior_burning_blade":
                    yield return ResolveDirectAttackRoutine(source, target, 6);
                    int extraBurn = target.State.burn > 0 ? ScaleWarriorChainValue(source, "burn", 4) : 0;
                    ApplyWarriorBurn(source, target, 2 + extraBurn);
                    yield break;
                case "warrior_burning_wind":
                    yield return ResolveBurningWindRoutine(source);
                    yield break;
                case "warrior_grand_fire_slash":
                    yield return ResolveDirectionalAttackRoutine(source, targetedCoord, card);
                    ApplyWarriorBurnToCoords(source, GetDirectionalAreaCoords(source.State.coord, targetedCoord, 1, 0), 2);
                    yield break;
                case "warrior_fire_ring":
                    yield return ResolveWarriorAreaAttackRoutine(source, source.State.coord, 1, 3, burn: 1);
                    yield break;
                case "warrior_ignite":
                    yield return ResolveIgniteRoutine(source, target);
                    yield break;
                case "warrior_combust":
                    yield return ResolveCombustRoutine(source, target);
                    yield break;
                case "warrior_endless_fireworks":
                    yield return ResolveDirectAttackRoutine(source, target, Mathf.Max(0, target.State.burn));
                    yield break;
                case "warrior_ember_brand":
                    yield return MoveTowardTargetRoutine(source, target, 1);
                    yield return ResolveDirectAttackRoutine(source, target, 8);
                    ApplyWarriorBurn(source, target, 2);
                    if (target != null && target.State.burn > 0)
                        RefundEnergyOnHit(source, target, card, energySpent);
                    yield break;
                case "warrior_molten":
                    source.State.energy += Mathf.Max(0, target.State.burn / 3);
                    yield break;
                case "warrior_double_burn":
                    if (target != null)
                        ApplyWarriorBurn(source, target, Mathf.Max(0, target.State.burn));
                    yield break;
                case "warrior_ember_guard":
                    GainArmorWithFeedback(source, 5 + (_enemyUnits.Any(enemy => enemy != null && enemy.IsAlive && enemy.State.burn > 0) ? 5 : 0));
                    yield break;
                case "warrior_blazing_step":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    ApplyBurnToAdjacentEnemies(source, 2);
                    MarkWarriorEvent(source, "burn");
                    yield break;
                case "warrior_inferno_heart":
                    source.State.warriorInfernoHeart = true;
                    yield break;

                case "warrior_vile_words":
                    AddFearCardsToEnemy(source, target, 1);
                    DrawCardsForUnit(source, 1, true);
                    yield break;
                case "warrior_fear_howl":
                    AddFearCardsToEnemy(source, target, 2);
                    yield return ApplyKnockbackRoutine(source, target, 1);
                    yield break;
                case "warrior_scarecrow":
                    AddFearCardsToEnemy(source, GetNearestEnemy(source), 1);
                    GainArmorWithFeedback(source, 8);
                    yield break;
                case "warrior_contagion":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    AddFearCardsToEnemy(source, GetNearestEnemy(source), 3);
                    yield break;
                case "warrior_intimidate":
                    AddFearCardsToAllEnemies(source, 1);
                    yield break;
                case "warrior_empty_city":
                    GainArmorWithFeedback(source, 12);
                    if (CountFearCardsInEnemyDrawPiles() >= 3)
                        DrawCardsForUnit(source, 1, true);
                    yield break;
                case "warrior_warcry_fear":
                    GainArmorWithFeedback(source, 7);
                    if (HasAnyFearIntent())
                    {
                        var nearest = GetNearestEnemy(source);
                        if (nearest != null)
                            yield return MoveTowardTargetRoutine(source, nearest, 2);
                        GainArmorWithFeedback(source, 5);
                    }
                    yield break;
                case "warrior_frighten_back":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    AddFearCardsToEnemy(source, GetNearestEnemy(source), 1);
                    yield break;
                case "warrior_nightmare_step":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 2);
                    ConsumeFearIntentCard();
                    yield break;
                case "warrior_screaming_raid":
                    ConsumeFearIntentCard();
                    yield return MoveTowardTargetRoutine(source, target, 2);
                    yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "fear", 15));
                    yield break;
                case "warrior_fear_descends":
                    yield return ResolveFearDescendsRoutine(source, target);
                    yield break;
                case "warrior_inner_demon":
                    source.State.warriorDrawOnFearAdded = true;
                    yield break;
                case "warrior_omen":
                    source.State.warriorExtraFearFirstEachTurn = true;
                    yield break;
                case "warrior_mind_seize":
                    source.State.warriorGainStrengthOnFearPlayed = true;
                    yield break;
                case "warrior_mind_guard":
                    source.State.warriorArmorOnFearAdded = true;
                    yield break;

                case "warrior_blood_sacrifice":
                    ApplyWarriorBleed(source, source, 1);
                    source.GainStrength(2);
                    yield break;
                case "warrior_bloodletting":
                    ApplyWarriorBleed(source, source, 2);
                    DrawCardsForUnit(source, 2, true);
                    yield break;
                case "warrior_pain_strike":
                    yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "bleed", 11));
                    ApplyWarriorBleed(source, target, 3);
                    yield break;
                case "warrior_life_for_life":
                    yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "bleed", 6));
                    ApplyWarriorBleed(source, target, 2);
                    if (source.State.bleed > 0)
                        DrawCardsForUnit(source, 1, true);
                    yield break;
                case "warrior_blood_surge":
                    source.GainStrength(ScaleWarriorChainValue(source, "bleed", Mathf.Max(0, source.State.bleed)));
                    yield break;
                case "warrior_martyrdom":
                    yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "bleed", 6 + Mathf.Max(0, source.State.bleed)));
                    yield break;
                case "warrior_blood_forged":
                    yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "bleed", 6 + source.State.warriorBleedEventsThisBattle * 3));
                    RefundEnergyOnHit(source, target, card, energySpent);
                    yield break;
                case "warrior_blood_sword":
                    source.State.warriorDamageMultiplierThisTurn = 2;
                    ApplyWarriorBleed(source, source, 99);
                    yield break;
                case "warrior_brutality":
                    GainArmorWithFeedback(source, 6);
                    ApplyWarriorBleed(source, source, 1);
                    if (source.State.warriorBleedEventsThisBattle >= 7)
                        source.State.vampirism += 1;
                    yield break;
                case "warrior_scab":
                    GainArmorWithFeedback(source, 16);
                    ApplyWarriorBleed(source, source, 2);
                    yield break;
                case "warrior_pain_draw":
                    int bleed = Mathf.Max(0, source.State.bleed);
                    source.State.bleed = 0;
                    source.GainStrength(bleed / 2);
                    yield break;
                case "warrior_endure":
                    int delayed = Mathf.Max(0, source.State.bleed);
                    source.State.bleed = 0;
                    source.State.warriorDelayedBleed += delayed;
                    DrawCardsForUnit(source, 1, true);
                    yield break;
                case "warrior_red_step":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    ApplyWarriorBleed(source, source, 1);
                    GainArmorWithFeedback(source, 3);
                    yield break;
                case "warrior_blood_pact":
                    source.State.warriorBloodPactActive = true;
                    yield break;
                case "warrior_backflow":
                    source.State.warriorHealOnBleedGain = true;
                    yield break;
                case "warrior_death_harvest":
                    yield return ResolveDeathHarvestRoutine(source, target);
                    yield break;

                case "warrior_flash_step_slash":
                    yield return MoveTowardTargetRoutine(source, target, 1);
                    yield return ResolveDirectAttackRoutine(source, target, 5);
                    if (source.State.warriorMoveEventThisTurn)
                        yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "move", 5));
                    yield break;
                case "warrior_break_platform":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    DestroyAdjacentBarrier(source.State.coord, targetedCoord);
                    yield break;
                case "warrior_charge":
                {
                    bool hadMove = source.State.warriorMoveEventThisTurn;
                    yield return MoveTowardTargetRoutine(source, target, 1);
                    int chargeDamage = ScaleWarriorChainValue(source, "move", 4);
                    if (hadMove || source.State.warriorMoveEventThisTurn)
                        chargeDamage += 8;
                    yield return ResolveDirectAttackRoutine(source, target, chargeDamage);
                    yield break;
                }
                case "warrior_quake":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    int quakeDamage = source.State.warriorMoveEventThisTurn ? ScaleWarriorChainValue(source, "move", 6) : 4;
                    yield return ResolveWarriorAreaAttackRoutine(source, source.State.coord, 1, quakeDamage);
                    ConvertRandomBarrierToRuin(source.State.coord, 1, 4);
                    yield break;
                case "warrior_swap_guard":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    if (source.State.warriorMoveEventThisTurn)
                        GainArmorWithFeedback(source, ScaleWarriorChainValue(source, "move", 5));
                    yield break;
                case "warrior_cover_lean":
                    GainArmorWithFeedback(source, 8);
                    if (source.State.warriorMoveEventThisTurn && HasAdjacentBarrierOrRuin(source.State.coord))
                        GainArmorWithFeedback(source, ScaleWarriorChainValue(source, "move", 8));
                    yield break;
                case "warrior_clear_shield":
                    DestroyAdjacentRuin(source.State.coord);
                    GainArmorWithFeedback(source, 6);
                    yield break;
                case "warrior_skirmish":
                    source.State.warriorSkirmishArmorOnMove = true;
                    yield break;

                // === 新增草案卡 ===
                case "warrior_close_step":
                {
                    int steps = IsAdjacentToAnyEnemy(targetedCoord)
                        ? Mathf.Max(1, HexAxialCoord.Distance(source.State.coord, targetedCoord))
                        : 1;
                    yield return ResolveCardMoveRoutine(source, targetedCoord, steps);
                    yield break;
                }
                case "warrior_windstep_ready":
                    source.State.warriorWindstepReady = true;
                    yield break;
                case "warrior_opening_stagger":
                    source.State.warriorFirstAttackKnockback = true;
                    yield break;
                case "warrior_opening_reach":
                    source.State.warriorOpeningReach = true;
                    yield break;
                case "warrior_leap_step":
                    yield return ResolveJumpMoveRoutine(source, targetedCoord, 2);
                    yield break;
                case "warrior_blast_barrel":
                    if (target != null && target.IsAlive)
                        target.State.blastBarrelDamage = Mathf.Max(target.State.blastBarrelDamage, 8);
                    yield break;
                case "warrior_hook":
                    yield return ResolveDirectAttackRoutine(source, target, 4);
                    if (target != null && target.IsAlive)
                        yield return ApplyPullRoutine(source, target, 1);
                    yield break;
                case "warrior_break_slash":
                    yield return ResolveDirectAttackRoutine(source, target, 8);
                    DestroyAdjacentRuin(source.State.coord);
                    yield break;
                case "warrior_light_gear":
                    source.State.warriorLightGear = true;
                    yield break;
                case "warrior_fortify":
                    PlaceTemporaryObstaclesAround(source, 1, 1);
                    yield break;
                case "warrior_block_path":
                    PlaceRuinsInLine(source, targetedCoord, 1, 0);
                    yield break;
                case "warrior_rolling_siege":
                    yield return ResolveRollingSiegeRoutine(source, targetedCoord);
                    yield break;
                case "warrior_pierce_step":
                    yield return ResolveJumpMoveRoutine(source, targetedCoord, 2);
                    yield break;
                case "warrior_backwall_smash":
                    yield return ResolveDirectAttackRoutine(source, target, IsBehindBlocked(source, target) ? 34 : 10);
                    yield break;
                case "warrior_dismantle_slash":
                {
                    bool hasNearRuin = target != null && HasRuinAdjacent(target.State.coord);
                    yield return ResolveDirectAttackRoutine(source, target, hasNearRuin ? 16 : 6);
                    if (target != null)
                        DestroyAdjacentRuin(target.State.coord);
                    yield break;
                }
                case "warrior_clear_path":
                    DestroyAdjacentRuin(source.State.coord);
                    DestroyAdjacentBarrier(source.State.coord, targetedCoord);
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    yield break;

                case "warrior_intent_intercept":
                    ConsumeOneIntentCardToPlayerExhaust(source, target);
                    AddFearCardsToEnemy(source, target, 2);
                    yield break;
                case "warrior_fear_press":
                    ConsumeFearIntentCard();
                    yield return ResolveDirectAttackRoutine(source, target, 9);
                    yield break;
                case "warrior_evasion_plan":
                {
                    int moveSteps = HasAnyFearIntent() ? 2 : 1;
                    yield return ResolveCardMoveRoutine(source, targetedCoord, moveSteps);
                    GainArmorWithFeedback(source, 4);
                    yield break;
                }
                case "warrior_fear_echo":
                    source.State.warriorFearEcho = true;
                    yield break;
            }
        }

        private bool IsAdjacentToAnyEnemy(HexAxialCoord coord)
        {
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy != null && enemy.IsAlive && GetDistanceToUnit(coord, enemy) == 1)
                    return true;
            }

            return false;
        }

        private bool HasRuinAdjacent(HexAxialCoord center)
        {
            if (grid == null)
                return false;

            foreach (var neighbor in grid.GetNeighbors(center))
            {
                if (grid.TryGetTile(neighbor, out var tile) && tile != null && TileHasRuin(tile))
                    return true;
            }

            return false;
        }

        private bool DestroyAdjacentRuin(HexAxialCoord center)
        {
            if (grid == null)
                return false;

            foreach (var neighbor in grid.GetNeighbors(center))
            {
                if (grid.TryGetTile(neighbor, out var tile) && tile != null && TileHasRuin(tile))
                {
                    tile.ClearStructure();
                    tile.FlashClick();
                    return true;
                }
            }

            return false;
        }

        private bool IsBehindBlocked(HexBattleUnit source, HexBattleUnit target)
        {
            if (grid == null || source == null || target == null)
                return false;

            int directionIndex = HexBattlePathing.GetPrimaryDirectionIndex(grid, source.State.coord, target.State.coord);
            int behindIndex = (directionIndex + 3) % 6;
            var behind = HexAxialCoord.Neighbor(source.State.coord, behindIndex);
            if (!grid.IsCoordInside(behind))
                return true;

            return grid.TryGetTile(behind, out var tile) && tile != null && !TileCanEnter(tile);
        }

        private IEnumerator ResolveJumpMoveRoutine(HexBattleUnit source, HexAxialCoord destination, int maxSteps)
        {
            if (grid == null || source == null || !source.IsAlive)
                yield break;

            int distance = HexAxialCoord.Distance(source.State.coord, destination);
            if (distance <= 0 || distance > Mathf.Max(1, maxSteps))
                yield break;
            if (IsMovementDestinationBlocked(destination, source))
                yield break;

            var path = new List<HexAxialCoord> { source.State.coord, destination };
            if (IsLivingWallMovementPathBlocked(path, source))
                yield break;
            yield return MoveUnitRoutine(source, path, 0);
        }

        private void PlaceTemporaryObstaclesAround(HexBattleUnit source, int count, int lifespanTurns)
        {
            if (source == null || grid == null || count <= 0)
                return;

            var candidates = grid.GetNeighbors(source.State.coord)
                .Where(coord => grid.TryGetTile(coord, out var tile) &&
                                tile != null &&
                                TileCanEnter(tile) &&
                                !IsOccupied(coord, source))
                .OrderBy(_ => Random.value)
                .Take(Mathf.Max(1, count))
                .ToList();

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!grid.TryGetTile(candidates[i], out var tile) || tile == null)
                    continue;

                tile.SetProp(HexPropLibrary.DefaultRuinPropId, 1);
                tile.FlashClick();
                _temporaryObstacles.Add(candidates[i]);
            }
        }

        private void PlaceRuinsInLine(HexBattleUnit source, HexAxialCoord aimedCoord, int length, int width)
        {
            if (source == null || grid == null)
                return;

            var coords = GetDirectionalAreaCoords(source.State.coord, aimedCoord, Mathf.Max(1, length), width);
            for (int i = 0; i < coords.Count; i++)
            {
                if (!grid.TryGetTile(coords[i], out var tile) || tile == null)
                    continue;
                if (!TileCanEnter(tile) || IsOccupied(coords[i], source))
                    continue;

                tile.SetProp(HexPropLibrary.DefaultRuinPropId, 3);
                tile.FlashClick();
            }
        }

        private IEnumerator ResolveRollingSiegeRoutine(HexBattleUnit source, HexAxialCoord aimedCoord)
        {
            if (source == null || grid == null)
                yield break;

            DestroyAdjacentRuin(source.State.coord);
            int directionIndex = HexBattlePathing.GetPrimaryDirectionIndex(grid, source.State.coord, aimedCoord);
            var current = source.State.coord;
            for (int step = 0; step < 6; step++)
            {
                current = HexAxialCoord.Neighbor(current, directionIndex);
                if (!grid.IsCoordInside(current))
                    break;

                var victim = FindUnitAtCoord(current, source);
                if (victim != null)
                {
                    yield return ResolveDirectAttackRoutine(source, victim, 4, knockback: 1);
                }

                if ((grid.TryGetTile(current, out var tile) && tile != null && TileBlocksLineOfSight(tile)) ||
                    FindLivingWallAtCoord(current, source) != null)
                    break;
            }
        }

        private void ConsumeOneIntentCardToPlayerExhaust(HexBattleUnit source, HexBattleUnit target)
        {
            if (target == null || !_enemyIntentSlots.TryGetValue(target, out var slots) || slots == null || slots.Count == 0)
                return;

            var slot = slots[0];
            slots.RemoveAt(0);
            if (slot?.card?.definition != null && source != null)
                source.Deck.AddToExhaustPile(slot.card.definition);
        }

        private readonly List<HexAxialCoord> _temporaryObstacles = new();

        private void ExpireTemporaryObstacles()
        {
            if (grid == null || _temporaryObstacles.Count == 0)
                return;

            for (int i = 0; i < _temporaryObstacles.Count; i++)
            {
                if (grid.TryGetTile(_temporaryObstacles[i], out var tile) && tile != null && TileHasRuin(tile) && TileStructureHp(tile) <= 1)
                    tile.ClearStructure();
            }

            _temporaryObstacles.Clear();
        }

        private int GetWarriorFirstAttackRangeBonus(HexCardInstance card)
        {
            if (_playerUnit == null || card?.definition == null)
                return 0;
            if (card.definition.cardType != HexCardType.Attack)
                return 0;
            if (!_playerUnit.State.warriorOpeningReach || _playerUnit.State.warriorFirstAttackCardUsedThisTurn)
                return 0;

            return 1;
        }

        private IEnumerator ApplyWarriorFirstAttackCardEffects(HexBattleUnit source, HexBattleUnit target, HexCardInstance card)
        {
            if (source == null || source != _playerUnit || card?.definition == null)
                yield break;
            if (source.State.profession != HexCardProfession.Warrior)
                yield break;
            if (card.definition.cardType != HexCardType.Attack)
                yield break;
            if (source.State.warriorFirstAttackCardUsedThisTurn)
                yield break;

            source.State.warriorFirstAttackCardUsedThisTurn = true;
            if (source.State.warriorFirstAttackKnockback && target != null && target.IsAlive && target.State.faction != source.State.faction)
                yield return ApplyKnockbackRoutine(source, target, 1);
        }

        private IEnumerator ResolveWarriorAreaAttackRoutine(HexBattleUnit source, HexAxialCoord center, int radius, int damage, int burn = 0, int knockback = 0)
        {
            var targets = GetEnemiesInArea(center, Mathf.Max(0, radius), source)
                .OrderBy(enemy => GetUnitDistance(source, enemy))
                .ToList();
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive)
                    continue;

                yield return ResolveDirectAttackRoutine(source, target, damage, knockback: knockback);
                if (target.IsAlive && burn > 0)
                    ApplyWarriorBurn(source, target, burn);
            }
        }

        private IEnumerator ResolveWarriorAdjacentMultiAttackRoutine(HexBattleUnit source, int maxTargets, int damage)
        {
            var targets = _enemyUnits
                .Where(enemy => enemy != null && enemy.IsAlive && GetUnitDistance(source, enemy) <= 1)
                .OrderBy(enemy => enemy.State.currentHealth)
                .Take(Mathf.Max(1, maxTargets))
                .ToList();
            for (int i = 0; i < targets.Count; i++)
                yield return ResolveDirectAttackRoutine(source, targets[i], damage);
        }

        private IEnumerator MoveTowardTargetRoutine(HexBattleUnit source, HexBattleUnit target, int maxSteps)
        {
            if (source == null || target == null || maxSteps <= 0)
                yield break;

            var path = FindBestApproachPath(source, target.State.coord, 1);
            if (path == null || path.Count < 2)
                yield break;

            if (path.Count - 1 > maxSteps)
                path = path.Take(maxSteps + 1).ToList();

            yield return MoveUnitRoutine(source, path, 0, target.State.coord);
        }

        private IEnumerator KnockbackAdjacentEnemiesRoutine(HexBattleUnit source, int distance)
        {
            var targets = _enemyUnits
                .Where(enemy => enemy != null && enemy.IsAlive && GetUnitDistance(source, enemy) <= 1)
                .ToList();
            for (int i = 0; i < targets.Count; i++)
                yield return ApplyKnockbackRoutine(source, targets[i], distance);
        }

        private void RefundEnergyOnHit(HexBattleUnit source, HexBattleUnit target, HexCardInstance card, int energySpent)
        {
            if (source == null || target == null)
                return;

            KeywordTriggerEngine.OnHitConfirmed(source, card, energySpent);
        }

        private void RecycleOneExhaustedCard(HexBattleUnit source)
        {
            if (source == null || source.Deck.ExhaustPile.Count == 0)
                return;

            source.Deck.AddToDrawPile(source.Deck.ExhaustPile[0].definition, false);
        }

        private void MoveExhaustCardsFromDrawToHand(HexBattleUnit source, int count)
        {
            if (source == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                var card = source.Deck.DrawFirstMatchingToHand(definition => HexCardLibrary.HasKeyword(definition, HexCardKeywordType.Exhaust), out _);
                if (card == null)
                    break;
            }
        }

        private IEnumerator ResolveWarriorSimplifyRoutine(HexBattleUnit source)
        {
            if (source == null)
                yield break;

            for (int i = 0; i < 2; i++)
            {
                if (!source.Deck.TryRemoveFromDrawOrDiscard(
                        definition => HexCardLibrary.HasKeyword(definition, HexCardKeywordType.Exhaust),
                        out var card) ||
                    card?.definition == null)
                    break;

                source.Deck.AddCardInstanceToHand(card);
                HexBattleUnit autoTarget = GetNearestEnemy(source);
                HexAxialCoord targeted = autoTarget != null ? autoTarget.State.coord : source.State.coord;
                yield return ResolveWarriorDesignCardRoutine(source, autoTarget, card, 0, targeted);
                if (source.Deck.Hand.Contains(card))
                    DiscardOrExhaustCard(source, card, HexCardLibrary.HasKeyword(card.definition, HexCardKeywordType.Exhaust));
            }
        }

        private bool HasAdjacentBarrierOrRuin(HexAxialCoord center)
        {
            if (grid == null)
                return false;

            foreach (var neighbor in grid.GetNeighbors(center))
            {
                if (!grid.TryGetTile(neighbor, out var tile) || tile == null)
                    continue;
                if (TileIsBarrier(tile) || TileHasRuin(tile))
                    return true;
            }

            return false;
        }

        private IEnumerator ResolveEmberChaosRoutine(HexBattleUnit source)
        {
            if (source == null)
                yield break;

            var cardsToExhaust = source.Deck.Hand.ToList();
            int damage = cardsToExhaust.Count * 3;
            for (int i = 0; i < cardsToExhaust.Count; i++)
                DiscardOrExhaustCard(source, cardsToExhaust[i], true);

            if (damage <= 0)
                yield break;

            yield return ResolveWarriorAreaAttackRoutine(source, source.State.coord, 2, damage);
        }

        private void ApplyWarriorBurn(HexBattleUnit source, HexBattleUnit target, int amount)
        {
            if (target == null || amount <= 0)
                return;

            target.ApplyBurn(amount);
            MarkWarriorEvent(source, "burn");
        }

        private void ApplyWarriorBurnToArea(HexBattleUnit source, HexAxialCoord center, int radius, int amount)
        {
            var targets = GetEnemiesInArea(center, Mathf.Max(0, radius), source);
            for (int i = 0; i < targets.Count; i++)
                ApplyWarriorBurn(source, targets[i], amount);
        }

        private void ApplyWarriorBurnToCoords(HexBattleUnit source, IEnumerable<HexAxialCoord> coords, int amount)
        {
            if (coords == null)
                return;

            var covered = new HashSet<HexAxialCoord>(coords);
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy != null && enemy.IsAlive && enemy.OccupiedCoords.Any(covered.Contains))
                    ApplyWarriorBurn(source, enemy, amount);
            }
        }

        private IEnumerator ResolveBurningWindRoutine(HexBattleUnit source)
        {
            var targets = _enemyUnits
                .Where(enemy => enemy != null && enemy.IsAlive && GetUnitDistance(source, enemy) <= 1)
                .ToList();
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                source.GainStrength(Mathf.Max(0, target.State.burn) * 2);
                yield return ResolveDirectAttackRoutine(source, target, 5);
            }
        }

        private IEnumerator ResolveIgniteRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            if (target == null)
                yield break;

            int burn = Mathf.Max(0, target.State.burn);
            if (burn > 0)
                yield return ResolveDirectAttackRoutine(source, target, burn);

            var adjacent = _enemyUnits
                .Where(enemy => enemy != null && enemy.IsAlive && enemy != target && GetUnitDistance(target, enemy) <= 1)
                .ToList();
            for (int i = 0; i < adjacent.Count; i++)
                ApplyWarriorBurn(source, adjacent[i], 1);
        }

        private IEnumerator ResolveCombustRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            if (target == null)
                yield break;

            int burn = Mathf.Max(0, target.State.burn);
            target.State.burn = 0;
            if (burn <= 0)
                yield break;

            GainArmorWithFeedback(source, burn);
            yield return ResolveDirectAttackRoutine(source, target, ScaleWarriorChainValue(source, "burn", burn * 2));
        }

        private void ApplyWarriorBleed(HexBattleUnit source, HexBattleUnit target, int amount)
        {
            if (target == null || amount <= 0)
                return;

            target.ApplyBleed(amount);
            if (source != null && source == target && source.State.warriorHealOnBleedGain)
                source.Heal(1);
            MarkWarriorEvent(source, "bleed");
            if (source?.State == null || source.State.profession != HexCardProfession.Warrior)
                return;

            source.State.warriorBleedEventsThisBattle += 1;
            source.State.warriorBleedEventsThisTurn += 1;
            if (source.State.warriorBloodPactActive)
                source.State.warriorNextAttackDamageBonus += 2;
        }

        private void AddFearCardsToEnemy(HexBattleUnit source, HexBattleUnit target, int count)
        {
            if (source == null || count <= 0)
                return;

            target ??= GetNearestEnemy(source);
            var fear = HexCardLibrary.GetFearToken();
            if (target == null || fear == null)
                return;

            int total = count;
            if (source.State.warriorExtraFearFirstEachTurn && !source.State.warriorExtraFearUsedThisTurn)
            {
                total += 1;
                source.State.warriorExtraFearUsedThisTurn = true;
            }

            for (int i = 0; i < total; i++)
                target.Deck.AddToDrawPile(fear, false);

            MarkWarriorEvent(source, "fear");
            if (source.State.warriorDrawOnFearAdded)
                DrawCardsForUnit(source, total, true);
            if (source.State.warriorArmorOnFearAdded)
                GainArmorWithFeedback(source, total * 3);
        }

        private void AddFearCardsToAllEnemies(HexBattleUnit source, int count)
        {
            var targets = _enemyUnits.Where(enemy => enemy != null && enemy.IsAlive).ToList();
            for (int i = 0; i < targets.Count; i++)
                AddFearCardsToEnemy(source, targets[i], count);
        }

        private HexBattleUnit GetNearestEnemy(HexBattleUnit source)
        {
            if (source == null)
                return null;

            return _enemyUnits
                .Where(enemy => enemy != null && enemy.IsAlive)
                .OrderBy(enemy => GetUnitDistance(source, enemy))
                .FirstOrDefault();
        }

        private int CountFearCardsInEnemyDrawPiles()
        {
            int count = 0;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null)
                    continue;

                count += enemy.Deck.DrawPile.Count(card => IsFearToken(card));
            }

            return count;
        }

        private bool HasAnyFearIntent()
        {
            return _enemyIntentSlots.Values.Any(slots => slots != null && slots.Any(slot => IsFearIntentCard(slot?.card)));
        }

        private bool ConsumeFearIntentCard()
        {
            foreach (var kvp in _enemyIntentSlots.ToList())
            {
                var slots = kvp.Value;
                if (slots == null)
                    continue;

                for (int i = 0; i < slots.Count; i++)
                {
                    if (!IsFearIntentCard(slots[i]?.card))
                        continue;

                    DiscardOrExhaustCard(kvp.Key, slots[i].card, false);
                    slots.RemoveAt(i);
                    TriggerWarriorFearEcho();
                    return true;
                }
            }

            return false;
        }

        private void TriggerWarriorFearEcho()
        {
            if (_playerUnit == null || !_playerUnit.IsAlive)
                return;
            if (!_playerUnit.State.warriorFearEcho || _playerUnit.State.warriorFearEchoUsedThisTurn)
                return;

            _playerUnit.State.warriorFearEchoUsedThisTurn = true;
            DrawCardsForUnit(_playerUnit, 1, true);
        }

        private static bool IsFearIntentCard(HexCardInstance card)
        {
            return card?.definition != null && (IsFearToken(card) || HasCardTag(card.definition, "恐惧"));
        }

        private void OnEnemyFearTokenDrawn(HexBattleUnit enemy)
        {
            if (_playerUnit == null || !_playerUnit.IsAlive)
                return;

            if (_playerUnit.State.warriorGainStrengthOnFearPlayed)
                _playerUnit.GainStrength(2);

            TriggerWarriorFearEcho();
        }

        private IEnumerator ResolveFearDescendsRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            if (target == null)
                yield break;

            bool empowered = HasAnyFearIntent();
            var movement = ResolveForcedMovement(source, target, empowered ? 3 : 1, false);
            if (movement != null && movement.path.Count > 1)
                yield return MoveUnitRoutine(target, movement.path, 0);
            if (movement != null && movement.collided)
                ApplyDamageToUnit(target, 50, source);
        }

        private IEnumerator ResolveDeathHarvestRoutine(HexBattleUnit source, HexBattleUnit target)
        {
            if (target == null)
                yield break;

            int beforeHealth = target.State.currentHealth;
            yield return ResolveDirectAttackRoutine(source, target, 2);
            int healthLost = Mathf.Max(0, beforeHealth - target.State.currentHealth);
            if (healthLost > 0)
                source.Heal(healthLost);
        }

        private void MarkWarriorEvent(HexBattleUnit source, string eventKey)
        {
            if (source?.State == null || source.State.profession != HexCardProfession.Warrior)
                return;

            switch (eventKey)
            {
                case "burn":
                    source.State.warriorBurnEventThisTurn = true;
                    break;
                case "fear":
                    source.State.warriorFearEventThisTurn = true;
                    break;
                case "bleed":
                    source.State.warriorBleedEventThisTurn = true;
                    break;
                case "exhaust":
                    source.State.warriorExhaustEventThisTurn = true;
                    break;
                case "focus":
                    source.State.warriorFocusEventThisTurn = true;
                    break;
                case "move":
                    source.State.warriorMoveEventThisTurn = true;
                    if (source.State.warriorWindstepReady && !source.State.warriorWindstepUsedThisTurn)
                    {
                        source.State.warriorWindstepUsedThisTurn = true;
                        source.GainStrength(2);
                    }
                    if (source.State.warriorLightGear && !source.State.warriorLightGearUsedThisTurn)
                    {
                        source.State.warriorLightGearUsedThisTurn = true;
                        source.State.energy += 1;
                    }
                    break;
            }
        }

        private void NotifyWarriorExhaust(HexBattleUnit unit)
        {
            if (unit?.State == null || unit.State.profession != HexCardProfession.Warrior)
                return;

            MarkWarriorEvent(unit, "exhaust");
            if (unit.State.drawOnExhaust)
                DrawCardsForUnit(unit, 1);
        }

        private void ApplyWarriorFocusBonus(HexBattleUnit unit, int amount, bool queueSecondHit = false)
        {
            if (unit?.State == null || amount <= 0)
                return;

            if (queueSecondHit)
            {
                unit.State.warriorNextAttackDamageBonus += amount;
                unit.State.warriorNextAttackDamageBonusQueued += amount;
            }
            else
            {
                unit.State.warriorNextAttackDamageBonus += amount;
            }

            MarkWarriorEvent(unit, "focus");
        }

        private int ScaleWarriorChainValue(HexBattleUnit source, string eventKey, int baseValue)
        {
            if (source?.State == null || baseValue <= 0)
                return baseValue;

            bool eventTriggered = eventKey switch
            {
                "burn" => source.State.warriorBurnEventThisTurn,
                "fear" => source.State.warriorFearEventThisTurn,
                "bleed" => source.State.warriorBleedEventThisTurn,
                "move" => source.State.warriorMoveEventThisTurn,
                "exhaust" => source.State.warriorExhaustEventThisTurn,
                "focus" => source.State.warriorFocusEventThisTurn,
                _ => false,
            };
            if (!eventTriggered || IsWarriorFinisherUsed(source, eventKey))
                return baseValue;

            SetWarriorFinisherUsed(source, eventKey);
            int chainLength = 0;
            if (source.State.warriorBurnEventThisTurn)
                chainLength++;
            if (source.State.warriorFearEventThisTurn)
                chainLength++;
            if (source.State.warriorBleedEventThisTurn)
                chainLength++;
            if (source.State.warriorMoveEventThisTurn)
                chainLength++;
            if (source.State.warriorExhaustEventThisTurn)
                chainLength++;
            if (source.State.warriorFocusEventThisTurn)
                chainLength++;

            float multiplier = chainLength switch
            {
                <= 0 => 1f,
                1 => 1.5f,
                2 => 2f,
                _ => 1f + 4f * Mathf.Pow(1.25f, chainLength - 3),
            };
            return Mathf.CeilToInt(baseValue * multiplier);
        }

        private static bool IsWarriorFinisherUsed(HexBattleUnit source, string eventKey)
        {
            return eventKey switch
            {
                "burn" => source.State.warriorBurnFinisherUsedThisTurn,
                "fear" => source.State.warriorFearFinisherUsedThisTurn,
                "bleed" => source.State.warriorBleedFinisherUsedThisTurn,
                "move" => source.State.warriorMoveFinisherUsedThisTurn,
                "exhaust" => source.State.warriorExhaustFinisherUsedThisTurn,
                "focus" => source.State.warriorFocusFinisherUsedThisTurn,
                _ => false,
            };
        }

        private static void SetWarriorFinisherUsed(HexBattleUnit source, string eventKey)
        {
            switch (eventKey)
            {
                case "burn":
                    source.State.warriorBurnFinisherUsedThisTurn = true;
                    break;
                case "fear":
                    source.State.warriorFearFinisherUsedThisTurn = true;
                    break;
                case "bleed":
                    source.State.warriorBleedFinisherUsedThisTurn = true;
                    break;
                case "move":
                    source.State.warriorMoveFinisherUsedThisTurn = true;
                    break;
                case "exhaust":
                    source.State.warriorExhaustFinisherUsedThisTurn = true;
                    break;
                case "focus":
                    source.State.warriorFocusFinisherUsedThisTurn = true;
                    break;
            }
        }

        private IEnumerator ResolveCustomCardRoutine(HexBattleUnit source, HexBattleUnit target, HexCardInstance card, int energySpent, HexAxialCoord targetedCoord, System.Action<bool> setHandled)
        {
            if (card?.definition != null && card.definition.id.StartsWith("warrior_", System.StringComparison.OrdinalIgnoreCase))
            {
                yield return ResolveWarriorDesignCardRoutine(source, target, card, energySpent, targetedCoord);
                setHandled?.Invoke(true);
                yield break;
            }

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
                case "warrior_move_forward":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 2);
                    setHandled?.Invoke(true);
                    yield break;
                case "warrior_sidestep":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    GainArmorWithFeedback(source, 4);
                    setHandled?.Invoke(true);
                    yield break;
                case "warrior_break_platform":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    DestroyAdjacentBarrier(source.State.coord, targetedCoord);
                    setHandled?.Invoke(true);
                    yield break;
                case "warrior_blazing_step":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    ApplyBurnToAdjacentEnemies(source, 2);
                    setHandled?.Invoke(true);
                    yield break;
                case "warrior_red_step":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    source.ApplyBleed(1);
                    GainArmorWithFeedback(source, 3);
                    setHandled?.Invoke(true);
                    yield break;
                case "warrior_frighten_back":
                    yield return ResolveCardMoveRoutine(source, targetedCoord, 1);
                    AddFearToNearestEnemy(source);
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
                    GainArmorWithFeedback(source, 4);
                    source.State.druidBonusArmorOnNextTransform += 3;
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_006":
                    GainArmorWithFeedback(source, 3);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_007":
                    GainArmorWithFeedback(source, 4);
                    setHandled?.Invoke(true);
                    yield break;
                case "C_03_010":
                    bool wasMammoth = source.State.druidForm == HexDruidFormType.Mammoth;
                    GainArmorWithFeedback(source, 6);
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
                    yield return ResolveDirectAttackRoutine(source, target, 8, onHit: dealt => GainArmorWithFeedback(source, dealt), addDaze: 1);
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
                    GainArmorWithFeedback(source, 4);
                    UpgradeRandomCard(source);
                    break;
                case "防御姿态":
                    source.State.weapon = HexWeaponType.None;
                    source.GainToughness(6);
                    break;
                case "整备":
                    GainArmorWithFeedback(source, 8);
                    source.State.skillCooldown = 0;
                    DrawCardsForUnit(source, 1);
                    break;
                case "轻装上阵":
                    DiscountSkillCardsInHand(source, -1, true);
                    break;
                case "鲜血护盾":
                    ApplyDamageWithFeedback(source, 3, source);
                    GainArmorWithFeedback(source, 15);
                    break;
                case "百变护甲":
                    source.State.armorOnSkillCard += 4;
                    break;
                case "退避":
                    ExhaustRandomHandCard(source);
                    source.State.currentMovePoints += 2;
                    GainArmorWithFeedback(source, 8);
                    break;
                case "燃烧契约":
                    source.ApplyBurn(1);
                    DrawCardsForUnit(source, 3);
                    break;
                case "放血":
                    ApplyDamageWithFeedback(source, source.Deck.Hand.Count, source);
                    source.State.energy += source.Deck.Hand.Count;
                    break;
                case "嘲讽":
                    GainArmorWithFeedback(source, 8);
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
                    GainArmorWithFeedback(source, energySpent);
                    break;
                case "愤怒":
                    yield return ResolveDirectAttackRoutine(source, target, source.Deck.Hand.Count(instance => instance.definition.cardType == HexCardType.Attack));
                    card.IncreaseBattleAmount(6);
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
            HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
            int totalHealthLost = 0;
            int totalThornsDamage = 0;
            for (int i = 0; i < _enemyUnits.Count; i++)
            {
                var enemy = _enemyUnits[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                HexDamageResult result = ApplyAttackDamage(source, enemy, 6, snapshot);
                totalHealthLost += result.healthLost;
                if (enemy.IsAlive)
                {
                    enemy.ApplyBleed(1);
                    totalThornsDamage += Mathf.Max(0, enemy.State.thorns);
                }
                if (enemy.IsAlive)
                    enemy.PlayHitAnimation();
            }
            CompleteAttackDamageBatch(source, totalHealthLost);
            if (totalThornsDamage > 0 && source.IsAlive)
                ApplyDamageToUnit(source, totalThornsDamage, null, HexDamageTags.Reaction);
            yield return ResolveDeathsAndBattleEndRoutine();
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

                HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
                int totalHealthLost = 0;
                int totalThornsDamage = 0;
                for (int i = 0; i < targets.Count; i++)
                {
                    var areaTarget = targets[i];
                    if (areaTarget == null || !areaTarget.IsAlive || !source.IsAlive)
                        continue;

                    areaTarget.FaceTarget(source.transform.position);
                    HexDamageResult result = ApplyAttackDamage(
                        source,
                        areaTarget,
                        6 + Mathf.Max(0, source.State.strength),
                        snapshot);
                    totalHealthLost += result.healthLost;

                    bool survived = areaTarget.IsAlive;
                    if (survived)
                    {
                        areaTarget.PlayHitAnimation();
                        totalThornsDamage += Mathf.Max(0, areaTarget.State.thorns);
                    }
                }

                CompleteAttackDamageBatch(source, totalHealthLost);
                if (totalThornsDamage > 0 && source.IsAlive)
                    ApplyDamageToUnit(source, totalThornsDamage, null, HexDamageTags.Reaction);

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

                yield return ResolveDeathsAndBattleEndRoutine();
                if (_battleFinished || !source.IsAlive)
                    yield break;
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

            if (!source.IsAlive || _battleFinished)
                yield break;

            HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
            int totalHealthLost = 0;
            int totalThornsDamage = 0;
            foreach (var enemy in hitEnemies)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                HexDamageResult result = ApplyAttackDamage(
                    source,
                    enemy,
                    passThroughDamage + Mathf.Max(0, source.State.strength),
                    snapshot);
                totalHealthLost += result.healthLost;
                if (enemy.IsAlive)
                {
                    enemy.PlayHitAnimation();
                    totalThornsDamage += Mathf.Max(0, enemy.State.thorns);
                }
            }
            CompleteAttackDamageBatch(source, totalHealthLost);
            if (totalThornsDamage > 0 && source.IsAlive)
                ApplyDamageToUnit(source, totalThornsDamage, null, HexDamageTags.Reaction);
            yield return ResolveDeathsAndBattleEndRoutine();
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
            if (targetWasAlive && !target.IsAlive && source.IsAlive)
            {
                source.State.maxHealth += 3;
                source.Heal(3);
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
            if (source != null && target != null && ReferenceEquals(source, target))
            {
                Debug.LogError(
                    $"[RuinAttack] ResolveDirectAttackRoutine source==target ({GetUnitDisplayName(source)}). " +
                    "Likely ruin-attack sentinel leaked into unit damage path.");
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

                HexDamageResult damageResult = ApplyAttackDamage(
                    source,
                    target,
                    baseDamage + Mathf.Max(0, source.State.strength));
                onHit?.Invoke(damageResult.healthLost);
                bool targetSurvivedHit = target.IsAlive;
                if (targetSurvivedHit)
                {
                    target.PlayHitAnimation();
                    yield return new WaitForSeconds(Mathf.Max(0.08f, target.GetHitDuration() * 0.85f));
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
                    if (_battleFinished)
                        yield break;
                    if (target.IsAlive && target.State.thorns > 0 && source.IsAlive)
                        ApplyDamageToUnit(source, target.State.thorns, target, HexDamageTags.Reaction);
                }

                yield return ResolveDeathsAndBattleEndRoutine();
                if (_battleFinished || !source.IsAlive || !target.IsAlive)
                    yield break;
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
                GainArmorWithFeedback(unit, unit.State.druidBonusArmorOnNextTransform);
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
                        GainArmorWithFeedback(unit, unit.State.currentMovePoints);
                        unit.State.currentMovePoints = 0;
                    }
                    break;
            }
        }

        private void HandlePostMovementPassives(HexBattleUnit unit, IReadOnlyList<HexAxialCoord> path, HexAxialCoord? towardTargetCoord, int movedDistance)
        {
            ResolveConsumableMovementTriggers(unit, path);
            if (unit == null || !unit.IsAlive)
                return;
            if (unit?.State != null && unit.State.profession == HexCardProfession.Warrior && movedDistance > 0)
            {
                MarkWarriorEvent(unit, "move");
                if (unit.State.warriorSkirmishArmorOnMove)
                    GainArmorWithFeedback(unit, 2);
            }

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

        private bool IsTileActionCard(HexCardDefinition definition)
        {
            return definition != null &&
                   definition.targetType == HexCardTargetType.Tile &&
                   (definition.cardType == HexCardType.Action ||
                    definition.effectType == HexCardEffectType.Move ||
                    definition.effectType == HexCardEffectType.DestroyBarrier ||
                    definition.effectType == HexCardEffectType.PlaceRuin);
        }

        private static bool IsEnemyMoveCard(HexCardDefinition definition)
        {
            return definition != null &&
                   (definition.effectType == HexCardEffectType.MoveToward ||
                    definition.effectType == HexCardEffectType.MoveAway ||
                    definition.effectType == HexCardEffectType.Move);
        }

        private static bool RequiresTraversableTileTarget(HexCardDefinition definition)
        {
            return definition != null &&
                   definition.targetType == HexCardTargetType.Tile &&
                   definition.effectType == HexCardEffectType.Move;
        }

        private static bool CanUseAsMovementTarget(HexTile tile)
        {
            if (tile == null)
                return false;

            return TileCanEnter(tile);
        }

        private static bool TileCanEnter(HexTile tile)
        {
            if (tile == null)
                return false;
            return tile.Controller != null ? tile.Controller.CanEnter() : !tile.BlocksMovement;
        }

        private static bool TileHasRuin(HexTile tile)
        {
            return tile != null && (tile.Controller != null ? tile.Controller.Model.HasRuin : tile.HasRuin);
        }

        private static bool TileIsBarrier(HexTile tile)
        {
            if (tile == null)
                return false;
            return tile.Controller != null
                ? tile.Controller.Model.structureType == HexTerrainStructureType.Barrier
                : tile.structureType == HexTerrainStructureType.Barrier;
        }

        private static bool TileBlocksLineOfSight(HexTile tile)
        {
            if (tile == null)
                return false;
            return tile.Controller != null ? tile.Controller.Model.BlocksLineOfSight : tile.BlocksLineOfSight;
        }

        private static HexTerrainPickupType TilePickupType(HexTile tile)
        {
            if (tile == null)
                return HexTerrainPickupType.None;
            return tile.Controller != null ? tile.Controller.Model.pickupType : tile.pickupType;
        }

        private static int TileStructureHp(HexTile tile)
        {
            if (tile == null)
                return 0;
            return tile.Controller != null ? tile.Controller.Model.structureHp : tile.structureHp;
        }

        private static bool ResolveRuinAttackTarget(HexBattleUnit source, HexCardDefinition definition, HexAxialCoord targetedCoord)
        {
            return source != null &&
                   definition != null &&
                   definition.cardType == HexCardType.Attack &&
                   definition.targetType == HexCardTargetType.EnemyUnit &&
                   !targetedCoord.Equals(source.State.coord);
        }

        private bool IsRuinDirectAttackPlay(
            HexBattleUnit source,
            HexBattleUnit target,
            HexCardInstance card,
            HexAxialCoord? directionalCoord,
            HexAxialCoord targetedCoord)
        {
            if (source == null || card?.definition == null || !directionalCoord.HasValue)
                return false;
            if (target != source)
                return false;
            if (!ResolveRuinAttackTarget(source, card.definition, targetedCoord))
                return false;
            if (!grid.TryGetTile(targetedCoord, out var tile) || tile == null)
                return false;
            return TileHasRuin(tile);
        }

        private bool CanAttackRuinTile(HexBattleUnit source, HexCardDefinition definition, HexTile tile)
        {
            if (source == null || definition == null || tile == null)
                return false;
            if (definition.cardType != HexCardType.Attack || definition.targetType != HexCardTargetType.EnemyUnit)
                return false;
            if (!TileHasRuin(tile))
                return false;
            if (HexAxialCoord.Distance(source.State.coord, tile.coord) > definition.castRange + GetWarriorFirstAttackRangeBonus(_draggedCard))
                return false;
            return HasLineOfSight(source.State.coord, tile.coord);
        }

        private static bool IsFearToken(HexCardInstance card)
        {
            return card?.definition != null && card.definition.id == "status_fear_token";
        }

        private void ExhaustHandCardsTriggeredByPlay(HexBattleUnit unit, HexCardInstance playedCard)
        {
            var cardsToExhaust = KeywordTriggerEngine.CollectHandCardsToExhaustAfterPlay(unit, playedCard);
            for (int i = 0; i < cardsToExhaust.Count; i++)
                DiscardOrExhaustCard(unit, cardsToExhaust[i], true);
        }

        private static bool HasCardTag(HexCardDefinition definition, string tag)
        {
            if (definition?.tags == null || string.IsNullOrWhiteSpace(tag))
                return false;

            for (int i = 0; i < definition.tags.Length; i++)
            {
                if (string.Equals(definition.tags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool IsInEnemyAttackRange(HexBattleUnit enemy, HexBattleUnit target, HexEnemyDefinition definition, HexCardDefinition cardDefinition = null)
        {
            if (enemy == null || target == null)
                return false;

            int distance = GetUnitDistance(enemy, target);
            int minRange = Mathf.Max(1, definition?.attackMinRange ?? enemy.State.enemyAttackMinRange);
            int maxRange = Mathf.Max(minRange, cardDefinition != null ? cardDefinition.castRange : definition?.attackMaxRange ?? enemy.State.enemyAttackMaxRange);
            if (cardDefinition != null && cardDefinition.castRange <= 1)
                minRange = 1;

            return distance >= minRange && distance <= maxRange;
        }

        private bool CanResolveTileAction(HexBattleUnit unit, HexCardDefinition definition, HexAxialCoord targetCoord)
        {
            if (unit == null || definition == null || grid == null)
                return false;

            if (definition.effectType == HexCardEffectType.DestroyBarrier &&
                HexAxialCoord.Distance(unit.State.coord, targetCoord) <= Mathf.Max(1, definition.castRange))
            {
                if (FindLivingWallAtCoord(targetCoord) != null)
                    return true;
                if (grid.TryGetTile(targetCoord, out var targetTile) &&
                    targetTile.structureType == HexTerrainStructureType.Barrier)
                    return true;
            }

            if (definition.effectType == HexCardEffectType.PlaceRuin)
                return HexAxialCoord.Distance(unit.State.coord, targetCoord) <= Mathf.Max(1, definition.castRange);

            var path = BuildCardMovementPath(unit, targetCoord, Mathf.Max(1, definition.amount));
            return path != null && path.Count >= 2;
        }

        private IEnumerator ResolveCardMoveRoutine(HexBattleUnit unit, HexAxialCoord destination, int maxSteps)
        {
            if (unit == null || !unit.IsAlive)
                yield break;

            var path = BuildCardMovementPath(unit, destination, Mathf.Max(1, maxSteps));
            if (path == null || path.Count < 2)
                yield break;

            yield return MoveUnitRoutine(unit, path, 0);
        }

        private List<HexAxialCoord> BuildCardMovementPath(HexBattleUnit unit, HexAxialCoord destination, int maxSteps)
        {
            if (grid == null || unit == null || maxSteps <= 0 || unit.State.rooted || unit.State.bind > 0)
                return null;

            if (IsToadJumpMovement(unit))
            {
                int distance = HexAxialCoord.Distance(unit.State.coord, destination);
                if (distance <= 0 || distance > maxSteps || IsMovementDestinationBlocked(destination, unit))
                    return null;

                var directPath = new List<HexAxialCoord> { unit.State.coord, destination };
                return IsLivingWallMovementPathBlocked(directPath, unit) ? null : directPath;
            }

            var path = HexBattlePathing.FindPath(
                grid,
                unit.State.coord,
                destination,
                coord => IsMovementBlocked(coord, unit),
                (from, to) => IsLivingWallMovementTransitionBlocked(from, to, unit));
            if (path == null || path.Count < 2 || path.Count - 1 > maxSteps)
                return null;

            return path;
        }

        private IEnumerator ResolveEnemyIdealRangeMoveRoutine(HexBattleUnit enemy, HexBattleUnit target, int maxSteps)
        {
            if (grid == null || enemy == null || target == null || maxSteps <= 0)
                yield break;
            if (enemy.State.rooted || enemy.State.bind > 0)
                yield break;

            GetEnemyIdealAttackRange(enemy, out int minRange, out int maxRange);
            var path = FindBestIdealRangeMovePath(enemy, target.State.coord, minRange, maxRange, maxSteps);
            if (path == null || path.Count < 2)
                yield break;

            yield return MoveUnitRoutine(enemy, path, 0, target.State.coord);
        }

        private List<HexAxialCoord> FindBestIdealRangeMovePath(
            HexBattleUnit movingUnit,
            HexAxialCoord targetCoord,
            int minRange,
            int maxRange,
            int maxSteps)
        {
            if (grid == null || movingUnit == null || maxSteps <= 0)
                return null;

            int currentDistance = HexAxialCoord.Distance(movingUnit.State.coord, targetCoord);
            bool tooFar = currentDistance > maxRange;
            bool tooClose = currentDistance < minRange;

            List<HexAxialCoord> bestPath = null;
            int bestPrimary = int.MaxValue;
            int bestSecondary = int.MaxValue;
            int bestSteps = int.MaxValue;
            int bestQ = int.MaxValue;
            int bestR = int.MaxValue;

            foreach (var candidate in grid.Tiles.Keys.OrderBy(coord => coord.q).ThenBy(coord => coord.r))
            {
                if (candidate.Equals(movingUnit.State.coord))
                    continue;
                if (IsMovementDestinationBlocked(candidate, movingUnit))
                    continue;

                List<HexAxialCoord> path = IsToadJumpMovement(movingUnit)
                    ? new List<HexAxialCoord> { movingUnit.State.coord, candidate }
                    : HexBattlePathing.FindPath(
                        grid,
                        movingUnit.State.coord,
                        candidate,
                        coord => IsMovementBlocked(coord, movingUnit),
                        (from, to) => IsLivingWallMovementTransitionBlocked(from, to, movingUnit));
                if (path == null || path.Count < 2 || IsLivingWallMovementPathBlocked(path, movingUnit))
                    continue;

                int steps = path.Count - 1;
                if (steps > maxSteps)
                    continue;

                int destDistance = HexAxialCoord.Distance(candidate, targetCoord);
                int bandError = DistanceToIdealBand(destDistance, minRange, maxRange);
                bool inBand = bandError == 0;

                int primary;
                int secondary;
                if (tooFar)
                {
                    // Prefer landing in band; else reduce distance as much as possible.
                    primary = inBand ? 0 : 1;
                    secondary = inBand ? 0 : destDistance;
                }
                else if (tooClose)
                {
                    // Prefer landing in band; else increase distance without overshooting max.
                    primary = inBand ? 0 : 1;
                    secondary = inBand
                        ? 0
                        : Mathf.Max(0, destDistance - maxRange) * 1000
                          + Mathf.Max(0, minRange - destDistance) * 10
                          - Mathf.Max(0, destDistance - currentDistance);
                }
                else
                {
                    // Orbit: must move, hard-prefer staying in band, then minimize band error.
                    primary = inBand ? 0 : 1;
                    secondary = bandError;
                }

                bool better = bestPath == null
                              || primary < bestPrimary
                              || (primary == bestPrimary && secondary < bestSecondary)
                              || (primary == bestPrimary && secondary == bestSecondary && steps < bestSteps)
                              || (primary == bestPrimary && secondary == bestSecondary && steps == bestSteps
                                  && (candidate.q < bestQ || (candidate.q == bestQ && candidate.r < bestR)));
                if (!better)
                    continue;

                bestPath = path;
                bestPrimary = primary;
                bestSecondary = secondary;
                bestSteps = steps;
                bestQ = candidate.q;
                bestR = candidate.r;
            }

            return bestPath;
        }

        private static int DistanceToIdealBand(int distance, int minRange, int maxRange)
        {
            if (distance < minRange)
                return minRange - distance;
            if (distance > maxRange)
                return distance - maxRange;
            return 0;
        }

        private IEnumerator ResolveRetreatRoutine(HexBattleUnit unit, HexBattleUnit threat, int maxSteps)
        {
            if (grid == null || unit == null || threat == null || maxSteps <= 0 || unit.State.rooted || unit.State.bind > 0)
                yield break;

            List<HexAxialCoord> bestPath = null;
            int bestDistance = HexAxialCoord.Distance(unit.State.coord, threat.State.coord);
            foreach (var candidate in grid.Tiles.Keys)
            {
                if (candidate.Equals(unit.State.coord))
                    continue;
                if (IsMovementDestinationBlocked(candidate, unit))
                    continue;

                var path = HexBattlePathing.FindPath(
                    grid,
                    unit.State.coord,
                    candidate,
                    coord => IsMovementBlocked(coord, unit),
                    (from, to) => IsLivingWallMovementTransitionBlocked(from, to, unit));
                if (path == null || path.Count < 2 || path.Count - 1 > maxSteps)
                    continue;

                int distance = HexAxialCoord.Distance(candidate, threat.State.coord);
                if (distance > bestDistance || (distance == bestDistance && (bestPath == null || path.Count < bestPath.Count)))
                {
                    bestPath = path;
                    bestDistance = distance;
                }
            }

            if (bestPath != null)
                yield return MoveUnitRoutine(unit, bestPath, 0, threat.State.coord);
        }

        private void ApplyBurnToAdjacentEnemies(HexBattleUnit source, int amount)
        {
            if (source == null || amount <= 0)
                return;

            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit.State.faction == source.State.faction)
                    continue;
                if (GetUnitDistance(source, unit) <= 1)
                    unit.ApplyBurn(amount);
            }
        }

        private void AddFearToNearestEnemy(HexBattleUnit source)
        {
            var fear = HexCardLibrary.GetFearToken();
            if (source == null || fear == null)
                return;

            var enemy = _enemyUnits
                .Where(candidate => candidate != null && candidate.IsAlive)
                .OrderBy(candidate => GetUnitDistance(source, candidate))
                .FirstOrDefault();
            enemy?.Deck.AddToDrawPile(fear, false);
        }

        private void DestroyAdjacentBarrier(HexAxialCoord sourceCoord, HexAxialCoord targetCoord)
        {
            if (DestroyBarrierAt(targetCoord))
                return;

            foreach (var neighbor in grid.GetNeighbors(sourceCoord))
            {
                if (DestroyBarrierAt(neighbor))
                    return;
            }
        }

        private bool DestroyBarrierAt(HexAxialCoord coord)
        {
            HexBattleUnit livingWall = FindLivingWallAtCoord(coord);
            if (livingWall != null)
                return ApplyLivingWallBreak(livingWall);

            if (grid == null || !grid.TryGetTile(coord, out var tile) || tile == null)
                return false;

            if (!TileIsBarrier(tile))
                return false;

            tile.ClearStructure();
            tile.FlashClick();
            return true;
        }

        private bool PlaceRuinNear(HexBattleUnit source, int radius, int hp)
        {
            if (source == null || grid == null)
                return false;

            var candidates = HexBattlePathing.GetCoordsInRange(source.State.coord, Mathf.Max(1, radius))
                .Where(coord => grid.TryGetTile(coord, out var tile) &&
                                tile != null &&
                                !coord.Equals(source.State.coord) &&
                                TileCanEnter(tile) &&
                                !IsOccupied(coord, source))
                .OrderBy(_ => Random.value)
                .ToList();

            if (candidates.Count == 0)
                return false;

            if (!grid.TryGetTile(candidates[0], out var chosenTile) || chosenTile == null)
                return false;

            chosenTile.SetProp(HexPropLibrary.DefaultRuinPropId, Mathf.Max(1, hp));
            chosenTile.FlashClick();
            return true;
        }

        private bool TrySummonGoblinMinion(HexBattleUnit source)
        {
            var ownerDefinition = source != null ? HexCardLibrary.GetEnemyDefinition(source.State.enemyDefinitionId) : null;
            int maxSummons = ownerDefinition?.maxSummons > 0 ? ownerDefinition.maxSummons : 2;
            int health = ownerDefinition?.summonHealth > 0 ? ownerDefinition.summonHealth : 15;
            return TrySummonEnemy(source, "goblin", health, maxSummons);
        }

        private bool TrySummonEnemy(HexBattleUnit source, string definitionId, int health, int maxSummons)
        {
            if (source == null || grid == null || string.IsNullOrWhiteSpace(definitionId))
                return false;
            int ownedSummons = _enemyUnits.Count(unit => unit != null && unit.IsAlive && unit.State.isSummonedEnemy && unit.State.summonOwnerId == source.State.id && unit.State.enemyDefinitionId == definitionId);
            if (maxSummons > 0 && ownedSummons >= maxSummons)
                return false;

            var definition = HexCardLibrary.GetEnemyDefinition(definitionId);
            if (definition == null)
                return false;
            var candidates = HexBattlePathing.GetCoordsInRange(source.State.coord, 2)
                .Where(coord => !coord.Equals(source.State.coord) && grid.TryGetTile(coord, out var tile) && tile != null && TileCanEnter(tile) && !IsOccupied(coord, source))
                .OrderBy(coord => HexAxialCoord.Distance(source.State.coord, coord)).ThenBy(_ => Random.value).ToList();
            if (candidates.Count == 0)
                return false;

            var summonRoot = new GameObject($"Summoned_{definitionId}_{_enemyUnits.Count + 1}");
            summonRoot.transform.SetParent(source.transform.parent != null ? source.transform.parent : transform, false);
            var summoned = summonRoot.AddComponent<HexBattleUnit>();
            summoned.Initialize(new HexBattleUnitState
            {
                id = $"enemy_summon_{_enemyUnits.Count + 1}",
                displayName = definition.displayName,
                enemyDefinitionId = definition.id,
                faction = HexBattleFaction.Enemy,
                maxHealth = Mathf.Max(1, health),
                currentHealth = Mathf.Max(1, health),
                armor = 0,
                energy = 0,
                maxEnergy = 0,
                drawPerTurn = 0,
                maxMovePoints = 0,
                currentMovePoints = 0,
                attackRange = definition.attackMaxRange,
                emptyDrawPileStrengthGain = definition.emptyDrawPileStrengthGain,
                isSummonedEnemy = true,
                summonOwnerId = source.State.id,
                coord = candidates[0],
            }, null, definition.deckDefinitions);
            summoned.SnapTo(grid, unitYOffset);
            EnsureEnemyDefinition(summoned);
            _enemyUnits.Add(summoned);
            _units.Add(summoned);
            summoned.RefreshLabel();
            return true;
        }

        private IEnumerator ResolveChieftainQuakeRoutine(HexBattleUnit source, HexCardInstance card)
        {
            if (source == null || card?.definition == null)
                yield break;

            var targets = _units
                .Where(unit => unit != null && unit.IsAlive && unit != source && GetUnitDistance(source, unit) <= Mathf.Max(1, card.definition.effectRadius))
                .ToList();

            source.PlayAttackAnimation();
            yield return new WaitForSeconds(Mathf.Max(0.1f, source.GetAttackDuration() * 0.7f));
            HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
            int totalHealthLost = 0;
            int totalThornsDamage = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive)
                    continue;

                HexDamageResult result = ApplyAttackDamage(
                    source,
                    target,
                    card.EffectiveAmount + Mathf.Max(0, source.State.strength),
                    snapshot);
                totalHealthLost += result.healthLost;
                if (target.IsAlive)
                {
                    totalThornsDamage += Mathf.Max(0, target.State.thorns);
                    var push = ResolveForcedMovement(source, target, 1, false);
                    if (push != null && push.path.Count > 1)
                        yield return MoveUnitRoutine(target, push.path, 0);
                }
            }

            CompleteAttackDamageBatch(source, totalHealthLost);
            if (totalThornsDamage > 0 && source.IsAlive)
                ApplyDamageToUnit(source, totalThornsDamage, null, HexDamageTags.Reaction);
            yield return ResolveDeathsAndBattleEndRoutine();

            ConvertRandomBarrierToRuin(source.State.coord, Mathf.Max(1, card.definition.effectRadius), 4);
        }

        private bool ConvertRandomBarrierToRuin(HexAxialCoord center, int radius, int hp)
        {
            if (grid == null)
                return false;

            var candidates = HexBattlePathing.GetCoordsInRange(center, radius)
                .Where(coord => grid.TryGetTile(coord, out var tile) && tile != null && TileIsBarrier(tile))
                .OrderBy(_ => Random.value)
                .ToList();
            if (candidates.Count == 0)
                return false;

            if (!grid.TryGetTile(candidates[0], out var chosenTile) || chosenTile == null)
                return false;

            chosenTile.SetProp(HexPropLibrary.DefaultRuinPropId, Mathf.Max(1, hp));
            chosenTile.FlashClick();
            return true;
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
                    : HexBattlePathing.FindPath(
                        grid,
                        movingUnit.State.coord,
                        candidate,
                        coord => IsMovementBlocked(coord, movingUnit),
                        (from, to) => IsLivingWallMovementTransitionBlocked(from, to, movingUnit));
                if (path == null || path.Count < 2 || IsLivingWallMovementPathBlocked(path, movingUnit))
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
                if (grid == null || !grid.IsCoordInside(next) || IsMovementBlocked(next, movingUnit) ||
                    IsLivingWallMovementTransitionBlocked(current, next, movingUnit))
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

            if (unit.State.faction == HexBattleFaction.Player)
                ApplyPickupToUnit(tile, unit);
            ApplyTileEffectsToUnit(tile, unit);
        }

        private void ApplyPickupToUnit(HexTile tile, HexBattleUnit unit)
        {
            if (tile == null || unit == null || TilePickupType(tile) == HexTerrainPickupType.None)
                return;

            HexTerrainPickupType pickupType = tile.ConsumePickup(out int amount);
            if (pickupType == HexTerrainPickupType.None)
                return;

            switch (pickupType)
            {
                case HexTerrainPickupType.Heal:
                    unit.Heal(Mathf.Max(1, amount));
                    break;
                case HexTerrainPickupType.TemporaryStrength:
                    unit.GainStrength(Mathf.Max(1, amount));
                    break;
                case HexTerrainPickupType.TemporaryCard:
                    AddGeneratedCardToHand(unit, HexCardLibrary.GetTemporaryThrowingAxe());
                    break;
            }

            unit.RefreshLabel();
            _ui?.Refresh();
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
                        total += Mathf.Max(0, card.EffectiveAmount + enemy.State.strength);
                }
            }

            return total;
        }

        private void RecordCardPlay(HexBattleUnit source, HexBattleUnit target, HexCardInstance card, HexAxialCoord targetCoord)
        {
            if (source == null || card?.definition == null)
                return;

            _playLog.Add(new HexCardPlayLogEntry
            {
                turnOwner = _currentTurn == HexBattleFaction.Player ? "玩家回合" : "敌人回合",
                sourceName = GetUnitDisplayName(source),
                targetName = target != null ? GetUnitDisplayName(target) : $"格子({targetCoord.q},{targetCoord.r})",
                cardName = card.definition.displayName,
            });
        }

        private void GainArmorWithFeedback(HexBattleUnit target, int amount)
        {
            if (target == null || amount <= 0)
                return;

            int beforeArmor = target.State.armor;
            target.GainArmor(amount);
            int gainedArmor = Mathf.Max(0, target.State.armor - beforeArmor);
            if (gainedArmor > 0)
                _ui?.ShowFloatingCombatText(target, HexFloatingFeedbackKind.Armor, gainedArmor);
        }

        private HexDamageResult ApplyDamageWithFeedback(
            HexBattleUnit target,
            int amount,
            HexBattleUnit source,
            HexDamageTags tags = HexDamageTags.Environment,
            HexAttackModifierSnapshot? attackModifierSnapshot = null,
            float targetDamageMultiplier = 1f)
        {
            if (target == null || amount <= 0)
                return HexDamageResult.None(amount);

            int beforeHealth = target.State.currentHealth;
            HexDamageResult result = HexDamageResolver.Resolve(
                new HexDamageRequest(
                    source,
                    target,
                    amount,
                    tags,
                    attackModifierSnapshot,
                    targetDamageMultiplier));
            if (result.armorLost > 0)
                _ui?.ShowFloatingCombatText(target, HexFloatingFeedbackKind.ArmorDamage, result.armorLost);
            if (result.healthLost > 0)
                _ui?.ShowFloatingCombatText(target, HexFloatingFeedbackKind.HealthDamage, result.healthLost);
            else if (result.armorLost <= 0 && beforeHealth == target.State.currentHealth)
                _ui?.ShowFloatingCombatText(target, HexFloatingFeedbackKind.Blocked, 0);

            if ((result.healthLost > 0 || result.armorLost > 0) &&
                target.IsAlive &&
                target != source &&
                source == _activeAttackPassiveSource &&
                _activeAttackPassiveCard?.definition?.cardType == HexCardType.Attack)
            {
                source.ApplyBattleLongAttackPassives(target);
            }

            return result;
        }

        private static string GetUnitDisplayName(HexBattleUnit unit)
        {
            if (unit?.State == null)
                return "未知目标";

            if (!string.IsNullOrWhiteSpace(unit.State.displayName))
                return unit.State.displayName;

            return unit.State.faction == HexBattleFaction.Player ? "玩家" : "敌人";
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
            card.ResetRoundFlags();
            card.ResetActionFlags();
            unit.Deck.DiscardFromHand(card, exhaust);
            if (!exhaust)
                return;

            if (unit.State.profession == HexCardProfession.Warrior)
                NotifyWarriorExhaust(unit);
            else if (unit.State.drawOnExhaust)
                DrawCardsForUnit(unit, 1);

            if (unit.State.armorOnExhaustCost > 0)
                GainArmorWithFeedback(unit, exhaustedCost * unit.State.armorOnExhaustCost);
        }

        private HexDamageResult ApplyDamageToUnit(
            HexBattleUnit target,
            int amount,
            HexBattleUnit source,
            HexDamageTags tags = HexDamageTags.Environment)
        {
            if (target == null || amount <= 0)
                return HexDamageResult.None(amount);

            int beforeHealth = target.State.currentHealth;
            HexDamageResult result = ApplyDamageWithFeedback(target, amount, source, tags);

            int selfHealthLost = Mathf.Max(0, beforeHealth - target.State.currentHealth);
            if (selfHealthLost > 0 && target == source && target.State.gainStrengthOnSelfDamage)
                target.GainStrength(1);

            return result;
        }

        private bool HasRuinInCoords(IEnumerable<HexAxialCoord> coords)
        {
            if (grid == null || coords == null)
                return false;

            foreach (var coord in coords)
            {
                if (FindLivingWallAtCoord(coord) != null)
                    return true;
                if (grid.TryGetTile(coord, out var tile) && tile != null && TileHasRuin(tile))
                    return true;
            }

            return false;
        }

        private void DamageRuinsInCoords(
            IEnumerable<HexAxialCoord> coords,
            int amount,
            IEnumerable<HexBattleUnit> unitTargets = null)
        {
            if (grid == null || coords == null || amount <= 0)
                return;

            var seen = new HashSet<HexAxialCoord>();
            var seenWalls = new HashSet<HexBattleUnit>();
            var excludedWalls = unitTargets != null
                ? new HashSet<HexBattleUnit>(unitTargets.Where(unit => unit != null && unit.IsLivingWall))
                : new HashSet<HexBattleUnit>();
            foreach (var coord in coords)
            {
                if (!seen.Add(coord))
                    continue;
                HexBattleUnit livingWall = FindLivingWallAtCoord(coord);
                if (livingWall != null && !excludedWalls.Contains(livingWall) && seenWalls.Add(livingWall))
                    ApplyLivingWallBreak(livingWall);
                if (!grid.TryGetTile(coord, out var tile) || tile == null || !TileHasRuin(tile))
                    continue;

                tile.DamageStructure(amount, out bool destroyed);
                tile.FlashClick();
                if (destroyed)
                    Debug.Log($"Ruin at {coord.q},{coord.r} was destroyed.");
            }
        }

        private HexDamageResult ApplyAttackDamage(HexBattleUnit source, HexBattleUnit target, int baseDamage)
        {
            HexAttackModifierSnapshot snapshot = BeginAttackDamageBatch(source);
            HexDamageResult result = ApplyAttackDamage(source, target, baseDamage, snapshot);
            CompleteAttackDamageBatch(source, result.healthLost);
            return result;
        }

        private HexAttackModifierSnapshot BeginAttackDamageBatch(HexBattleUnit source)
        {
            HexAttackModifierSnapshot snapshot = HexDamageResolver.CaptureAttackModifiers(source);
            HexDamageResolver.ConsumeAttackModifiers(source, snapshot);
            return snapshot;
        }

        private void CompleteAttackDamageBatch(HexBattleUnit source, int totalHealthLost)
        {
            HexDamageResolver.CompleteAttackBatch(source, totalHealthLost);
        }

        private HexDamageResult ApplyAttackDamage(
            HexBattleUnit source,
            HexBattleUnit target,
            int baseDamage,
            HexAttackModifierSnapshot snapshot)
        {
            if (target == null || baseDamage <= 0)
                return HexDamageResult.None(baseDamage);

            float targetDamageMultiplier =
                target.State.enemyDamageReductionActive && HasAdjacentStructure(target, HexTerrainStructureType.Barrier)
                    ? 0.75f
                    : 1f;
            HexDamageResult result = ApplyDamageWithFeedback(
                target,
                baseDamage,
                source,
                HexDamageTags.Attack,
                snapshot,
                targetDamageMultiplier);

            if (source != null && source.State.enemyIgnitionPassive && target.IsAlive && Random.value < 0.5f)
                target.ApplyBurn(1);
            if (source != null && source.State.enemySpreadActiveThisTurn && target.IsAlive && HasAdjacentStructure(target, HexTerrainStructureType.Ruin))
                target.ApplyBind(1);
            if (!target.IsAlive && target.State.enemyDefinitionId == "mimic" && target.State.enemySpreadActiveThisTurn &&
                grid != null && grid.TryGetTile(target.State.coord, out var deathTile) && deathTile != null)
                deathTile.SetProp(HexPropLibrary.DefaultRuinPropId, 4);

            if (target.State.blastBarrelDamage > 0 && result.finalDamage > 0)
            {
                int blast = target.State.blastBarrelDamage;
                target.State.blastBarrelDamage = 0;
                TriggerBlastBarrel(target, source, blast);
            }

            return result;
        }

        private void TriggerBlastBarrel(HexBattleUnit barreled, HexBattleUnit source, int blastDamage)
        {
            if (barreled == null || blastDamage <= 0)
                return;

            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit == null || !unit.IsAlive || unit == barreled)
                    continue;
                if (GetUnitDistance(barreled, unit) <= 1)
                    ApplyDamageToUnit(unit, blastDamage, source);
            }

            if (barreled.IsAlive)
                ApplyDamageToUnit(barreled, blastDamage, source);
        }

        private bool CanAttackTarget(HexBattleUnit source, HexBattleUnit candidate)
        {
            if (source == null || candidate == null || !candidate.IsAlive)
                return false;

            if (!TryGetRequiredAttackTarget(source, out var requiredTarget))
                return HasLineOfSightToUnit(source, candidate);

            return candidate == requiredTarget && HasLineOfSightToUnit(source, candidate);
        }

        private bool HasLineOfSightToUnit(HexBattleUnit source, HexBattleUnit target)
        {
            if (source == null || target == null)
                return false;

            IReadOnlyList<HexAxialCoord> sourceCoords = source.OccupiedCoords;
            IReadOnlyList<HexAxialCoord> targetCoords = target.OccupiedCoords;
            for (int sourceIndex = 0; sourceIndex < sourceCoords.Count; sourceIndex++)
                for (int targetIndex = 0; targetIndex < targetCoords.Count; targetIndex++)
                    if (HasLineOfSight(sourceCoords[sourceIndex], targetCoords[targetIndex], source, target))
                        return true;
            return false;
        }

        private bool HasLineOfSight(HexAxialCoord sourceCoord, HexAxialCoord targetCoord)
        {
            return HasLineOfSight(sourceCoord, targetCoord, null, null);
        }

        private bool HasLineOfSight(HexAxialCoord sourceCoord, HexAxialCoord targetCoord, HexBattleUnit sourceUnit, HexBattleUnit targetUnit)
        {
            if (grid == null)
                return true;

            int distance = HexAxialCoord.Distance(sourceCoord, targetCoord);
            if (distance <= 1)
                return true;

            var lineCoords = HexBattlePathing.GetLineCoords(grid, sourceCoord, targetCoord, distance);
            for (int i = 0; i < lineCoords.Count; i++)
            {
                var coord = lineCoords[i];
                if (coord.Equals(targetCoord))
                    return true;
                if (grid.TryGetTile(coord, out var tile) && tile != null && TileBlocksLineOfSight(tile))
                    return false;
                var wall = FindLivingWallAtCoord(coord, sourceUnit);
                if (wall != null && wall != targetUnit)
                    return false;
            }

            return true;
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

            return GetConsumableTauntTarget(enemy) ?? _playerUnit;
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
                if (GetUnitDistance(source, preferredTarget) > card.definition.castRange)
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
                if (GetUnitDistance(source, preferredTarget) > card.definition.castRange)
                    return false;

                var areaTargets = GetEnemiesInArea(preferredTarget.State.coord, card.definition.effectRadius, source);
                if (areaTargets.Count == 0)
                    return false;
                if (card.definition.cardType == HexCardType.Attack && !areaTargets.Contains(preferredTarget))
                    return false;

                target = areaTargets[0];
                return true;
            }

            if (GetUnitDistance(source, preferredTarget) > card.definition.castRange)
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
                .OrderBy(unit => GetUnitDistance(source, unit))
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
            if (unit.State.vampirism > 0)
                builder.Append($"  Lifesteal {unit.State.vampirism}");
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

        private void AppendEnemyIntent(StringBuilder builder, HexBattleUnit enemy)
        {
            if (enemy == null || enemy.State == null || enemy.State.faction != HexBattleFaction.Enemy)
                return;

            builder.Append("  Intent ");
            if (_enemyIntentSlots.TryGetValue(enemy, out var slots) && slots != null && slots.Count > 0)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (i > 0)
                        builder.Append(" > ");

                    var slot = slots[i];
                    builder.Append(slot.slotKind);
                    builder.Append(':');
                    builder.Append(slot.card?.definition != null ? slot.card.definition.displayName : "?");
                }
                return;
            }

            if (enemy.Deck.Hand.Count == 0)
                return;

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

                if (unit.Occupies(coord))
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

                var directPath = new List<HexAxialCoord> { unit.State.coord, destination };
                return IsLivingWallMovementPathBlocked(directPath, unit) ? null : directPath;
            }

            return HexBattlePathing.FindPath(
                grid,
                unit.State.coord,
                destination,
                coord => IsMovementBlocked(coord, unit),
                (from, to) => IsLivingWallMovementTransitionBlocked(from, to, unit));
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
            if (grid == null || !grid.IsCoordInside(coord))
                return true;

            if (grid.TryGetTile(coord, out var tile) && tile != null && !TileCanEnter(tile))
                return true;

            if (movingUnit?.State?.faction == HexBattleFaction.Player && FindLivingWallAtCoord(coord, movingUnit) != null)
                return true;

            if (CanIgnoreOccupiedTilesWhileMoving(movingUnit))
                return false;

            return IsOccupied(coord, movingUnit);
        }

        private bool IsLivingWallMovementTransitionBlocked(
            HexAxialCoord from,
            HexAxialCoord to,
            HexBattleUnit movingUnit)
        {
            if (grid == null || movingUnit?.State?.faction != HexBattleFaction.Player)
                return false;

            List<HexBattleUnit> walls = GetLivingWalls();
            for (int i = 0; i < walls.Count; i++)
            {
                if (HexLivingWallRules.MovementSegmentCrossesWall(grid, from, to, walls[i].OccupiedCoords))
                    return true;
            }

            return false;
        }

        private bool IsLivingWallMovementPathBlocked(
            IReadOnlyList<HexAxialCoord> path,
            HexBattleUnit movingUnit)
        {
            if (path == null || path.Count < 2)
                return false;

            for (int i = 1; i < path.Count; i++)
                if (IsLivingWallMovementTransitionBlocked(path[i - 1], path[i], movingUnit))
                    return true;

            return false;
        }

        private bool IsForcedMovementBlocked(HexAxialCoord coord, HexBattleUnit movingUnit)
        {
            if (grid == null || !grid.IsCoordInside(coord))
                return true;

            if (grid.TryGetTile(coord, out var tile) && tile != null && !TileCanEnter(tile))
                return true;

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
            if (target.IsLivingWall)
                return ResolveLivingWallForcedMovement(source, target, distance, moveTowardSource);
            if (target.State.toughness > 0 || target.State.cannotBeKnockedBackThisTurn)
            {
                return new ForcedMovementResult
                {
                    path = new List<HexAxialCoord> { target.State.coord },
                    intendedDestination = target.State.coord,
                    actualDestination = target.State.coord,
                    collided = true,
                };
            }

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
                    if (IsForcedMovementBlocked(neighbor, movingUnit) ||
                        IsLivingWallMovementTransitionBlocked(current, neighbor, movingUnit))
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

            return HexBattlePathing.FindPath(
                grid,
                start,
                destination,
                coord => IsForcedMovementBlocked(coord, movingUnit),
                (from, to) => IsLivingWallMovementTransitionBlocked(from, to, movingUnit));
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

                if (unit.Occupies(coord))
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

                if (GetDistanceToUnit(center, enemy) <= radius)
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

                if (GetDistanceToUnit(center, unit) <= radius)
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

                if (unit.OccupiedCoords.Any(coveredCoords.Contains))
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

                bool blocksLine = grid.TryGetTile(current, out var currentTile) &&
                                  currentTile != null &&
                                  TileBlocksLineOfSight(currentTile);
                blocksLine |= FindLivingWallAtCoord(current) != null;
                if (blocksLine)
                    break;

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
                TryGetHoveredAttackTarget(_playerUnit, _draggedCard.definition, out var hoveredTarget) &&
                hoveredTarget != null)
            {
                if (grid.TryGetTile(hoveredTarget.TargetCoord, out var hoveredUnitTile))
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

            unit = _units.FirstOrDefault(candidate => candidate.IsAlive && candidate.Occupies(tile.coord));
            return unit != null;
        }

        private bool TryGetHoveredAttackTarget(HexBattleUnit attacker, HexCardDefinition definition, out IHexAttackTarget target)
        {
            target = null;
            if (TryGetHoveredUnit(out var unit) && unit != null && unit.IsAttackTargetValid)
            {
                target = unit;
                return true;
            }

            if (!TryGetHoveredTile(out var tile, out _))
                return false;
            if (CanAttackRuinTile(attacker, definition, tile))
            {
                target = tile;
                return true;
            }

            return false;
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
                    if (RequiresTraversableTileTarget(_draggedCard.definition) && !CanUseAsMovementTarget(tile))
                        targetable = false;
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
                    : _enemyUnits.Any(enemy => enemy != null && enemy.IsAlive && enemy.Occupies(coord));
                if (!targetable &&
                    _draggedCard.definition.cardType == HexCardType.Attack &&
                    _draggedCard.definition.targetType == HexCardTargetType.EnemyUnit)
                {
                    targetable = CanAttackRuinTile(_playerUnit, _draggedCard.definition, tile);
                }
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

            if (_draggedCard == null || !IsTileActionCard(_draggedCard.definition) || _currentTurn != HexBattleFaction.Player || _busy || !_playerUnit.IsAlive)
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

            int cardMoveRange = Mathf.Max(1, _draggedCard.EffectiveAmount);
            var reachable = GetReachableCosts(_playerUnit, cardMoveRange);
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
                    cost <= cardMoveRange &&
                    !IsOccupied(tile.coord, _playerUnit);
                tile.SetMoveIndicator(canReach, canReach);
            }

            if (_hoveredTile != null && !_hoveredTile.coord.Equals(_playerUnit.State.coord))
                ApplyMovementPreview(reachable, _hoveredTile.coord, cardMoveRange);
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

        private void ApplyMovementPreview(Dictionary<HexAxialCoord, int> reachable, HexAxialCoord hoveredCoord, int maxMoveCost)
        {
            foreach (var tile in grid.Tiles.Values)
                tile.SetPathPreview(false, false, false);

            if (IsMovementDestinationBlocked(hoveredCoord, _playerUnit))
            {
                if (grid.TryGetTile(hoveredCoord, out var occupiedTile))
                    occupiedTile.SetPathPreview(true, true, true);
                return;
            }

            var path = BuildCardMovementPath(_playerUnit, hoveredCoord, maxMoveCost);
            bool canReach = path != null &&
                            path.Count >= 2 &&
                            reachable.TryGetValue(hoveredCoord, out int hoveredCost) &&
                            hoveredCost <= maxMoveCost;

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
            return GetReachableCosts(unit, unit != null ? unit.State.currentMovePoints : 0);
        }

        private Dictionary<HexAxialCoord, int> GetReachableCosts(HexBattleUnit unit, int maxMoveCost)
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

                    var directPath = new List<HexAxialCoord> { unit.State.coord, tile.coord };
                    if (IsLivingWallMovementPathBlocked(directPath, unit))
                        continue;

                    int distance = GetDistanceToUnit(tile.coord, unit);
                    if (distance <= maxMoveCost)
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
                if (currentCost >= maxMoveCost)
                    continue;

                foreach (var neighbor in grid.GetNeighbors(current))
                {
                    if (!grid.IsCoordInside(neighbor) || IsMovementDestinationBlocked(neighbor, unit) ||
                        IsLivingWallMovementTransitionBlocked(current, neighbor, unit))
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
