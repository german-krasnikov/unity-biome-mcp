// Per-session MCP tool permission config — deny-set backed by EditorPrefs.
// Default = allow all; only explicit denials are persisted.
// Catalog is read LIVE on each call via delegate (no stale snapshot).
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UnityMCP.Editor
{
    public class PermissionConfig
    {
        // Must match the mcp.json server key — Claude names tools mcp__<SERVER_NAME>__<tool>.
        // NOT SERVER_NAME.Replace('-', '_') — hyphens are legal in MCP tool-name
        // segments and are never touched by Claude's sanitizer. MCP_TOOL_PREFIX is
        // derived as MCP_BLANKET + "__" so blanket and per-tool prefix cannot drift.
        // Mirrors SERVER_NAME/MCP_BLANKET in server/src/unity_mcp/config/merger.py
        // and server/src/unity_mcp/backend_def.py. Keep both in sync manually.
        // Cross-language drift guard: server/tests/test_server_name_consistency.py
        public const string SERVER_NAME     = "unity-biome-mcp";
        public const string MCP_BLANKET     = "mcp__" + SERVER_NAME;
        public const string MCP_TOOL_PREFIX = MCP_BLANKET + "__";

        // EditorPrefs key prefix shared by all PermissionConfig instances using the default ctor.
        // MUST equal the value previously hardcoded in the default ctor — changing it orphans saved prefs.
        public const string DEFAULT_PREFIX = "UnityMCP_ChatPerm_";

        private readonly string _prefix;
        private readonly System.Func<Dictionary<string, string[]>> _getCatalog;

        // Production ctor: live catalog that includes plugin tools.
        public PermissionConfig()
            : this(DEFAULT_PREFIX, LiveCatalog) { }

        // Testable ctor: inject key prefix + catalog delegate.
        public PermissionConfig(string prefix, System.Func<Dictionary<string, string[]>> getCatalog)
        {
            _prefix     = prefix;
            _getCatalog = getCatalog;
        }

        // Compose catalog categories + plugin tools into one live snapshot.
        private static Dictionary<string, string[]> LiveCatalog()
        {
            var cat = MCPSettings.GetCatalogCategories();
            var plugins = PluginRegistry.GetAllPluginToolNames();
            if (plugins.Length == 0) return cat;
            // Copy so we don't mutate the cached default.
            var merged = new Dictionary<string, string[]>(cat);
            // FIX 3: guard — don't overwrite a user-defined PLUGINS category.
            if (!merged.ContainsKey("PLUGINS"))
                merged["PLUGINS"] = plugins;
            return merged;
        }

        private bool IsAllowed(string toolName) =>
            EditorPrefs.GetBool(_prefix + toolName, true);

        private bool HasAnyDenied() =>
            AllTools().Any(t => !IsAllowed(t));

        /// <summary>
        /// Returns null when no tools are denied (→ caller uses compact blanket arg).
        /// Returns non-null array of allowed tool names when ≥1 tool is denied.
        /// Returns empty array when ALL tools are denied (→ caller omits --allowedTools).
        /// </summary>
        public string[] GetAllowedToolIds() =>
            HasAnyDenied() ? AllTools().Where(IsAllowed).ToArray() : null;

        public List<(string toolName, string category, bool allowed)> GetToolStates()
        {
            var result = new List<(string, string, bool)>();
            foreach (var kv in _getCatalog())
                foreach (var t in kv.Value)
                    result.Add((t, kv.Key, IsAllowed(t)));
            return result;
        }

        public void SetToolAllowed(string toolName, bool allowed) =>
            EditorPrefs.SetBool(_prefix + toolName, allowed);

        public void SetCategoryAllowed(string category, bool allowed)
        {
            if (!_getCatalog().TryGetValue(category, out var tools)) return;
            foreach (var t in tools) SetToolAllowed(t, allowed);
        }

        public void AllowAll()
        {
            foreach (var t in AllTools()) EditorPrefs.DeleteKey(_prefix + t);
        }

        public void DenyAll()
        {
            foreach (var t in AllTools()) SetToolAllowed(t, false);
        }

        private IEnumerable<string> AllTools() =>
            _getCatalog().Values.SelectMany(v => v);
    }
}
