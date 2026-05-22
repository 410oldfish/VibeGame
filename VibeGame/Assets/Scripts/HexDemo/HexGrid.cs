using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public enum HexTileEffectType
    {
        Burning = 0,
        Poisoned = 1,
        Entangled = 2,
        Custom = 3,
    }

    [System.Serializable]
    public sealed class HexTileEffectState
    {
        public HexTileEffectType effectType;
        public int stacks;
        public int duration;

        public HexTileEffectState Clone()
        {
            return new HexTileEffectState
            {
                effectType = effectType,
                stacks = stacks,
                duration = duration,
            };
        }
    }

    public sealed class HexGrid : MonoBehaviour
    {
        private const float TileModelVisualYOffset = -0.9f;
        private static readonly Vector3[] HexCorners =
        {
            Corner(0),
            Corner(1),
            Corner(2),
            Corner(3),
            Corner(4),
            Corner(5),
        };

        [Header("Grid Size (Axial Island Bounds)")]
        public int width = 10;
        public int height = 10;

        [Header("Hex Geometry")]
        [Tooltip("Pointy-top hex radius in world units.")]
        public float hexSize = 0.5f;

        [Tooltip("Lowest world Y position for the floating island.")]
        public float tileY = 0f;

        [Tooltip("Vertical thickness below each tile top.")]
        public float tileDepth = 0.45f;

        [Tooltip("Height difference between terrain tiers.")]
        public float heightStep = 0.22f;

        [Header("Tile Model")]
        public GameObject tilePrefab;
        [Range(1f, 1.3f)]
        public float tileFillScale = 1.08f;

        [Header("Materials")]
        public Material tileMaterial;
        public Color tileBaseColor = new(0.42f, 0.78f, 0.42f, 1f);
        public Color edgeColor = new(0.16f, 0.34f, 0.27f, 1f);
        public Color sideColor = new(0.18f, 0.27f, 0.42f, 1f);

        [Header("Click Layers")]
        public LayerMask clickLayerMask = ~0;

        private readonly Dictionary<HexAxialCoord, HexTile> _tiles = new();
        private Vector3 _originOffset;
        public IReadOnlyDictionary<HexAxialCoord, HexTile> Tiles => _tiles;

        public void Build()
        {
            Clear();
            CalculateOriginOffset();

            for (int q = 0; q < width; q++)
            {
                for (int r = 0; r < height; r++)
                {
                    var coord = new HexAxialCoord(q, r);
                    if (!ShouldCreateTile(coord))
                        continue;

                    CreateTile(coord);
                }
            }
        }

        private void CalculateOriginOffset()
        {
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int q = 0; q < width; q++)
            {
                for (int r = 0; r < height; r++)
                {
                    Vector3 p = AxialToWorldRaw(new HexAxialCoord(q, r));
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                    minZ = Mathf.Min(minZ, p.z);
                    maxZ = Mathf.Max(maxZ, p.z);
                }
            }

            _originOffset = new Vector3(-(minX + maxX) * 0.5f, 0f, -(minZ + maxZ) * 0.5f);
        }

        private bool ShouldCreateTile(HexAxialCoord coord)
        {
            float centerQ = (width - 1) * 0.5f;
            float centerR = (height - 1) * 0.5f;
            float nq = (coord.q - centerQ) / Mathf.Max(1f, centerQ);
            float nr = (coord.r - centerR) / Mathf.Max(1f, centerR);
            float island = nq * nq * 0.95f + nr * nr * 0.9f + nq * nr * 0.25f;
            float edgeNoise = Mathf.PerlinNoise(coord.q * 0.61f + 8.2f, coord.r * 0.61f + 3.7f) * 0.25f;
            return island < 1.05f + edgeNoise;
        }

        private void CreateTile(HexAxialCoord coord)
        {
            float terrainLift = GetTerrainHeight(coord);
            var tileGO = new GameObject($"Tile_{coord.q}_{coord.r}");
            tileGO.transform.SetParent(transform, worldPositionStays: false);
            tileGO.transform.position = AxialToWorld(coord) + Vector3.up * terrainLift;
            float topHeight = CreateTileVisual(tileGO.transform, coord, terrainLift);

            var tile = tileGO.AddComponent<HexTile>();
            tile.coord = coord;
            tile.grid = this;
            tile.topHeight = topHeight;
            tile.CacheVisuals();

            _tiles[coord] = tile;
        }

        private float CreateTileVisual(Transform parent, HexAxialCoord coord, float terrainLift)
        {
            if (tilePrefab != null)
                return CreatePrefabTileVisual(parent, coord, terrainLift);

            return CreateFallbackTileVisual(parent, coord, terrainLift);
        }

        private float CreatePrefabTileVisual(Transform parent, HexAxialCoord coord, float terrainLift)
        {
            var instance = Instantiate(tilePrefab, parent);
            instance.name = "Tile_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var bounds = CalculateLocalBounds(instance.transform);
            float sourceDiameter = Mathf.Max(bounds.size.x, bounds.size.z, 0.0001f);
            float targetDiameter = hexSize * 2f;
            float scale = (targetDiameter / sourceDiameter) * tileFillScale;
            instance.transform.localScale = Vector3.one * scale;

            bounds = CalculateLocalBounds(instance.transform);
            instance.transform.localPosition = new Vector3(0f, TileModelVisualYOffset - bounds.min.y, 0f);

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            Color topColor = GetTerrainColor(coord, terrainLift);
            Color wallColor = GetSideColor(terrainLift);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null)
                        continue;

                    var materialCopy = new Material(materials[m]);
                    bool topLike = materials.Length == 1 || m == 0;
                    materialCopy.color = topLike ? topColor : wallColor;
                    materials[m] = materialCopy;
                }
                renderer.sharedMaterials = materials;
            }

            bounds = CalculateLocalBounds(instance.transform);
            return bounds.max.y;
        }

        private float CreateFallbackTileVisual(Transform parent, HexAxialCoord coord, float terrainLift)
        {
            var mf = parent.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateHexPrismMesh(hexSize, tileDepth, tileDepth);

            var mr = parent.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[]
            {
                CreateDefaultMaterial(GetTerrainColor(coord, terrainLift)),
                CreateDefaultMaterial(GetSideColor(terrainLift))
            };

            return tileDepth;
        }

        private float GetTerrainHeight(HexAxialCoord coord)
        {
            float n = Mathf.PerlinNoise(coord.q * 0.22f + 12.4f, coord.r * 0.22f + 5.9f);
            float centerQ = (width - 1) * 0.5f;
            float centerR = (height - 1) * 0.5f;
            float dist = Mathf.Abs(coord.q - centerQ) / Mathf.Max(1f, centerQ) +
                         Mathf.Abs(coord.r - centerR) / Mathf.Max(1f, centerR);

            int tier = n > 0.68f ? 2 : n > 0.43f ? 1 : 0;
            if (dist > 1.45f)
                tier = Mathf.Min(tier, 1);

            return tier * heightStep;
        }

        private Color GetTerrainColor(HexAxialCoord coord, float topHeight)
        {
            float n = Mathf.PerlinNoise(coord.q * 0.37f + 1.1f, coord.r * 0.37f + 2.6f);
            if (n < 0.18f)
                return new Color(0.35f, 0.78f, 0.82f, 1f);

            if (topHeight >= heightStep * 1.5f)
                return new Color(0.22f, 0.57f, 0.43f, 1f);

            if (n > 0.67f)
                return new Color(0.72f, 0.9f, 0.37f, 1f);

            return new Color(0.43f, 0.78f, 0.38f, 1f);
        }

        private Color GetSideColor(float topHeight)
        {
            float shade = 1f - topHeight * 0.28f;
            return new Color(sideColor.r * shade, sideColor.g * shade, sideColor.b * shade, 1f);
        }

        private void Clear()
        {
            _tiles.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        public Vector3 AxialToWorld(HexAxialCoord coord)
        {
            return AxialToWorldRaw(coord) + _originOffset;
        }

        public Vector3 GetTileSurfaceWorld(HexAxialCoord coord)
        {
            Vector3 p = AxialToWorld(coord);
            if (_tiles.TryGetValue(coord, out var tile))
                p.y = tile.transform.position.y + tile.topHeight;
            return p;
        }

        private Vector3 AxialToWorldRaw(HexAxialCoord coord)
        {
            float x = hexSize * Mathf.Sqrt(3f) * (coord.q + coord.r * 0.5f);
            float z = hexSize * 1.5f * coord.r;
            return new Vector3(x, tileY, z);
        }

        public bool TryGetTile(HexAxialCoord coord, out HexTile tile) => _tiles.TryGetValue(coord, out tile);

        public bool IsCoordInside(HexAxialCoord coord)
        {
            if (_tiles.Count > 0)
                return _tiles.ContainsKey(coord);

            return coord.q >= 0 && coord.q < width && coord.r >= 0 && coord.r < height;
        }

        public IEnumerable<HexAxialCoord> GetNeighbors(HexAxialCoord coord)
        {
            for (int i = 0; i < HexAxialCoord.Directions.Length; i++)
            {
                var n = HexAxialCoord.Neighbor(coord, i);
                if (IsCoordInside(n))
                    yield return n;
            }
        }

        private static Material CreateDefaultMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = shader != null ? new Material(shader) : new Material(Shader.Find("UI/Default"));
            mat.color = color;
            return mat;
        }

        private static Bounds CalculateLocalBounds(Transform root)
        {
            bool hasBounds = false;
            Bounds result = default;

            foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null)
                    continue;

                var meshBounds = meshFilter.sharedMesh.bounds;
                var matrix = root.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                var corners = GetBoundsCorners(meshBounds);
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(corners[i]);
                    if (!hasBounds)
                    {
                        result = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        result.Encapsulate(point);
                    }
                }
            }

            return hasBounds ? result : new Bounds(Vector3.zero, Vector3.one);
        }

        private static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };
        }

        private static Mesh CreateHexPrismMesh(float radius, float topY, float depth)
        {
            var vertices = new List<Vector3>();
            var topTriangles = new List<int>();
            var sideTriangles = new List<int>();
            float bottomY = topY - depth;

            vertices.Add(new Vector3(0f, topY, 0f));
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i + 30f);
                vertices.Add(new Vector3(radius * Mathf.Cos(angle), topY, radius * Mathf.Sin(angle)));
            }

            int bottomStart = vertices.Count;
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i + 30f);
                vertices.Add(new Vector3(radius * Mathf.Cos(angle), bottomY, radius * Mathf.Sin(angle)));
            }

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, bottomY, 0f));

            for (int i = 0; i < 6; i++)
            {
                topTriangles.Add(0);
                topTriangles.Add(i + 1);
                topTriangles.Add(((i + 1) % 6) + 1);
            }

            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                int topA = i + 1;
                int topB = next + 1;
                int bottomA = bottomStart + i;
                int bottomB = bottomStart + next;

                sideTriangles.Add(topA);
                sideTriangles.Add(bottomA);
                sideTriangles.Add(topB);
                sideTriangles.Add(topB);
                sideTriangles.Add(bottomA);
                sideTriangles.Add(bottomB);
            }

            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                int bottomA = bottomStart + i;
                int bottomB = bottomStart + next;

                sideTriangles.Add(bottomCenter);
                sideTriangles.Add(bottomB);
                sideTriangles.Add(bottomA);
            }

            var mesh = new Mesh { name = "HexPrism" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(topTriangles, 0);
            mesh.SetTriangles(sideTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Corner(int index)
        {
            float angle = Mathf.Deg2Rad * (60f * index + 30f);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }

    public sealed class HexTile : MonoBehaviour
    {
        public HexAxialCoord coord;
        public HexGrid grid;
        public float topHeight;
        private readonly List<HexTileEffectState> _effects = new();

        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private MeshRenderer _clickRenderer;
        private readonly List<MaterialState> _materialStates = new();
        private readonly List<Color> _clickBaseColors = new();
        private Coroutine _flashRoutine;
        private float _hoverStrength;
        private bool _hoverColliderHit;
        private bool _rangeVisible;
        private bool _rangeTargetable;
        private bool _moveVisible;
        private bool _moveReachable;
        private bool _pathVisible;
        private bool _pathIsTarget;
        private bool _pathIsInvalid;

        private static readonly Color HoverTint = new(1f, 0.92f, 0.55f, 1f);
        private static readonly Color ClickTint = new(1f, 0.72f, 0.32f, 1f);
        private static readonly Color ColliderTint = new(0.22f, 0.9f, 1f, 0.18f);
        private static readonly Color ColliderHitTint = new(1f, 0.62f, 0.25f, 0.3f);
        private static readonly Color RangeTint = new(0.34f, 0.8f, 0.95f, 0.12f);
        private static readonly Color RangeTargetTint = new(1f, 0.4f, 0.2f, 0.28f);
        private static readonly Color MoveReachableTint = new(0.36f, 0.9f, 0.42f, 0.22f);
        private static readonly Color MoveBlockedTint = new(1f, 0.24f, 0.24f, 0.34f);
        private static readonly Color PathTileTint = new(0.14f, 0.5f, 1f, 0.78f);
        private static readonly Color PathTargetTint = new(1f, 0.9f, 0.18f, 0.92f);
        private static readonly Color PathInvalidTint = new(1f, 0.18f, 0.18f, 0.9f);

        public IReadOnlyList<HexTileEffectState> Effects => _effects;

        public void AddOrRefreshEffect(HexTileEffectType effectType, int stacks, int duration)
        {
            if (stacks <= 0 || duration <= 0)
                return;

            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].effectType != effectType)
                    continue;

                _effects[i].stacks += stacks;
                _effects[i].duration = Mathf.Max(_effects[i].duration, duration);
                return;
            }

            _effects.Add(new HexTileEffectState
            {
                effectType = effectType,
                stacks = stacks,
                duration = duration,
            });
        }

        public bool TryGetEffect(HexTileEffectType effectType, out HexTileEffectState effect)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].effectType != effectType)
                    continue;

                effect = _effects[i];
                return true;
            }

            effect = null;
            return false;
        }

        public void RemoveEffect(HexTileEffectType effectType)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (_effects[i].effectType == effectType)
                    _effects.RemoveAt(i);
            }
        }

        public void TickEffectsDuration()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                _effects[i].duration = Mathf.Max(0, _effects[i].duration - 1);
                if (_effects[i].duration <= 0)
                    _effects.RemoveAt(i);
            }
        }

        public void CacheVisuals()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _materialStates.Clear();
            _clickBaseColors.Clear();

            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                    continue;

                var materials = renderer.materials;
                bool isClickRenderer = renderer.name == "Top_Click_Collider";
                if (isClickRenderer && renderer is MeshRenderer meshRenderer)
                {
                    _clickRenderer = meshRenderer;
                    EnsureClickRendererSetup(meshRenderer, materials);
                    materials = meshRenderer.materials;
                }

                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || !material.HasProperty("_Color"))
                        continue;

                    if (isClickRenderer)
                        _clickBaseColors.Add(material.color);
                    else
                        _materialStates.Add(new MaterialState(material, material.color));
                }
            }

            ApplyVisuals();
        }

        public void SetHoverState(bool hovered, bool hasColliderHit)
        {
            _hoverStrength = hovered ? 1f : 0f;
            _hoverColliderHit = hasColliderHit;
            ApplyVisuals();
        }

        public void SetRangeIndicator(bool visible, bool targetable)
        {
            _rangeVisible = visible;
            _rangeTargetable = targetable;
            ApplyVisuals();
        }

        public void SetMoveIndicator(bool visible, bool reachable)
        {
            _moveVisible = visible;
            _moveReachable = reachable;
            ApplyVisuals();
        }

        public void SetPathPreview(bool visible, bool isTarget, bool isInvalid)
        {
            _pathVisible = visible;
            _pathIsTarget = isTarget;
            _pathIsInvalid = isInvalid;
            ApplyVisuals();
        }

        public void FlashClick()
        {
            if (!isActiveAndEnabled)
                return;

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);

            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private System.Collections.IEnumerator FlashRoutine()
        {
            const float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.PingPong(elapsed * 8f, 1f);
                ApplyFlash(pulse);
                yield return null;
            }

            ApplyFlash(0f);
            _flashRoutine = null;
        }

        private void ApplyVisuals()
        {
            Color tileTint = Color.clear;
            float tileTintStrength = 0f;
            if (_pathVisible)
            {
                tileTint = _pathIsInvalid ? PathInvalidTint : _pathIsTarget ? PathTargetTint : PathTileTint;
                tileTintStrength = _pathIsInvalid ? 0.65f : _pathIsTarget ? 0.58f : 0.48f;
            }
            else if (_rangeVisible)
            {
                tileTint = _rangeTargetable ? RangeTargetTint : RangeTint;
                tileTintStrength = _rangeTargetable ? 0.3f : 0.18f;
            }
            else if (_moveVisible)
            {
                tileTint = _moveReachable ? MoveReachableTint : MoveBlockedTint;
                tileTintStrength = _moveReachable ? 0.2f : 0.26f;
            }

            for (int i = 0; i < _materialStates.Count; i++)
            {
                var state = _materialStates[i];
                Color color = state.baseColor;
                if (tileTintStrength > 0f)
                    color = Color.Lerp(color, tileTint, tileTintStrength);
                if (_hoverStrength > 0.001f)
                    color = Color.Lerp(color, HoverTint, _hoverStrength * 0.35f);
                state.material.color = color;
            }

            if (_clickRenderer == null)
                return;

            bool showCollider = _hoverStrength > 0.001f || _rangeVisible || _moveVisible || _pathVisible;
            _clickRenderer.enabled = showCollider;
            var materials = _clickRenderer.materials;
            Color targetTint = _pathVisible
                ? (_pathIsInvalid ? PathInvalidTint : _pathIsTarget ? PathTargetTint : PathTileTint)
                : _rangeVisible
                ? (_rangeTargetable ? RangeTargetTint : RangeTint)
                : _moveVisible
                    ? (_moveReachable ? MoveReachableTint : MoveBlockedTint)
                : (_hoverColliderHit ? ColliderHitTint : ColliderTint);
            float lerpStrength = (_pathVisible || _rangeVisible || _moveVisible) ? 1f : _hoverStrength;
            for (int i = 0; i < materials.Length && i < _clickBaseColors.Count; i++)
                materials[i].color = Color.Lerp(_clickBaseColors[i], targetTint, lerpStrength);
        }

        private void ApplyFlash(float flashStrength)
        {
            for (int i = 0; i < _materialStates.Count; i++)
            {
                var state = _materialStates[i];
                state.material.color = Color.Lerp(state.baseColor, ClickTint, flashStrength * 0.75f);
            }
        }

        private static void EnsureClickRendererSetup(MeshRenderer renderer, Material[] materials)
        {
            if (materials == null || materials.Length == 0 || materials[0] == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
                materials = new[] { new Material(shader) };
                renderer.materials = materials;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                    continue;

                material.color = new Color(0.22f, 0.9f, 1f, 0f);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        private readonly struct MaterialState
        {
            public readonly Material material;
            public readonly Color baseColor;

            public MaterialState(Material material, Color baseColor)
            {
                this.material = material;
                this.baseColor = baseColor;
            }
        }
    }
}
