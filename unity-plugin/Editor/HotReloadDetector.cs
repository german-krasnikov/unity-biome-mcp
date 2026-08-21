using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class HotReloadDetector
    {
        // Test seam — override in tests; null = use native checks
        internal static System.Func<bool> _overrideForTest = null;

        // Cache the assembly-scan result; reset after HR patches assemblies (no domain reload).
        private static bool? _cachedPackageInstalled;

        static HotReloadDetector()
        {
            AssemblyReloadEvents.afterAssemblyReload += () => _cachedPackageInstalled = null;
        }

        internal static bool IsActive() =>
            _overrideForTest?.Invoke() ?? IsActiveNative();

        private static bool IsActiveNative() =>
            MCPSettings.GetHotReloadMode() || IsPackageInstalled() || IsAutoRefreshDisabled();

        internal static bool IsPackageInstalled()
        {
            if (_cachedPackageInstalled.HasValue) return _cachedPackageInstalled.Value;
            var found = false;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name == "SingularityGroup.HotReload.Runtime")
                { found = true; break; }
            _cachedPackageInstalled = found;
            return found;
        }

        internal static bool IsAutoRefreshDisabled() =>
            EditorPrefs.GetInt("kAutoRefresh", 1) == 0;
    }
}
