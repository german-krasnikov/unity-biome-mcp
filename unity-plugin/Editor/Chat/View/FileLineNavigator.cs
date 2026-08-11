// Opens a script file at a specific line via AssetDatabase.OpenAsset.
using UnityEditor;
using System;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Opens a source file at the given 1-based line number.
    /// Falls back to a simple open if the IDE doesn't support line jumping.
    /// </summary>
    internal static class FileLineNavigator
    {
#if UNITY_INCLUDE_TESTS
        /// <summary>Test seam: override to capture calls without touching AssetDatabase.</summary>
        internal static Action<string, int> OpenAtLineOverride;
#endif

        /// <summary>Open the script at assetPath and jump to the given 1-based line.</summary>
        internal static void OpenAtLine(string assetPath, int line)
        {
#if UNITY_INCLUDE_TESTS
            if (OpenAtLineOverride != null)
            {
                OpenAtLineOverride(assetPath, line);
                return;
            }
#endif
            var ms = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (ms != null)
                AssetDatabase.OpenAsset(ms, line);
        }
    }
}
