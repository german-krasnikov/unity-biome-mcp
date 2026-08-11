// Maps raw tool names to human-friendly verbs shown in the chat UI.
// Pure logic, NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class ToolVerbMap
    {
        // Single source of truth: derives from PermissionConfig so prefix can never drift.
        private const string P = PermissionConfig.MCP_TOOL_PREFIX;

        private static readonly Dictionary<string, string> _map =
            new Dictionary<string, string>
            {
                { P + "get_hierarchy",        "Reading scene" },
                { P + "set_property",         "Editing" },
                { P + "create_object",        "Creating" },
                { P + "screenshot",           "Capturing screenshot" },
                { P + "batch",                "Running batch" },
                { P + "manage_component",     "Editing components" },
                { P + "run_playtest",         "Playtesting" },
                { P + "validate_references",  "Checking refs" },
                { P + "inspect",              "Reading" },
                { P + "search_scene",         "Searching" },
                { P + "find_references",      "Searching" },
                { P + "wire_event",           "Wiring" },
                { P + "delete_object",        "Deleting" },
                { P + "get_component",        "Reading component" },
                { P + "set_parent",           "Re-parenting" },
                { P + "move_to",              "Moving" },
                { P + "get_console",          "Reading console" },
                { P + "get_compile_errors",   "Checking errors" },
                { P + "run_tests",            "Running tests" },
                { P + "scene",                "Managing scene" },
                // Built-in Claude Code tools (no MCP prefix; Agent confirmed from session transcripts)
                { "Agent",      "Agent" },
                { "Task",       "Agent" },      // insurance: older SDK may emit Task
                { "TodoWrite",  "Updated tasks" },
                { "Edit",       "Editing file" },
                { "Write",      "Writing file" },
                { "MultiEdit",  "Editing files" },
                { "Bash",       "Running command" },
                { "Read",       "Reading file" },
                { "LS",         "Listing files" },
                { "Glob",       "Searching files" },
                { "Grep",       "Searching" },
                { "WebFetch",   "Fetching" },
                { "WebSearch",  "Searching web" },
            };

        /// <summary>
        /// Returns a short human verb for a raw tool name.
        /// Falls back to stripping the mcp__ prefix and replacing underscores with spaces.
        /// </summary>
        public static string Humanize(string rawToolName)
        {
            if (string.IsNullOrEmpty(rawToolName)) return "Working";
            if (_map.TryGetValue(rawToolName, out var verb)) return verb;

            // Fallback: strip leading mcp__<server>__ prefix
            var name = rawToolName;
            var secondDunder = name.IndexOf("__", 4); // skip first "mcp__"
            if (name.StartsWith("mcp__") && secondDunder > 0)
                name = name.Substring(secondDunder + 2);

            return name.Replace('_', ' ');
        }
    }
}
