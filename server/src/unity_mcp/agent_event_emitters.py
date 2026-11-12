# Generated from dsl/events.biome
# source-sha256=685c2d7e869ad4a31b6eb2dc7abab7af837e908ef756cf7864ce84a2f3682b61
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
