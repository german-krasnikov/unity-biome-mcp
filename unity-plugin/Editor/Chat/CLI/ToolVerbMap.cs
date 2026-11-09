// Maps raw tool names to human-friendly verbs shown in the chat UI.
// Pure logic, NUnit-testable.
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Chat
{
    public static class ToolVerbMap
    {
        // Catalog tools (dsl/tools.biome VERB items) resolve through the generated lookup below;
        // this map stays only for tools outside the catalog's "чужой словарь" (built-in Claude Code tools).
        private static readonly Dictionary<string, string> _externalMap =
            new Dictionary<string, string>
            {
                // Built-in Claude Code tools (no MCP prefix; Agent confirmed from session transcripts)
                { "Agent",       "Agent" },
                { "Task",        "Agent" },         // insurance: older SDK may emit Task
                { "TodoWrite",   "Updated tasks" },  // kept in case it appears in other SDK builds
                { "TaskCreate",  "Creating task" },  // confirmed: Claude Code task panel (192 sessions)
                { "TaskUpdate",  "Updating task" },  // confirmed: status ∈ in_progress|completed
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

        // Catalog verbs (ToolVerbs.g.cs, generated from dsl/tools.biome VERB items) keyed by bare wire name.
        private static readonly Dictionary<string, string> _verbLookup =
            ToolVerbs.Rows.ToDictionary(r => r.Tool, r => r.Verb);

        /// <summary>
        /// Returns a short human verb for a raw tool name.
        /// Falls back to stripping the mcp__ prefix and replacing underscores with spaces.
        /// </summary>
        public static string Humanize(string rawToolName)
        {
            if (string.IsNullOrEmpty(rawToolName)) return "Working";
            if (_externalMap.TryGetValue(rawToolName, out var externalVerb)) return externalVerb;

            // Strip leading mcp__<server>__ prefix
            var name = rawToolName;
            var secondDunder = name.IndexOf("__", 4); // skip first "mcp__"
            if (name.StartsWith("mcp__") && secondDunder > 0)
                name = name.Substring(secondDunder + 2);

            if (_verbLookup.TryGetValue(name, out var catalogVerb)) return catalogVerb;

            return name.Replace('_', ' ');
        }
    }
}
