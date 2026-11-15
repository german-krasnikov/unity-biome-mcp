// Stateful JSON AgentEvent → ChatEvent mapper for relay v2 protocol.
// The kind vocabulary and the MapKind switch are generated from dsl/events.biome
// (E1, Q9) — see Generated/AgentEventParser.{Held,Reset,Map}.g.cs. This file keeps
// only the native entry points (Parse, Reset). No Unity API deps — pure NUnit testable.
namespace UnityMCP.Editor.Chat
{
    internal sealed partial class AgentEventParser
    {
        internal void Reset() => ResetHeld();

        /// <summary>Parse one JSON AgentEvent line. Returns null for unknown/unhandled kinds.</summary>
        internal ChatEvent? Parse(string jsonLine)
        {
            if (string.IsNullOrEmpty(jsonLine)) return null;
            var kind = JsonHelper.ExtractString(jsonLine, "kind");
            if (string.IsNullOrEmpty(kind)) return null;
            var payload = JsonHelper.ExtractObject(jsonLine, "payload");
            var sid     = JsonHelper.ExtractString(jsonLine, "session_id") ?? "";
            return MapKind(kind, payload, sid);
        }
    }
}
