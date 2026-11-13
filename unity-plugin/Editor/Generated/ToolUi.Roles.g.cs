// Generated from dsl/tools.biome
// source-sha256=385b6968907572e67385e5e7dc2e105b35a7e35bfd730786d74a3fea06784905
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    internal static partial class ToolUi
    {
        // (tool, role, argument name): what the mutation card reads. The argument names are the catalog's own.
        internal static readonly (string tool, string role, string arg)[] Roles =
        {
        ("create_object", "name", "name"),
        ("delete_object", "target", "path"),
        ("manage_component", "target", "path"),
        ("manage_component", "name", "type"),
        ("rename_object", "target", "path"),
        ("rename_object", "newname", "name"),
        ("set_active", "target", "path"),
        ("set_active", "value", "active"),
        ("set_property", "target", "path"),
        ("set_property", "prop", "prop"),
        ("set_property", "value", "value"),
        ("set_property_delta", "target", "path"),
        ("set_property_delta", "prop", "prop"),
        ("set_property_delta", "value", "delta"),
        };
    }
}
