// Generated from dsl/tools.biome
// source-sha256=fdb456e124945afeb73e1ecd54f2c1f941271dd9b786628523f0e60d600d24dd
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
