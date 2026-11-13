// Generated from dsl/tools.biome
// source-sha256=fb0ae2e48afd8305810f08991a9eb3150492943f27c500c18e93ef755b12c6ea
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // The BLAST radius of a command (BLAST lines of dsl/tools.biome): how disruptive a mutation is. A command absent here has radius 0.
    internal static class ToolBlast
    {
        internal static int RadiusOf(string cmd)
        {
            switch (cmd)
            {
                case "batch": return 3;
                case "create_object": return 2;
                case "delete_object": return 4;
                case "execute_code": return 5;
                case "find_objects": return 0;
                case "get_component": return 0;
                case "get_hierarchy": return 0;
                case "get_object_detail": return 0;
                case "inspect": return 0;
                case "manage_component": return 2;
                case "query_state": return 0;
                case "scene": return 3;
                case "screenshot": return 0;
                case "set_active": return 1;
                case "set_material": return 1;
                case "set_property": return 1;
                case "wire_event": return 2;
                default:
                    return 0;
            }
        }
    }
}
