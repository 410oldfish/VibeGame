using System.IO;
using UnityEditor;
using UnityEngine;

namespace HexDemo.Editor
{
    public static class HexTilePrefabBuilder
    {
        private const string SourceModelPath = "Assets/Models/Terrain/HexCube.fbx";
        private const string OutputPrefabPath = "Assets/Models/Terrain/HexCubeTile.prefab";
        private const string OutlineMaterialPath = "Assets/Models/Terrain/HexCubeTile_Outline.mat";
        private const string ClickMeshPath = "Assets/Models/Terrain/HexCubeTile_ClickTop.asset";

        [MenuItem("Tools/Hex Demo/Rebuild Hex Tile Prefab")]
        public static void Generate()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            if (model == null)
            {
                Debug.LogError($"Missing source model at {SourceModelPath}");
                return;
            }

            EnsureFolder("Assets/Models");
            EnsureFolder("Assets/Models/Terrain");

            var root = new GameObject("HexCubeTile");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 30f);
                visual.transform.localScale = Vector3.one;

                var bounds = CalculateLocalBounds(visual.transform);
                float sourceDiameter = Mathf.Max(bounds.size.x, bounds.size.z, 0.0001f);
                float normalizeScale = 2f / sourceDiameter;
                visual.transform.localScale = Vector3.one * normalizeScale;

                bounds = CalculateLocalBounds(visual.transform);
                visual.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
                bounds = CalculateLocalBounds(visual.transform);

                foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                float topHeight = bounds.max.y;

                var clickGO = new GameObject("Top_Click_Collider");
                clickGO.transform.SetParent(root.transform, false);
                clickGO.transform.localPosition = new Vector3(0f, topHeight + 0.03f, 0f);

                var clickMesh = EnsureClickMesh();
                var filter = clickGO.AddComponent<MeshFilter>();
                filter.sharedMesh = clickMesh;
                var colliderMesh = clickGO.AddComponent<MeshCollider>();
                colliderMesh.sharedMesh = clickMesh;

                var outlineGO = new GameObject("Top_Edge_Line");
                outlineGO.transform.SetParent(root.transform, false);
                var line = outlineGO.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 6;
                line.widthMultiplier = 0.06f;
                line.numCornerVertices = 2;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = EnsureOutlineMaterial();

                for (int i = 0; i < 6; i++)
                {
                    float angle = Mathf.Deg2Rad * (60f * i + 30f);
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), topHeight + 0.032f, Mathf.Sin(angle)));
                }

                PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Generated hex tile prefab at {OutputPrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh EnsureClickMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(ClickMeshPath);
            if (existing != null)
                return existing;

            var mesh = CreateHexTopMesh(1f, 0f);
            AssetDatabase.CreateAsset(mesh, ClickMeshPath);
            return mesh;
        }

        private static Material EnsureOutlineMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("UI/Default");
            var material = new Material(shader)
            {
                color = new Color(0.16f, 0.34f, 0.27f, 1f)
            };

            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
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
                foreach (Vector3 corner in GetBoundsCorners(meshBounds))
                {
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
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

        private static Mesh CreateHexTopMesh(float radius, float y)
        {
            var mesh = new Mesh { name = "HexTop" };
            var vertices = new Vector3[7];
            vertices[0] = new Vector3(0f, y, 0f);

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i + 30f);
                vertices[i + 1] = new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle));
            }

            var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                int triIndex = i * 3;
                triangles[triIndex] = 0;
                triangles[triIndex + 1] = i + 1;
                triangles[triIndex + 2] = ((i + 1) % 6) + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
