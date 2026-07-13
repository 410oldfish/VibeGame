using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HexDemo
{
    [Flags]
    public enum HexStructureInteractionKind
    {
        None = 0,
        Barrier = 1,
        Ruin = 2,
    }

    public interface IHexStructureInteractionTarget
    {
        HexBattleUnit OwnerUnit { get; }
        HexAxialCoord Coord { get; }
        HexStructureInteractionKind SupportedInteractions { get; }
    }

    public sealed class HexLivingWallSegmentView : MonoBehaviour, IHexStructureInteractionTarget
    {
        public HexBattleUnit OwnerUnit { get; private set; }
        public HexAxialCoord Coord { get; private set; }
        public HexStructureInteractionKind SupportedInteractions =>
            HexStructureInteractionKind.Barrier | HexStructureInteractionKind.Ruin;

        public void Initialize(HexBattleUnit owner, HexAxialCoord coord)
        {
            OwnerUnit = owner;
            Coord = coord;
        }

        public void SetCoord(HexAxialCoord coord)
        {
            Coord = coord;
        }
    }

    public sealed class HexLivingWallView : MonoBehaviour
    {
        private sealed class SegmentBinding
        {
            public Transform transform;
            public Renderer renderer;
            public HexLivingWallSegmentView target;
            public bool isCore;
        }

        private static Material s_coreMaterial;
        private static Material s_segmentMaterial;
        private static Material s_previewMaterial;
        private static Material s_dangerMaterial;
        private HexBattleUnit _unit;
        private HexGrid _grid;
        private Transform _segmentsRoot;
        private readonly Dictionary<HexAxialCoord, (Renderer renderer, bool isCore)> _segments = new();
        private readonly List<SegmentBinding> _segmentBindings = new();

        public void Initialize(HexBattleUnit unit, HexGrid grid)
        {
            _unit = unit;
            _grid = grid;
            Rebuild();
        }

        public void Rebuild()
        {
            if (_unit == null || _unit.State == null || _grid == null)
                return;

            ClearSegments();
            var root = new GameObject("LivingWallSegments");
            root.transform.SetParent(transform, false);
            _segmentsRoot = root.transform;

            IReadOnlyList<HexAxialCoord> occupied = _unit.OccupiedCoords;
            Vector3 coreWorld = _grid.AxialToWorld(_unit.State.coord);
            for (int i = 0; i < occupied.Count; i++)
            {
                HexAxialCoord coord = occupied[i];
                bool isCore = coord.Equals(_unit.State.coord);
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = isCore ? "LivingWallCore" : $"LivingWallSegment_{i}";
                segment.transform.SetParent(_segmentsRoot, false);
                Vector3 local = _grid.AxialToWorld(coord) - coreWorld;
                segment.transform.localPosition = new Vector3(local.x, 0.58f, local.z);
                float width = Mathf.Max(0.25f, _grid.hexSize * 1.25f);
                segment.transform.localScale = new Vector3(width, isCore ? 1.35f : 1.05f, width);

                var renderer = segment.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = GetMaterial(isCore);
                    _segments[coord] = (renderer, isCore);
                }
                var target = segment.AddComponent<HexLivingWallSegmentView>();
                target.Initialize(_unit, coord);
                _segmentBindings.Add(new SegmentBinding
                {
                    transform = segment.transform,
                    renderer = renderer,
                    target = target,
                    isCore = isCore,
                });
            }
        }

        public void SyncToOwner()
        {
            if (_unit == null || _unit.State == null || _grid == null)
                return;

            IReadOnlyList<HexAxialCoord> occupied = _unit.OccupiedCoords;
            if (_segmentsRoot == null || _segmentBindings.Count != occupied.Count)
            {
                Rebuild();
                return;
            }

            _segments.Clear();
            Vector3 coreWorld = _grid.AxialToWorld(_unit.State.coord);
            for (int i = 0; i < occupied.Count; i++)
            {
                SegmentBinding binding = _segmentBindings[i];
                if (binding?.transform == null || binding.target == null)
                {
                    Rebuild();
                    return;
                }

                HexAxialCoord coord = occupied[i];
                bool isCore = coord.Equals(_unit.State.coord);
                Vector3 local = _grid.AxialToWorld(coord) - coreWorld;
                binding.transform.localPosition = new Vector3(local.x, 0.58f, local.z);
                binding.target.SetCoord(coord);
                binding.isCore = isCore;
                if (binding.renderer != null)
                    _segments[coord] = (binding.renderer, isCore);
            }
        }

        public void SetIntentPreview(IReadOnlyCollection<HexAxialCoord> highlightedCoords, bool danger)
        {
            foreach (var entry in _segments)
            {
                bool highlighted = highlightedCoords != null && highlightedCoords.Contains(entry.Key);
                entry.Value.renderer.sharedMaterial = highlighted
                    ? GetPreviewMaterial(danger)
                    : GetMaterial(entry.Value.isCore);
            }
        }

        private void ClearSegments()
        {
            _segments.Clear();
            _segmentBindings.Clear();
            if (_segmentsRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(_segmentsRoot.gameObject);
            else
                DestroyImmediate(_segmentsRoot.gameObject);
            _segmentsRoot = null;
        }

        private static Material GetMaterial(bool core)
        {
            if (core && s_coreMaterial != null)
                return s_coreMaterial;
            if (!core && s_segmentMaterial != null)
                return s_segmentMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = core ? "LivingWallCore_Runtime" : "LivingWallSegment_Runtime",
                color = core
                    ? new Color(0.34f, 0.16f, 0.12f, 1f)
                    : new Color(0.28f, 0.38f, 0.22f, 1f),
            };
            if (core)
                s_coreMaterial = material;
            else
                s_segmentMaterial = material;
            return material;
        }

        private static Material GetPreviewMaterial(bool danger)
        {
            if (danger && s_dangerMaterial != null)
                return s_dangerMaterial;
            if (!danger && s_previewMaterial != null)
                return s_previewMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = danger ? "LivingWallDanger_Runtime" : "LivingWallPreview_Runtime",
                color = danger
                    ? new Color(0.9f, 0.16f, 0.08f, 1f)
                    : new Color(0.82f, 0.66f, 0.12f, 1f),
            };
            if (danger)
                s_dangerMaterial = material;
            else
                s_previewMaterial = material;
            return material;
        }
    }
}
