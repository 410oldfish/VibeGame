using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexDemo
{
    /// <summary>
    /// Screen-space battle HUD. World-space unit and terrain bars intentionally remain uGUI.
    /// </summary>
    internal sealed class HexBattleToolkitUI : MonoBehaviour, IBattleHudView
    {
        private sealed class CardElement
        {
            internal readonly VisualElement root;
            internal readonly Label cost;
            internal readonly Label title;
            internal readonly Label description;
            internal HexCardInstance card;
            internal int pointerId = -1;
            internal bool playable;

            internal CardElement(VisualElement root)
            {
                this.root = root;
                cost = root.Q<Label>("cost");
                title = root.Q<Label>("name");
                description = root.Q<Label>("description");
            }
        }

        private HexBattleController _controller;
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _topBar;
        private VisualElement _enemyList;
        private VisualElement _actionBar;
        private VisualElement _hand;
        private VisualElement _feedbackLayer;
        private VisualElement _modalLayer;
        private Label _phaseLabel;
        private Label _playerLabel;
        private VisualElement _playerStatuses;
        private Button _endTurnButton;
        private Button _drawButton;
        private Button _discardButton;
        private Button _exhaustButton;
        private Button _logButton;
        private VisualElement _consumables;
        private readonly Dictionary<string, CardElement> _cards = new();
        private readonly List<VisualElement> _feedbackPool = new();
        private VisualElement _activeModal;
        private bool _callbacksRegistered;
        private int _playerStatusHash = int.MinValue;
        private int _enemyHash = int.MinValue;
        private int _consumableHash = int.MinValue;

        public GameObject Host => gameObject;

        public void Initialize(HexBattleController controller)
        {
            _controller = controller;
            _document = HexUiToolkitRuntime.AttachDocument(gameObject, "BattleRoot", 100);
            BuildOrQueryTree();
            RegisterCallbacks();
            Refresh();
        }

        private void OnEnable()
        {
            if (_root != null)
                RegisterCallbacks();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            RemoveActiveModal();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseTopModal();
        }

        private void BuildOrQueryTree()
        {
            _root = _document.rootVisualElement;
            HexUiToolkitRuntime.PrepareRoot(_root);
            var screen = _root.Q<VisualElement>("battle-root");
            if (screen == null)
            {
                screen = new VisualElement { name = "battle-root", pickingMode = PickingMode.Ignore };
                screen.AddToClassList("hex-screen");
                _root.Add(screen);
            }

            _topBar = EnsureChild(screen, "top-bar", "hex-topbar", "hex-panel");
            _enemyList = EnsureChild(screen, "enemy-list", "hex-enemies");
            _actionBar = EnsureChild(screen, "action-bar", "hex-actions");
            _hand = EnsureChild(screen, "hand", "hex-hand");
            _feedbackLayer = EnsureChild(screen, "feedback-layer", "hex-screen");
            _modalLayer = EnsureChild(screen, "modal-layer", "hex-screen");
            _enemyList.pickingMode = PickingMode.Ignore;
            _hand.pickingMode = PickingMode.Ignore;
            _feedbackLayer.pickingMode = PickingMode.Ignore;
            _modalLayer.pickingMode = PickingMode.Ignore;

            _topBar.Clear();
            _phaseLabel = NewLabel("phase", "hex-subtitle");
            _playerLabel = NewLabel("player", "hex-grow");
            _playerStatuses = new VisualElement { name = "player-statuses", pickingMode = PickingMode.Ignore };
            _playerStatuses.AddToClassList("hex-row");
            _topBar.Add(_phaseLabel);
            _topBar.Add(_playerLabel);
            _topBar.Add(_playerStatuses);

            _actionBar.Clear();
            _drawButton = NewButton("draw", "抽牌 0");
            _discardButton = NewButton("discard", "弃牌 0");
            _exhaustButton = NewButton("exhaust", "消耗 0");
            _logButton = NewButton("log", "记录");
            _endTurnButton = NewButton("end-turn", "结束回合");
            _actionBar.Add(_drawButton);
            _actionBar.Add(_discardButton);
            _actionBar.Add(_exhaustButton);
            _actionBar.Add(_logButton);
            _actionBar.Add(_endTurnButton);

            _consumables = new VisualElement { name = "consumables" };
            _consumables.AddToClassList("hex-row");
            _consumables.style.position = Position.Absolute;
            _consumables.style.left = 18;
            _consumables.style.bottom = 278;
            screen.Add(_consumables);
        }

        private void RegisterCallbacks()
        {
            if (_callbacksRegistered || _endTurnButton == null)
                return;
            _callbacksRegistered = true;
            _endTurnButton.clicked += OnEndTurn;
            _drawButton.clicked += OnDrawPile;
            _discardButton.clicked += OnDiscardPile;
            _exhaustButton.clicked += OnExhaustPile;
            _logButton.clicked += OnLog;
        }

        private void UnregisterCallbacks()
        {
            if (!_callbacksRegistered)
                return;
            _callbacksRegistered = false;
            _endTurnButton.clicked -= OnEndTurn;
            _drawButton.clicked -= OnDrawPile;
            _discardButton.clicked -= OnDiscardPile;
            _exhaustButton.clicked -= OnExhaustPile;
            _logButton.clicked -= OnLog;
        }

        private void OnEndTurn() => _controller?.RequestEndTurn();
        private void OnDrawPile() => OpenCardList("抽牌堆", _controller.GetLocalDrawPile());
        private void OnDiscardPile() => OpenCardList("弃牌堆", _controller.GetLocalDiscardPile());
        private void OnExhaustPile() => OpenCardList("消耗堆", _controller.GetLocalExhaustPile());
        private void OnLog()
        {
            var entries = _controller.GetPlayLog();
            var lines = entries.Select(entry => $"{entry.turnOwner} · {entry.sourceName} → {entry.targetName} · {entry.cardName}").ToList();
            OpenStringList("出牌记录", lines);
        }

        public void Refresh()
        {
            if (_controller == null || _root == null)
                return;

            var snapshot = _controller.GetBattleHudSnapshot();
            _phaseLabel.text = snapshot.phaseLabel;
            _playerLabel.text = $"{snapshot.player.displayName}  生命 {snapshot.player.currentHealth}/{snapshot.player.maxHealth}  护甲 {snapshot.player.armor}  能量 {snapshot.player.energy}/{snapshot.player.maxEnergy}  力量 {snapshot.player.power}";
            _endTurnButton.SetEnabled(snapshot.canEndTurn);
            _drawButton.text = $"抽牌 {snapshot.piles.draw}";
            _discardButton.text = $"弃牌 {snapshot.piles.discard}";
            _exhaustButton.text = $"消耗 {snapshot.piles.exhaust}";
            int playerStatusHash = GetStatusHash(snapshot.player.statuses);
            if (_playerStatusHash != playerStatusHash)
            {
                _playerStatusHash = playerStatusHash;
                RefreshBadges(_playerStatuses, snapshot.player.statuses);
            }
            RefreshEnemies(snapshot);
            RefreshHand(_controller.GetLocalHand());
            RefreshConsumables();
        }

        private void RefreshEnemies(BattleHudSnapshot snapshot)
        {
            int hash = 17;
            for (int i = 0; i < snapshot.enemies.Count; i++)
            {
                var enemy = snapshot.enemies[i];
                hash = hash * 31 + (enemy.displayName?.GetHashCode() ?? 0);
                hash = hash * 31 + enemy.currentHealth;
                hash = hash * 31 + enemy.maxHealth;
                hash = hash * 31 + enemy.armor;
                hash = hash * 31 + GetStatusHash(enemy.statuses);
                for (int slot = 0; slot < enemy.intentSlots.Count; slot++)
                {
                    var intent = enemy.intentSlots[slot];
                    hash = hash * 31 + (intent.cardName?.GetHashCode() ?? 0);
                    hash = hash * 31 + intent.cardCost;
                    hash = hash * 31 + intent.executionOrder;
                    hash = hash * 31 + (intent.isEmpty ? 1 : 0);
                }
            }
            if (_enemyHash == hash)
                return;
            _enemyHash = hash;
            _enemyList.Clear();
            for (int i = 0; i < snapshot.enemies.Count; i++)
            {
                var enemy = snapshot.enemies[i];
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("hex-panel");
                row.style.marginBottom = 8;
                row.Add(NewLabel(null, "hex-subtitle", $"{enemy.displayName}  HP {enemy.currentHealth}/{enemy.maxHealth}  护甲 {enemy.armor}"));
                var intents = new VisualElement { pickingMode = PickingMode.Ignore };
                intents.AddToClassList("hex-row");
                for (int slot = 0; slot < enemy.intentSlots.Count; slot++)
                {
                    var intent = enemy.intentSlots[slot];
                    var text = intent.isEmpty ? $"{intent.slotLabel}: —" : $"{intent.executionOrder}. {intent.slotLabel}: {intent.cardName} [{intent.cardCost}]";
                    intents.Add(NewLabel(null, "hex-badge", text));
                }
                row.Add(intents);
                var statuses = new VisualElement { pickingMode = PickingMode.Ignore };
                statuses.AddToClassList("hex-row");
                RefreshBadges(statuses, enemy.statuses);
                row.Add(statuses);
                _enemyList.Add(row);
            }
        }

        private static void RefreshBadges(VisualElement root, IReadOnlyList<BattleStatusEntry> statuses)
        {
            root.Clear();
            if (statuses == null)
                return;
            for (int i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                var badge = NewLabel(null, "hex-badge", $"{status.shortLabel} {status.stacks}");
                badge.tooltip = status.tooltip;
                root.Add(badge);
            }
        }

        private static int GetStatusHash(IReadOnlyList<BattleStatusEntry> statuses)
        {
            int hash = 17;
            if (statuses == null)
                return hash;
            for (int i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                hash = hash * 31 + (int)status.kind;
                hash = hash * 31 + status.stacks;
                hash = hash * 31 + (status.isPermanent ? 1 : 0);
            }
            return hash;
        }

        private void RefreshHand(IReadOnlyList<HexCardInstance> hand)
        {
            var live = new HashSet<string>();
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card == null || string.IsNullOrEmpty(card.runtimeId))
                    continue;
                live.Add(card.runtimeId);
                if (!_cards.TryGetValue(card.runtimeId, out var view))
                {
                    view = CreateCardElement(card);
                    _cards.Add(card.runtimeId, view);
                }
                BindCard(view, card);
                _hand.Add(view.root);
            }

            foreach (string id in _cards.Keys.Where(id => !live.Contains(id)).ToList())
            {
                _cards[id].root.RemoveFromHierarchy();
                _cards.Remove(id);
            }
        }

        private CardElement CreateCardElement(HexCardInstance card)
        {
            var asset = HexUiToolkitRuntime.LoadTemplate("Card");
            var root = asset != null ? asset.Instantiate().Q<VisualElement>("card") : null;
            if (root == null)
            {
                root = new VisualElement { name = "card" };
                root.AddToClassList("hex-card");
                root.Add(NewLabel("cost", "hex-card__cost"));
                root.Add(NewLabel("name", "hex-card__name"));
                root.Add(NewLabel("description", "hex-card__description"));
            }
            root.pickingMode = PickingMode.Position;
            var view = new CardElement(root) { card = card };
            root.RegisterCallback<PointerDownEvent>(evt => BeginCardPointer(view, evt));
            root.RegisterCallback<PointerMoveEvent>(evt => MoveCardPointer(view, evt));
            root.RegisterCallback<PointerUpEvent>(evt => EndCardPointer(view, evt));
            root.RegisterCallback<PointerCaptureOutEvent>(_ => CancelCardPointer(view));
            return view;
        }

        private void BindCard(CardElement view, HexCardInstance card)
        {
            view.card = card;
            int cost = _controller.GetLocalCardCost(card);
            view.cost.text = cost.ToString();
            view.title.text = card.definition?.displayName ?? "未知卡牌";
            view.description.text = card.definition?.description ?? string.Empty;
            view.root.style.backgroundColor = card.definition != null ? Color.Lerp(card.definition.color, new Color(0.1f, 0.12f, 0.16f), 0.35f) : new Color(0.2f, 0.22f, 0.27f);
            bool playable = card.definition != null && !card.definition.isUnplayable && _controller.GetLocalPlayerState()?.energy >= cost;
            view.playable = playable;
            view.root.EnableInClassList("hex-card--disabled", !playable);
            view.root.tooltip = playable ? card.definition.description : "当前不可打出";
        }

        private void BeginCardPointer(CardElement view, PointerDownEvent evt)
        {
            if (evt.button != 0 || view.card?.definition == null || !view.playable)
                return;
            view.pointerId = evt.pointerId;
            view.root.CapturePointer(evt.pointerId);
            view.root.style.opacity = 0.35f;
            _controller.BeginCardDrag(view.card);
            evt.StopPropagation();
        }

        private void MoveCardPointer(CardElement view, PointerMoveEvent evt)
        {
            if (view.pointerId != evt.pointerId)
                return;
            _controller.UpdateDraggedCard(Input.mousePosition);
            evt.StopPropagation();
        }

        private void EndCardPointer(CardElement view, PointerUpEvent evt)
        {
            if (view.pointerId != evt.pointerId)
                return;
            view.pointerId = -1;
            view.root.ReleasePointer(evt.pointerId);
            view.root.style.opacity = 1f;
            _controller.EndCardDrag(Input.mousePosition);
            evt.StopPropagation();
        }

        private void CancelCardPointer(CardElement view)
        {
            if (view.pointerId < 0)
                return;
            view.pointerId = -1;
            view.root.style.opacity = 1f;
            _controller?.CancelCardDrag();
        }

        private void RefreshConsumables()
        {
            var prompt = _controller.GetConsumableTargetPrompt();
            var items = _controller.GetConsumables();
            var player = _controller.GetLocalPlayerState();
            int hash = prompt?.GetHashCode() ?? 0;
            hash = hash * 31 + (_controller.CanSelectConsumables() ? 1 : 0);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                hash = hash * 31 + (item?.runtimeId?.GetHashCode() ?? 0);
                hash = hash * 31 + (item?.remainingUses ?? 0);
                hash = hash * 31 + (item != null && _controller.IsConsumableSelected(item.runtimeId) ? 1 : 0);
            }
            hash = hash * 31 + (player?.flyingSecretTurns ?? 0);
            hash = hash * 31 + (player?.stealSecretTurns ?? 0);
            if (_consumableHash == hash)
                return;
            _consumableHash = hash;

            _consumables.Clear();
            if (!string.IsNullOrWhiteSpace(prompt))
                _consumables.Add(NewLabel(null, "hex-badge", prompt));
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item?.Definition == null)
                    continue;
                string id = item.runtimeId;
                var button = NewButton(null, $"{item.Definition.displayName} ×{item.remainingUses}");
                button.SetEnabled(_controller.CanSelectConsumables() && item.remainingUses > 0);
                button.EnableInClassList("hex-badge", _controller.IsConsumableSelected(id));
                button.clicked += () => _controller.RequestUseConsumable(id);
                _consumables.Add(button);
            }

            if (player != null && player.flyingSecretTurns > 0)
            {
                var flying = NewButton(null, "飞行秘技 [1]");
                flying.clicked += _controller.RequestUseFlyingSecretSkill;
                _consumables.Add(flying);
            }
            if (player != null && player.stealSecretTurns > 0)
            {
                var steal = NewButton(null, "窃取秘技 [1]");
                steal.clicked += _controller.RequestUseStealSecretSkill;
                _consumables.Add(steal);
            }
        }

        public bool IsEnemyIntentPopupOpen() => _activeModal != null && _activeModal.name == "enemy-hand-modal";

        public void OpenEnemyHandPopup(HexBattleUnit enemy, Vector2 screenPosition)
        {
            if (enemy == null)
                return;
            OpenCardList("敌方手牌", _controller.GetEnemyHand(enemy), "enemy-hand-modal");
        }

        public bool IsBlockingWorldClick() => _activeModal != null;

        public void OpenTerrainDetailPopup(HexTile tile, Vector2 screenPosition)
        {
            if (tile == null)
                return;
            var lines = new List<string>
            {
                $"坐标 ({tile.coord.q}, {tile.coord.r})",
                $"区域：{tile.zone}",
                $"通行：{(tile.BlocksMovement ? "阻挡" : "可进入")}",
                $"视线：{(tile.BlocksLineOfSight ? "阻挡" : "不阻挡")}",
            };
            if (tile.HasRuin)
                lines.Add($"遗迹 HP：{tile.structureHp}/{Mathf.Max(tile.structureMaxHp, tile.structureHp)}");
            OpenStringList("地形详情", lines, "terrain-modal");
        }

        public void CloseTerrainDetailPopup()
        {
            if (_activeModal?.name == "terrain-modal")
                CloseTopModal();
        }

        public void CloseEnemyHandPopup()
        {
            if (_activeModal?.name == "enemy-hand-modal")
                CloseTopModal();
        }

        public void CloseTopModal()
        {
            if (_activeModal != null)
            {
                RemoveActiveModal();
                return;
            }
            _controller?.CancelConsumableTargeting();
        }

        private void RemoveActiveModal()
        {
            _activeModal?.RemoveFromHierarchy();
            _activeModal = null;
            if (_modalLayer != null)
                _modalLayer.pickingMode = PickingMode.Ignore;
        }

        private void OpenCardList(string title, IReadOnlyList<HexCardInstance> cards, string modalName = "card-list-modal")
        {
            var lines = new List<string>();
            if (cards != null)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    lines.Add(card?.definition == null ? "未知卡牌" : $"[{card.definition.energyCost}] {card.definition.displayName} — {card.definition.description}");
                }
            }
            OpenStringList(title, lines, modalName);
        }

        private void OpenStringList(string title, IList<string> lines, string modalName = "string-list-modal")
        {
            RemoveActiveModal();
            var mask = new VisualElement { name = modalName, pickingMode = PickingMode.Position };
            mask.AddToClassList("hex-modal-mask");
            var panel = new VisualElement();
            panel.AddToClassList("hex-panel");
            panel.AddToClassList("hex-modal");
            panel.Add(NewLabel(null, "hex-title", title));
            var items = lines != null ? new List<string>(lines) : new List<string>();
            var list = new ListView(items, 38, () => NewLabel(null, "hex-muted"), (element, index) => ((Label)element).text = items[index]);
            list.AddToClassList("hex-modal__content");
            panel.Add(list);
            var close = NewButton(null, "关闭");
            close.clicked += CloseTopModal;
            panel.Add(close);
            mask.Add(panel);
            mask.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == mask)
                    CloseTopModal();
            });
            _modalLayer.Add(mask);
            _modalLayer.pickingMode = PickingMode.Position;
            _activeModal = mask;
        }

        public void ShowFloatingCombatText(HexBattleUnit unit, HexFloatingFeedbackKind kind, int amount)
        {
            if (unit == null)
                return;
            string text = kind switch
            {
                HexFloatingFeedbackKind.Armor => $"护甲 +{amount}",
                HexFloatingFeedbackKind.ArmorDamage => $"护甲 -{amount}",
                HexFloatingFeedbackKind.Blocked => "格挡 0",
                _ => $"生命 -{amount}",
            };
            StartCoroutine(AnimateFeedback(unit.GetTargetPoint() + Vector3.up * 0.35f, text, 0.85f));
        }

        public void ShowPlayedCard(HexBattleUnit source, HexCardInstance card)
        {
            if (source == null || card?.definition == null)
                return;
            StartCoroutine(AnimateFeedback(source.GetTargetPoint(), $"[{card.definition.energyCost}] {card.definition.displayName}", 1.05f));
        }

        private IEnumerator AnimateFeedback(Vector3 world, string value, float duration)
        {
            var element = AcquireFeedback();
            element.Q<Label>().text = value;
            Camera camera = _controller.rayCamera != null ? _controller.rayCamera : Camera.main;
            Vector2 position = camera != null && _root.panel != null
                ? RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, camera)
                : new Vector2(_root.resolvedStyle.width * 0.5f, _root.resolvedStyle.height * 0.5f);
            element.style.left = position.x;
            element.style.top = position.y;
            element.style.display = DisplayStyle.Flex;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                element.style.top = position.y - 76f * t;
                element.style.opacity = 1f - Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            element.style.display = DisplayStyle.None;
            element.style.opacity = 1f;
        }

        private VisualElement AcquireFeedback()
        {
            for (int i = 0; i < _feedbackPool.Count; i++)
            {
                if (_feedbackPool[i].resolvedStyle.display == DisplayStyle.None)
                    return _feedbackPool[i];
            }
            var element = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform | UsageHints.DynamicColor };
            element.AddToClassList("hex-feedback");
            element.Add(new Label());
            _feedbackLayer.Add(element);
            _feedbackPool.Add(element);
            return element;
        }

        private static VisualElement EnsureChild(VisualElement parent, string name, params string[] classes)
        {
            var element = parent.Q<VisualElement>(name);
            if (element == null)
            {
                element = new VisualElement { name = name };
                parent.Add(element);
            }
            for (int i = 0; i < classes.Length; i++)
                element.AddToClassList(classes[i]);
            return element;
        }

        private static Label NewLabel(string name, string className, string text = "")
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            if (!string.IsNullOrEmpty(className))
                label.AddToClassList(className);
            return label;
        }

        private static Button NewButton(string name, string text)
        {
            var button = new Button { name = name, text = text };
            button.AddToClassList("hex-button");
            return button;
        }
    }
}
