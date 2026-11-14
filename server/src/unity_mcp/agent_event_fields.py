# Generated from dsl/events.biome
# source-sha256=7bbcda5cee8f82a31af5ab53ea89e690fff3cc4819c3b1a0421cef4523ed6227
# Edit the source, then run biome generate.

PAYLOAD_FIELDS: dict[str, dict[str, tuple[str, str]]] = {
    "session_started": {
        "provider_session_id": ("text", "required"),
    },
    "session_resumed": {
        "previous_session_id": ("text", "required"),
        "new_session_id": ("text", "required"),
    },
    "session_ended": {
        "reason": ("text", "optional"),
    },
    "turn_started": {
        "text": ("text", "required"),
    },
    "turn_completed": {
    },
    "assistant_delta": {
        "text": ("text", "required"),
    },
    "thought_delta": {
        "text": ("text", "required"),
    },
    "tool_call_started": {
        "name": ("text", "required"),
        "id": ("text", "required"),
        "args": ("object", "required"),
    },
    "tool_call_completed": {
        "id": ("text", "required"),
        "result": ("text", "required"),
    },
    "tool_call_failed": {
        "id": ("text", "required"),
        "error": ("text", "required"),
    },
    "permission_requested": {
        "request_id": ("text", "required"),
        "tool_name": ("text", "optional"),
        "input": ("object", "required"),
        "is_ask_user": ("bool", "optional"),
    },
    "permission_resolved": {
        "request_id": ("text", "required"),
        "granted": ("bool", "required"),
    },
    "file_change_detected": {
        "path": ("text", "required"),
        "change_type": ("text", "optional"),
    },
    "plan_step_started": {
        "description": ("text", "required"),
        "step": ("text", "optional"),
    },
    "plan_step_completed": {
        "step": ("text", "required"),
        "description": ("text", "optional"),
        "outcome": ("text", "optional"),
    },
    "error": {
        "message": ("text", "required"),
    },
    "warning": {
        "code": ("text", "required"),
        "message": ("text", "optional"),
        "progress_pct": ("number", "optional"),
    },
    "cost_update": {
        "cost_usd": ("number", "required"),
        "input_tokens": ("int", "required"),
        "output_tokens": ("int", "required"),
    },
    "capabilities_changed": {
        "state": ("text", "required"),
        "previous_state": ("text", "optional"),
    },
    "heartbeat": {
    },
}
