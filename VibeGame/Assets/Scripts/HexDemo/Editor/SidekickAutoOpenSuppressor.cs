#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HexDemo.Editor
{
    /// <summary>
    /// Project policy: Sidekick remains available from the Synty menu, but must not open
    /// automatically on editor/domain startup or when entering Play Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class SidekickAutoOpenSuppressor
    {
        private const string AutoOpenPreference = "syntySkAutoOpenState";
        private const string SidekickFirstInitSession = "FirstInitDone";

        static SidekickAutoOpenSuppressor()
        {
            EditorPrefs.SetBool(AutoOpenPreference, false);
            SessionState.SetBool(SidekickFirstInitSession, true);
            EditorApplication.delayCall += CloseAutomaticallyOpenedWindows;
        }

        private static void CloseAutomaticallyOpenedWindows()
        {
            EditorApplication.delayCall -= CloseAutomaticallyOpenedWindows;
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window == null)
                    continue;

                string typeName = window.GetType().FullName ?? string.Empty;
                string title = window.titleContent != null ? window.titleContent.text : string.Empty;
                bool isSidekickWindow = typeName == "Synty.SidekickCharacters.ModularCharacterWindow" ||
                                        typeName == "Synty.SidekickCharacters.ToolDownloader" ||
                                        title == "Sidekick Character Tool" ||
                                        title == "Sidekick Tool Downloader";
                if (!isSidekickWindow)
                    continue;

                try
                {
                    window.Close();
                }
                catch (System.NullReferenceException)
                {
                    // The Synty window can destroy its backing GUI object during Close().
                }
            }
        }
    }
}
#endif
