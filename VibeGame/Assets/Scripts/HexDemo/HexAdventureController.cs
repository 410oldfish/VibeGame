using System.Collections.Generic;
using System.Linq;
using TMPro;
using TEngine;
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
        private const string ProfessionSelectPrefabPath = "Assets/Prefabs/UI/Adventure/ProfessionSelectRoot.prefab";
        private const string AdventureMapPrefabPath = "Assets/Prefabs/UI/Adventure/AdventureMapRoot.prefab";
        private const string AdventureOverlayModalPrefabPath = "Assets/Prefabs/UI/Adventure/AdventureOverlayModal.prefab";
        private const string AdventureBottomButtonPrefabPath = "Assets/Prefabs/UI/Adventure/AdventureBottomButton.prefab";
        private const string AdventureChoiceButtonPrefabPath = "Assets/Prefabs/UI/Adventure/AdventureChoiceButton.prefab";
        private const string AdventureShopOfferCardPrefabPath = "Assets/Prefabs/UI/Adventure/AdventureShopOfferCard.prefab";
        private const string AdventureRewardCardPrefabPath = "Assets/Prefabs/UI/Adventure/AdventureRewardCard.prefab";

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
        private bool _updateRegistered;

        public static void TryBootstrap()
        {
            if (Object.FindFirstObjectByType<HexAdventureController>() != null)
                return;

            var go = new GameObject(nameof(HexAdventureController));
            go.AddComponent<HexAdventureController>();
        }

        private void Start()
        {
            HexGameModule.Initialize();
            Screen.SetResolution(1920, 1080, false);
            HexTMPFontProvider.EnsureInitialized();
            EnsureEventSystem();
            _networkSession = HexNetworkSessionController.EnsureExists();
            HexDemo.Network.GameNetworkManager.EnsureExists();
            _sceneCamera = Camera.main;
            RegisterUpdate();
            GameEvent.Send(HexGameEvents.AdventureStarted, this);
            ShowProfessionSelection();
        }

        private void OnDestroy()
        {
            UnregisterUpdate();
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
                maxHealth = GetProfessionMaxHealth(profession),
                currentHealth = GetProfessionMaxHealth(profession),
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

        private static int GetProfessionMaxHealth(HexCardProfession profession)
        {
            return profession switch
            {
                HexCardProfession.Warrior => 70,
                _ => 10,
            };
        }

        private void ShowProfessionSelection()
        {
            CleanupRoom();
            if (_mapCanvas != null)
                Destroy(_mapCanvas.gameObject);
            CleanupProfessionSelection();

            if (TryBuildProfessionSelectionFromPrefab(out var background, out var title, out var subtitle, out var networkLabel, out var gridRoot))
            {
                title.alignment = TextAlignmentOptions.Center;
                title.text = "选择职业";
                subtitle.alignment = TextAlignmentOptions.Center;
                subtitle.text = "职业会决定初始牌组，并限制后续奖励和商店出现的职业卡牌。";
                networkLabel.alignment = TextAlignmentOptions.Center;
                networkLabel.text = GetNetworkStatusText();

                CreateProfessionCard(gridRoot.transform, HexCardProfession.Warrior, "战士", "武器切换、力量、击飞和范围攻击。", new Color(0.72f, 0.26f, 0.2f, 1f));
                CreateProfessionCard(gridRoot.transform, HexCardProfession.Paladin, "骑士", "护甲、防守反击和神圣打击。", new Color(0.86f, 0.72f, 0.34f, 1f));
                CreateProfessionCard(gridRoot.transform, HexCardProfession.Druid, "德鲁伊", "变形、地块效果、燃烧和自然控制。", new Color(0.32f, 0.62f, 0.34f, 1f));

                if (_sceneCamera != null)
                {
                    _sceneCamera.orthographic = true;
                    _sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
                    _sceneCamera.transform.rotation = Quaternion.identity;
                    _sceneCamera.orthographicSize = 5.5f;
                    _sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                    _sceneCamera.backgroundColor = new Color(0.12f, 0.13f, 0.17f, 1f);
                }

                return;
            }

            var canvasGO = new GameObject("ProfessionSelect_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _professionCanvas = canvasGO.GetComponent<Canvas>();
            _professionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _professionCanvas.sortingOrder = 120;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var backgroundLegacy = CreatePanel(canvasGO.transform, "Background", Vector2.zero, new Vector2(1920f, 1080f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            backgroundLegacy.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

            var titleLegacy = CreateText(backgroundLegacy.transform, "Title", new Vector2(0f, -92f), new Vector2(900f, 70f), 42f, FontStyles.Bold);
            titleLegacy.alignment = TextAlignmentOptions.Center;
            titleLegacy.text = "\u9009\u62e9\u804c\u4e1a";

            var subtitleLegacy = CreateText(backgroundLegacy.transform, "Subtitle", new Vector2(0f, -158f), new Vector2(960f, 44f), 24f, FontStyles.Normal);
            subtitleLegacy.alignment = TextAlignmentOptions.Center;
            subtitleLegacy.text = "\u804c\u4e1a\u4f1a\u51b3\u5b9a\u521d\u59cb\u724c\u7ec4\uff0c\u5e76\u9650\u5236\u540e\u7eed\u5956\u52b1\u548c\u5546\u5e97\u51fa\u73b0\u7684\u804c\u4e1a\u5361\u724c\u3002";

            var networkLabelLegacy = CreateText(backgroundLegacy.transform, "NetworkStatus", new Vector2(0f, -212f), new Vector2(960f, 34f), 21f, FontStyles.Bold);
            networkLabelLegacy.alignment = TextAlignmentOptions.Center;
            networkLabelLegacy.text = GetNetworkStatusText();

            var gridRootLegacy = CreatePanel(backgroundLegacy.transform, "ProfessionGrid", new Vector2(0f, -20f), new Vector2(1060f, 360f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            gridRootLegacy.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

            var layout = gridRootLegacy.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(320f, 300f);
            layout.spacing = new Vector2(28f, 0f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;

            CreateProfessionCard(gridRootLegacy.transform, HexCardProfession.Warrior, "\u6218\u58eb", "\u6b66\u5668\u5207\u6362\u3001\u529b\u91cf\u3001\u51fb\u98de\u548c\u8303\u56f4\u653b\u51fb\u3002", new Color(0.72f, 0.26f, 0.2f, 1f));
            CreateProfessionCard(gridRootLegacy.transform, HexCardProfession.Paladin, "\u9a91\u58eb", "\u62a4\u7532\u3001\u9632\u5b88\u53cd\u51fb\u548c\u795e\u5723\u6253\u51fb\u3002", new Color(0.86f, 0.72f, 0.34f, 1f));
            CreateProfessionCard(gridRootLegacy.transform, HexCardProfession.Druid, "\u5fb7\u9c81\u4f0a", "\u53d8\u5f62\u3001\u5730\u5757\u6548\u679c\u3001\u71c3\u70e7\u548c\u81ea\u7136\u63a7\u5236\u3002", new Color(0.32f, 0.62f, 0.34f, 1f));

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
            GameEvent.Send(HexGameEvents.ProfessionSelected, profession);

            if (_networkSession.IsOffline || _networkSession.CanHostStartRun())
                StartNewRun(profession);
        }

        private void RegisterUpdate()
        {
            if (_updateRegistered)
                return;

            HexGameModule.Update.AddUpdateListener(UpdateMapPanInput);
            _updateRegistered = true;
        }

        private void UnregisterUpdate()
        {
            if (!_updateRegistered)
                return;

            HexGameModule.Update.RemoveUpdateListener(UpdateMapPanInput);
            _updateRegistered = false;
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
            if (TryBuildMapCanvasFromPrefab())
            {
                BuildMapEdges();
                BuildMapNodes();
                UpdateMapPanBounds();
                ApplyMapPan();
                RefreshMapState();
                return;
            }

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
                _runSummaryLabel.text = $"{GetProfessionDisplayName(_runState.profession)}\nHP {_runState.currentHealth}/{_runState.maxHealth}\nGold {_runState.gold}\nDeck {_runState.deckDefinitions.Count}\nItems {_runState.consumables.Count}/{HexConsumableLibrary.GetSlotCount(_runState.profession)}";

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
            HexMapNodeType nodeType = GetBattleNodeType(title);

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

            var playerCoord = HexBattleSetupUtility.FindClosestExistingCoord(grid, new HexAxialCoord(3, 5));
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
                var enemyCoord = HexBattleSetupUtility.FindClosestExistingCoord(grid, desiredEnemyCoords[Mathf.Min(i, desiredEnemyCoords.Length - 1)], enemyUnits.Select(unit => unit.State.coord).Append(playerCoord));
                string enemyDefinitionId = GetEncounterEnemyDefinitionId(i, nodeType);
                var enemyDefinition = HexCardLibrary.GetEnemyDefinition(enemyDefinitionId);
                if (enemyDefinition == null)
                {
                    Debug.LogError($"Unknown encounter enemyDefinitionId: {enemyDefinitionId}");
                    Destroy(enemyRoot);
                    continue;
                }
                enemyUnit.Initialize(new HexBattleUnitState
                {
                    id = $"enemy_{i + 1}",
                    displayName = enemyDefinition.displayName,
                    enemyDefinitionId = enemyDefinition.id,
                    faction = HexBattleFaction.Enemy,
                    maxHealth = GetEncounterEnemyHealth(enemyDefinition.id),
                    currentHealth = GetEncounterEnemyHealth(enemyDefinition.id),
                    armor = 0,
                    energy = 0,
                    maxEnergy = 0,
                    drawPerTurn = 0,
                    maxMovePoints = 0,
                    currentMovePoints = 0,
                    attackRange = enemyDefinition.attackMaxRange,
                    emptyDrawPileStrengthGain = enemyDefinition.emptyDrawPileStrengthGain,
                    coord = enemyCoord,
                }, enemyAnimator, enemyDefinition.deckDefinitions);
                enemyUnit.SnapTo(grid, 0.03f);
                enemyUnits.Add(enemyUnit);
            }

            var controllerGO = new GameObject("BattleController");
            controllerGO.transform.SetParent(roomRoot.transform, false);
            var battleController = controllerGO.AddComponent<HexBattleController>();
            battleController.Initialize(grid, playerUnit, enemyUnits, _sceneCamera, _runState);
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
                coord = HexBattleSetupUtility.FindClosestExistingCoord(grid, new HexAxialCoord(3, 4)),
            }, playerAnimator, _runState.deckDefinitions);
            playerUnit.SnapTo(grid, 0.03f);

            var campfire = SpawnCampfire(roomRoot.transform, grid, HexBattleSetupUtility.FindClosestExistingCoord(grid, new HexAxialCoord(5, 4)));

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
                    var cardPanel = CreateShopOfferCardRoot(cardGrid.transform, $"Offer_{i}");
                    var cardImage = cardPanel.GetComponent<Image>();
                    if (cardImage != null)
                        cardImage.color = Color.Lerp(offer.card.color, Color.black, 0.12f);

                    var cardTitle = cardPanel.Find("Title")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(cardPanel.transform, "Title", new Vector2(14f, -14f), new Vector2(140f, 32f), 22f, FontStyles.Bold);
                    ConfigureTopLeftText(cardTitle.rectTransform, new Vector2(14f, -14f), new Vector2(140f, 32f), 22f, FontStyles.Bold);
                    cardTitle.text = offer.card.displayName;

                    var body = cardPanel.Find("Body")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(cardPanel.transform, "Body", new Vector2(14f, -58f), new Vector2(146f, 72f), 18f, FontStyles.Normal);
                    ConfigureTopLeftText(body.rectTransform, new Vector2(14f, -58f), new Vector2(146f, 72f), 18f, FontStyles.Normal);
                    body.text = offer.card.effectType == HexCardEffectType.Attack
                        ? $"Deal {offer.card.amount}\nRange {offer.card.range}"
                        : $"Gain {offer.card.amount} Armor";

                    var buyPanel = cardPanel.Find("BuyPanel")?.GetComponent<RectTransform>()
                        ?? CreatePanel(cardPanel.transform, "BuyPanel", new Vector2(0f, 12f), new Vector2(144f, 48f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                    buyPanel.anchorMin = new Vector2(0.5f, 0f);
                    buyPanel.anchorMax = new Vector2(0.5f, 0f);
                    buyPanel.pivot = new Vector2(0.5f, 0f);
                    buyPanel.anchoredPosition = new Vector2(0f, 12f);
                    buyPanel.sizeDelta = new Vector2(144f, 48f);
                    var buyButton = buyPanel.GetComponent<Button>() ?? buyPanel.gameObject.AddComponent<Button>();
                    buyButton.onClick.RemoveAllListeners();
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
                    var buyLabel = buyPanel.Find("BuyLabel")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(buyPanel.transform, "BuyLabel", Vector2.zero, new Vector2(144f, 48f), 20f, FontStyles.Bold);
                    var buyLabelRect = buyLabel.rectTransform;
                    buyLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    buyLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    buyLabelRect.pivot = new Vector2(0.5f, 0.5f);
                    buyLabelRect.anchoredPosition = Vector2.zero;
                    buyLabelRect.sizeDelta = new Vector2(144f, 48f);
                    HexTMPFontProvider.ApplyTo(buyLabel);
                    buyLabel.fontSize = 20f;
                    buyLabel.fontStyle = FontStyles.Bold;
                    buyLabel.color = Color.white;
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
                    var cardPanel = CreateRewardCardRoot(cardGrid.transform, $"Reward_{i}");
                    var cardImage = cardPanel.GetComponent<Image>();
                    if (cardImage != null)
                        cardImage.color = Color.Lerp(reward.color, Color.black, 0.08f);

                    var cost = cardPanel.Find("Cost")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(cardPanel.transform, "Cost", new Vector2(16f, -14f), new Vector2(48f, 34f), 26f, FontStyles.Bold);
                    ConfigureTopLeftText(cost.rectTransform, new Vector2(16f, -14f), new Vector2(48f, 34f), 26f, FontStyles.Bold);
                    cost.text = reward.energyCost < 0 ? "X" : reward.energyCost.ToString();

                    var rarity = cardPanel.Find("Rarity")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(cardPanel.transform, "Rarity", new Vector2(78f, -16f), new Vector2(140f, 28f), 18f, FontStyles.Bold);
                    ConfigureTopLeftText(rarity.rectTransform, new Vector2(78f, -16f), new Vector2(140f, 28f), 18f, FontStyles.Bold);
                    rarity.text = reward.rarity;
                    rarity.alignment = TextAlignmentOptions.TopRight;

                    var title = cardPanel.Find("Title")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(cardPanel.transform, "Title", new Vector2(18f, -50f), new Vector2(204f, 58f), 24f, FontStyles.Bold);
                    ConfigureTopLeftText(title.rectTransform, new Vector2(18f, -50f), new Vector2(204f, 58f), 24f, FontStyles.Bold);
                    title.text = reward.displayName;

                    var meta = cardPanel.Find("Meta")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(cardPanel.transform, "Meta", new Vector2(18f, -98f), new Vector2(204f, 40f), 17f, FontStyles.Bold);
                    ConfigureTopLeftText(meta.rectTransform, new Vector2(18f, -98f), new Vector2(204f, 40f), 17f, FontStyles.Bold);
                    meta.text = $"{reward.cardType}   Cast {reward.castRange}   Area {reward.effectRadius}";

                    var bodyText = cardPanel.Find("Body")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(cardPanel.transform, "Body", new Vector2(18f, -138f), new Vector2(204f, 92f), 18f, FontStyles.Normal);
                    ConfigureTopLeftText(bodyText.rectTransform, new Vector2(18f, -138f), new Vector2(204f, 92f), 18f, FontStyles.Normal);
                    bodyText.text = string.IsNullOrWhiteSpace(reward.description)
                        ? (reward.effectType == HexCardEffectType.Attack ? $"Deal {reward.amount} damage." : $"Gain {reward.amount} armor.")
                        : reward.description;

                    var pickButtonPanel = cardPanel.Find("PickButton")?.GetComponent<RectTransform>()
                        ?? CreatePanel(cardPanel.transform, "PickButton", new Vector2(0f, 14f), new Vector2(180f, 50f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                    pickButtonPanel.anchorMin = new Vector2(0.5f, 0f);
                    pickButtonPanel.anchorMax = new Vector2(0.5f, 0f);
                    pickButtonPanel.pivot = new Vector2(0.5f, 0f);
                    pickButtonPanel.anchoredPosition = new Vector2(0f, 14f);
                    pickButtonPanel.sizeDelta = new Vector2(180f, 50f);
                    var pickButton = pickButtonPanel.GetComponent<Button>() ?? pickButtonPanel.gameObject.AddComponent<Button>();
                    pickButton.onClick.RemoveAllListeners();
                    pickButton.onClick.AddListener(() =>
                    {
                        _runState.deckDefinitions.Add(reward);
                        ShowConsumableReward();
                    });
                    var pickLabel = pickButtonPanel.Find("Label")?.GetComponent<TextMeshProUGUI>()
                        ?? CreateText(pickButtonPanel.transform, "Label", Vector2.zero, new Vector2(180f, 50f), 22f, FontStyles.Bold);
                    var pickLabelRect = pickLabel.rectTransform;
                    pickLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
                    pickLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    pickLabelRect.pivot = new Vector2(0.5f, 0.5f);
                    pickLabelRect.anchoredPosition = Vector2.zero;
                    pickLabelRect.sizeDelta = new Vector2(180f, 50f);
                    HexTMPFontProvider.ApplyTo(pickLabel);
                    pickLabel.fontSize = 22f;
                    pickLabel.fontStyle = FontStyles.Bold;
                    pickLabel.color = Color.white;
                    pickLabel.alignment = TextAlignmentOptions.Center;
                    pickLabel.text = "Choose";
                }
            });
        }

        private void ShowConsumableReward(string excludedRewardId = null)
        {
            var reward = HexConsumableLibrary.GetRandomDropExcluding(excludedRewardId);
            if (reward == null)
            {
                CompleteRoomAndReturnToMap();
                return;
            }

            BuildOverlay("道具掉落", overlay =>
            {
                var title = CreateText(overlay.transform, "ItemTitle", new Vector2(56f, -112f), new Vector2(700f, 50f), 34f, FontStyles.Bold);
                title.text = reward.displayName;
                var body = CreateText(overlay.transform, "ItemBody", new Vector2(56f, -176f), new Vector2(700f, 150f), 24f, FontStyles.Normal);
                body.text = $"{reward.category} · 可使用 {reward.maxUses} 次\n{reward.description}";

                int capacity = HexConsumableLibrary.GetSlotCount(_runState.profession);
                if (_runState.consumables.Count < capacity)
                {
                    CreateConsumableRewardButton(overlay.transform, new Vector2(-120f, 40f), "拾取", () =>
                    {
                        _runState.consumables.Add(new HexConsumableInstance(reward));
                        CompleteRoomAndReturnToMap();
                    });
                    CreateConsumableRewardButton(overlay.transform, new Vector2(120f, 40f), "刷新", () => ShowConsumableReward(reward.id));
                }
                else
                {
                    var hint = CreateText(overlay.transform, "FullHint", new Vector2(56f, -334f), new Vector2(700f, 42f), 22f, FontStyles.Bold);
                    hint.text = "道具栏已满，选择一个道具丢弃并替换：";
                    for (int i = 0; i < _runState.consumables.Count; i++)
                    {
                        int index = i;
                        var existing = _runState.consumables[i];
                        string existingName = existing?.Definition?.displayName ?? "未知道具";
                        CreateChoiceButton(overlay.transform, new Vector2(0f, -30f - i * 64f), $"丢弃 {existingName}", () =>
                        {
                            _runState.consumables[index] = new HexConsumableInstance(reward);
                            CompleteRoomAndReturnToMap();
                        });
                    }

                    CreateConsumableRewardButton(overlay.transform, new Vector2(250f, 210f), "刷新道具", () => ShowConsumableReward(reward.id));
                }

                var skip = CreateBottomButton(overlay.transform, "放弃道具", CompleteRoomAndReturnToMap);
                skip.anchoredPosition = new Vector2(0f, 24f);
            });
        }

        private RectTransform CreateConsumableRewardButton(Transform parent, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction onClick)
        {
            var panel = CreatePanel(parent, $"{label}_ConsumableReward", anchoredPosition, new Vector2(210f, 60f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.GetComponent<Image>().color = label.Contains("刷新")
                ? new Color(0.2f, 0.42f, 0.62f, 0.96f)
                : new Color(0.24f, 0.5f, 0.3f, 0.96f);

            var button = panel.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var text = CreateText(panel, "Label", Vector2.zero, new Vector2(202f, 56f), 24f, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = label;
            return panel;
        }

        private RectTransform CreateShopOfferCardRoot(Transform parent, string instanceName)
        {
            var prefab = LoadAdventureShopOfferCardPrefab();
            if (prefab == null)
                return CreatePanel(parent, instanceName, Vector2.zero, new Vector2(176f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var go = Instantiate(prefab, parent);
            go.name = instanceName;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(176f, 220f);
            return rect;
        }

        private RectTransform CreateRewardCardRoot(Transform parent, string instanceName)
        {
            var prefab = LoadAdventureRewardCardPrefab();
            if (prefab == null)
                return CreatePanel(parent, instanceName, Vector2.zero, new Vector2(240f, 300f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var go = Instantiate(prefab, parent);
            go.name = instanceName;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(240f, 300f);
            return rect;
        }

        private void BuildOverlay(string title, System.Action<RectTransform> bodyBuilder)
        {
            if (_mapCanvas != null)
                _mapCanvas.gameObject.SetActive(true);
            CleanupOverlay();
            _overlayRoot.gameObject.SetActive(true);
            _overlayRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

            var modal = CreateOverlayModalRoot();
            var titleText = modal.Find("Title")?.GetComponent<TextMeshProUGUI>()
                ?? CreateText(modal.transform, "Title", new Vector2(36f, -28f), new Vector2(600f, 42f), 34f, FontStyles.Bold);
            if (modal.Find("Title") != null)
            {
                var titleRect = titleText.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(0f, 1f);
                titleRect.pivot = new Vector2(0f, 1f);
                titleRect.anchoredPosition = new Vector2(36f, -28f);
                titleRect.sizeDelta = new Vector2(600f, 42f);
                HexTMPFontProvider.ApplyTo(titleText);
                titleText.fontSize = 34f;
                titleText.fontStyle = FontStyles.Bold;
                titleText.color = Color.white;
                titleText.alignment = TextAlignmentOptions.TopLeft;
            }
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
            RectTransform buttonPanel;
            Button button;
            var prefab = LoadAdventureBottomButtonPrefab();
            if (prefab != null)
            {
                var instance = Instantiate(prefab, parent);
                instance.name = $"{text}_Button";
                buttonPanel = instance.GetComponent<RectTransform>();
                button = instance.GetComponent<Button>() ?? instance.AddComponent<Button>();
                buttonPanel.anchorMin = new Vector2(0.5f, 0f);
                buttonPanel.anchorMax = new Vector2(0.5f, 0f);
                buttonPanel.pivot = new Vector2(0.5f, 0f);
                buttonPanel.anchoredPosition = new Vector2(0f, 24f);
                buttonPanel.sizeDelta = new Vector2(240f, 70f);
                var image = instance.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.16f, 0.17f, 0.22f, 0.9f);
            }
            else
            {
                buttonPanel = CreatePanel(parent, $"{text}_Button", new Vector2(0f, 24f), new Vector2(240f, 70f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                button = buttonPanel.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            var label = buttonPanel.Find("Label")?.GetComponent<TextMeshProUGUI>()
                ?? CreateText(buttonPanel.transform, "Label", Vector2.zero, new Vector2(240f, 70f), 26f, FontStyles.Bold);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(240f, 70f);
            HexTMPFontProvider.ApplyTo(label);
            label.fontSize = 26f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            return buttonPanel;
        }

        private void CreateChoiceButton(Transform parent, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform panel;
            Button button;
            var prefab = LoadAdventureChoiceButtonPrefab();
            if (prefab != null)
            {
                var instance = Instantiate(prefab, parent);
                instance.name = $"{label}_Choice";
                panel = instance.GetComponent<RectTransform>();
                button = instance.GetComponent<Button>() ?? instance.AddComponent<Button>();
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = anchoredPosition;
                panel.sizeDelta = new Vector2(420f, 58f);
                var image = instance.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.16f, 0.17f, 0.22f, 0.9f);
            }
            else
            {
                panel = CreatePanel(parent, $"{label}_Choice", anchoredPosition, new Vector2(420f, 58f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                button = panel.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            var text = panel.Find("Label")?.GetComponent<TextMeshProUGUI>()
                ?? CreateText(panel.transform, "Label", Vector2.zero, new Vector2(420f, 58f), 24f, FontStyles.Bold);
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(420f, 58f);
            HexTMPFontProvider.ApplyTo(text);
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
        }

        private RectTransform CreateOverlayModalRoot()
        {
            var prefab = LoadAdventureOverlayModalPrefab();
            if (prefab == null)
            {
                var modal = CreatePanel(_overlayRoot.transform, "Modal", Vector2.zero, new Vector2(920f, 640f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                modal.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.96f);
                return modal;
            }

            var instance = Instantiate(prefab, _overlayRoot.transform);
            instance.name = "Modal";
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(920f, 640f);
            var image = instance.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.12f, 0.12f, 0.16f, 0.96f);
            return rect;
        }

        private bool TryBuildProfessionSelectionFromPrefab(out RectTransform background, out TextMeshProUGUI title, out TextMeshProUGUI subtitle, out TextMeshProUGUI networkLabel, out RectTransform gridRoot)
        {
            background = null;
            title = null;
            subtitle = null;
            networkLabel = null;
            gridRoot = null;

            var prefab = LoadProfessionSelectPrefab();
            if (prefab == null)
                return false;

            var instance = Instantiate(prefab);
            _professionCanvas = instance.GetComponent<Canvas>();
            if (_professionCanvas == null)
            {
                Destroy(instance);
                _professionCanvas = null;
                return false;
            }

            _professionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _professionCanvas.sortingOrder = 120;

            var scaler = instance.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = instance.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (instance.GetComponent<GraphicRaycaster>() == null)
                instance.AddComponent<GraphicRaycaster>();

            background = FindByPath<RectTransform>(_professionCanvas.transform, "Background");
            title = FindByPath<TextMeshProUGUI>(_professionCanvas.transform, "Background/Title");
            subtitle = FindByPath<TextMeshProUGUI>(_professionCanvas.transform, "Background/Subtitle");
            networkLabel = FindByPath<TextMeshProUGUI>(_professionCanvas.transform, "Background/NetworkStatus");
            gridRoot = FindByPath<RectTransform>(_professionCanvas.transform, "Background/ProfessionGrid");
            if (background == null || title == null || subtitle == null || networkLabel == null || gridRoot == null)
            {
                Destroy(instance);
                _professionCanvas = null;
                return false;
            }

            background.anchorMin = new Vector2(0.5f, 0.5f);
            background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(1920f, 1080f);
            var bgImage = background.GetComponent<Image>();
            if (bgImage != null)
                bgImage.color = new Color(0.12f, 0.13f, 0.17f, 1f);

            ConfigureTopLeftText(title.rectTransform, new Vector2(0f, -92f), new Vector2(900f, 70f), 42f, FontStyles.Bold);
            ConfigureTopLeftText(subtitle.rectTransform, new Vector2(0f, -158f), new Vector2(960f, 44f), 24f, FontStyles.Normal);
            ConfigureTopLeftText(networkLabel.rectTransform, new Vector2(0f, -212f), new Vector2(960f, 34f), 21f, FontStyles.Bold);

            gridRoot.anchorMin = new Vector2(0.5f, 0.5f);
            gridRoot.anchorMax = new Vector2(0.5f, 0.5f);
            gridRoot.pivot = new Vector2(0.5f, 0.5f);
            gridRoot.anchoredPosition = new Vector2(0f, -20f);
            gridRoot.sizeDelta = new Vector2(1060f, 360f);
            var gridImage = gridRoot.GetComponent<Image>();
            if (gridImage != null)
                gridImage.color = new Color(1f, 1f, 1f, 0.02f);

            var gridLayout = gridRoot.GetComponent<GridLayoutGroup>() ?? gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(320f, 300f);
            gridLayout.spacing = new Vector2(28f, 0f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            return true;
        }

        private bool TryBuildMapCanvasFromPrefab()
        {
            var prefab = LoadAdventureMapPrefab();
            if (prefab == null)
                return false;

            var instance = Instantiate(prefab);
            _mapCanvas = instance.GetComponent<Canvas>();
            if (_mapCanvas == null)
            {
                Destroy(instance);
                _mapCanvas = null;
                return false;
            }

            _mapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _mapCanvas.sortingOrder = 80;
            var scaler = instance.GetComponent<CanvasScaler>() ?? instance.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (instance.GetComponent<GraphicRaycaster>() == null)
                instance.AddComponent<GraphicRaycaster>();

            var background = FindByPath<RectTransform>(_mapCanvas.transform, "Background");
            var title = FindByPath<TextMeshProUGUI>(_mapCanvas.transform, "Background/Title");
            _runSummaryLabel = FindByPath<TextMeshProUGUI>(_mapCanvas.transform, "Background/RunSummary");
            _mapRoot = FindByPath<RectTransform>(_mapCanvas.transform, "Background/MapRoot");
            _overlayRoot = FindByPath<RectTransform>(_mapCanvas.transform, "Background/OverlayRoot");
            if (background == null || title == null || _runSummaryLabel == null || _mapRoot == null || _overlayRoot == null)
            {
                Destroy(instance);
                _mapCanvas = null;
                _runSummaryLabel = null;
                _mapRoot = null;
                _overlayRoot = null;
                return false;
            }

            background.anchorMin = new Vector2(0.5f, 0.5f);
            background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(1920f, 1080f);
            var bgImage = background.GetComponent<Image>();
            if (bgImage != null)
                bgImage.color = new Color(0.86f, 0.83f, 0.88f, 1f);

            ConfigureTopLeftText(title.rectTransform, new Vector2(36f, -24f), new Vector2(560f, 50f), 34f, FontStyles.Bold);
            title.text = "Hex Run Map";
            ConfigureTopLeftText(_runSummaryLabel.rectTransform, new Vector2(36f, -78f), new Vector2(620f, 120f), 24f, FontStyles.Normal);

            _mapRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _mapRoot.pivot = new Vector2(0.5f, 0.5f);
            _mapRoot.anchoredPosition = Vector2.zero;
            _mapRoot.sizeDelta = new Vector2(1500f, 860f);
            var mapImage = _mapRoot.GetComponent<Image>();
            if (mapImage != null)
                mapImage.color = new Color(1f, 1f, 1f, 0.02f);

            _overlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _overlayRoot.pivot = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchoredPosition = Vector2.zero;
            _overlayRoot.sizeDelta = new Vector2(1920f, 1080f);
            var overlayImage = _overlayRoot.GetComponent<Image>();
            if (overlayImage != null)
                overlayImage.color = new Color(0f, 0f, 0f, 0f);
            _overlayRoot.gameObject.SetActive(false);
            return true;
        }

        private static void ConfigureTopLeftText(RectTransform rect, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
                return;
            HexTMPFontProvider.ApplyTo(text);
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
        }

        private static T FindByPath<T>(Transform root, string path) where T : Component
        {
            var node = root.Find(path);
            return node != null ? node.GetComponent<T>() : null;
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

        private static string GetEncounterEnemyDefinitionId(int enemyIndex, HexMapNodeType nodeType)
        {
            if (nodeType == HexMapNodeType.Boss)
                return "tribal_chieftain";
            if (nodeType == HexMapNodeType.EliteBattle)
                return enemyIndex == 0 ? "goblin_captain" : "spear_goblin";

            return (enemyIndex % 3) switch
            {
                1 => "spear_goblin",
                2 => "goblin_captain",
                _ => "goblin",
            };
        }

        private static int GetEncounterEnemyHealth(string enemyDefinitionId)
        {
            return enemyDefinitionId switch
            {
                "tribal_chieftain" => 60,
                "goblin_captain" => 28,
                "spear_goblin" => 14,
                _ => 12,
            };
        }

        private static HexMapNodeType GetBattleNodeType(string title)
        {
            if (!string.IsNullOrWhiteSpace(title) && title.ToLowerInvariant().Contains("boss"))
                return HexMapNodeType.Boss;
            if (!string.IsNullOrWhiteSpace(title) && title.ToLowerInvariant().Contains("elite"))
                return HexMapNodeType.EliteBattle;

            return HexMapNodeType.SmallBattle;
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

        private static GameObject LoadProfessionSelectPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(ProfessionSelectPrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadAdventureMapPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(AdventureMapPrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadAdventureOverlayModalPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(AdventureOverlayModalPrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadAdventureBottomButtonPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(AdventureBottomButtonPrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadAdventureChoiceButtonPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(AdventureChoiceButtonPrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadAdventureShopOfferCardPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(AdventureShopOfferCardPrefabPath);
#else
            return null;
#endif
        }

        private static GameObject LoadAdventureRewardCardPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(AdventureRewardCardPrefabPath);
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
