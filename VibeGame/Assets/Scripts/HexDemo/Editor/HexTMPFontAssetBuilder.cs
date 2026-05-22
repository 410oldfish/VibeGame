using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HexDemo.Editor
{
    public static class HexTMPFontAssetBuilder
    {
        private const string SourceFontPath = "Assets/TextMesh Pro/Resources/Fonts/simhei.ttf";
        private const string OutputFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/HexChineseDynamic SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("HexDemo/Build Chinese TMP Font")]
        public static void Generate()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"Chinese TMP font source not found: {SourceFontPath}");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(OutputFontAssetPath);

            var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                Debug.LogError("Failed to create TMP font asset from simhei.ttf");
                return;
            }

            fontAsset.name = "HexChineseDynamic SDF";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);
            fontAsset.material.name = "HexChineseDynamic SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            var tmpSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (tmpSettings != null)
            {
                TMP_Settings.defaultFontAsset = fontAsset;
                TMP_Settings.fallbackFontAssets ??= new List<TMP_FontAsset>();
                if (!TMP_Settings.fallbackFontAssets.Contains(fontAsset))
                    TMP_Settings.fallbackFontAssets.Insert(0, fontAsset);
                EditorUtility.SetDirty(tmpSettings);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated Chinese TMP font asset at {OutputFontAssetPath}");
        }
    }
}
