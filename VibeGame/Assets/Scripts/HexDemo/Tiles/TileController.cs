using UnityEngine;

namespace HexDemo
{
    public sealed class TileController
    {
        private readonly TileModel _model;
        private readonly TileView _view;
        private readonly HexGrid _grid;
        private HexTile _tile;

        public TileController(HexGrid grid, TileModel model, TileView view)
        {
            _grid = grid;
            _model = model;
            _view = view;
        }

        public TileModel Model => _model;
        public void BindTile(HexTile tile) => _tile = tile;

        public bool CanEnter() => _model != null && _model.CanEnter;
        public bool BlocksLineOfSight() => _model != null && _model.BlocksLineOfSight;
        public bool HasRuin() => _model != null && _model.HasRuin;
        public bool HasBarrier() => _model != null && _model.HasBarrier;
        public bool ShouldShowDetail() =>
            _model != null && (_model.HasBarrier || _model.HasRuin || _model.IsNonNormalZone);

        public void SetZone(HexTerrainZoneType zone)
        {
            if (_model == null)
                return;
            _model.zone = zone;
            RefreshVisuals();
        }

        public void SetProp(string propId, int? hpOverride = null)
        {
            if (_model == null)
                return;

            var definition = HexPropLibrary.Get(propId);
            if (definition == null)
            {
                Debug.LogWarning($"[TileController] Unknown propId '{propId}'.");
                return;
            }

            _model.ApplyPropDefinition(definition);
            if (definition.IsRuin && hpOverride.HasValue)
            {
                _model.structureMaxHp = Mathf.Max(1, hpOverride.Value);
                _model.structureHp = _model.structureMaxHp;
            }

            RefreshVisuals();
        }

        public void SetStructure(HexTerrainStructureType type, int hp = 0)
        {
            if (_model == null)
                return;

            if (type == HexTerrainStructureType.None)
            {
                ClearStructure();
                return;
            }

            var definition = HexPropLibrary.GetOrDefault(type);
            if (definition != null)
            {
                SetProp(definition.propId, type == HexTerrainStructureType.Ruin ? Mathf.Max(1, hp > 0 ? hp : definition.ruinHp) : (int?)null);
                return;
            }

            _model.ClearPropRuntime();
            _model.structureType = type;
            _model.structureMaxHp = type == HexTerrainStructureType.Ruin ? Mathf.Max(1, hp) : 0;
            _model.structureHp = _model.structureMaxHp;
            RefreshVisuals();
        }

        public void ClearStructure()
        {
            if (_model == null)
                return;

            _model.structureType = HexTerrainStructureType.None;
            _model.ClearPropRuntime();
            RefreshVisuals();
        }

        public bool DamageStructure(int amount, out bool destroyed)
        {
            destroyed = false;
            if (_model == null || _model.structureType != HexTerrainStructureType.Ruin || amount <= 0)
                return false;

            if (_model.fuseTurns.HasValue && !_model.fuseArmed)
            {
                _model.fuseArmed = true;
                Debug.Log($"[TileController] Fuse armed on '{_model.propId}' at {_model.coord.q},{_model.coord.r} (stub).");
            }

            _model.structureHp = Mathf.Max(0, _model.structureHp - amount);
            destroyed = _model.structureHp <= 0;
            if (destroyed)
            {
                var definition = HexPropLibrary.Get(_model.propId);
                ClearStructure();
                if (_tile != null)
                    PropEffectStub.ResolveOnRemove(_tile, definition);
            }
            else
            {
                RefreshVisuals();
            }

            return true;
        }

        public void RefreshStructureVisual() => RefreshVisuals();

        private void RefreshVisuals()
        {
            if (_view == null || _model == null)
                return;

            _view.RefreshStructure(
                _model,
                _grid != null ? _grid.ruinVisualPrefab : null,
                _grid != null ? _grid.barrierVisualPrefab : null);
            _view.RefreshHpBar(_model);
        }
    }
}
