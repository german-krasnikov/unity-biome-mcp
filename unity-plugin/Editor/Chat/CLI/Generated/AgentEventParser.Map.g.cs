// Generated from dsl/events.biome
// source-sha256=7bbcda5cee8f82a31af5ab53ea89e690fff3cc4819c3b1a0421cef4523ed6227
// Edit the source, then run biome generate.

using UnityMCP.Editor.Chat.Payload;

namespace UnityMCP.Editor.Chat
{
    internal sealed partial class AgentEventParser
    {
        private ChatEvent? MapKind(string kind, string payload, string sid)
        {
            switch (kind)
            {
            case "session_started":
            {
                var provider_session_id = EventPayload.S(payload, "provider_session_id", sid);
                var ev = ChatEvent.SessionInit(provider_session_id);
                return ev;
            }
            case "session_resumed":
            {
                var previous_session_id = EventPayload.S(payload, "previous_session_id");
                var new_session_id = EventPayload.S(payload, "new_session_id");
                return null;
            }
            case "session_ended":
            {
                var reason = EventPayload.S(payload, "reason");
                return null;
            }
            case "turn_started":
            {
                var text = EventPayload.S(payload, "text");
                return null;
            }
            case "turn_completed":
            {
                var cost_usd = _held_cost_usd;
                var input_tokens = _held_input_tokens;
                var output_tokens = _held_output_tokens;
                var ev = ChatEvent.TurnDone(sid, cost_usd, input_tokens, output_tokens);
                ResetHeld();
                return ev;
            }
            case "assistant_delta":
            {
                var text = EventPayload.S(payload, "text");
                var ev = ChatEvent.TextDelta(text);
                return ev;
            }
            case "thought_delta":
            {
                var text = EventPayload.S(payload, "text");
                var ev = ChatEvent.Thinking(text);
                return ev;
            }
            case "tool_call_started":
            {
                var name = EventPayload.S(payload, "name");
                var id = EventPayload.S(payload, "id");
                var args = EventPayload.O(payload, "args");
                var ev = ChatEvent.ToolStart(name, args, id);
                return ev;
            }
            case "tool_call_completed":
            {
                var id = EventPayload.S(payload, "id");
                var result = EventPayload.S(payload, "result");
                var ev = ChatEvent.ToolResult(id, result, true);
                return ev;
            }
            case "tool_call_failed":
            {
                var id = EventPayload.S(payload, "id");
                var error = EventPayload.S(payload, "error");
                var ev = ChatEvent.ToolResult(id, error, false);
                return ev;
            }
            case "permission_requested":
            {
                var request_id = EventPayload.S(payload, "request_id");
                var tool_name = EventPayload.S(payload, "tool_name");
                var input = EventPayload.O(payload, "input");
                var is_ask_user = EventPayload.B(payload, "is_ask_user");
                if (is_ask_user) return ChatEvent.AskUser(request_id, input);
                var ev = ChatEvent.PermissionPrompt(request_id, tool_name, input);
                return ev;
            }
            case "permission_resolved":
            {
                var request_id = EventPayload.S(payload, "request_id");
                var granted = EventPayload.B(payload, "granted");
                return null;
            }
            case "file_change_detected":
            {
                var path = EventPayload.S(payload, "path");
                var change_type = EventPayload.S(payload, "change_type");
                var ev = ChatEvent.FileChange(path);
                return ev;
            }
            case "plan_step_started":
            {
                var description = EventPayload.S(payload, "description");
                var step = EventPayload.S(payload, "step");
                var ev = ChatEvent.PlanUpdate(kind, description);
                return ev;
            }
            case "plan_step_completed":
            {
                var step = EventPayload.S(payload, "step");
                var description = EventPayload.S(payload, "description");
                var outcome = EventPayload.S(payload, "outcome");
                var ev = ChatEvent.PlanUpdate(kind, description);
                return ev;
            }
            case "error":
            {
                var message = EventPayload.S(payload, "message");
                var ev = ChatEvent.Error(message);
                return ev;
            }
            case "warning":
            {
                var code = EventPayload.S(payload, "code");
                var message = EventPayload.S(payload, "message");
                var progress_pct = EventPayload.F(payload, "progress_pct");
                if (code == "rate_limit") return ChatEvent.RateLimit(message);
                if (code == "tool_progress") return ChatEvent.ToolProgress(progress_pct, message);
                return null;
            }
            case "cost_update":
            {
                var cost_usd = EventPayload.F(payload, "cost_usd");
                var input_tokens = EventPayload.I(payload, "input_tokens");
                var output_tokens = EventPayload.I(payload, "output_tokens");
                _held_cost_usd = cost_usd;
                _held_input_tokens = input_tokens;
                _held_output_tokens = output_tokens;
                return null;
            }
            case "capabilities_changed":
            {
                var state = EventPayload.S(payload, "state");
                var previous_state = EventPayload.S(payload, "previous_state");
                var ev = ChatEvent.CapabilitiesChanged(state);
                return ev;
            }
            case "heartbeat":
            {
                var ev = ChatEvent.Heartbeat();
                return ev;
            }
                default:
                    return null;
            }
        }
    }
}
