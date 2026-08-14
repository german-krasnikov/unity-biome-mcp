// Stateful JSON AgentEvent → ChatEvent mapper for relay v2 protocol.
// Accumulates cost_update fields until turn_completed emits a TurnDone.
// No Unity API deps — pure NUnit testable. ~90 lines.
using System.Globalization;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AgentEventParser
    {
        private float _pendingCostUsd;
        private int   _pendingInputTokens;
        private int   _pendingOutputTokens;

        internal void Reset()
        {
            _pendingCostUsd = 0f;
            _pendingInputTokens = 0;
            _pendingOutputTokens = 0;
        }

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

        private ChatEvent? MapKind(string kind, string payload, string sid)
        {
            switch (kind)
            {
                case "assistant_delta":
                    return ChatEvent.TextDelta(JsonHelper.ExtractString(payload, "text") ?? "");

                case "thought_delta":
                    return ChatEvent.Thinking(JsonHelper.ExtractString(payload, "text") ?? "");

                case "tool_call_started":
                    return ChatEvent.ToolStart(
                        JsonHelper.ExtractString(payload, "name") ?? "",
                        JsonHelper.ExtractObject(payload, "args"),
                        JsonHelper.ExtractString(payload, "id") ?? "");

                case "tool_call_completed":
                    return ChatEvent.ToolResult(
                        JsonHelper.ExtractString(payload, "id") ?? "",
                        JsonHelper.ExtractString(payload, "result") ?? "", true);

                case "tool_call_failed":
                    return ChatEvent.ToolResult(
                        JsonHelper.ExtractString(payload, "id") ?? "",
                        JsonHelper.ExtractString(payload, "error") ?? "", false);

                case "session_started":
                    return ChatEvent.SessionInit(
                        JsonHelper.ExtractString(payload, "provider_session_id") ?? sid);

                case "cost_update":
                    float.TryParse(JsonHelper.ExtractString(payload, "cost_usd"),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out _pendingCostUsd);
                    int.TryParse(JsonHelper.ExtractString(payload, "input_tokens"),
                        out _pendingInputTokens);
                    int.TryParse(JsonHelper.ExtractString(payload, "output_tokens"),
                        out _pendingOutputTokens);
                    return null; // accumulate until turn_completed

                case "turn_completed":
                    var done = ChatEvent.TurnDone(sid, _pendingCostUsd,
                        _pendingInputTokens, _pendingOutputTokens);
                    Reset();
                    return done;

                case "error":
                    return ChatEvent.Error(JsonHelper.ExtractString(payload, "message") ?? "");

                case "warning":
                    return JsonHelper.ExtractString(payload, "code") == "rate_limit"
                        ? ChatEvent.RateLimit(JsonHelper.ExtractString(payload, "message") ?? "")
                        : null;

                case "permission_requested":
                    return ChatEvent.PermissionPrompt(
                        JsonHelper.ExtractString(payload, "request_id") ?? "",
                        JsonHelper.ExtractString(payload, "tool_name") ?? "",
                        JsonHelper.ExtractObject(payload, "input"));

                case "capabilities_changed":
                    return ChatEvent.CapabilitiesChanged(
                        JsonHelper.ExtractString(payload, "state") ?? "");

                case "plan_step_started":
                case "plan_step_completed":
                    return ChatEvent.PlanUpdate(kind,
                        JsonHelper.ExtractString(payload, "description") ?? "");

                case "file_change_detected":
                    return ChatEvent.FileChange(JsonHelper.ExtractString(payload, "path") ?? "");

                case "heartbeat":
                    return ChatEvent.Heartbeat();

                default:
                    return null;
            }
        }
    }
}
