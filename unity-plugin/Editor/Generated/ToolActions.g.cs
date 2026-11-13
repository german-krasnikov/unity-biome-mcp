// Generated from dsl/tools.biome
// source-sha256=9a68cfced712b9ba09c849f1d4a2de28edab0cff7a1d4be33cfe7d26ba2ba04f
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // The declared action vocabulary of a tool (the ACTION lines of dsl/tools.biome): what the MCP client is told exists. A tool
    // not here, or an action not listed, is undeclared -- dispatch may still accept it until its catalog entry is caught up.
    internal static class ToolActions
    {
        internal static string[] Names(string cmd)
        {
            switch (cmd)
            {
                case "animation":
                    return new[]
                    {
                        "get",
                        "create",
                        "edit",
                        "add_key",
                        "remove_key",
                        "remove_curve",
                        "set_keys",
                        "set_loop",
                        "set_wrap",
                        "set_framerate",
                        "preview",
                        "get_clip_path",
                        "get_events",
                        "add_event",
                        "remove_event",
                    };
                case "animator":
                    return new[]
                    {
                        "get",
                        "add_param",
                        "add_state",
                        "add_transition",
                        "set_default",
                        "remove",
                        "add_blend_tree",
                        "edit_blend_tree",
                        "get_blend_tree",
                        "add_layer",
                        "remove_layer",
                        "rename_layer",
                        "set_layer_weight",
                        "set_layer_blending",
                        "set_state_speed",
                        "update_transition",
                        "set_avatar",
                        "rename_state",
                        "rename_param",
                    };
                case "asset":
                    return new[]
                    {
                        "find",
                        "get_info",
                        "create",
                        "move",
                        "validate_move",
                        "duplicate",
                        "delete",
                        "get_dependencies",
                        "find_dependents",
                        "import_settings",
                        "export_package",
                        "import_package",
                        "read_text",
                        "write_text",
                        "reimport",
                    };
                case "build":
                    return new[]
                    {
                        "build",
                    };
                case "editor":
                    return new[]
                    {
                        "state",
                        "mutation_mode",
                        "play",
                        "pause",
                        "stop",
                        "select",
                        "project_path",
                    };
                case "manage_component":
                    return new[]
                    {
                        "add",
                        "remove",
                    };
                case "material":
                    return new[]
                    {
                        "create",
                        "get",
                        "set",
                        "copy",
                        "list_properties",
                        "list_slots",
                        "get_errors",
                        "list_shaders",
                        "set_fields",
                    };
                case "material_audit":
                    return new[]
                    {
                        "summary",
                        "materials",
                        "textures",
                        "duplicates",
                        "compression",
                        "recommendations",
                    };
                case "menu":
                    return new[]
                    {
                        "execute",
                        "list",
                    };
                case "package":
                    return new[]
                    {
                        "list",
                        "search",
                        "add",
                        "remove",
                    };
                case "particle":
                    return new[]
                    {
                        "get",
                        "create",
                        "set",
                        "apply",
                        "play",
                        "stop",
                        "pause",
                    };
                case "prefab":
                    return new[]
                    {
                        "save",
                        "create_variant",
                        "instantiate",
                        "apply",
                        "revert",
                        "get_overrides",
                        "unpack",
                        "edit",
                    };
                case "profile":
                    return new[]
                    {
                        "start",
                        "stop",
                        "status",
                        "analyze",
                        "compare",
                        "list_sessions",
                    };
                case "references":
                    return new[]
                    {
                        "get",
                        "find_to",
                        "remap",
                    };
                case "render_analyze":
                    return new[]
                    {
                        "stats",
                        "materials",
                        "shaders",
                        "overdraw",
                        "audit",
                        "compare",
                        "batching",
                        "lights",
                        "shadow_audit",
                        "probe_audit",
                        "light_optimize",
                        "frame_debug",
                    };
                case "scene":
                    return new[]
                    {
                        "new",
                        "open",
                        "save",
                        "discard",
                        "open_additive",
                        "close",
                        "set_active",
                        "list",
                        "save_copy",
                    };
                case "scene_environment":
                    return new[]
                    {
                        "get",
                        "set",
                    };
                case "scriptable_object":
                    return new[]
                    {
                        "create",
                        "get",
                        "set",
                        "list_types",
                        "find",
                    };
                case "shader":
                    return new[]
                    {
                        "get",
                        "create",
                        "set",
                        "graph_get",
                        "graph_create",
                        "graph_node",
                        "graph_edge",
                        "graph_add_property",
                        "graph_remove_property",
                        "graph_rename_property",
                        "graph_get_layout",
                        "graph_set_layout",
                        "graph_auto_layout",
                    };
                case "spatial_query":
                    return new[]
                    {
                        "nearest",
                        "in_front_of",
                        "objects_in_radius",
                        "bounds_info",
                        "raycast",
                        "spatial_map",
                        "objects_in_polygon",
                    };
                case "timeline":
                    return new[]
                    {
                        "get",
                        "create",
                        "edit",
                        "add_track",
                        "remove_track",
                        "add_clip",
                        "remove_clip",
                        "set_binding",
                        "set_timing",
                        "mute",
                        "unmute",
                        "lock",
                        "unlock",
                        "rename_track",
                        "reorder_track",
                        "duplicate_clip",
                        "add_marker",
                        "remove_marker",
                        "set_track_offset",
                        "set_duration",
                        "add_sub_track",
                        "set_clip_in",
                        "get_bindings",
                        "preview",
                    };
                case "transfer_object":
                    return new[]
                    {
                        "move",
                        "copy",
                    };
                case "uitk_element":
                    return new[]
                    {
                        "query",
                        "get",
                        "set_style",
                        "add_class",
                        "remove_class",
                        "get_style",
                        "enable",
                        "disable",
                    };
                case "uitk_file":
                    return new[]
                    {
                        "read",
                        "revert",
                        "write",
                        "create_uxml",
                        "create_uss",
                        "set-attr",
                        "add-class",
                        "remove-class",
                        "add-element",
                        "remove-element",
                        "set-rule",
                        "remove-rule",
                    };
                default:
                    return System.Array.Empty<string>();
            }
        }

        internal static bool IsKnown(string cmd, string action)
        {
            return System.Array.IndexOf(Names(cmd), action) >= 0;
        }
    }
}
