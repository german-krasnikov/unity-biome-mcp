# Generated from dsl/events.biome
# source-sha256=7bbcda5cee8f82a31af5ab53ea89e690fff3cc4819c3b1a0421cef4523ed6227
# Edit the source, then run biome generate.

ALL_KINDS: tuple[str, ...] = (
    "session_started",
    "session_resumed",
    "session_ended",
    "turn_started",
    "turn_completed",
    "assistant_delta",
    "thought_delta",
    "tool_call_started",
    "tool_call_completed",
    "tool_call_failed",
    "permission_requested",
    "permission_resolved",
    "file_change_detected",
    "plan_step_started",
    "plan_step_completed",
    "error",
    "warning",
    "cost_update",
    "capabilities_changed",
    "heartbeat",
)
