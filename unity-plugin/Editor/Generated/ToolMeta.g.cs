// Generated from dsl/tools.biome
// source-sha256=248027926549299f4c3163bc33876cec4fa2c56aa2a1ec3ea1f690994f128a6e
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // CommandOptions for every TOOL/INTERNAL of dsl/tools.biome that this construct can state: Mutating
    // (effect != RO), Runtime, Required/Optional (from ARG), MaxResponseChars (from MAXCHARS), and the GUARD/WIRE flags.
    // Description, FileHandler and MutatingArgsPolicy are not data here: a caller sets one on the returned struct (a mutable
    // struct) after Get. kind (Register/RegisterAction/RegisterAsync) is a call-site choice, not data here either.
    internal static class ToolMeta
    {
        internal static CommandOptions Get(string cmd)
        {
            switch (cmd)
            {
                case "alias_status":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AllowedDuringCompile = true,
                    };
                case "analyze_lod_culling":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "focus",
                        }),
                    };
                case "animation":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "clip",
                            "clip_name",
                            "property",
                            "keys",
                            "time",
                            "component_type",
                            "binding_path",
                            "tangent",
                            "function_name",
                            "int_param",
                            "float_param",
                            "string_param",
                        }),
                    };
                case "animator":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "state",
                            "states",
                            "params",
                            "source",
                            "target",
                            "conditions",
                            "duration",
                            "exit_time",
                            "has_exit_time",
                            "type",
                            "name",
                            "blend_type",
                            "param",
                            "param_y",
                            "children",
                            "edit_action",
                            "layer",
                            "weight",
                            "blending",
                            "value",
                            "avatar_path",
                        }),
                    };
                case "ask_user":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "questions",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AlwaysAllowed = true,
                       AllowedDuringCompile = true,
                    };
                case "asset":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "type",
                            "name",
                            "folder",
                            "source",
                            "dest",
                            "prop",
                            "value",
                            "recursive",
                            "labels",
                            "output",
                            "include_deps",
                            "content",
                            "class_name",
                            "path_only",
                        }),
                    };
                case "attach_uitk":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "uxml",
                            "panel_settings",
                            "sort_order",
                        }),
                    };
                case "auto_wire":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "dry_run",
                        }),
                    };
                case "autofit_collider":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "type",
                        }),
                    };
                case "bake":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "target",
                        }),
                        Optional = string.Join(",", new string[] {
                            "action",
                        }),
                    };
                case "batch":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "commands",
                        }),
                        Optional = string.Join(",", new string[] {
                            "timeout",
                            "atomic",
                            "validate_aliases",
                            "on_error",
                        }),
                       AllowedDuringCompile = true,
                    };
                case "build":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "target",
                            "scenes",
                            "path",
                            "dev",
                        }),
                       PlayOk = true,
                    };
                case "cancel_test_run":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "run_id",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AlwaysAllowed = true,
                       AllowedDuringCompile = true,
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "check_colliders":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                        }),
                    };
                case "checkpoint":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "label",
                        }),
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "compile_preflight":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "file_path",
                            "new_content",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AllowedDuringCompile = true,
                    };
                case "compile_status":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = "",
                        Optional = "",
                       AllowedDuringCompile = true,
                    };
                case "create_object":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "name",
                        }),
                        Optional = string.Join(",", new string[] {
                            "parent",
                            "components",
                            "primitive",
                            "prefab_path",
                            "scene",
                        }),
                    };
                case "create_ui":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "type",
                        }),
                        Optional = string.Join(",", new string[] {
                            "name",
                            "parent",
                            "anchor",
                            "pos",
                            "size",
                            "pivot",
                            "color",
                            "text",
                            "font_size",
                            "render_mode",
                            "font_min",
                            "font_max",
                        }),
                    };
                case "debug_animator":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "debug_physics":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "radius",
                        }),
                    };
                case "delete_object":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "id",
                            "path",
                            "force",
                        }),
                    };
                case "diagnose":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "expected_compile",
                            "prev_mvid",
                        }),
                       AlwaysAllowed = true,
                       AllowedDuringCompile = true,
                    };
                case "editor":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "paths",
                            "enable",
                            "action",
                        }),
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "execute_code":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "code",
                        }),
                        Optional = string.Join(",", new string[] {
                            "undo_label",
                        }),
                       AllowedDuringCompile = true,
                    };
                case "export_package":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = "",
                        Optional = "",
                    };
                case "export_playtest_aliases_to_defs":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "asset",
                            "defs",
                        }),
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "find_objects":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "name",
                            "tag",
                            "layer",
                            "component",
                        }),
                    };
                case "fingerprint":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "depth",
                        }),
                    };
                case "get_aliases":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = "",
                        Optional = "",
                       AllowedDuringCompile = true,
                    };
                case "get_capabilities":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AllowedDuringCompile = true,
                    };
                case "get_changes":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "clear",
                        }),
                    };
                case "get_compile_errors":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AllowedDuringCompile = true,
                    };
                case "get_component":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "fields",
                            "full",
                            "compress",
                            "component",
                            "type",
                        }),
                    };
                case "get_components_list":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "id",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "get_console":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "count",
                            "level",
                            "first",
                            "keyword",
                            "count_only",
                            "since",
                        }),
                        MaxResponseChars = 20000,
                       AllowedDuringCompile = true,
                    };
                case "get_enabled_tools":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AlwaysAllowed = true,
                       AllowedDuringCompile = true,
                    };
                case "get_frame_stats":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "include",
                        }),
                    };
                case "get_hierarchy":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "depth",
                            "root",
                            "filter",
                            "components",
                            "compress",
                            "summary",
                            "incremental",
                            "full",
                            "scene",
                        }),
                        MaxResponseChars = 50000,
                    };
                case "get_memory":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "include",
                        }),
                    };
                case "get_object_detail":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "id",
                        }),
                        Optional = string.Join(",", new string[] {
                            "full",
                        }),
                    };
                case "get_playtest_run":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = "",
                        Optional = "",
                       AllowedDuringCompile = true,
                    };
                case "get_schema":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "type",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "get_selection":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "get_spatial_context":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "radius",
                        }),
                    };
                case "get_status":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = "",
                        Optional = "",
                       AlwaysAllowed = true,
                       AllowedDuringCompile = true,
                    };
                case "get_test_count":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AllowedDuringCompile = true,
                    };
                case "get_test_progress":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "run_id",
                        }),
                       AllowedDuringCompile = true,
                    };
                case "get_test_results":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "run_id",
                        }),
                       AllowedDuringCompile = true,
                    };
                case "get_test_run":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "run_id",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AllowedDuringCompile = true,
                    };
                case "get_unity_events":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                        }),
                    };
                case "get_version":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = "",
                        Optional = "",
                    };
                case "get_watches":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "import_package":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = "",
                        Optional = "",
                    };
                case "inspect":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "paths",
                            "components",
                            "fields",
                            "full",
                            "compress",
                            "find_type",
                        }),
                    };
                case "inspect_uitk":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "depth",
                            "selector",
                            "filter",
                            "show_unity_private",
                            "show_style",
                        }),
                    };
                case "invoke_method":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "component",
                            "method",
                        }),
                        Optional = string.Join(",", new string[] {
                            "args",
                        }),
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "lint_playtest":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "script",
                        }),
                       AllowedDuringCompile = true,
                    };
                case "lint_scene_refs":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "snippet",
                        }),
                    };
                case "lint_ugui":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "root",
                        }),
                    };
                case "lint_uitk":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "fix",
                        }),
                    };
                case "list_events":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "path",
                            "component",
                            "event",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "list_playtest_files":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = "",
                        Optional = "",
                       AllowedDuringCompile = true,
                    };
                case "list_test_runs":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "limit",
                        }),
                       AllowedDuringCompile = true,
                    };
                case "manage_component":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "type",
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "material":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "object_path",
                            "shader",
                            "prop",
                            "value",
                            "source",
                            "targets",
                            "slot",
                            "filter",
                            "target",
                        }),
                    };
                case "material_audit":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "platform",
                            "action",
                        }),
                    };
                case "menu":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                        }),
                    };
                case "move_to":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "position",
                        }),
                        Optional = string.Join(",", new string[] {
                            "timeout",
                        }),
                       PlayOk = true,
                    };
                case "object_diff":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "path_a",
                            "path_b",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "package":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "name",
                            "version",
                            "query",
                        }),
                       PlayOk = true,
                    };
                case "particle":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "name",
                            "module",
                            "prop",
                            "value",
                            "preset",
                        }),
                    };
                case "ping":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = "",
                        Optional = "",
                       AlwaysAllowed = true,
                       AllowedDuringCompile = true,
                    };
                case "ping_object":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "prefab":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "asset_path",
                            "base_path",
                            "variant_path",
                            "component",
                            "prop",
                            "value",
                            "add_component",
                            "remove_component",
                            "recursive",
                            "mode",
                            "scope",
                            "format",
                        }),
                    };
                case "profile":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "duration",
                            "threshold_ms",
                            "session",
                            "compare_with",
                            "focus",
                            "mode",
                        }),
                    };
                case "project_settings":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                            "target",
                        }),
                        Optional = string.Join(",", new string[] {
                            "prop",
                            "value",
                            "index",
                            "build_target",
                        }),
                    };
                case "query_state":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "queries",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "recompile":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "references":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "children",
                            "depth",
                            "source",
                            "target",
                            "mappings",
                        }),
                    };
                case "region_clear":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "vertices",
                        }),
                        Optional = string.Join(",", new string[] {
                            "dry_run",
                            "filter",
                            "cap",
                        }),
                    };
                case "rename_object":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "name",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "render_analyze":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "baseline_id",
                            "max_events",
                            "detail",
                        }),
                    };
                case "resolve_scene_refs":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "refs",
                        }),
                        Optional = string.Join(",", new string[] {
                            "fields",
                        }),
                    };
                case "resolve_test_request":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "request_id",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                       AllowedDuringCompile = true,
                    };
                case "run_playtest":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "script",
                            "timeout",
                            "abort_on_fail",
                            "defs",
                            "path",
                            "snapshot_on_failure",
                            "fresh",
                            "before_hook",
                            "after_hook",
                            "format",
                        }),
                       PlayOk = true,
                    };
                case "run_tests":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "filter",
                            "request_id",
                            "mode",
                        }),
                       PlayOk = true,
                    };
                case "runtime_snapshot":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "type",
                        }),
                        Optional = string.Join(",", new string[] {
                            "name",
                            "component",
                            "compress",
                        }),
                        MaxResponseChars = 50000,
                    };
                case "scan_scene":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "scene":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "scene",
                            "include_unsaved",
                        }),
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "scene_diff":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "scene_environment":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "prop",
                            "value",
                            "action",
                        }),
                    };
                case "scene_health":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "focus",
                        }),
                    };
                case "screenshot":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "width",
                            "height",
                            "camera",
                            "path",
                            "output_path",
                            "describe",
                            "raw",
                            "zoom",
                            "angles",
                            "supersample",
                            "offset",
                            "fixed_size",
                            "highlight",
                            "show_colliders",
                            "angle",
                            "annotation_id",
                        }),
                       AllowedDuringCompile = true,
                       SpecialDispatch = true,
                    };
                case "scriptable_object":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "type",
                            "prop",
                            "value",
                            "fields",
                            "filter",
                        }),
                    };
                case "search_scene":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "query",
                        }),
                        Optional = string.Join(",", new string[] {
                            "root",
                            "limit",
                            "scene",
                        }),
                        MaxResponseChars = 30000,
                    };
                case "serialized_field_rename_audit":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "type",
                            "old_field",
                            "new_field",
                        }),
                        Optional = string.Join(",", new string[] {
                            "include",
                        }),
                    };
                case "set_active":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "active",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "set_material":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "color",
                        }),
                        Optional = string.Join(",", new string[] {
                            "shader",
                        }),
                    };
                case "set_parent":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "parent",
                            "world_position_stays",
                        }),
                    };
                case "set_property":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "value",
                            "dry_run",
                            "find_type",
                            "ref_component_type",
                            "component",
                            "prop",
                        }),
                    };
                case "set_property_delta":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "component",
                            "prop",
                            "delta",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "set_rect":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "anchor",
                            "pos",
                            "size",
                            "pivot",
                            "offset_min",
                            "offset_max",
                            "pos3",
                        }),
                    };
                case "set_sibling_index":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "index",
                        }),
                        Optional = string.Join(",", new string[] {
                        }),
                    };
                case "shader":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "target",
                            "preset",
                            "code",
                            "shader_name",
                            "prop",
                            "value",
                            "keyword",
                            "enabled",
                            "node_type",
                            "node_id",
                            "node_action",
                            "output_node",
                            "output_slot",
                            "input_node",
                            "input_slot",
                            "edge_action",
                            "name",
                            "type",
                            "default_value",
                            "reference_name",
                            "new_name",
                            "layout",
                            "h_gap",
                            "v_gap",
                        }),
                    };
                case "source_patch_write":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = "",
                        Optional = "",
                    };
                case "spatial_query":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "target",
                            "distance",
                            "radius",
                            "component",
                            "cell_size",
                            "layer_mask",
                            "center",
                            "vertices",
                            "region_id",
                            "cap",
                        }),
                    };
                case "start_playtest":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = "",
                        Optional = "",
                       PlayOk = true,
                    };
                case "sync_playtest_aliases_from_defs":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "defs",
                            "asset",
                        }),
                       PlayOk = true,
                       NoUndo = true,
                    };
                case "test_step":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "position",
                        }),
                        Optional = string.Join(",", new string[] {
                            "wait_after",
                            "timeout",
                            "checks_before",
                            "checks_after",
                        }),
                       PlayOk = true,
                    };
                case "timeline":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "track",
                            "track_type",
                            "clip",
                            "binding",
                            "start",
                            "duration",
                            "blend_in",
                            "blend_out",
                            "asset_path",
                            "director_path",
                            "tracks",
                            "time",
                            "name",
                            "clip_in",
                            "index",
                            "offset",
                            "value",
                        }),
                    };
                case "transfer_object":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "target_scene",
                            "parent",
                            "world_position_stays",
                        }),
                    };
                case "uitk_element":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "action",
                        }),
                        Optional = string.Join(",", new string[] {
                            "path",
                            "ref",
                            "selector",
                            "name",
                            "value",
                            "property",
                            "class_name",
                        }),
                    };
                case "uitk_file":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "content",
                            "selector",
                            "attr",
                            "value",
                            "cls",
                            "parent",
                            "tag",
                            "attrs",
                            "prop",
                            "action",
                        }),
                       NotBatchable = true,
                    };
                case "undo_last":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "turns",
                        }),
                    };
                case "unwire_event":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "component",
                            "event",
                        }),
                        Optional = string.Join(",", new string[] {
                            "index",
                        }),
                    };
                case "validate_playtest_aliases":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "defs",
                            "asset",
                        }),
                       AllowedDuringCompile = true,
                    };
                case "validate_references":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                            "path",
                        }),
                        Optional = string.Join(",", new string[] {
                            "depth",
                            "verbose",
                            "ignore_optional",
                        }),
                    };
                case "validate_triggers":
                    return new CommandOptions
                    {
                        Mutating = false,
                        Required = string.Join(",", new string[] {
                        }),
                        Optional = string.Join(",", new string[] {
                            "min_distance",
                            "root",
                        }),
                    };
                case "wait_until":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Runtime = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "component",
                            "field",
                            "value",
                        }),
                        Optional = string.Join(",", new string[] {
                            "timeout",
                            "negate",
                            "abort_on_fail",
                        }),
                    };
                case "wire_event":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "path",
                            "component",
                            "event",
                            "target",
                            "method",
                        }),
                        Optional = string.Join(",", new string[] {
                            "arg_value",
                            "target_component_type",
                            "parameter_types",
                            "arg_type",
                        }),
                    };
                default:
                    throw new System.ArgumentException($"ToolMeta.Get: no entry for '{cmd}'");
            }
        }
    }
}
