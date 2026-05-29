using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HexDemo
{
    /// <summary>
    /// Runtime-only bootstrap:
    /// - Generate a hex grid (clickable tiles)
    /// - Spawn a simple 3D unit model
    /// - Click-to-move the unit step-by-step on the hex grid
    /// </summary>
    public sealed class HexDemoRuntimeBootstrap : MonoBehaviour
    {
        private const string Starter02PrefabPath = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Starter_02.prefab";
        private const string Starter03PrefabPath = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_03/Starter_03.prefab";
        private const string Starter02ControllerPath = "Assets/Animations/Starter02/Starter_02.controller";
        private const string TerrainTilePrefabPath = "Assets/Models/Terrain/HexCubeTile.prefab";

        [Header("Grid")]
        public int gridWidth = 11;
        public int gridHeight = 11;
        public float hexSize = 0.55f;
        public float tileY = 0f;

        [Header("Unit")]
        public float unitYOffset = 0.03f;
        public float moveSpeed = 6.5f;
        public float stepArriveThreshold = 0.02f;
        public float stepStopDelay = 0.05f;
        public GameObject unitPrefab;

        private const string GridRootName = "HexGrid_Runtime";
        private const string UnitRootName = "Unit_Runtime";

        public static void TryBootstrap()
        {
            HexAdventureController.TryBootstrap();
        }

        private IEnumerator BootstrapRoutine()
        {
            // Wait one frame so the scene camera & physics world are ready.
            yield return null;
            Screen.SetResolution(1920, 1080, false);
            HexTMPFontProvider.EnsureInitialized();

            // Build grid.
            var gridRoot = new GameObject(GridRootName);
            var grid = gridRoot.AddComponent<HexGrid>();
            grid.width = gridWidth;
            grid.height = gridHeight;
            grid.hexSize = hexSize;
            grid.tileY = tileY;
            grid.tilePrefab = LoadTerrainTilePrefab();
            grid.clickLayerMask = ~0;
            grid.Build();

            ConfigureCameraForGrid(grid);

            var playerCoord = FindClosestExistingCoord(grid, new HexAxialCoord(Mathf.FloorToInt(gridWidth * 0.5f) - 2, Mathf.FloorToInt(gridHeight * 0.5f)));
            var enemyCoord = FindClosestExistingCoord(grid, new HexAxialCoord(Mathf.FloorToInt(gridWidth * 0.5f) + 2, Mathf.FloorToInt(gridHeight * 0.5f)));

            var playerRoot = new GameObject(UnitRootName + "_Player");
            var enemyRoot = new GameObject(UnitRootName + "_Enemy");

            var playerAnimator = SpawnCharacterModel(playerRoot.transform, unitPrefab != null ? unitPrefab : LoadStarter02Prefab());
            var enemyAnimator = SpawnCharacterModel(enemyRoot.transform, LoadEnemyPrefab() ?? LoadStarter02Prefab());

            var playerUnit = playerRoot.AddComponent<HexBattleUnit>();
            playerUnit.Initialize(new HexBattleUnitState
            {
                id = "player_01",
                displayName = "Hero",
                faction = HexBattleFaction.Player,
                maxHealth = 10,
                currentHealth = 10,
                armor = 0,
                energy = 0,
                maxEnergy = 3,
                drawPerTurn = 4,
                maxMovePoints = 2,
                currentMovePoints = 2,
                attackRange = 1,
                coord = playerCoord,
            }, playerAnimator, HexCardLibrary.CreateStarterDeck());
            playerUnit.SnapTo(grid, unitYOffset);

            var enemyUnit = enemyRoot.AddComponent<HexBattleUnit>();
            enemyUnit.Initialize(new HexBattleUnitState
            {
                id = "enemy_01",
                displayName = "Goblin",
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
            enemyUnit.SnapTo(grid, unitYOffset);

            var battleControllerGO = new GameObject("HexBattleController_Runtime");
            var battleController = battleControllerGO.AddComponent<HexBattleController>();
            battleController.unitYOffset = unitYOffset;
            battleController.moveSpeed = moveSpeed;
            battleController.stepStopDelay = stepStopDelay;
            battleController.Initialize(grid, playerUnit, new[] { enemyUnit }, Camera.main);

            Destroy(gameObject);
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
                model.name = "Starter_02_Missing_Fallback";
                model.transform.SetParent(unitRoot, worldPositionStays: false);
                model.transform.localScale = new Vector3(0.6f, 1f, 0.6f);

                var renderer = model.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(0.95f, 0.85f, 0.35f, 1f);
            }

            foreach (var collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false;

            return animator;
        }

        private static void ConfigureCameraForGrid(HexGrid grid)
        {
            var cam = Camera.main;
            if (cam == null || grid == null || grid.Tiles.Count == 0)
                return;

            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var kvp in grid.Tiles)
            {
                Vector3 center = grid.AxialToWorld(kvp.Key);
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

            float padding = grid.hexSize * 1.35f;
            bounds.Expand(new Vector3(padding, 0f, padding));

            float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));
            Vector3 focus = bounds.center;
            Vector3 viewDirection = Quaternion.Euler(50f, 43f, 0f) * Vector3.forward;

            cam.orthographic = true;
            cam.transform.position = focus - viewDirection * 18f;
            cam.transform.LookAt(focus, Vector3.up);

            Vector3[] corners =
            {
                new(bounds.min.x, focus.y, bounds.min.z),
                new(bounds.min.x, focus.y, bounds.max.z),
                new(bounds.max.x, focus.y, bounds.min.z),
                new(bounds.max.x, focus.y, bounds.max.z),
            };

            float halfWidth = 0f;
            float halfHeight = 0f;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 cameraLocal = cam.transform.InverseTransformPoint(corners[i]);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(cameraLocal.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(cameraLocal.y));
            }

            cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect) * 1.03f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainTilePrefabPath);
            if (prefab != null)
                return prefab;

            return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Terrain/HexCube.fbx");
