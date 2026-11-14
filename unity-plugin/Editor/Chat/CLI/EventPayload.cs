// Typed JSON payload reads for AgentEventParser.MapKind (generated) — thin wrappers over JsonHelper.
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat.Payload
{
    internal static class EventPayload
    {
        public static string S(string json, string key, string fallback = null) =>
            JsonHelper.ExtractString(json, key) ?? fallback ?? "";

        public static int I(string json, string key) => JsonHelper.ExtractInt(json, key);

        public static float F(string json, string key) => JsonHelper.ExtractFloat(json, key);

        public static bool B(string json, string key) => JsonHelper.ExtractString(json, key) == "true";

        public static string O(string json, string key) => JsonHelper.ExtractObject(json, key);
    }
}
