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
    public class CommandRegistryCompletenessTests
    {
        // Statically enumerated from CommandRouter.cs, CommandRouter.MediaHandlers.cs,
        // CommandRouter.ObjectHandlers.cs and Debug/WatchCommandHandler.cs.
        // Update this list deliberately if a command is added/removed/renamed.
        // "navmesh" is conditionally compiled (#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION).
        private static readonly string[] BaseCommands =
        {
            "analyze_lod_culling", "animation", "animator", "ask_user", "asset", "auto_wire",
            "autofit_collider", "batch", "check_colliders", "checkpoint", "clear_console",
            "compile_preflight", "compile_status", "create_object", "create_ui", "debug_animator",
            "debug_physics", "delete_object", "diagnose", "editor", "execute_code", "find_objects",
            "fingerprint", "force_refresh", "get_capabilities", "get_changes", "get_compile_errors",
            "get_component", "get_components_list", "get_console", "get_disabled_tools",
            "get_enabled_tools", "get_frame_stats", "get_hierarchy", "get_memory", "get_object_detail",
            "get_perf", "get_schema", "get_selection", "get_spatial_context", "get_test_count",
            "get_test_results", "get_watches", "inspect", "invoke_method", "manage_component",
            "material", "material_audit", "menu", "move_to", "object_diff", "particle",
            "ping", "ping_object", "prefab", "profile", "project_settings", "query_state", "recompile",
            "references", "region_clear", "rename_object", "render_analyze", "run_playtest", "run_tests", "scan_scene",
            "scene", "scene_diff", "scene_environment", "scene_health", "screenshot",
            "scriptable_object", "search_scene", "set_active", "set_material", "set_parent",
            "set_property", "set_property_delta", "set_rect", "set_runtime_property",
            "set_tool_catalog", "shader", "spatial_query", "sync", "sync_status", "test_step",
            "timeline", "transfer_object", "undo_last", "unwire_event", "validate_layout",
            "validate_references", "wait_until", "watch_add", "watch_clear", "watch_remove",
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
