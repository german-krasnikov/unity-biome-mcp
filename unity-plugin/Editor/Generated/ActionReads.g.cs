// Generated from dsl/tools.biome
// source-sha256=fdb456e124945afeb73e1ecd54f2c1f941271dd9b786628523f0e60d600d24dd
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // The actions of a command that only read (the READ lines of dsl/tools.biome, or the READ qualifier of an ACTION line): what CommandRegistry.IsMutating asks. A command that is
    // not here, or an action that is not listed (or is spelt in another case), is a write.
    internal static class ActionReads
    {
        internal static bool IsRead(string cmd, string action)
        {
            switch (cmd)
            {
                case "animation":
                    switch (action)
                    {
                        case "get":
                            return true;
                        case "get_clip_path":
                            return true;
                        case "get_events":
                            return true;
                        default:
                            return false;
                    }
                case "animator":
                    switch (action)
                    {
                        case "get":
                            return true;
                        case "get_blend_tree":
                            return true;
                        default:
                            return false;
                    }
                case "asset":
                    switch (action)
                    {
                        case "find":
                            return true;
                        case "get_info":
                            return true;
                        case "validate_move":
                            return true;
                        case "get_dependencies":
                            return true;
                        case "find_dependents":
                            return true;
                        default:
                            return false;
                    }
                case "bake":
                    switch (action)
                    {
                        case "settings":
                        case "status":
                            return true;
                        default:
                            return false;
                    }
                case "build":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "editor":
                    switch (action)
                    {
                        case "state":
                            return true;
                        case "project_path":
                            return true;
                        default:
                            return false;
                    }
                case "manage_component":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "material":
                    switch (action)
                    {
                        case "get":
                            return true;
                        case "list_properties":
                            return true;
                        case "list_slots":
                            return true;
                        case "get_errors":
                            return true;
                        case "list_shaders":
                            return true;
                        default:
                            return false;
                    }
                case "material_audit":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "menu":
                    switch (action)
                    {
                        case "list":
                            return true;
                        default:
                            return false;
                    }
                case "navmesh_query":
                    switch (action)
                    {
                        case "get_settings":
                        case "path":
                        case "raycast":
                        case "sample":
                        case "status":
                            return true;
                        default:
                            return false;
                    }
                case "package":
                    switch (action)
                    {
                        case "list":
                            return true;
                        case "search":
                            return true;
                        default:
                            return false;
                    }
                case "particle":
                    switch (action)
                    {
                        case "get":
                            return true;
                        default:
                            return false;
                    }
                case "prefab":
                    switch (action)
                    {
                        case "get_overrides":
                            return true;
                        default:
                            return false;
                    }
                case "profile":
                    switch (action)
                    {
                        case "status":
                            return true;
                        case "analyze":
                            return true;
                        case "compare":
                            return true;
                        case "list_sessions":
                            return true;
                        default:
                            return false;
                    }
                case "project_settings":
                    switch (action)
                    {
                        case "get":
                            return true;
                        default:
                            return false;
                    }
                case "references":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "render_analyze":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "scene":
                    switch (action)
                    {
                        case "list":
                            return true;
                        default:
                            return false;
                    }
                case "scene_environment":
                    switch (action)
                    {
                        case "get":
                            return true;
                        default:
                            return false;
                    }
                case "scriptable_object":
                    switch (action)
                    {
                        case "get":
                            return true;
                        case "list_types":
                            return true;
                        case "find":
                            return true;
                        default:
                            return false;
                    }
                case "shader":
                    switch (action)
                    {
                        case "get":
                            return true;
                        case "graph_get":
                            return true;
                        default:
                            return false;
                    }
                case "spatial_query":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "timeline":
                    switch (action)
                    {
                        case "get":
                            return true;
                        case "get_bindings":
                            return true;
                        default:
                            return false;
                    }
                case "transfer_object":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "uitk_element":
                    switch (action)
                    {
                        default:
                            return false;
                    }
                case "uitk_file":
                    switch (action)
                    {
                        case "read":
                            return true;
                        default:
                            return false;
                    }
                default:
                    return false;
            }
        }
    }
}
