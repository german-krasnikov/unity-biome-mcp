// Maps raw tool names to human-friendly verbs shown in the chat UI.
// Pure logic, NUnit-testable.
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Chat
{
    public static class ToolVerbMap
    {
        // External tool verbs (dsl/data/toolverbs-external.core-data, Wave 2C-3):
        // built-in Claude Code tools (no MCP prefix) → human verb.
        // Previously hardcoded here; now generated from ExternalToolVerbRows.
        private static readonly Dictionary<string, string> _externalMap =
            ExternalToolVerbRows.All.ToDictionary(r => r.Tool, r => r.Verb);

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
