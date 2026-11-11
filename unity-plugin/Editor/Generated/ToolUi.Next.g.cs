// Generated from dsl/tools.biome
// source-sha256=ae7ae5192ef26bc47e023f639ac9ca2c0ed65b883653a301f194ebed26f4de43
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    internal static partial class ToolUi
    {
        internal static string NextAfter(string cmd)
        {
            switch (cmd)
            {
                case "batch": return "get_console level=Error";
                case "create_object": return "get_hierarchy depth=1";
                case "delete_object": return "get_hierarchy depth=1";
                case "manage_component": return "get_components_list";
                case "set_parent": return "get_hierarchy depth=1";
                case "set_property": return "get_console level=Error";
                case "unwire_event": return "get_component";
                case "wire_event": return "validate_references";
                default:
                    return null;
            }
        }
    }
}
