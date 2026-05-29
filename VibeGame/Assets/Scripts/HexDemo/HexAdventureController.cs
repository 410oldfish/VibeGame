using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HexDemo
{
    public sealed class HexAdventureController : MonoBehaviour
    {
        private const string Starter02PrefabPath = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Starter_02.prefab";
        private const string Starter03PrefabPath = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_03/Starter_03.prefab";
        private const string Starter02ControllerPath = "Assets/Animations/Starter02/Starter_02.controller";
        private const string TerrainTilePrefabPath = "Assets/Models/Terrain/HexCubeTile.prefab";
        private const string CampfirePrefabPath = "Assets/Models/SceneProp/low-poly-campfire/source/campfire.fbx";

        private readonly Dictionary<string, Button> _nodeButtons = new();
        private readonly HashSet<string> _visitedNodeIds = new();
        private readonly List<GameObject> _roomRoots = new();

        private HexRunState _runState;
        private HexMapData _mapData;
        private HexNetworkSessionController _networkSession;
        private string _currentNodeId;
        private string _pendingRoomNodeId;

        private Canvas _mapCanvas;
        private Canvas _professionCanvas;
        private RectTransform _mapRoot;
        private Vector2 _mapPanOffset;
        private Vector2 _mapPanMin;
        private Vector2 _mapPanMax;
        private TextMeshProUGUI _runSummaryLabel;
        private RectTransform _overlayRoot;
        private Camera _sceneCamera;
        private bool _isDraggingMap;
        private Vector2 _lastMapPointerPosition;

        public static void TryBootstrap()
        {
            if (Object.FindFirstObjectByType<HexAdventureController>() != null)
                return;

            var go = new GameObject(nameof(HexAdventureController));
            go.AddComponent<HexAdventureController>();
        }

        private void Start()
        {
            Screen.SetResolution(1920, 1080, false);
            HexTMPFontProvider.EnsureInitialized();
            EnsureEventSystem();
            _networkSession = HexNetworkSessionController.EnsureExists();
            Network.GameNetworkManager.EnsureExists();
            _sceneCamera = Camera.main;
            ShowProfessionSelection();
        }

        private void Update()
        {
            UpdateMapPanInput();
        }

        public void StartNewRun()
        {
            ShowProfessionSelection();
        }

        public void StartNewRun(HexCardProfession profession)
        {
            CleanupRoom();
            CleanupProfessionSelection();
            if (_mapCanvas != null)
                Destroy(_mapCanvas.gameObject);

            _runState = new HexRunState
            {
                maxHealth = 10,
                currentHealth = 10,
                gold = 0,
                profession = profession,
                deckDefinitions = HexCardLibrary.CreateStarterDeck(profession),
            };
            _mapData = HexAdventureMapGenerator.Generate();
            _currentNodeId = _mapData.startNodeId;
            _visitedNodeIds.Clear();
            _visitedNodeIds.Add(_currentNodeId);

            BuildMapCanvas();
            ShowMap();
        }

        private void ShowProfessionSelection()
        {
            CleanupRoom();
            if (_mapCanvas != null)
                Destroy(_mapCanvas.gameObject);
            CleanupProfessionSelection();

            var canvasGO = new GameObject("ProfessionSelect_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _professionCanvas = canvasGO.GetComponent<Canvas>();
            _professionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _professionCanvas.sortingOrder = 120;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreatePanel(canvasGO.transform, "Background", Vector2.zero, new Vector2(1920f, 1080f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            background.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

            var title = CreateText(background.transform, "Title", new Vector2(0f, -92f), new Vector2(900f, 70f), 42f, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.text = "\u9009\u62e9\u804c\u4e1a";

            var subtitle = CreateText(background.transform, "Subtitle", new Vector2(0f, -158f), new Vector2(960f, 44f), 24f, FontStyles.Normal);
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.text = "\u804c\u4e1a\u4f1a\u51b3\u5b9a\u521d\u59cb\u724c\u7ec4\uff0c\u5e76\u9650\u5236\u540e\u7eed\u5956\u52b1\u548c\u5546\u5e97\u51fa\u73b0\u7684\u804c\u4e1a\u5361\u724c\u3002";

            var networkLabel = CreateText(background.transform, "NetworkStatus", new Vector2(0f, -212f), new Vector2(960f, 34f), 21f, FontStyles.Bold);
            networkLabel.alignment = TextAlignmentOptions.Center;
            networkLabel.text = GetNetworkStatusText();

            var gridRoot = CreatePanel(background.transform, "ProfessionGrid", new Vector2(0f, -20f), new Vector2(1060f, 360f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            gridRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            var layout = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(320f, 300f);
            layout.spacing = new Vector2(28f, 0f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;

            CreateProfessionCard(gridRoot.transform, HexCardProfession.Warrior, "\u6218\u58eb", "\u6b66\u5668\u5207\u6362\u3001\u529b\u91cf\u3001\u51fb\u98de\u548c\u8303\u56f4\u653b\u51fb\u3002", new Color(0.72f, 0.26f, 0.2f, 1f));
            CreateProfessionCard(gridRoot.transform, HexCardProfession.Paladin, "\u9a91\u58eb", "\u62a4\u7532\u3001\u9632\u5b88\u53cd\u51fb\u548c\u795e\u5723\u6253\u51fb\u3002", new Color(0.86f, 0.72f, 0.34f, 1f));
            CreateProfessionCard(gridRoot.transform, HexCardProfession.Druid, "\u5fb7\u9c81\u4f0a", "\u53d8\u5f62\u3001\u5730\u5757\u6548\u679c\u3001\u71c3\u70e7\u548c\u81ea\u7136\u63a7\u5236\u3002", new Color(0.32f, 0.62f, 0.34f, 1f));

            if (_sceneCamera != null)
            {
                _sceneCamera.orthographic = true;
                _sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
                _sceneCamera.transform.rotation = Quaternion.identity;
                _sceneCamera.orthographicSize = 5.5f;
                _sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                _sceneCamera.backgroundColor = new Color(0.12f, 0.13f, 0.17f, 1f);
            }
        }

        private void CreateProfessionCard(Transform parent, HexCardProfession profession, string title, string description, Color color)
        {
            var card = CreatePanel(parent, $"{profession}_Card", Vector2.zero, new Vector2(320f, 300f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            card.GetComponent<Image>().color = Color.Lerp(color, Color.black, 0.12f);

            var button = card.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => ChooseProfession(profession));

            var titleText = CreateText(card.transform, "Title", new Vector2(0f, -34f), new Vector2(280f, 54f), 34f, FontStyles.Bold);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.text = title;

            var deck = HexCardLibrary.CreateStarterDeck(profession);
            var countText = CreateText(card.transform, "Deck", new Vector2(0f, -100f), new Vector2(280f, 34f), 22f, FontStyles.Bold);
            countText.alignment = TextAlignmentOptions.Center;
            countText.text = $"\u521d\u59cb\u724c\u7ec4 {deck.Count} \u5f20";

            var body = CreateText(card.transform, "Body", new Vector2(24f, -148f), new Vector2(272f, 84f), 22f, FontStyles.Normal);
            body.text = description;

            var cta = CreateText(card.transform, "CTA", new Vector2(0f, 26f), new Vector2(240f, 42f), 24f, FontStyles.Bold);
            cta.alignment = TextAlignmentOptions.Center;
            cta.text = "\u5f00\u59cb";
        }
        private void ChooseProfession(HexCardProfession profession)
        {
            _networkSession ??= HexNetworkSessionController.EnsureExists();
            _networkSession.SelectLocalProfession(profession);
            _networkSession.ConfirmLocalReady();

            if (_networkSession.IsOffline || _networkSession.CanHostStartRun())
                StartNewRun(profession);
        }

        private string GetNetworkStatusText()
        {
            _networkSession ??= HexNetworkSessionController.EnsureExists();
            return _networkSession.Mode switch
            {
                HexNetworkMode.Host => $"\u4e3b\u673a\u623f\u95f4 {_networkSession.RoomSettings.roomCode}\uff1a\u7b49\u5f85\u6240\u6709\u73a9\u5bb6\u786e\u8ba4\u540e\u5f00\u59cb",
                HexNetworkMode.Client => $"\u5df2\u52a0\u5165\u623f\u95f4 {_networkSession.RoomSettings.roomCode}\uff1a\u9009\u62e9\u804c\u4e1a\u5e76\u7b49\u5f85\u4e3b\u673a\u5f00\u59cb",
                _ => "\u5f53\u524d\u4e3a\u672c\u5730\u5355\u4eba\u6a21\u5f0f\uff1b\u8054\u7f51\u623f\u95f4\u4f1a\u590d\u7528\u540c\u4e00\u5957\u804c\u4e1a\u786e\u8ba4\u6d41\u7a0b\u3002",
            };
        }

        private void CleanupProfessionSelection()
        {
            if (_professionCanvas == null)
                return;

            Destroy(_professionCanvas.gameObject);
            _professionCanvas = null;
        }

        private void BuildMapCanvas()
        {
            var canvasGO = new GameObject("AdventureMap_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _mapCanvas = canvasGO.GetComponent<Canvas>();
            _mapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _mapCanvas.sortingOrder = 80;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreatePanel(canvasGO.transform, "Background", Vector2.zero, new Vector2(1920f, 1080f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            background.GetComponent<Image>().color = new Color(0.86f, 0.83f, 0.88f, 1f);

            var title = CreateText(background.transform, "Title", new Vector2(36f, -24f), new Vector2(560f, 50f), 34f, FontStyles.Bold);
            title.text = "Hex Run Map";

            _runSummaryLabel = CreateText(background.transform, "RunSummary", new Vector2(36f, -78f), new Vector2(620f, 120f), 24f, FontStyles.Normal);

            _mapRoot = CreatePanel(background.transform, "MapRoot", Vector2.zero, new Vector2(1500f, 860f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _mapRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            _overlayRoot = CreatePanel(background.transform, "OverlayRoot", Vector2.zero, new Vector2(1920f, 1080f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _overlayRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _overlayRoot.gameObject.SetActive(false);

            BuildMapEdges();
            BuildMapNodes();
            UpdateMapPanBounds();
            ApplyMapPan();
            RefreshMapState();
        }

        private void BuildMapEdges()
        {
            for (int i = 0; i < _mapData.nodes.Count; i++)
            {
                var node = _mapData.nodes[i];
                for (int edgeIndex = 0; edgeIndex < node.outgoingNodeIds.Count; edgeIndex++)
                {
                    var target = _mapData.GetNode(node.outgoingNodeIds[edgeIndex]);
                    if (target == null)
                        continue;

                    CreateEdge(node.uiPosition, target.uiPosition);
                }
            }
        }

        private void BuildMapNodes()
        {
            _nodeButtons.Clear();
            for (int i = 0; i < _mapData.nodes.Count; i++)
            {
                var node = _mapData.nodes[i];
                var nodeRect = CreatePanel(_mapRoot.transform, node.id, node.uiPosition, new Vector2(82f, 82f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                var button = nodeRect.gameObject.AddComponent<Button>();
                string nodeId = node.id;
                button.onClick.AddListener(() => TryEnterNode(nodeId));

                var icon = CreateText(nodeRect.transform, "Icon", Vector2.zero, new Vector2(82f, 82f), 32f, FontStyles.Bold);
                icon.alignment = TextAlignmentOptions.Center;
                icon.text = GetNodeSymbol(node.nodeType);

                var caption = CreateText(nodeRect.transform, "Caption", new Vector2(-30f, -88f), new Vector2(140f, 26f), 18f, FontStyles.Bold);
                caption.alignment = TextAlignmentOptions.Center;
                caption.text = GetNodeLabel(node.nodeType);

                _nodeButtons[node.id] = button;
            }
        }

        private void RefreshMapState()
        {
            if (_runSummaryLabel != null)
                _runSummaryLabel.text = $"{GetProfessionDisplayName(_runState.profession)}\nHP {_runState.currentHealth}/{_runState.maxHealth}\nGold {_runState.gold}\nDeck {_runState.deckDefinitions.Count}";

            var currentNode = _mapData.GetNode(_currentNodeId);
            var availableNodeIds = currentNode != null ? new HashSet<string>(currentNode.outgoingNodeIds) : new HashSet<string>();

            foreach (var pair in _nodeButtons)
            {
                var node = _mapData.GetNode(pair.Key);
                var button = pair.Value;
                var image = button.GetComponent<Image>();
                var text = button.GetComponentInChildren<TextMeshProUGUI>();

                bool isCurrent = pair.Key == _currentNodeId;
                bool isVisited = _visitedNodeIds.Contains(pair.Key);
                bool isAvailable = availableNodeIds.Contains(pair.Key);

                button.interactable = isAvailable;
                image.color = isCurrent
                    ? new Color(0.95f, 0.9f, 0.55f, 1f)
                    : isAvailable
                    ? GetNodeColor(node.nodeType)
                    : isVisited
                    ? Color.Lerp(GetNodeColor(node.nodeType), Color.black, 0.32f)
                    : Color.Lerp(GetNodeColor(node.nodeType), Color.black, 0.52f);

                if (text != null)
                    text.color = button.interactable || isCurrent ? new Color(0.14f, 0.1f, 0.1f, 1f) : new Color(0.22f, 0.18f, 0.18f, 0.88f);
            }
        }

        private void ShowMap()
        {
            CleanupRoom();
            if (_mapCanvas != null)
                _mapCanvas.gameObject.SetActive(true);

            if (_sceneCamera != null)
            {
                _sceneCamera.orthographic = true;
                _sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
                _sceneCamera.transform.rotation = Quaternion.identity;
                _sceneCamera.orthographicSize = 5.5f;
                _sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                _sceneCamera.backgroundColor = new Color(0.14f, 0.15f, 0.2f, 1f);
            }

            _isDraggingMap = false;
            ApplyMapPan();
            RefreshMapState();
        }

        private void TryEnterNode(string nodeId)
        {
            var currentNode = _mapData.GetNode(_currentNodeId);
            if (currentNode == null || !currentNode.outgoingNodeIds.Contains(nodeId))
                return;

            var targetNode = _mapData.GetNode(nodeId);
            if (targetNode == null)
                return;

            _pendingRoomNodeId = nodeId;
            if (_mapCanvas != null && (targetNode.nodeType == HexMapNodeType.SmallBattle || targetNode.nodeType == HexMapNodeType.EliteBattle || targetNode.nodeType == HexMapNodeType.Boss || targetNode.nodeType == HexMapNodeType.Rest))
                _mapCanvas.gameObject.SetActive(false);

            switch (targetNode.nodeType)
            {
                case HexMapNodeType.SmallBattle:
                    EnterBattleRoom(1, "Small Battle");
                    break;
                case HexMapNodeType.EliteBattle:
                    EnterBattleRoom(3, "Elite Battle");
                    break;
                case HexMapNodeType.Boss:
                    EnterBattleRoom(5, "Boss Battle");
                    break;
                case HexMapNodeType.Shop:
                    ShowShop();
                    break;
                case HexMapNodeType.Event:
                    ShowEvent();
                    break;
                case HexMapNodeType.Rest:
                    EnterRestRoom();
                    break;
            }
        }

        private void EnterBattleRoom(int enemyCount, string title)
        {
            var roomRoot = new GameObject($"Room_{title.Replace(" ", "_")}");
            _roomRoots.Add(roomRoot);

            var grid = roomRoot.AddComponent<HexGrid>();
            grid.width = 11;
            grid.height = 11;
            grid.hexSize = 0.55f;
            grid.tileY = 0f;
            grid.heightStep = 0f;
            grid.tilePrefab = LoadTerrainTilePrefab();
            grid.clickLayerMask = ~0;
            grid.Build();
            ConfigureBattleCamera(grid);

            var playerCoord = FindClosestExistingCoord(grid, new HexAxialCoord(3, 5));
            var playerRoot = new GameObject("PlayerUnit");
            playerRoot.transform.SetParent(roomRoot.transform, false);
            var playerAnimator = SpawnCharacterModel(playerRoot.transform, LoadStarter02Prefab());
            var playerUnit = playerRoot.AddComponent<HexBattleUnit>();
            playerUnit.Initialize(new HexBattleUnitState
            {
                id = "player_run",
                displayName = "Hero",
                faction = HexBattleFaction.Player,
                maxHealth = _runState.maxHealth,
                currentHealth = _runState.currentHealth,
                armor = 0,
                energy = 0,
                profession = _runState.profession,
                maxEnergy = 3,
                drawPerTurn = 4,
                maxMovePoints = 2,
                currentMovePoints = 2,
                attackRange = 1,
                coord = playerCoord,
            }, playerAnimator, _runState.deckDefinitions);
            playerUnit.SnapTo(grid, 0.03f);

            var enemyUnits = new List<HexBattleUnit>();
            var desiredEnemyCoords = new[]
            {
                new HexAxialCoord(7, 5),
                new HexAxialCoord(7, 4),
                new HexAxialCoord(7, 6),
                new HexAxialCoord(8, 5),
                new HexAxialCoord(8, 4),
            };

            for (int i = 0; i < enemyCount; i++)
            {
                var enemyRoot = new GameObject($"EnemyUnit_{i + 1}");
                enemyRoot.transform.SetParent(roomRoot.transform, false);
                var enemyAnimator = SpawnCharacterModel(enemyRoot.transform, LoadEnemyPrefab() ?? LoadStarter02Prefab());
                var enemyUnit = enemyRoot.AddComponent<HexBattleUnit>();
                var enemyCoord = FindClosestExistingCoord(grid, desiredEnemyCoords[Mathf.Min(i, desiredEnemyCoords.Length - 1)], enemyUnits.Select(unit => unit.State.coord).Append(playerCoord));
                enemyUnit.Initialize(new HexBattleUnitState
                {
                    id = $"enemy_{i + 1}",
                    displayName = i == 0 ? "Goblin" : $"Goblin {i + 1}",
                    faction = HexBattleFaction.Enemy,
                    maxHealth = 3,
                    currentHealth = 3,
                    armor = 0,
                    energy = 0,
                    maxEnergy = 0,
                    drawPerTurn = 0,
                    maxMovePoints = 0,
                    currentMovePoints = 0,
                    attackRange = 1,
                    emptyDrawPileStrengthGain = 3,
                    coord = enemyCoord,
                }, enemyAnimator, HexCardLibrary.CreateGoblinDeck());
                enemyUnit.SnapTo(grid, 0.03f);
                enemyUnits.Add(enemyUnit);
            }

            var controllerGO = new GameObject("BattleController");
            controllerGO.transform.SetParent(roomRoot.transform, false);
            var battleController = controllerGO.AddComponent<HexBattleController>();
            battleController.Initialize(grid, playerUnit, enemyUnits, _sceneCamera);
            battleController.BattleFinished += OnBattleFinished;
        }

        private void OnBattleFinished(bool playerWon, int goldReward, HexBattleUnit playerUnit)
        {
            if (playerUnit != null)
                _runState.currentHealth = Mathf.Clamp(playerUnit.State.currentHealth, 0, _runState.maxHealth);

            if (!playerWon)
            {
                ShowSimpleOverlay("Defeat", $"You were defeated.\nGold {_runState.gold}", "Restart Run", StartNewRun);
                return;
            }

            _runState.gold += goldReward;
            ShowBattleReward(goldReward);
        }

        private void EnterRestRoom()
        {
            var roomRoot = new GameObject("Room_Rest");
            _roomRoots.Add(roomRoot);

            var grid = roomRoot.AddComponent<HexGrid>();
            grid.width = 9;
            grid.height = 9;
            grid.hexSize = 0.55f;
            grid.tileY = 0f;
            grid.heightStep = 0f;
            grid.tilePrefab = LoadTerrainTilePrefab();
            grid.clickLayerMask = ~0;
            grid.Build();
            ConfigureBattleCamera(grid);

            var playerRoot = new GameObject("PlayerUnit");
            playerRoot.transform.SetParent(roomRoot.transform, false);
            var playerAnimator = SpawnCharacterModel(playerRoot.transform, LoadStarter02Prefab());
            var playerUnit = playerRoot.AddComponent<HexBattleUnit>();
            playerUnit.Initialize(new HexBattleUnitState
            {
                id = "player_rest",
                displayName = "Hero",
                faction = HexBattleFaction.Player,
                maxHealth = _runState.maxHealth,
                currentHealth = _runState.currentHealth,
                armor = 0,
                energy = 0,
                profession = _runState.profession,
                maxEnergy = 3,
                drawPerTurn = 4,
                maxMovePoints = 0,
                currentMovePoints = 0,
                attackRange = 1,
                coord = FindClosestExistingCoord(grid, new HexAxialCoord(3, 4)),
            }, playerAnimator, _runState.deckDefinitions);
            playerUnit.SnapTo(grid, 0.03f);

            var campfire = SpawnCampfire(roomRoot.transform, grid, FindClosestExistingCoord(grid, new HexAxialCoord(5, 4)));

            var restController = roomRoot.AddComponent<HexRestController>();
            restController.campfireObject = campfire;
            restController.healAmount = 10;
            restController.Initialize(playerUnit, _sceneCamera);
            restController.RestFinished += (_, unit) =>
            {
                if (unit != null)
                    _runState.currentHealth = Mathf.Clamp(unit.State.currentHealth, 0, _runState.maxHealth);
                CompleteRoomAndReturnToMap();
            };
        }

        private void ShowShop()
        {
            var offers = new List<(HexCardDefinition card, int price)>();
            for (int i = 0; i < 8; i++)
            {
                var card = HexCardLibrary.GetRandomRewardCard(_runState.profession);
                int price = 6 + card.energyCost * 4 + Random.Range(0, 4);
                offers.Add((card, price));
            }

            BuildOverlay("Shop", overlay =>
            {
                var summary = CreateText(overlay.transform, "ShopSummary", new Vector2(42f, -86f), new Vector2(600f, 40f), 24f, FontStyles.Bold);
                summary.text = $"Gold {_runState.gold}";

                var cardGrid = CreatePanel(overlay.transform, "CardGrid", new Vector2(0f, -40f), new Vector2(1460f, 480f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                cardGrid.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.28f);
                var grid = cardGrid.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(176f, 220f);
                grid.spacing = new Vector2(16f, 16f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.padding = new RectOffset(18, 18, 18, 18);

                for (int i = 0; i < offers.Count; i++)
                {
                    var offer = offers[i];
                    var cardPanel = CreatePanel(cardGrid.transform, $"Offer_{i}", Vector2.zero, new Vector2(176f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                    cardPanel.GetComponent<Image>().color = Color.Lerp(offer.card.color, Color.black, 0.12f);

                    var cardTitle = CreateText(cardPanel.transform, "Title", new Vector2(14f, -14f), new Vector2(140f, 32f), 22f, FontStyles.Bold);
                    cardTitle.text = offer.card.displayName;
                    var body = CreateText(cardPanel.transform, "Body", new Vector2(14f, -58f), new Vector2(146f, 72f), 18f, FontStyles.Normal);
                    body.text = offer.card.effectType == HexCardEffectType.Attack
                        ? $"Deal {offer.card.amount}\nRange {offer.card.range}"
                        : $"Gain {offer.card.amount} Armor";

                    var buyPanel = CreatePanel(cardPanel.transform, "BuyPanel", new Vector2(0f, 12f), new Vector2(144f, 48f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                    var buyButton = buyPanel.gameObject.AddComponent<Button>();
                    int offerIndex = i;
                    buyButton.onClick.AddListener(() =>
                    {
                        var selected = offers[offerIndex];
                        if (_runState.gold < selected.price)
                            return;

                        _runState.gold -= selected.price;
                        _runState.deckDefinitions.Add(selected.card);
                        RefreshOverlayShop(overlay, offers);
                    });
                    var buyLabel = CreateText(buyPanel.transform, "BuyLabel", Vector2.zero, new Vector2(144f, 48f), 20f, FontStyles.Bold);
                    buyLabel.alignment = TextAlignmentOptions.Center;
                    buyLabel.text = $"Buy {offer.price}";
                }

                var leave = CreateBottomButton(overlay.transform, "Leave Shop", () => CompleteRoomAndReturnToMap());
                leave.anchoredPosition = new Vector2(0f, 28f);
            });
        }

        private void RefreshOverlayShop(RectTransform overlay, List<(HexCardDefinition card, int price)> offers)
        {
            var summary = overlay.Find("ShopSummary")?.GetComponent<TextMeshProUGUI>();
            if (summary != null)
                summary.text = $"Gold {_runState.gold}";

            for (int i = 0; i < offers.Count; i++)
            {
                var buyLabel = overlay.Find($"CardGrid/Offer_{i}/BuyPanel/BuyLabel")?.GetComponent<TextMeshProUGUI>();
                var buyButton = overlay.Find($"CardGrid/Offer_{i}/BuyPanel")?.GetComponent<Button>();
                if (buyLabel != null)
                    buyLabel.text = $"Buy {offers[i].price}";
                if (buyButton != null)
                    buyButton.interactable = _runState.gold >= offers[i].price;
            }
        }

        private void ShowEvent()
        {
            BuildOverlay("Event", overlay =>
            {
                var body = CreateText(overlay.transform, "Body", new Vector2(54f, -92f), new Vector2(760f, 120f), 26f, FontStyles.Normal);
                body.text = "A strange roadside shrine offers three choices.";

                CreateChoiceButton(overlay.transform, new Vector2(0f, 140f), "Gain 15 Gold", () =>
                {
                    _runState.gold += 15;
                    CompleteRoomAndReturnToMap();
                });
                CreateChoiceButton(overlay.transform, new Vector2(0f, 64f), "Recover 5 HP", () =>
                {
                    _runState.currentHealth = Mathf.Min(_runState.maxHealth, _runState.currentHealth + 5);
                    CompleteRoomAndReturnToMap();
                });
                CreateChoiceButton(overlay.transform, new Vector2(0f, -12f), "Gain Random Card", () =>
                {
                    _runState.deckDefinitions.Add(HexCardLibrary.GetRandomRewardCard(_runState.profession));
                    CompleteRoomAndReturnToMap();
                });
            });
        }

        private void ShowBattleReward(int goldReward)
        {
            CleanupRoom();
            var rewards = HexCardLibrary.GetRewardChoices(3, _runState.profession);
            BuildOverlay("Victory", overlay =>
            {
                var body = CreateText(overlay.transform, "Body", new Vector2(48f, -92f), new Vector2(760f, 88f), 26f, FontStyles.Normal);
                body.text = $"Battle won. Gained {goldReward} Gold.\nChoose 1 card reward.";

                var cardGrid = CreatePanel(overlay.transform, "RewardGrid", new Vector2(0f, -4f), new Vector2(820f, 360f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                cardGrid.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.18f);
                var layout = cardGrid.gameObject.AddComponent<GridLayoutGroup>();
                layout.cellSize = new Vector2(240f, 300f);
                layout.spacing = new Vector2(18f, 12f);
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 3;
                layout.padding = new RectOffset(18, 18, 18, 18);
                layout.childAlignment = TextAnchor.MiddleCenter;

                for (int i = 0; i < rewards.Count; i++)
                {
                    var reward = rewards[i];
                    var cardPanel = CreatePanel(cardGrid.transform, $"Reward_{i}", Vector2.zero, new Vector2(240f, 300f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                    cardPanel.GetComponent<Image>().color = Color.Lerp(reward.color, Color.black, 0.08f);

                    var cost = CreateText(cardPanel.transform, "Cost", new Vector2(16f, -14f), new Vector2(48f, 34f), 26f, FontStyles.Bold);
                    cost.text = reward.energyCost < 0 ? "X" : reward.energyCost.ToString();

                    var rarity = CreateText(cardPanel.transform, "Rarity", new Vector2(78f, -16f), new Vector2(140f, 28f), 18f, FontStyles.Bold);
                    rarity.text = reward.rarity;
                    rarity.alignment = TextAlignmentOptions.TopRight;

                    var title = CreateText(cardPanel.transform, "Title", new Vector2(18f, -50f), new Vector2(204f, 58f), 24f, FontStyles.Bold);
                    title.text = reward.displayName;

                    var meta = CreateText(cardPanel.transform, "Meta", new Vector2(18f, -98f), new Vector2(204f, 40f), 17f, FontStyles.Bold);
                    meta.text = $"{reward.cardType}   Cast {reward.castRange}   Area {reward.effectRadius}";

                    var bodyText = CreateText(cardPanel.transform, "Body", new Vector2(18f, -138f), new Vector2(204f, 92f), 18f, FontStyles.Normal);
                    bodyText.text = string.IsNullOrWhiteSpace(reward.description)
                        ? (reward.effectType == HexCardEffectType.Attack ? $"Deal {reward.amount} damage." : $"Gain {reward.amount} armor.")
                        : reward.description;

                    var pickButtonPanel = CreatePanel(cardPanel.transform, "PickButton", new Vector2(0f, 14f), new Vector2(180f, 50f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                    var pickButton = pickButtonPanel.gameObject.AddComponent<Button>();
                    pickButton.onClick.AddListener(() =>
                    {
                        _runState.deckDefinitions.Add(reward);
                        CompleteRoomAndReturnToMap();
                    });
                    var pickLabel = CreateText(pickButtonPanel.transform, "Label", Vector2.zero, new Vector2(180f, 50f), 22f, FontStyles.Bold);
                    pickLabel.alignment = TextAlignmentOptions.Center;
                    pickLabel.text = "Choose";
                }
            });
        }

        private void BuildOverlay(string title, System.Action<RectTransform> bodyBuilder)
        {
            if (_mapCanvas != null)
                _mapCanvas.gameObject.SetActive(true);
            CleanupOverlay();
            _overlayRoot.gameObject.SetActive(true);
            _overlayRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

            var modal = CreatePanel(_overlayRoot.transform, "Modal", Vector2.zero, new Vector2(920f, 640f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            modal.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.96f);
            var titleText = CreateText(modal.transform, "Title", new Vector2(36f, -28f), new Vector2(600f, 42f), 34f, FontStyles.Bold);
            titleText.text = title;
            bodyBuilder?.Invoke(modal);
        }

        private void ShowSimpleOverlay(string title, string body, string buttonText, UnityEngine.Events.UnityAction callback)
        {
            BuildOverlay(title, overlay =>
            {
                var bodyText = CreateText(overlay.transform, "Body", new Vector2(56f, -110f), new Vector2(700f, 120f), 28f, FontStyles.Normal);
                bodyText.text = body;
                var button = CreateBottomButton(overlay.transform, buttonText, callback);
                button.anchoredPosition = new Vector2(0f, 28f);
            });
        }

        private RectTransform CreateBottomButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var buttonPanel = CreatePanel(parent, $"{text}_Button", new Vector2(0f, 24f), new Vector2(240f, 70f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var button = buttonPanel.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var label = CreateText(buttonPanel.transform, "Label", Vector2.zero, new Vector2(240f, 70f), 26f, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            return buttonPanel;
        }

        private void CreateChoiceButton(Transform parent, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction onClick)
        {
            var panel = CreatePanel(parent, $"{label}_Choice", anchoredPosition, new Vector2(420f, 58f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var button = panel.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var text = CreateText(panel.transform, "Label", Vector2.zero, new Vector2(420f, 58f), 24f, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
        }

        private void CompleteRoomAndReturnToMap()
        {
            CleanupRoom();
            CleanupOverlay();
            _overlayRoot.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(_pendingRoomNodeId))
            {
                _currentNodeId = _pendingRoomNodeId;
                _visitedNodeIds.Add(_currentNodeId);
                _pendingRoomNodeId = null;
            }

            if (_currentNodeId == _mapData.bossNodeId)
            {
                ShowSimpleOverlay("Victory", $"Boss defeated.\nGold {_runState.gold}", "New Run", StartNewRun);
                return;
            }

            ShowMap();
        }

        private void CleanupOverlay()
        {
            if (_overlayRoot == null)
                return;

            for (int i = _overlayRoot.childCount - 1; i >= 0; i--)
                Destroy(_overlayRoot.GetChild(i).gameObject);
        }

        private void CleanupRoom()
        {
            for (int i = _roomRoots.Count - 1; i >= 0; i--)
            {
                if (_roomRoots[i] != null)
                    Destroy(_roomRoots[i]);
            }
            _roomRoots.Clear();
        }

        private static string GetProfessionDisplayName(HexCardProfession profession)
        {
            return profession switch
            {
                HexCardProfession.Warrior => "\u6218\u58eb",
                HexCardProfession.Paladin => "\u9a91\u58eb",
                HexCardProfession.Druid => "\u5fb7\u9c81\u4f0a",
                _ => "\u672a\u77e5\u804c\u4e1a",
            };
        }

        private void UpdateMapPanInput()
        {
            if (_mapCanvas == null || !_mapCanvas.gameObject.activeInHierarchy || _overlayRoot == null || _overlayRoot.gameObject.activeSelf || _mapRoot == null)
                return;

            if (Input.GetMouseButtonDown(0) && !IsPointerOverMapButton())
            {
                _isDraggingMap = true;
                _lastMapPointerPosition = Input.mousePosition;
            }

            if (_isDraggingMap && Input.GetMouseButton(0))
            {
                Vector2 currentPosition = Input.mousePosition;
                Vector2 delta = currentPosition - _lastMapPointerPosition;
                _lastMapPointerPosition = currentPosition;
                _mapPanOffset += delta;
                ApplyMapPan();
            }

            if (Input.GetMouseButtonUp(0))
                _isDraggingMap = false;
        }

        private void UpdateMapPanBounds()
        {
            if (_mapRoot == null || _mapData == null || _mapData.nodes.Count == 0)
            {
                _mapPanMin = Vector2.zero;
                _mapPanMax = Vector2.zero;
                return;
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < _mapData.nodes.Count; i++)
            {
                Vector2 p = _mapData.nodes[i].uiPosition;
                minX = Mathf.Min(minX, p.x - 120f);
                maxX = Mathf.Max(maxX, p.x + 120f);
                minY = Mathf.Min(minY, p.y - 120f);
                maxY = Mathf.Max(maxY, p.y + 120f);
            }

            float viewportHalfWidth = _mapRoot.sizeDelta.x * 0.5f;
            float viewportHalfHeight = _mapRoot.sizeDelta.y * 0.5f;
            float overflowX = Mathf.Max(0f, maxX - viewportHalfWidth, viewportHalfWidth + minX);
            float overflowY = Mathf.Max(0f, maxY - viewportHalfHeight, viewportHalfHeight + minY);

            _mapPanMin = new Vector2(-overflowX, -overflowY);
            _mapPanMax = new Vector2(overflowX, overflowY);
            _mapPanOffset = Vector2.Max(_mapPanMin, Vector2.Min(_mapPanMax, _mapPanOffset));
        }

        private void ApplyMapPan()
        {
            if (_mapRoot == null)
                return;

            _mapPanOffset = Vector2.Max(_mapPanMin, Vector2.Min(_mapPanMax, _mapPanOffset));
            _mapRoot.anchoredPosition = _mapPanOffset;
        }

        private bool IsPointerOverMapButton()
        {
            if (EventSystem.current == null)
                return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].gameObject.GetComponentInParent<Button>() != null)
                    return true;
            }

            return false;
        }

        private Animator SpawnCharacterModel(Transform unitRoot, GameObject prefab)
        {
            GameObject model;
            Animator animator = null;

            if (prefab != null)
            {
                model = Instantiate(prefab, unitRoot);
                model.name = prefab.name + "_Runtime";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                animator = model.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.enabled = true;
                    animator.runtimeAnimatorController = LoadStarter02Controller();
                    animator.applyRootMotion = false;
                }
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.transform.SetParent(unitRoot, false);
                animator = null;
            }

            foreach (var collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false;

            return animator;
        }

        private GameObject SpawnCampfire(Transform parent, HexGrid grid, HexAxialCoord coord)
        {
            var surfacePoint = grid.GetTileSurfaceWorld(coord);
            var campfirePrefab = LoadCampfirePrefab();
            GameObject campfire;

            if (campfirePrefab != null)
            {
                campfire = Instantiate(campfirePrefab, parent);
                campfire.name = "Campfire";
                campfire.transform.position = surfacePoint;
                campfire.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                campfire.transform.localScale = Vector3.one * 20f;

                var renderers = campfire.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);

                    float bottomOffset = bounds.min.y - campfire.transform.position.y;
                    campfire.transform.position -= new Vector3(0f, bottomOffset, 0f);
                }

                var colliders = campfire.GetComponentsInChildren<Collider>();
                if (colliders.Length == 0)
                {
                    var box = campfire.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 0.6f, 0f);
                    box.size = new Vector3(1.4f, 1.2f, 1.4f);
                }
            }
            else
            {
                campfire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                campfire.name = "Campfire";
                campfire.transform.SetParent(parent, false);
                campfire.transform.position = surfacePoint + new Vector3(0f, 0.38f, 0f);
                campfire.transform.localScale = new Vector3(0.6f, 0.35f, 0.6f);
                var campfireRenderer = campfire.GetComponent<Renderer>();
                if (campfireRenderer != null)
                    campfireRenderer.material.color = new Color(0.95f, 0.48f, 0.18f, 1f);
            }

            return campfire;
        }

        private void ConfigureBattleCamera(HexGrid grid)
        {
            if (_sceneCamera == null || grid == null || grid.Tiles.Count == 0)
                return;

            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var tile in grid.Tiles.Values)
            {
                Vector3 center = tile.transform.position;
                if (!hasBounds)
                {
                    bounds = new Bounds(center, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(center);
                }
            }

            bounds.Expand(new Vector3(grid.hexSize * 2.4f, 0f, grid.hexSize * 2.4f));
            Vector3 focus = bounds.center;
            Vector3 viewDirection = Quaternion.Euler(50f, 43f, 0f) * Vector3.forward;

            _sceneCamera.orthographic = true;
            _sceneCamera.transform.position = focus - viewDirection * 18f;
            _sceneCamera.transform.LookAt(focus, Vector3.up);
            _sceneCamera.orthographicSize = 5.6f;
            _sceneCamera.nearClipPlane = 0.1f;
            _sceneCamera.farClipPlane = 100f;
            _sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            _sceneCamera.backgroundColor = new Color(0.28f, 0.55f, 0.78f, 1f);
        }

        private void CreateEdge(Vector2 from, Vector2 to)
        {
            var line = CreatePanel(_mapRoot.transform, "Edge", (from + to) * 0.5f, new Vector2(Vector2.Distance(from, to), 8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var image = line.GetComponent<Image>();
            image.color = new Color(0.36f, 0.31f, 0.42f, 0.68f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.22f, 0.9f);
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(text);
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private static string GetNodeSymbol(HexMapNodeType nodeType)
        {
            return nodeType switch
            {
                HexMapNodeType.Start => "S",
                HexMapNodeType.SmallBattle => "F",
                HexMapNodeType.EliteBattle => "E",
                HexMapNodeType.Event => "?",
                HexMapNodeType.Shop => "$",
                HexMapNodeType.Rest => "R",
                HexMapNodeType.Boss => "B",
                _ => "?",
            };
        }

        private static string GetNodeLabel(HexMapNodeType nodeType)
        {
            return nodeType switch
            {
                HexMapNodeType.Start => "Start",
                HexMapNodeType.SmallBattle => "Battle",
                HexMapNodeType.EliteBattle => "Elite",
                HexMapNodeType.Event => "Event",
                HexMapNodeType.Shop => "Shop",
                HexMapNodeType.Rest => "Rest",
                HexMapNodeType.Boss => "Boss",
                _ => string.Empty,
            };
        }

        private static Color GetNodeColor(HexMapNodeType nodeType)
        {
            return nodeType switch
            {
                HexMapNodeType.Start => new Color(0.76f, 0.72f, 0.55f, 1f),
                HexMapNodeType.SmallBattle => new Color(0.72f, 0.48f, 0.42f, 1f),
                HexMapNodeType.EliteBattle => new Color(0.62f, 0.42f, 0.66f, 1f),
                HexMapNodeType.Event => new Color(0.78f, 0.67f, 0.35f, 1f),
                HexMapNodeType.Shop => new Color(0.38f, 0.68f, 0.42f, 1f),
                HexMapNodeType.Rest => new Color(0.42f, 0.62f, 0.38f, 1f),
                HexMapNodeType.Boss => new Color(0.48f, 0.2f, 0.24f, 1f),
                _ => Color.white,
            };
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystemGO);
        }

        private static HexAxialCoord FindClosestExistingCoord(HexGrid grid, HexAxialCoord desired, IEnumerable<HexAxialCoord> blockedCoords = null)
        {
            var blocked = blockedCoords != null ? new HashSet<HexAxialCoord>(blockedCoords) : null;
            float bestDistance = float.PositiveInfinity;
            HexAxialCoord bestCoord = desired;
            foreach (var kvp in grid.Tiles)
            {
                if (blocked != null && blocked.Contains(kvp.Key))
                    continue;

                float distance = HexAxialCoord.Distance(kvp.Key, desired);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCoord = kvp.Key;
                }
            }

            return bestCoord;
        }

        private static GameObject LoadStarter02Prefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(Starter02PrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadEnemyPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(Starter03PrefabPath);
#else
            return null;
#endif
        }

        private static RuntimeAnimatorController LoadStarter02Controller()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Starter02ControllerPath);
#else
            return null;
#endif
        }

        private static GameObject LoadTerrainTilePrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(TerrainTilePrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadCampfirePrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(CampfirePrefabPath);
#else
            return null;
#endif
        }
    }
}
