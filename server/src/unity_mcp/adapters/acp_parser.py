"""Pure ACP NDJSON line → AgentEvent converter.

Stateless. Never raises. Unknown types return [].
"""
from __future__ import annotations

import json
from typing import TYPE_CHECKING

from ..agent_event import AgentEvent

if TYPE_CHECKING:
    from .protocol import EventContext


def _make(kind: str, payload: dict, ctx: EventContext) -> AgentEvent:
    return AgentEvent(
        conversation_id=ctx.conversation_id,
        session_id=ctx.session_id,
        turn_id=ctx.turn_id,
        sequence=ctx.sequence,
        kind=kind,
        payload=payload,
    )


def parse_acp_line(line: str, ctx: EventContext) -> list[AgentEvent]:
    """Convert one ACP NDJSON line → 0-N AgentEvent. Returns [] on unknown. Never raises."""
    try:
        return _parse(line, ctx)
    except Exception:  # noqa: BLE001
        return []


def _parse(line: str, ctx: EventContext) -> list[AgentEvent]:
    if not line or not line.strip():
        return []
    msg = json.loads(line)
    if not isinstance(msg, dict):
        return []
    msg_type = msg.get("type", "")

    if msg_type == "session/create":
        return [_make("session_started", {"provider_session_id": msg.get("session_id", "")}, ctx)]

    if msg_type == "session/update":
        return _parse_update(msg.get("content", {}), ctx)

    if msg_type == "session/complete":
        return [
            _make("cost_update", {
                "cost_usd":      float(msg.get("cost_usd", 0.0)),
                "input_tokens":  int(msg.get("input_tokens", 0)),
                "output_tokens": int(msg.get("output_tokens", 0)),
            }, ctx),
            _make("turn_completed", {}, ctx),
        ]

    if msg_type == "session/error":
        return [_make("error", {"message": msg.get("message", "")}, ctx)]

    if msg_type == "session/request_permission":
        return [_make("permission_requested", {
            "tool_name":  msg.get("tool_name", ""),
            "request_id": msg.get("request_id", ""),
            "input":      msg.get("input", {}),
        }, ctx)]

    return []


def _parse_update(content: dict, ctx: EventContext) -> list[AgentEvent]:
    c_type = content.get("type", "")

    if c_type == "text":
        return [_make("assistant_delta", {"text": content.get("text", "")}, ctx)]

    if c_type == "thinking":
        return [_make("thought_delta", {"text": content.get("text", "")}, ctx)]

    if c_type == "tool_call":
        return [_make("tool_call_started", {
            "name": content.get("name", ""),
            "id":   content.get("id", ""),
            "args": content.get("args", {}),
        }, ctx)]

    if c_type == "tool_result":
        id_ = content.get("id", "")
        if content.get("ok", False):
            return [_make("tool_call_completed", {"id": id_, "result": content.get("result", "")}, ctx)]
        return [_make("tool_call_failed", {"id": id_, "error": content.get("error", "")}, ctx)]

    return []
