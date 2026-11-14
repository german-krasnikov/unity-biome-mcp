// Generated from dsl/tools.biome
// source-sha256=c232024f9b8a86f2efa3a4cd269813172482f66b9e96b82e3bac0e6bca44a5d0
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
