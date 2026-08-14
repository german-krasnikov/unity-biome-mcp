// Minimal C# projection of relay v2 capabilities received in _cmd_start response.
// Used by UI to show/hide mode options based on provider support. ~50 lines.
namespace UnityMCP.Editor.Chat
{
    internal sealed class ProviderCapabilities
    {
        internal string   ProviderId      { get; }
        internal string   ProtocolVersion { get; }
        internal bool     HasResume       { get; }
        internal bool     HasPlanMode     { get; }
        internal bool     HasAgentMode    { get; }
        internal string[] SupportedModes  { get; }

        private ProviderCapabilities(string pid, string pv, bool resume,
            bool plan, bool agent, string[] modes)
        {
            ProviderId      = pid;
            ProtocolVersion = pv;
            HasResume       = resume;
            HasPlanMode     = plan;
            HasAgentMode    = agent;
            SupportedModes  = modes;
        }

        internal static ProviderCapabilities Empty =>
            new ProviderCapabilities("", "1.0", false, false, false,
                System.Array.Empty<string>());

        internal static ProviderCapabilities FromJson(string capsJson)
        {
            if (string.IsNullOrEmpty(capsJson)) return Empty;
            var session = JsonHelper.ExtractObject(capsJson, "session");
            var perms   = JsonHelper.ExtractObject(capsJson, "permissions");
            return new ProviderCapabilities(
                JsonHelper.ExtractString(capsJson, "provider_id") ?? "",
                JsonHelper.ExtractString(capsJson, "protocol_version") ?? "2.0",
                JsonHelper.ExtractString(session, "has_resume") == "true",
                JsonHelper.ExtractString(perms,   "has_plan_mode") == "true",
                JsonHelper.ExtractString(perms,   "has_agent_mode") == "true",
                ParseModes(JsonHelper.ExtractArray(capsJson, "modes")));
        }

        private static string[] ParseModes(string arrayJson)
        {
            // Minimal scan: extract quoted strings from ["ask","agent"] format.
            var result = new System.Collections.Generic.List<string>();
            int i = 1;
            while (i < arrayJson.Length - 1)
            {
                if (arrayJson[i] == '"')
                {
                    var end = arrayJson.IndexOf('"', i + 1);
                    if (end < 0) break;
                    result.Add(arrayJson.Substring(i + 1, end - i - 1));
                    i = end + 1;
                }
                else i++;
            }
            return result.ToArray();
        }
    }
}
