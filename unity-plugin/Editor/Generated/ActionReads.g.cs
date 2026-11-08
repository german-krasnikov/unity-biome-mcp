// Generated from dsl/tools.biome
// source-sha256=8d9d7ec60320675ee3ac64b4a452d8429aee96f4439d88f9f2295120b8bb2a69
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // The actions of a command that only read (the READ lines of dsl/tools.biome): what CommandRegistry.IsMutating asks. A command that is
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
                        case "get_clip_path":
                        case "get_events":
                            return true;
                        default:
                            return false;
                    }
                case "animator":
                    switch (action)
                    {
                        case "get":
                        case "get_blend_tree":
                            return true;
                        default:
                            return false;
                    }
                case "asset":
                    switch (action)
                    {
                        case "find":
                        case "find_dependents":
                        case "get_dependencies":
                        case "get_info":
                        case "validate_move":
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
                case "editor":
                    switch (action)
                    {
                        case "project_path":
                        case "state":
                            return true;
                        default:
                            return false;
                    }
                case "material":
                    switch (action)
                    {
                        case "get":
                        case "get_errors":
                        case "list_properties":
                        case "list_shaders":
                        case "list_slots":
                            return true;
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
                        case "analyze":
                        case "compare":
                        case "list_sessions":
                        case "status":
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
                        case "find":
                        case "get":
                        case "list_types":
                            return true;
                        default:
                            return false;
                    }
                case "shader":
                    switch (action)
                    {
                        case "get":
                        case "graph_get":
                            return true;
                        default:
                            return false;
                    }
                case "timeline":
                    switch (action)
                    {
                        case "get":
                        case "get_bindings":
                            return true;
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
