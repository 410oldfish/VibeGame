using UnityEngine;

namespace HexDemo
{
    public sealed class TileController
    {
        private readonly TileModel _model;
        private readonly TileView _view;
        private readonly HexGrid _grid;

        public TileController(HexGrid grid, TileModel model, TileView view)
        {
            _grid = grid;
            _model = model;
            _view = view;
        }

        public TileModel Model => _model;
        public bool CanEnter() => _model != null && _model.CanEnter;

        public void SetStructure(HexTerrainStructureType type, int hp = 0)
        {
            if (_model == null)
                return;

            _model.structureType = type;
            _model.structureHp = type == HexTerrainStructureType.Ruin ? Mathf.Max(1, hp) : 0;
            RefreshStructureVisual();
        }

        public void ClearStructure()
        {
            if (_model == null)
                return;

            _model.structureType = HexTerrainStructureType.None;
            _model.structureHp = 0;
            RefreshStructureVisual();
        }

        public bool DamageStructure(int amount, out bool destroyed)
        {
            destroyed = false;
            if (_model == null || _model.structureType != HexTerrainStructureType.Ruin || amount <= 0)
                return false;

            _model.structureHp = Mathf.Max(0, _model.structureHp - amount);
            destroyed = _model.structureHp <= 0;
            if (destroyed)
                ClearStructure();
            else
                RefreshStructureVisual();

            return true;
        }

        public void RefreshStructureVisual()
        {
            if (_view == null || _model == null)
                return;

            _view.RefreshStructure(_model, _grid != null ? _grid.ruinVisualPrefab : null, _grid != null ? _grid.highGroundVisualPrefab : null);
        }
    }
}
