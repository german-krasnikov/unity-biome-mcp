// Generated from dsl/tools.biome
// source-sha256=25f0d8d3327300a7121d6209899f434ca13aff2e27ef733c833bda852df5edf6
// Edit the source, then run biome generate.

namespace UnityMCP.Editor.Chat
{
    // The human verb of a tool in the chat UI (VERB lines of dsl/tools.biome); a tool without one falls back to its name.
    internal static class ToolVerbs
    {
        internal static readonly (string Tool, string Verb)[] Rows =
        {
        ("batch", "Running batch"),
        ("create_object", "Creating"),
        ("delete_object", "Deleting"),
        ("get_compile_errors", "Checking errors"),
        ("get_component", "Reading component"),
        ("get_components_list", "Reading components"),
        ("get_console", "Reading console"),
        ("get_hierarchy", "Reading scene"),
        ("inspect", "Reading"),
        ("manage_component", "Editing components"),
        ("move_to", "Moving"),
        ("run_playtest", "Playtesting"),
        ("run_tests", "Running tests"),
        ("scene", "Managing scene"),
        ("screenshot", "Capturing screenshot"),
        ("search_scene", "Searching"),
        ("set_parent", "Re-parenting"),
        ("set_property", "Editing"),
        ("validate_references", "Checking refs"),
        ("wire_event", "Wiring"),
        };
    }
}
