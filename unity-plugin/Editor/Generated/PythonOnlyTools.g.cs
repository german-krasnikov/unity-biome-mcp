// Generated from dsl/tools.biome
// source-sha256=22d5b56170705618537a6cda2e383e185768772939d760f99df4e8e641e19e58
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // The tools that only the Python server implements (PYTOOL lines of dsl/tools.biome): a direct TCP caller gets an actionable error.
    internal static class PythonOnlyTools
    {
        internal static readonly System.Collections.Generic.HashSet<string> Names = new System.Collections.Generic.HashSet<string>
        {
"animator_intent",
"apply_scene_change",
"apply_template",
"ask",
"auto_fix",
"await_compile",
"brief_build",
"budget_status",
"checkpoint_create",
"checkpoint_restore",
"configure_objects",
"console_mark",
"debug",
"discover_tools",
"do",
"doctor",
"get_changeset",
"get_console_since",
"get_metrics",
"lint_playtest_suite",
"list_connections",
"list_skills",
"list_templates",
"load_session",
"mcp_status",
"navmesh_query",
"permission_prompt",
"reconnect_unity",
"release_smoke",
"resolve_tool_schema",
"run_playtest_suite",
"run_tests_wait",
"save_session",
"save_skill",
"save_template",
"scene_change_plan",
"screenshot_baseline",
"screenshot_compare",
"set_llm_config",
"set_properties",
"setup_objects",
"smart_build",
"snapshot",
"sync_unity",
"ui_intent",
"uitk_intent",
"use_skill",
"verify_after_change",
"vfx_intent",
"watch",
        };
    }
}
