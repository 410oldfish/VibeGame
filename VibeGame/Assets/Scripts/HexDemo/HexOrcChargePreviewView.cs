using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexOrcChargePreviewView : MonoBehaviour
    {
        private LineRenderer _line;
        private Material _material;

        public void SetPreview(HexGrid grid, IReadOnlyList<HexAxialCoord> coords, bool empowered)
        {
            EnsureLine();
            if (grid == null || coords == null || coords.Count < 2)
            {
                _line.enabled = false;
                return;
            }

            _line.enabled = true;
            _line.positionCount = coords.Count;
            for (int i = 0; i < coords.Count; i++)
                _line.SetPosition(i, grid.AxialToWorld(coords[i]) + Vector3.up * 0.16f);

            Color color = empowered
                ? new Color(1f, 0.34f, 0.04f, 0.95f)
                : new Color(0.95f, 0.12f, 0.06f, 0.9f);
            _line.startColor = color;
            _line.endColor = color;
            _material.color = color;
        }

        public void Clear()
        {
            if (_line != null)
                _line.enabled = false;
        }

        private void EnsureLine()
        {
            if (_line != null)
                return;

            var lineObject = new GameObject("OrcChargeIntentPreview");
            lineObject.transform.SetParent(transform, false);
            _line = lineObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.widthMultiplier = 0.09f;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader) { name = "OrcChargePreview_Runtime" };
            _line.sharedMaterial = _material;
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
