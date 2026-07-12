using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HexDemo
{
    public sealed class HexBattleSandboxBootstrap : MonoBehaviour
    {
        private const string DefaultScenarioResourcePath = "Debug/BattleSandbox_Default";
        private const string Starter02PrefabPath = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Starter_02.prefab";
        private const string Starter03PrefabPath = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_03/Starter_03.prefab";
        private const string Starter02ControllerPath = "Assets/Animations/Starter02/Starter_02.controller";
        private const string TerrainTilePrefabPath = "Assets/Models/Terrain/HexCubeTile.prefab";

        [SerializeField] private HexBattleSandboxScenarioSO scenario;
        [SerializeField] private bool autoStartOnPlay = true;

        private bool _started;

        private void Awake()
        {
            TryAssignDefaultScenario();
        }

        private void Start()
        {
            if (!autoStartOnPlay || _started)
                return;

            _started = true;
            BuildSandboxBattle();
        }

        public void BuildSandboxBattle()
        {
            TryAssignDefaultScenario();
            if (scenario == null)
            {
                Debug.LogError($"[BattleSandbox] Scenario is null. Please assign one in Inspector or create/load Resources/{DefaultScenarioResourcePath}.asset");
                return;
            }

            EnsureEventSystem();

            var root = new GameObject("BattleSandboxRuntime");
            var grid = BuildGrid(root.transform, scenario.terrain);
            var battleCamera = EnsureBattleCamera(grid);

            var player = BuildPlayerUnit(root.transform, grid, scenario.player);
            var enemies = BuildEnemyUnits(root.transform, grid, scenario.enemies, player.State.coord);

            var controllerGO = new GameObject("BattleController");
            controllerGO.transform.SetParent(root.transform, false);
            var battleController = controllerGO.AddComponent<HexBattleController>();
            battleController.Initialize(grid, player, enemies, battleCamera);
        }

        private static HexGrid BuildGrid(Transform parent, HexBattleSandboxScenarioSO.TerrainConfig config)
        {
            var gridGO = new GameObject("BattleGrid");
            gridGO.transform.SetParent(parent, false);
            var grid = gridGO.AddComponent<HexGrid>();
            grid.width = Mathf.Max(3, config.width);
            grid.height = Mathf.Max(3, config.height);
            grid.hexSize = Mathf.Max(0.1f, config.hexSize);
            grid.tileY = config.tileY;
            grid.tileDepth = Mathf.Max(0.05f, config.tileDepth);
            grid.heightStep = Mathf.Max(0f, config.heightStep);
            grid.generateFeatureTerrain = config.generateFeatureTerrain;
            grid.highGroundChance = Mathf.Clamp01(config.highGroundChance);
            grid.ruinChance = Mathf.Clamp01(config.ruinChance);
            grid.clickLayerMask = ~0;
            grid.tilePrefab = LoadTerrainTilePrefab();
            grid.Build();

            ApplyTerrainOverrides(grid, config.overrides);
            return grid;
        }

        private static void ApplyTerrainOverrides(HexGrid grid, List<HexBattleSandboxScenarioSO.TerrainOverride> overrides)
        {
            if (overrides == null)
                return;

            for (int i = 0; i < overrides.Count; i++)
            {
                var item = overrides[i];
                var coord = new HexAxialCoord(item.coord.x, item.coord.y);
                if (!grid.TryGetTile(coord, out var tile) || tile == null)
                    continue;

                HexTerrainZoneType zone = item.zone;
                if (zone == HexTerrainZoneType.Normal && item.baseTerrain == HexTerrainBaseType.Pit)
                    zone = HexTerrainZoneType.Pit;
                tile.zone = zone;

                if (!string.IsNullOrWhiteSpace(item.propId))
                    tile.SetProp(item.propId, item.structureHp > 0 ? item.structureHp : (int?)null);
                else if (item.structureType != HexTerrainStructureType.None)
                    tile.SetStructure(item.structureType, item.structureHp);

                tile.SetPickup(item.pickupType, item.pickupAmount);
            }
        }

        private static HexBattleUnit BuildPlayerUnit(Transform parent, HexGrid grid, HexBattleSandboxScenarioSO.PlayerConfig config)
        {
            var root = new GameObject("PlayerUnit");
            root.transform.SetParent(parent, false);
            var animator = SpawnCharacterModel(root.transform, LoadStarter02Prefab());
            var unit = root.AddComponent<HexBattleUnit>();
            var deck = ResolveDeckFromIds(config.deckCardIds, config.profession, true);
            var desired = new HexAxialCoord(config.spawnCoord.x, config.spawnCoord.y);
            var coord = HexBattleSetupUtility.FindClosestExistingCoord(grid, desired);

            unit.Initialize(new HexBattleUnitState
            {
                id = "sandbox_player",
                displayName = string.IsNullOrWhiteSpace(config.displayName) ? "Hero" : config.displayName,
                faction = HexBattleFaction.Player,
                profession = config.profession,
                maxHealth = Mathf.Max(1, config.maxHealth),
                currentHealth = Mathf.Clamp(config.currentHealth, 1, Mathf.Max(1, config.maxHealth)),
                armor = 0,
                energy = 0,
                maxEnergy = Mathf.Max(0, config.maxEnergy),
                drawPerTurn = Mathf.Max(0, config.drawPerTurn),
                maxMovePoints = Mathf.Max(0, config.maxMovePoints),
                currentMovePoints = Mathf.Max(0, config.maxMovePoints),
                attackRange = Mathf.Max(1, config.attackRange),
                coord = coord,
            }, animator, deck);

            unit.SnapTo(grid, 0.03f);
            return unit;
        }

        private static List<HexBattleUnit> BuildEnemyUnits(Transform parent, HexGrid grid, List<HexBattleSandboxScenarioSO.EnemyConfig> configs, HexAxialCoord playerCoord)
        {
            var enemies = new List<HexBattleUnit>();
            if (configs == null)
                return enemies;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                var enemyDef = HexCardLibrary.GetEnemyDefinition(cfg.enemyDefinitionId);
                var desired = new HexAxialCoord(cfg.spawnCoord.x, cfg.spawnCoord.y);
                var occupied = enemies.Select(e => e.State.coord).Append(playerCoord);
                var spawn = HexBattleSetupUtility.FindClosestExistingCoord(grid, desired, occupied);
                var deck = ResolveDeckFromIds(cfg.deckCardIds, HexCardProfession.Monster, false);
                if (deck.Count == 0)
                    deck = enemyDef.deckDefinitions;

                var root = new GameObject($"Enemy_{i + 1}_{enemyDef.id}");
                root.transform.SetParent(parent, false);
                var animator = SpawnCharacterModel(root.transform, LoadEnemyPrefab() ?? LoadStarter02Prefab());
                var unit = root.AddComponent<HexBattleUnit>();

                int maxHealth = cfg.maxHealthOverride > 0 ? cfg.maxHealthOverride : GetDefaultEnemyHealth(enemyDef.id);
                int currentHealth = cfg.currentHealthOverride > 0 ? Mathf.Min(cfg.currentHealthOverride, maxHealth) : maxHealth;
                unit.Initialize(new HexBattleUnitState
                {
                    id = $"sandbox_enemy_{i + 1}",
                    displayName = string.IsNullOrWhiteSpace(cfg.displayNameOverride) ? enemyDef.displayName : cfg.displayNameOverride,
                    enemyDefinitionId = enemyDef.id,
                    faction = HexBattleFaction.Enemy,
                    maxHealth = maxHealth,
                    currentHealth = Mathf.Max(1, currentHealth),
                    armor = 0,
                    energy = 0,
                    maxEnergy = 0,
                    drawPerTurn = 0,
                    maxMovePoints = 0,
                    currentMovePoints = 0,
                    attackRange = enemyDef.attackMaxRange,
                    emptyDrawPileStrengthGain = enemyDef.emptyDrawPileStrengthGain,
                    coord = spawn,
                }, animator, deck);

                unit.SnapTo(grid, 0.03f);
                enemies.Add(unit);
            }

            return enemies;
        }

        private static List<HexCardDefinition> ResolveDeckFromIds(List<string> ids, HexCardProfession fallbackProfession, bool allowStarterFallback)
        {
            var result = new List<HexCardDefinition>();
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    var card = HexCardLibrary.GetCardById(id.Trim());
                    if (card == null)
                    {
                        Debug.LogWarning($"[BattleSandbox] Unknown card id: {id}");
                        continue;
                    }

                    result.Add(card);
                }
            }

            if (result.Count == 0 && allowStarterFallback)
                result = HexCardLibrary.CreateStarterDeck(fallbackProfession);
            return result;
        }

        private static Camera EnsureBattleCamera(HexGrid grid)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }

            if (grid != null && grid.Tiles.Count > 0)
            {
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
                cam.orthographic = true;
                cam.transform.position = focus - viewDirection * 18f;
                cam.transform.LookAt(focus, Vector3.up);
                cam.orthographicSize = 5.6f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.28f, 0.55f, 0.78f, 1f);
            }

            return cam;
        }

        private static Animator SpawnCharacterModel(Transform unitRoot, GameObject prefab)
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
            }

            foreach (var collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            return animator;
        }

        private static int GetDefaultEnemyHealth(string enemyDefinitionId)
        {
            return enemyDefinitionId switch
            {
                "tribal_chieftain" => 60,
                "goblin_captain" => 28,
                "spear_goblin" => 14,
                _ => 12,
            };
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystemGO);
        }

        private void OnValidate()
        {
            TryAssignDefaultScenario();
            if (scenario == null)
                return;
            if (scenario.enemies == null || scenario.enemies.Count == 0)
                Debug.LogWarning("[BattleSandbox] scenario.enemies is empty.");

            var coordSet = new HashSet<Vector2Int>();
            for (int i = 0; i < scenario.enemies.Count; i++)
            {
                var enemy = scenario.enemies[i];
                if (enemy == null)
                    continue;

                if (!coordSet.Add(enemy.spawnCoord))
                    Debug.LogWarning($"[BattleSandbox] Duplicate enemy spawn coord: {enemy.spawnCoord}");

                if (HexCardLibrary.GetEnemyDefinition(enemy.enemyDefinitionId).id != enemy.enemyDefinitionId && !string.IsNullOrWhiteSpace(enemy.enemyDefinitionId))
                    Debug.LogWarning($"[BattleSandbox] Unknown enemyDefinitionId, fallback will apply: {enemy.enemyDefinitionId}");

                if (enemy.deckCardIds == null)
                    continue;

                for (int c = 0; c < enemy.deckCardIds.Count; c++)
                {
                    var id = enemy.deckCardIds[c];
                    if (!string.IsNullOrWhiteSpace(id) && HexCardLibrary.GetCardById(id.Trim()) == null)
                        Debug.LogWarning($"[BattleSandbox] Unknown enemy card id: {id}");
                }
            }

            if (scenario.player != null && scenario.player.deckCardIds != null)
            {
                for (int c = 0; c < scenario.player.deckCardIds.Count; c++)
                {
                    var id = scenario.player.deckCardIds[c];
                    if (!string.IsNullOrWhiteSpace(id) && HexCardLibrary.GetCardById(id.Trim()) == null)
                        Debug.LogWarning($"[BattleSandbox] Unknown player card id: {id}");
                }
            }
        }

        private void TryAssignDefaultScenario()
        {
            if (scenario != null)
                return;

            scenario = Resources.Load<HexBattleSandboxScenarioSO>(DefaultScenarioResourcePath);
            if (scenario == null)
                return;

            Debug.Log($"[BattleSandbox] Auto loaded default scenario from Resources/{DefaultScenarioResourcePath}.");
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
    }
}
