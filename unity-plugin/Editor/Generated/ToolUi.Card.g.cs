// Generated from dsl/tools.biome
// source-sha256=ae7ae5192ef26bc47e023f639ac9ca2c0ed65b883653a301f194ebed26f4de43
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // generated: the card of a tool (CARD lines of dsl/tools.biome)
    internal static partial class ToolUi
    {
        internal static string CardOf(string cmd)
        {
            switch (cmd)
            {
                case "apply_scene_change": return "generic";
                case "batch": return "generic";
                case "create_object": return "create";
                case "delete_object": return "delete";
                case "manage_component": return "component";
                case "rename_object": return "rename";
                case "set_active": return "set";
                case "set_parent": return "generic";
                case "set_property": return "set";
                case "set_property_delta": return "set";
                case "wire_event": return "generic";
                default:
                    return null;
            }
        }
    }
}
