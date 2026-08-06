using UnityEngine;
using UnityEngine.UIElements;

namespace HexDemo
{
    internal static class HexUiToolkitRuntime
    {
        private const string ResourceRoot = "UI Toolkit/";
        private static PanelSettings s_panelSettings;
        private static Font s_chineseFont;

        internal static UIDocument AttachDocument(GameObject host, string screenResource, int sortingOrder)
        {
            var document = host.GetComponent<UIDocument>() ?? host.AddComponent<UIDocument>();
            document.panelSettings = GetPanelSettings();
            document.sortingOrder = sortingOrder;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>(ResourceRoot + "Screens/" + screenResource);
            return document;
        }

        internal static void PrepareRoot(VisualElement root)
        {
            if (root == null)
                return;

            s_chineseFont ??= Resources.Load<Font>("Fonts/simhei");
            if (s_chineseFont != null)
                root.style.unityFont = s_chineseFont;

            var theme = Resources.Load<StyleSheet>(ResourceRoot + "Styles/HexTheme");
            if (theme != null && !root.styleSheets.Contains(theme))
                root.styleSheets.Add(theme);
        }

        internal static VisualTreeAsset LoadTemplate(string name)
        {
            return Resources.Load<VisualTreeAsset>(ResourceRoot + "Templates/" + name);
        }

        private static PanelSettings GetPanelSettings()
        {
            if (s_panelSettings != null)
                return s_panelSettings;

            s_panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            s_panelSettings.name = "HexRuntimePanelSettings";
            s_panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ResourceRoot + "Styles/HexRuntimeTheme");
            s_panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            s_panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            s_panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            s_panelSettings.match = 0.5f;
            Object.DontDestroyOnLoad(s_panelSettings);
            return s_panelSettings;
        }
    }
}
