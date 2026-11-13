// Generated from dsl/tools.biome
// source-sha256=9a68cfced712b9ba09c849f1d4a2de28edab0cff7a1d4be33cfe7d26ba2ba04f
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
