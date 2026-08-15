using System.Collections.Generic;
using UnityEditor;

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

        // Minimal built-in default — used when no Python catalog received yet.
        // Generated from tool_specs._SPECS — categories and tool lists match get_catalog() output.
        private static readonly Dictionary<string, string[]> _defaultCatalog =
            new Dictionary<string, string[]>
            {
                { "CORE",       new[] { "batch","create_object","editor","get_compile_errors","get_component","get_console","get_hierarchy","inspect","manage_component","set_property" } },
                { "SCENE",      new[] { "apply_scene_change","autofit_collider","check_colliders","delete_object","find_objects","get_components_list","get_object_detail","get_selection","get_spatial_context","navmesh_query","object_diff","ping_object","region_clear","rename_object","scene","scene_change_plan","scene_diff","scene_environment","search_scene","set_active","set_material","set_parent","set_properties","set_property_delta","set_sibling_index","spatial_query","transfer_object" } },
                { "COMPONENTS", new[] { "auto_wire","references","unwire_event","wire_event" } },
                { "ASSETS",     new[] { "asset","material","material_audit","prefab","project_settings","scriptable_object","shader" } },
                { "MEDIA",      new[] { "analyze_lod_culling","animation","animator","create_ui","lint_ugui","particle","render_analyze","screenshot","screenshot_baseline","screenshot_compare","set_rect","timeline","ui_intent","validate_layout","vfx_intent" } },
                { "VERIFY",     new[] { "compile_preflight","diagnose","lint_scene_refs","resolve_scene_refs","scan_scene","scene_health","validate_references" } },
                { "RUNTIME",    new[] { "debug","debug_animator","debug_physics","get_frame_stats","get_memory","get_watches","invoke_method","move_to","profile","query_state","set_runtime_property","wait_until" } },
                { "TESTS",      new[] { "export_playtest_aliases_to_defs","get_test_count","get_test_progress","get_test_results","lint_playtest","run_playtest","run_tests","sync_playtest_aliases_from_defs","test_step","validate_playtest_aliases" } },
                { "SYSTEM",     new[] { "alias_status","animator_intent","apply_template","ask","ask_user","auto_fix","checkpoint","do","doctor","execute_code","fingerprint","get_capabilities","get_changes","get_enabled_tools","get_schema","list_skills","list_templates","load_session","menu","permission_prompt","recompile","reconnect_unity","save_session","save_skill","save_template","set_llm_config","smart_build","sync_unity","undo_last","use_skill" } },
            };

        // Returns catalog categories (from EditorPrefs JSON or built-in default).
        public static Dictionary<string, string[]> GetCatalogCategories()
        {
            var raw = GetCatalog();
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    var parsed = CatalogParser.Parse(raw);
                    if (parsed.Count > 0) return parsed;
                }
                catch { /* fall through */ }
            }
            return _defaultCatalog;
        }

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
