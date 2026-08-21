using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class HotReloadDetector
    {
        // Test seam — override in tests; null = use native checks
        internal static System.Func<bool> _overrideForTest = null;

        internal static bool IsActive() =>
            _overrideForTest?.Invoke() ?? IsActiveNative();

        private static bool IsActiveNative() =>
            MCPSettings.GetHotReloadMode() || IsPackageInstalled() || IsAutoRefreshDisabled();

        internal static bool IsPackageInstalled()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name == "SingularityGroup.HotReload.Runtime")
                    return true;
            return false;
        }

        internal static bool IsAutoRefreshDisabled() =>
            EditorPrefs.GetInt("kAutoRefresh", 1) == 0;
    }
}
