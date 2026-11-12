# Generated from dsl/events.biome
# source-sha256=685c2d7e869ad4a31b6eb2dc7abab7af837e908ef756cf7864ce84a2f3682b61
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
