"""Canonical AgentEvent model + ProviderCapabilities for relay metadata."""

import uuid
from datetime import UTC, datetime
from typing import Any, Literal, get_args

from pydantic import BaseModel, ConfigDict, Field

_ALL_KINDS = Literal[
    "session_started", "session_resumed", "session_ended",
    "turn_started", "turn_completed",
    "assistant_delta", "thought_delta",
    "tool_call_started", "tool_call_completed", "tool_call_failed",
    "permission_requested", "permission_resolved",
    "file_change_detected",
    "plan_step_started", "plan_step_completed",
    "error", "warning",
    "cost_update",
    "capabilities_changed",
    "heartbeat",
]

# All known event kind strings — exported for test assertions and default fallback.
_ALL_KIND_LIST: list[str] = list(get_args(_ALL_KINDS))

# Per-provider subset of _ALL_KIND_LIST. Unknown providers get the full list.
_PROVIDER_EVENT_KINDS: dict[str, list[str]] = {
    "claude": _ALL_KIND_LIST,
    "codex": [k for k in _ALL_KIND_LIST if k not in {"thought_delta", "session_resumed"}],
    "kimi": ["session_started", "assistant_delta", "turn_completed", "cost_update"],
    "agy": ["session_started", "assistant_delta", "turn_started", "turn_completed"],
    "opencode": [
        "session_started", "assistant_delta",
        "tool_call_started", "tool_call_completed", "tool_call_failed",
        "turn_completed", "cost_update",
    ],
}


class AgentEvent(BaseModel):
    """Canonical envelope for one normalized event from any provider."""
    model_config = ConfigDict(extra="allow")  # unknown fields preserved for forward compat

    schema_version:  int             = 1
    event_id:        str             = Field(default_factory=lambda: str(uuid.uuid4()))
    conversation_id: str             = ""
    session_id:      str             = ""
    turn_id:         int             = 0
    sequence:        int             = 0   # monotonic within session, set by emitter
    timestamp:       str             = Field(
        default_factory=lambda: datetime.now(UTC).isoformat()
    )
    kind:            str             = ""  # one of _ALL_KINDS, or unknown future kind
    payload:         dict[str, Any]  = Field(default_factory=dict)
    meta:            dict[str, Any] | None = None


class ProviderCapabilities(BaseModel):
    """Structured view of what one backend can emit."""
    model_config = ConfigDict(extra="allow")

    protocol_version: str            = "2.0"
    provider_id:      str            = ""
    transport:        str            = "stdio"
    session:          dict[str, Any] = Field(default_factory=dict)
    modes:            list[str]      = Field(default_factory=list)
    events:           list[str]      = Field(default_factory=list)
    permissions:      dict[str, Any] = Field(default_factory=dict)

    @classmethod
    def from_probe(cls, provider_id: str, probe: dict) -> ProviderCapabilities:
        """Wrap the raw dict from BackendDef.probe_capabilities()."""
        return cls(
            provider_id = provider_id,
            session     = {
                "has_resume":     probe.get("has_resume", False),
                "binary_version": probe.get("binary_version"),
            },
            modes  = probe.get("has_modes", []),
            events = _PROVIDER_EVENT_KINDS.get(provider_id, _ALL_KIND_LIST),
            permissions = {
                "has_plan_mode":  "ask"   in probe.get("has_modes", []),
                "has_agent_mode": "agent" in probe.get("has_modes", []),
            },
        )


# Remove pydantic helpers from module namespace — get_type_hints() would try to
# resolve their pydantic-internal annotations (FieldInfo, JsonValue) in this
# module's globals and fail. They're not re-exported from here.
del ConfigDict, Field
