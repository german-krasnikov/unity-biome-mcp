// T-6.3: Detects missed agent delegation — when a message contained [agent:name]
// but the model's first tool call was not Agent/Task.
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    internal static class AgentMissDetector
    {
        private static readonly Regex _pat =
            new Regex(@"\[agent:([^\]]+)\]", RegexOptions.Compiled);

        /// <summary>Returns the first agent name found in the LLM payload, or null.</summary>
        internal static string ExtractAgentName(string payload)
        {
            var m = _pat.Match(payload ?? "");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
