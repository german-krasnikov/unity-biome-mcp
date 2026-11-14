using System.Collections.Generic;

namespace UnityMCP.Editor
{
    // Gate for CommandValidator.Validate on the DIRECT (non-batch) dispatch path. BatchHelper.cs
    // already validates every batch sub-command unconditionally (BatchHelper.cs:144); this brings
    // CommandRouter.Process/ProcessAsync to the same strictness.
    //
    // F1c 2/2: enabled for every command whose CommandRegistry.Register*(...) call site in
    // CommandRouter.Registration.cs/WatchCommandHandler.cs passes ToolMeta.Get(cmd) directly as
    // its CommandOptions — i.e. its live Required/Optional contract IS the generated catalog
    // (dsl/tools.biome -> ToolMeta.g.cs), already kept in sync by the A1/A3 lanes
    // (test_toolmeta_vs_golden.py, test_catalog_vs_csharp.py). Deliberately NOT "every registered
    // command": roughly 50 commands (batch, get_component, set_material, references, timeline,
    // shader, scene_environment, asset, inspect, profile, get_schema, render_analyze,
    // get_frame_stats, ask_user, validate_playtest_aliases, among others) are registered with a
    // hand-written `required:`/`optional:` literal instead, and test_toolmeta_vs_golden.py's own
    // KNOWN_DIVERGENT table documents several of those literals as not yet reconciled with the
    // catalog (A3's remaining scope). Concretely verified during this slice: "batch" is
    // registered with optional "on_error,timeout,atomic,validate_aliases" — no "timeout_ms" —
    // while server/src/unity_mcp/tools/batch.py sends "timeout_ms" on effectively every call
    // (its own comment: "post-A4 ... sent on effectively every call"). Turning this gate on for
    // "batch" today would reject nearly every real batch call with "Unknown param: ?timeout_ms".
    // Enabling it is a separate, verified fix for a future slice, not bundled here.
    internal static class ArgContract
    {
        private static readonly HashSet<string> _enabled = new HashSet<string>
        {
            "alias_status", "analyze_lod_culling", "animator", "attach_uitk", "auto_wire", "autofit_collider",
            "bake", "build", "cancel_test_run", "check_colliders", "checkpoint", "compile_preflight",
            "compile_status", "create_object", "create_ui", "debug_animator", "debug_physics", "delete_object",
            "editor", "execute_code", "export_playtest_aliases_to_defs", "find_objects", "fingerprint", "get_aliases",
            "get_capabilities", "get_changes", "get_compile_errors", "get_components_list", "get_console", "get_enabled_tools",
            "get_memory", "get_selection", "get_spatial_context", "get_status", "get_test_count", "get_test_progress",
            "get_test_results", "get_unity_events", "get_watches", "invoke_method", "lint_playtest", "lint_scene_refs",
            "lint_ugui", "lint_uitk", "list_events", "list_test_runs", "manage_component", "material",
            "material_audit", "menu", "move_to", "object_diff", "package", "particle",
            "ping", "ping_object", "project_settings", "query_state", "recompile", "region_clear",
            "rename_object", "resolve_scene_refs", "resolve_test_request", "runtime_snapshot", "scan_scene", "scene",
            "scene_diff", "scene_health", "scriptable_object", "search_scene", "serialized_field_rename_audit", "set_active",
            "set_parent", "set_property_delta", "set_rect", "set_sibling_index", "spatial_query", "sync_playtest_aliases_from_defs",
            "test_step", "transfer_object", "uitk_element", "undo_last", "unwire_event", "validate_references",
            "validate_triggers", "wait_until", "wire_event",
        };

        internal static string Check(string cmd, string argsJson) =>
            _enabled.Contains(cmd) ? CommandValidator.Validate(cmd, argsJson) : null;

        internal static IEnumerable<string> EnabledForTest => _enabled;
    }
}