#else
            return null;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            TryBootstrap();
        }

        private static HexAxialCoord FindClosestExistingCoord(HexGrid grid, HexAxialCoord desired)
        {
            float bestDistance = float.PositiveInfinity;
            HexAxialCoord bestCoord = desired;
            foreach (var kvp in grid.Tiles)
            {
                float distance = HexAxialCoord.Distance(kvp.Key, desired);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCoord = kvp.Key;
                }
            }

            return bestCoord;
        }
    }

    public sealed class HexUnitClickMover : MonoBehaviour
    {
        public HexGrid grid;
        public HexAxialCoord startCoord;

        public float unitYOffset = 0.9f;
        public float moveSpeed = 6.5f;
        public float stepArriveThreshold = 0.02f;
        public float stepStopDelay = 0.05f;

        public Camera rayCamera;
        public LayerMask raycastMask = ~0;

        private HexAxialCoord _currentCoord;
        private Coroutine _moveRoutine;
        private bool _moving;
        private Animator _animator;
        private HexTile _hoveredTile;
        private bool _hoverHasColliderHit;

        public void Initialize()
        {
            if (grid == null) grid = Object.FindFirstObjectByType<HexGrid>();
            if (rayCamera == null) rayCamera = Camera.main;
            if (grid != null) raycastMask = grid.clickLayerMask;
            _animator = GetComponentInChildren<Animator>();
            SetMoving(false);

            _currentCoord = startCoord;
            Vector3 p = grid.AxialToWorld(_currentCoord) + Vector3.up * unitYOffset;
            transform.position = p;
        }

        private void Update()
        {
            if (grid == null) return;
            if (rayCamera == null) rayCamera = Camera.main;
            if (rayCamera == null) return;

            UpdateHoverFeedback();

            if (Input.GetMouseButtonDown(0))
            {
                if (_moving) StopMove();
                if (TryGetClickedCoord(out var target) && grid.IsCoordInside(target))
                {
                    if (grid.TryGetTile(target, out var clickedTile))
                        clickedTile.FlashClick();

                    var path = HexPathfinding.FindPath(grid, _currentCoord, target);
                    if (path != null && path.Count >= 2)
                    {
                        _moveRoutine = StartCoroutine(MoveAlongPath(path));
                    }
                }
            }
        }

        private void StopMove()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }
            SnapToNearestTile();
            SetMoving(false);
        }

        private void SnapToNearestTile()
        {
            if (grid == null) return;

            float bestDist = float.PositiveInfinity;
            HexAxialCoord bestCoord = _currentCoord;

            foreach (var kvp in grid.Tiles)
            {
                var coord = kvp.Key;
                Vector3 center = grid.AxialToWorld(coord) + Vector3.up * unitYOffset;
                float d = (center - transform.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestCoord = coord;
                }
            }

            _currentCoord = bestCoord;
            transform.position = grid.AxialToWorld(_currentCoord) + Vector3.up * unitYOffset;
        }

        private void UpdateHoverFeedback()
        {
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
            var hits = Physics.RaycastAll(ray, 500f, raycastMask, QueryTriggerInteraction.Ignore);
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

        private bool TryGetClickedCoord(out HexAxialCoord coord)
        {
            coord = default;

            if (!TryGetHoveredTile(out var tile, out _))
                return false;

            coord = tile.coord;
            return true;
        }

        private bool TryGetCoordFromGroundPlane(Ray ray, out HexAxialCoord coord)
        {
            coord = default;
            if (grid == null || grid.Tiles.Count == 0)
                return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, grid.tileY, 0f));
            if (!plane.Raycast(ray, out float enter))
                return false;

            Vector3 worldPoint = ray.GetPoint(enter);
            float bestDistance = float.PositiveInfinity;
            HexTile bestTile = null;
            foreach (var kvp in grid.Tiles)
            {
                var tile = kvp.Value;
                Vector3 center = grid.AxialToWorld(tile.coord);
                Vector2 a = new Vector2(center.x, center.z);
                Vector2 b = new Vector2(worldPoint.x, worldPoint.z);
                float distance = Vector2.SqrMagnitude(a - b);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTile = tile;
                }
            }

            if (bestTile == null)
                return false;

            coord = bestTile.coord;
            return true;
        }

        private IEnumerator MoveAlongPath(List<HexAxialCoord> path)
        {
            SetMoving(true);

            // Path includes start -> ... -> goal.
            for (int i = 1; i < path.Count; i++)
            {
                var from = path[i - 1];
                var to = path[i];
                _currentCoord = from;

                Vector3 startPos = grid.AxialToWorld(from) + Vector3.up * unitYOffset;
                Vector3 endPos = grid.AxialToWorld(to) + Vector3.up * unitYOffset;
                FaceMovementDirection(endPos - startPos);

                // Start position may have minor drift; sync it each step.
                transform.position = startPos;

                float t = 0f;
                float dist = Vector3.Distance(startPos, endPos);
                float duration = dist / Mathf.Max(0.0001f, moveSpeed);

                while (t < 1f)
                {
                    t += Time.deltaTime / Mathf.Max(0.0001f, duration);
                    transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));

                    // Allow smooth camera follow? Not needed for now.
                    yield return null;
                }

                transform.position = endPos;
                _currentCoord = to;

                // Step stop delay to make it "step-by-step".
                if (stepStopDelay > 0f)
                    yield return new WaitForSeconds(stepStopDelay);
            }

            SetMoving(false);
        }

        private void FaceMovementDirection(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        }

        private void SetMoving(bool moving)
        {
            _moving = moving;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetBool("IsMoving", moving);
        }
    }

    public static class HexPathfinding
    {
        public static List<HexAxialCoord> FindPath(HexGrid grid, HexAxialCoord start, HexAxialCoord goal)
        {
            if (!grid.IsCoordInside(start) || !grid.IsCoordInside(goal))
                return null;

            if (start.Equals(goal))
                return new List<HexAxialCoord> { start };

            var open = new List<HexAxialCoord> { start };
            var cameFrom = new Dictionary<HexAxialCoord, HexAxialCoord>();
            var gScore = new Dictionary<HexAxialCoord, int> { [start] = 0 };
            var fScore = new Dictionary<HexAxialCoord, int> { [start] = HexAxialCoord.Distance(start, goal) };

            while (open.Count > 0)
            {
                // Simple O(n) selection is fine for small grids.
                HexAxialCoord current = open[0];
                int bestF = fScore.ContainsKey(current) ? fScore[current] : int.MaxValue;
                for (int i = 1; i < open.Count; i++)
                {
                    var c = open[i];
                    int f = fScore.ContainsKey(c) ? fScore[c] : int.MaxValue;
                    if (f < bestF)
                    {
                        current = c;
                        bestF = f;
                    }
                }

                if (current.Equals(goal))
                {
                    return ReconstructPath(cameFrom, current, start);
                }

                open.Remove(current);

                foreach (var neighbor in grid.GetNeighbors(current))
                {
                    // All tiles walkable for now.
                    int tentativeG = gScore[current] + 1;
                    if (!gScore.TryGetValue(neighbor, out int oldG) || tentativeG < oldG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + HexAxialCoord.Distance(neighbor, goal);
                        if (!open.Contains(neighbor))
                            open.Add(neighbor);
                    }
                }
            }

            return null;
        }

        private static List<HexAxialCoord> ReconstructPath(
            Dictionary<HexAxialCoord, HexAxialCoord> cameFrom,
            HexAxialCoord current,
            HexAxialCoord start)
        {
            var path = new List<HexAxialCoord> { current };
            while (!current.Equals(start))
            {
                if (!cameFrom.TryGetValue(current, out var prev))
                    break;
                current = prev;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
