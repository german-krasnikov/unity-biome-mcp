// TDD safety net for Phase 2 structural refactor (ROI reliability sprint).
// Snapshot-guards the full set of commands CommandRouter.RegisterAll() wires into
// CommandRegistry. Any refactor that silently drops or renames a registration
// (CommandOptions migration, Bootstrap.Init split, MCPServer split) trips this test.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRegistryCompletenessTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Statically enumerated from CommandRouter.cs, CommandRouter.MediaHandlers.cs,
        // CommandRouter.ObjectHandlers.cs and Debug/WatchCommandHandler.cs.
        // Update this list deliberately if a command is added/removed/renamed.
        // "navmesh" is conditionally compiled (#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION).
        private static readonly string[] BaseCommands =
        {
            "alias_status", "analyze_lod_culling", "animation", "animator", "ask_user", "asset", "attach_uitk", "auto_wire",
            "autofit_collider", "bake", "batch", "build", "cancel_test_run", "check_colliders", "checkpoint", "checkpoint_undo_restore", "clear_console",
            "clear_held_types", "compile_preflight", "compile_status", "console_clear_buffer", "create_object", "create_ui", "debug_animator",
            "debug_physics", "delete_object", "diagnose", "editor", "execute_code", "find_objects",
            "export_playtest_aliases_to_defs",
            "fingerprint", "force_play_stop", "force_refresh", "get_capabilities", "get_changes", "get_compile_errors",
            "get_aliases", "get_component", "get_components_list", "get_console", "get_disabled_tools",
            "get_enabled_tools", "get_frame_stats", "get_hierarchy", "get_memory", "get_object_detail",
            "get_profile_context", "get_status", "get_test_run",
            "get_schema", "get_selection", "get_spatial_context", "get_test_count",
            "get_test_progress", "get_test_results", "get_unity_events", "get_watches", "inspect", "inspect_uitk", "invoke_method", "lint_playtest",
            "lint_scene_refs", "lint_ugui", "lint_uitk", "list_events", "list_playtest_files", "list_test_runs", "manage_component",
            "material", "material_audit", "menu", "move_to", "object_diff", "package", "particle",
            "ping", "ping_object", "prefab", "profile", "project_settings", "query_state", "recompile",
            "references", "region_clear", "rename_object", "render_analyze", "resolve_scene_refs", "resolve_test_request", "run_playtest", "run_tests", "runtime_snapshot", "scan_scene",
            "scene", "scene_diff", "scene_environment", "scene_health", "screenshot",
            "scriptable_object", "search_context", "search_scene", "serialized_field_rename_audit", "set_active", "set_client_label", "set_material", "set_parent",
            "set_property", "set_property_delta", "set_rect", "set_runtime_property",
            "set_sibling_index", "set_tool_catalog", "shader", "spatial_query", "sync", "sync_playtest_aliases_from_defs", "sync_status", "test_step",
            "timeline", "transfer_object", "uitk_element", "uitk_file", "undo_last", "unwire_event", "validate_triggers",
            "validate_playtest_aliases", "validate_references", "wait_until", "warm_type_cache", "watch_add", "watch_clear", "watch_remove",
            "watch_reset", "wire_event",
        };

        private static string[] ExpectedCommands
        {
            get
            {
                var list = new List<string>(BaseCommands);
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
                list.Add("navmesh");
#endif
                return list.ToArray();
            }
        }

        // Bootstrap.Init() populates CommandRegistry via EditorApplication.delayCall (M7),
        // and other test fixtures mutate the same static dictionary (e.g. CommandRegistryTests,
        // PluginRegistryTests). Without its own SetUp this test's result depends on execution
        // order / delayCall timing — force a known-clean baseline before asserting.
        [SetUp]
        public void SetUp()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
        }

        [Test]
        public void RegisterAll_RegistersExactlyExpectedCommands()
        {
            IEnumerable<string> actual = CommandRegistry.GetAllCommands();
            CollectionAssert.AreEquivalent(ExpectedCommands, actual);
        }
    }
}
