using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor
{
    public static partial class CommandRouter
    {
        // Cache for fast-path get_enabled_tools (bypasses main thread dispatch).
        // Always kept WARM so the TCP read thread never computes it (no EditorPrefs off-thread).
        // Writes: InvalidateEnabledToolsCache (settings UI, main thread) + end of RegisterAll
        //         (post-registration, main thread). Read thread uses ?? "" safety fallback only.
        private static volatile string _enabledToolsCache;

        // Internal accessor for tests — never null after first populate.
        internal static string PeekEnabledToolsCache => _enabledToolsCache;

        /// <summary>Thread-safe fast-path — never computes on the read thread (no EditorPrefs off-thread).</summary>
        internal static string ExecGetEnabledToolsCached() => _enabledToolsCache ?? "";

        // Called from Settings UI (always main thread) — REPOPULATES instead of nulling
        // so the read thread always sees a warm non-null value.
        internal static void InvalidateEnabledToolsCache() => _enabledToolsCache = ExecGetEnabledTools();

        private static string ExecGetEnabledTools()  => BuildToolList(enabled: true);
        private static string ExecGetDisabledTools() => BuildToolList(enabled: false);

        private static string BuildToolList(bool enabled)
        {
            var allTools = new HashSet<string>(MCPSettings.GetToolNames());
            foreach (var cmd in CommandRegistry.GetAllCommands())
                allTools.Add(cmd);
            var sb = new StringBuilder();
            bool first = true;
            foreach (var tool in allTools)
            {
                if (MCPSettings.IsToolEnabled(tool) == enabled)
                {
                    if (!first) sb.Append(",");
                    sb.Append(tool);
                    first = false;
                }
            }
            return sb.ToString();
        }
    }
}
