using UnityEngine;

namespace HexDemo
{
    public sealed class TileView : MonoBehaviour
    {
        private Transform _structureRoot;
        private GameObject _structureObject;

        public void Initialize(Transform structureRoot)
        {
            _structureRoot = structureRoot;
            if (_structureRoot == null)
            {
                var go = new GameObject("StructureRoot");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.1f;
                _structureRoot = go.transform;
            }
        }

        public void RefreshStructure(TileModel model, GameObject ruinPrefab = null, GameObject highGroundPrefab = null)
        {
            if (_structureRoot == null || model == null)
                return;

            if (_structureObject != null)
                DestroyImmediateSafe(_structureObject);

            if (model.structureType == HexTerrainStructureType.None)
                return;

            if (model.structureType == HexTerrainStructureType.Ruin)
                _structureObject = CreateStructure("RuinVisual", ruinPrefab, PrimitiveType.Cube, new Vector3(0.6f, 0.35f, 0.6f), new Color(0.52f, 0.41f, 0.3f, 1f));
            else if (model.structureType == HexTerrainStructureType.HighGround)
                _structureObject = CreateStructure("HighGroundVisual", highGroundPrefab, PrimitiveType.Sphere, new Vector3(0.6f, 0.25f, 0.6f), new Color(0.36f, 0.36f, 0.42f, 1f));
        }

        private GameObject CreateStructure(string name, GameObject prefab, PrimitiveType primitiveType, Vector3 localScale, Color fallbackColor)
        {
            GameObject go = prefab != null ? Instantiate(prefab, _structureRoot) : GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(_structureRoot, false);
            go.transform.localPosition = Vector3.up * 0.15f;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            if (prefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(renderer.material);
                    renderer.material.color = fallbackColor;
                }
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            return go;
        }

        private static void DestroyImmediateSafe(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
    }
}
