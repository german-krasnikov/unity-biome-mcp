// Generated from dsl/tools.biome
// source-sha256=16b40461ecc9b06e898b054cc35553ca287bcec1ac777667d0abcfce574b0966
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    // The declared action vocabulary of a tool (the ACTION lines of dsl/tools.biome): what the MCP client is told exists. A tool
    // not here, or an action not listed, is undeclared -- dispatch may still accept it until its catalog entry is caught up.
    internal static class ToolActions
    {
        internal static string[] Names(string cmd)
        {
            switch (cmd)
            {
                case "animation":
                    return new[]
                    {
                        "get",
                        "create",
                        "edit",
                        "add_key",
                        "remove_key",
                        "remove_curve",
                        "set_keys",
                        "set_loop",
                        "set_wrap",
                        "set_framerate",
                        "preview",
                        "get_clip_path",
                        "get_events",
                        "add_event",
                        "remove_event",
                    };
                default:
                    return System.Array.Empty<string>();
            }
        }

        internal static bool IsKnown(string cmd, string action)
        {
            return System.Array.IndexOf(Names(cmd), action) >= 0;
        }
    }
}
