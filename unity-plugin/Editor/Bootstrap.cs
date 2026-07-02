using UnityEditor;

namespace UnityMCP.Editor
{
    // M7 (ROI reliability sprint): CommandRegistry used to populate itself via a static
    // constructor triggered by first access from CommandRouter, and CommandRouter's
    // RegisterAll() calls back into CommandRegistry — a cyclic static-init dependency.
    // This explicit [InitializeOnLoadMethod] hook replaces that: it runs once per domain
    // reload, independent of which class gets touched first.
    internal static class Bootstrap
    {
        [InitializeOnLoadMethod]
        private static void Init()
        {
            // Defer to after the full [InitializeOnLoad] sweep: Unity does not guarantee
            // cross-type ordering, so plugin assemblies' [InitializeOnLoad] hooks (which
            // populate PluginRegistry) may not have run yet at this point.
            EditorApplication.delayCall += () =>
            {
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
            };
        }
    }
}
