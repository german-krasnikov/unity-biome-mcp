# Generated from dsl/events.biome
# source-sha256=7bbcda5cee8f82a31af5ab53ea89e690fff3cc4819c3b1a0421cef4523ed6227
# Edit the source, then run biome generate.

EMITTERS: dict[str, tuple[str, ...]] = {
    "session_started": (
        "claude",
        "codex",
        "kimi",
        "agy",
        "opencode",
    ),
    "session_resumed": (
        "claude",
    ),
    "session_ended": (
        "claude",
        "codex",
    ),
    "turn_started": (
        "claude",
        "codex",
        "agy",
    ),
    "turn_completed": (
        "claude",
        "codex",
        "kimi",
        "agy",
        "opencode",
    ),
    "assistant_delta": (
        "claude",
        "codex",
        "kimi",
        "agy",
        "opencode",
    ),
    "thought_delta": (
        "claude",
    ),
    "tool_call_started": (
        "claude",
        "codex",
        "opencode",
    ),
    "tool_call_completed": (
        "claude",
        "codex",
        "opencode",
    ),
    "tool_call_failed": (
        "claude",
        "codex",
        "opencode",
    ),
    "permission_requested": (
        "claude",
        "codex",
    ),
    "permission_resolved": (
        "claude",
        "codex",
    ),
    "file_change_detected": (
        "claude",
        "codex",
    ),
    "plan_step_started": (
        "claude",
        "codex",
    ),
    "plan_step_completed": (
        "claude",
        "codex",
    ),
    "error": (
        "claude",
        "codex",
    ),
    "warning": (
        "claude",
        "codex",
    ),
    "cost_update": (
        "claude",
        "codex",
        "kimi",
        "opencode",
    ),
    "capabilities_changed": (
        "claude",
        "codex",
    ),
    "heartbeat": (
        "claude",
        "codex",
    ),
}
