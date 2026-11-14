using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityMCP.Editor.Roslyn;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class MCPSettings
    {
        // ── EditorPrefs API (P0 — must stay intact) ──────────────────────────
        internal const string KeyPrefix      = "UnityMCP_Tool_";
        private const string KeyCatalog     = "UnityMCP_Catalog";

        public static bool IsToolEnabled(string toolName) =>
            EditorPrefs.GetBool(KeyPrefix + toolName, true);

        // ── Catalog persistence (P1) ─────────────────────────────────────────
        public static void SetCatalog(string json) =>
            EditorPrefs.SetString(KeyCatalog, json);

        public static string GetCatalog() =>
            EditorPrefs.GetString(KeyCatalog, null);

        // ── Security level ───────────────────────────────────────────────────
        private const string KeySecurityLevel = "UnityMCP_SecurityLevel";

        public static SecurityLevel GetSecurityLevel() =>
            (SecurityLevel)EditorPrefs.GetInt(KeySecurityLevel, (int)SecurityLevel.AllowAll);

        public static void SetSecurityLevel(SecurityLevel level) =>
            EditorPrefs.SetInt(KeySecurityLevel, (int)level);

        // Built-in default — every real MCP tool (TOOL + PYTOOL of dsl/tools.biome), grouped
        // by category. Generated from ToolRoster.g.cs; regenerate via `dsl.sh generate` when
        // the catalog changes — do not hand-edit this grouping.
        private static readonly Dictionary<string, string[]> _catalogCategories =
            ToolRoster.Rows
                .GroupBy(row => row.Category)
                .ToDictionary(g => g.Key, g => g.Select(row => row.Name).ToArray());

        // Returns catalog categories — ToolRoster.g.cs (generated) is the source of truth for
        // category keys and tool assignments. Saved EditorPrefs catalog only adds
        // per-tool enabled/disabled state; it never hides categories that the plugin
        // defines. On plugin update new categories appear immediately.
        public static Dictionary<string, string[]> GetCatalogCategories() => _catalogCategories;

        // ── Tool name list (P0 backward-compat) ──────────────────────────────
        public static string[] GetToolNames()
        {
            var all = new List<string>();
            foreach (var kv in GetCatalogCategories())
                all.AddRange(kv.Value);
            var pluginTools = PluginRegistry.GetAllPluginToolNames();
            all.AddRange(pluginTools);
            return all.ToArray();
        }

        // ── Init / lifecycle ─────────────────────────────────────────────────
        static MCPSettings()
        {
            EditorApplication.wantsToQuit += OnWantsToQuit;
        }

        private static bool OnWantsToQuit() => true;
    }
}
