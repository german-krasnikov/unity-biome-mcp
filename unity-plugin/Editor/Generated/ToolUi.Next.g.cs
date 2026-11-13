// Generated from dsl/tools.biome
// source-sha256=ef8eb2d7c3bba65c3484358fe680dbde4b719151b50f56ba6d58c13fdc3b5de4
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
