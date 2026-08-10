using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexDemo
{
    internal sealed class HexUiChoice
    {
        internal string title;
        internal string body;
        internal HexCardDefinition card;
        internal string cardCaption;
        internal bool enabled = true;
        internal Action action;
    }

    internal sealed class HexShopOfferView
    {
        internal HexCardDefinition card;
        internal int price;
    }

    internal sealed class HexAdventureToolkitView : MonoBehaviour, IAdventureView
    {
        private sealed class MapEdgeLayer : VisualElement
        {
            private HexMapData _map;

            internal MapEdgeLayer()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0;
                style.right = 0;
                style.top = 0;
                style.bottom = 0;
                generateVisualContent += Draw;
            }

            internal void SetMap(HexMapData map)
            {
                _map = map;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                if (_map == null)
                    return;
                var painter = context.painter2D;
                painter.lineWidth = 5f;
                painter.strokeColor = new Color(0.45f, 0.5f, 0.62f, 0.82f);
                for (int i = 0; i < _map.nodes.Count; i++)
                {
                    var source = _map.nodes[i];
                    for (int edge = 0; edge < source.outgoingNodeIds.Count; edge++)
                    {
                        var target = _map.GetNode(source.outgoingNodeIds[edge]);
                        if (target == null)
                            continue;
                        painter.BeginPath();
                        painter.MoveTo(ToContentPoint(source.uiPosition));
                        painter.LineTo(ToContentPoint(target.uiPosition));
                        painter.Stroke();
                    }
                }
            }
        }

        private const float MapWidth = 1400f;
        private const float MapHeight = 1600f;
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _toolbar;
        private VisualElement _viewport;
        private VisualElement _mapContent;
        private VisualElement _overlayLayer;
        private Label _summary;
        private readonly Dictionary<string, Button> _nodes = new();
        private Vector2 _pan = new(0f, -510f);
        private int _panPointerId = -1;
        private Vector2 _lastPointer;

        public bool IsOverlayOpen => _overlayLayer != null && _overlayLayer.childCount > 0;

        public void Initialize()
        {
            if (_document != null)
                return;
            _document = HexUiToolkitRuntime.AttachDocument(gameObject, "AdventureRoot", 80);
            _root = _document.rootVisualElement;
            HexUiToolkitRuntime.PrepareRoot(_root);
            var screen = _root.Q<VisualElement>("adventure-root") ?? _root;
            _toolbar = screen.Q<VisualElement>("map-toolbar") ?? NewChild(screen, "map-toolbar", "hex-map-toolbar", "hex-panel");
            _viewport = screen.Q<VisualElement>("map-viewport") ?? NewChild(screen, "map-viewport", "hex-map-canvas");
            _overlayLayer = screen.Q<VisualElement>("overlay-layer") ?? NewChild(screen, "overlay-layer", "hex-screen");
            _overlayLayer.pickingMode = PickingMode.Ignore;
            _viewport.RegisterCallback<PointerDownEvent>(OnMapPointerDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnMapPointerMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnMapPointerUp);
        }

        private void OnDisable()
        {
            _panPointerId = -1;
        }

        public void ShowProfessionSelection(string networkStatus, Action<HexCardProfession> choose)
        {
            Initialize();
            _root.style.display = DisplayStyle.Flex;
            _toolbar.style.display = DisplayStyle.None;
            _viewport.style.display = DisplayStyle.None;
            ClearOverlay();
            _overlayLayer.pickingMode = PickingMode.Position;
            var mask = NewOverlay("profession-selection");
            var panel = NewPanel(mask);
            panel.style.width = 1120;
            panel.Add(NewLabel("选择职业", "hex-title"));
            panel.Add(NewLabel("职业决定初始牌组，并限制后续奖励和商店卡牌。", "hex-muted"));
            panel.Add(NewLabel(networkStatus, "hex-muted"));
            var choices = new VisualElement();
            choices.AddToClassList("hex-choice-list");
            AddProfession(choices, HexCardProfession.Warrior, "战士", "武器切换、力量、击飞和范围攻击。", choose);
            AddProfession(choices, HexCardProfession.Paladin, "骑士", "护甲、防守反击和神圣打击。", choose);
            AddProfession(choices, HexCardProfession.Druid, "德鲁伊", "变形、地块效果、燃烧和自然控制。", choose);
            panel.Add(choices);
        }

        public void HideProfessionSelection()
        {
            if (_overlayLayer?.Q<VisualElement>("profession-selection") != null)
                ClearOverlay();
        }

        public void BuildMap(HexMapData map, string summary, string currentNodeId, ISet<string> visited, Action<string> enterNode)
        {
            Initialize();
            _root.style.display = DisplayStyle.Flex;
            _toolbar.style.display = DisplayStyle.Flex;
            _viewport.style.display = DisplayStyle.Flex;
            _toolbar.Clear();
            _toolbar.Add(NewLabel("冒险地图", "hex-title"));
            _summary = NewLabel(summary, "hex-grow");
            _toolbar.Add(_summary);
            _viewport.Clear();
            _mapContent = new VisualElement { name = "map-content", pickingMode = PickingMode.Ignore };
            _mapContent.style.position = Position.Absolute;
            _mapContent.style.width = MapWidth;
            _mapContent.style.height = MapHeight;
            var edges = new MapEdgeLayer();
            edges.SetMap(map);
            _mapContent.Add(edges);
            _nodes.Clear();
            for (int i = 0; i < map.nodes.Count; i++)
            {
                var node = map.nodes[i];
                string id = node.id;
                var button = new Button(() => enterNode(id))
                {
                    name = id,
                    text = $"{GetNodeSymbol(node.nodeType)}  {GetNodeLabel(node.nodeType)}",
                };
                button.AddToClassList("hex-map-node");
                Vector2 point = ToContentPoint(node.uiPosition);
                button.style.left = point.x - 56f;
                button.style.top = point.y - 32f;
                _mapContent.Add(button);
                _nodes.Add(id, button);
            }
            _viewport.Add(_mapContent);
            ApplyPan();
            RefreshMap(summary, map, currentNodeId, visited);
        }

        public void RefreshMap(string summary, HexMapData map, string currentNodeId, ISet<string> visited)
        {
            if (_summary != null)
                _summary.text = summary;
            var current = map?.GetNode(currentNodeId);
            var available = current != null ? new HashSet<string>(current.outgoingNodeIds) : new HashSet<string>();
            foreach (var pair in _nodes)
            {
                bool isCurrent = pair.Key == currentNodeId;
                bool isVisited = visited != null && visited.Contains(pair.Key);
                pair.Value.SetEnabled(available.Contains(pair.Key));
                pair.Value.EnableInClassList("hex-map-node--current", isCurrent);
                pair.Value.EnableInClassList("hex-map-node--visited", isVisited && !isCurrent);
            }
        }

        public void ShowMap()
        {
            Initialize();
            _root.style.display = DisplayStyle.Flex;
            _toolbar.style.display = DisplayStyle.Flex;
            _viewport.style.display = DisplayStyle.Flex;
        }

        public void HideMapForRoom()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        public void ShowOverlay(string title, string body, IReadOnlyList<HexUiChoice> choices)
        {
            Initialize();
            _root.style.display = DisplayStyle.Flex;
            ClearOverlay();
            _overlayLayer.pickingMode = PickingMode.Position;
            var mask = NewOverlay("adventure-modal");
            var panel = NewPanel(mask);
            bool hasCardChoices = ContainsCardChoices(choices);
            if (hasCardChoices)
                panel.AddToClassList("hex-modal--cards");
            panel.Add(NewLabel(title, "hex-title"));
            if (!string.IsNullOrWhiteSpace(body))
                panel.Add(NewLabel(body, "hex-muted"));
            VisualElement choiceRoot;
            if (hasCardChoices)
            {
                var cardScroll = new ScrollView(ScrollViewMode.Horizontal)
                {
                    name = "card-choice-scroll",
                    horizontalScrollerVisibility = ScrollerVisibility.Auto,
                    verticalScrollerVisibility = ScrollerVisibility.Hidden,
                };
                cardScroll.AddToClassList("hex-card-choice-scroll");
                cardScroll.contentContainer.AddToClassList("hex-card-choice-row");
                choiceRoot = cardScroll;
            }
            else
            {
                choiceRoot = new VisualElement();
                choiceRoot.AddToClassList("hex-choice-list");
            }
            if (choices != null)
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    var choice = choices[i];
                    if (choice.card != null && choice.card.profession == HexCardProfession.Warrior)
                    {
                        var cardButton = CreateCardChoiceButton(choice);
                        choiceRoot.Add(cardButton);
                        continue;
                    }

                    var button = new Button(() => choice.action?.Invoke()) { text = string.IsNullOrWhiteSpace(choice.body) ? choice.title : $"{choice.title}\n{choice.body}" };
                    button.AddToClassList("hex-button");
                    button.style.width = 250;
                    button.style.minHeight = 72;
                    button.SetEnabled(choice.enabled);
                    choiceRoot.Add(button);
                }
            }
            panel.Add(choiceRoot);
        }

        private static bool ContainsCardChoices(IReadOnlyList<HexUiChoice> choices)
        {
            if (choices == null)
                return false;

            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i]?.card != null)
                    return true;
            }

            return false;
        }

        public void ShowShop(IReadOnlyList<HexShopOfferView> offers, Func<int, bool> purchase, Func<int> getGold, Action leave)
        {
            void Render()
            {
                var choices = new List<HexUiChoice>();
                for (int i = 0; i < offers.Count; i++)
                {
                    int index = i;
                    var offer = offers[i];
                    choices.Add(new HexUiChoice
                    {
                        title = $"[{offer.card.energyCost}] {offer.card.displayName} — {offer.price} 金币",
                        body = offer.card.description,
                        card = offer.card,
                        cardCaption = $"{offer.price} 金币",
                        enabled = getGold() >= offer.price,
                        action = () =>
                        {
                            if (purchase(index))
                                Render();
                        },
                    });
                }
                choices.Add(new HexUiChoice { title = "离开商店", action = leave });
                ShowOverlay("商店", $"金币 {getGold()}", choices);
            }
            Render();
        }

        private static Button CreateCardChoiceButton(HexUiChoice choice)
        {
            var button = new Button(() => choice.action?.Invoke()) { text = string.Empty };
            button.AddToClassList("hex-card-choice");
            button.SetEnabled(choice.enabled);

            var layout = Resources.Load<HexCardUiLayoutSettings>("UI Toolkit/CardArt/WarriorCardLayout");
            var card = new WarriorCardVisualElement("warrior-card-choice");
            card.SetCardSize(210f);
            card.ApplyLayout(layout, 0.55f);
            card.SetContent(
                choice.card.energyCost < 0 ? "X" : choice.card.energyCost.ToString(),
                choice.card.displayName,
                choice.card.description);
            card.SetGuidesVisible(false);
            button.Add(card);

            var caption = new Label(string.IsNullOrWhiteSpace(choice.cardCaption) ? "选择" : choice.cardCaption);
            caption.AddToClassList("hex-card-choice__caption");
            button.Add(caption);
            return button;
        }

        public void ClearOverlay()
        {
            if (_overlayLayer == null)
                return;
            _overlayLayer.Clear();
            _overlayLayer.pickingMode = PickingMode.Ignore;
        }

        private void AddProfession(VisualElement parent, HexCardProfession profession, string title, string body, Action<HexCardProfession> choose)
        {
            var button = new Button(() => choose(profession))
            {
                text = $"{title}\n初始牌组 {HexCardLibrary.CreateStarterDeck(profession).Count} 张\n{body}\n开始",
            };
            button.AddToClassList("hex-card");
            button.style.width = 320;
            button.style.height = 300;
            parent.Add(button);
        }

        private void OnMapPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || IsButtonTarget(evt.target as VisualElement) || IsOverlayOpen)
                return;
            _panPointerId = evt.pointerId;
            _lastPointer = evt.position;
            _viewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnMapPointerMove(PointerMoveEvent evt)
        {
            if (_panPointerId != evt.pointerId)
                return;
            Vector2 current = evt.position;
            _pan += current - _lastPointer;
            _lastPointer = current;
            ClampPan();
            ApplyPan();
            evt.StopPropagation();
        }

        private void OnMapPointerUp(PointerUpEvent evt)
        {
            if (_panPointerId != evt.pointerId)
                return;
            _viewport.ReleasePointer(evt.pointerId);
            _panPointerId = -1;
            evt.StopPropagation();
        }

        private void ClampPan()
        {
            float viewportWidth = Mathf.Max(1f, _viewport.resolvedStyle.width);
            float viewportHeight = Mathf.Max(1f, _viewport.resolvedStyle.height);
            _pan.x = Mathf.Clamp(_pan.x, Mathf.Min(0f, viewportWidth - MapWidth), Mathf.Max(0f, viewportWidth - MapWidth));
            _pan.y = Mathf.Clamp(_pan.y, Mathf.Min(0f, viewportHeight - MapHeight), Mathf.Max(0f, viewportHeight - MapHeight));
        }

        private void ApplyPan()
        {
            if (_mapContent == null)
                return;
            _mapContent.style.left = _pan.x;
            _mapContent.style.top = _pan.y;
        }

        private VisualElement NewOverlay(string name)
        {
            var mask = new VisualElement { name = name, pickingMode = PickingMode.Position };
            mask.AddToClassList("hex-modal-mask");
            _overlayLayer.Add(mask);
            return mask;
        }

        private static VisualElement NewPanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.AddToClassList("hex-panel");
            panel.AddToClassList("hex-modal");
            parent.Add(panel);
            return panel;
        }

        private static VisualElement NewChild(VisualElement parent, string name, params string[] classes)
        {
            var child = new VisualElement { name = name };
            for (int i = 0; i < classes.Length; i++)
                child.AddToClassList(classes[i]);
            parent.Add(child);
            return child;
        }

        private static Label NewLabel(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private static bool IsButtonTarget(VisualElement element)
        {
            while (element != null)
            {
                if (element is Button)
                    return true;
                element = element.parent;
            }
            return false;
        }

        private static Vector2 ToContentPoint(Vector2 mapPoint) => new(mapPoint.x + MapWidth * 0.5f, MapHeight - (mapPoint.y + 500f));

        private static string GetNodeSymbol(HexMapNodeType type) => type switch
        {
            HexMapNodeType.Start => "S",
            HexMapNodeType.SmallBattle => "⚔",
            HexMapNodeType.EliteBattle => "E",
            HexMapNodeType.Event => "?",
            HexMapNodeType.Shop => "$",
            HexMapNodeType.Rest => "R",
            HexMapNodeType.Boss => "B",
            _ => "?",
        };

        private static string GetNodeLabel(HexMapNodeType type) => type switch
        {
            HexMapNodeType.Start => "起点",
            HexMapNodeType.SmallBattle => "战斗",
            HexMapNodeType.EliteBattle => "精英",
            HexMapNodeType.Event => "事件",
            HexMapNodeType.Shop => "商店",
            HexMapNodeType.Rest => "休息",
            HexMapNodeType.Boss => "首领",
            _ => string.Empty,
        };
    }
}
