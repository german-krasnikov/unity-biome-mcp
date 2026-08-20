"""Pure pipe-format string → AgentEvent converter.

Stateless. Never raises. Unknown prefixes return [].
"""
import json

from ..agent_event import AgentEvent
from .protocol import EventContext  # noqa: TC001


def _make_event(kind: str, payload: dict, ctx: EventContext) -> AgentEvent:
    return AgentEvent(
        conversation_id=ctx.conversation_id,
        session_id=ctx.session_id,
        turn_id=ctx.turn_id,
        sequence=ctx.sequence,
        kind=kind,
        payload=payload,
    )


def parse_pipe_string(pipe: str, ctx: EventContext) -> list[AgentEvent]:
    """Convert one pipe-format string to 0–2 AgentEvent objects.

    Returns [] for unknown or unsupported prefixes (forward compat).
    Returns [cost_update, turn_completed] for 'd|' prefix.
    Never raises.
    """
    try:
        return _parse(pipe, ctx)
    except Exception:  # noqa: BLE001
        return []


def _parse(pipe: str, ctx: EventContext) -> list[AgentEvent]:  # noqa: PLR0912
    if not pipe:
        return []

    parts = pipe.split("|", 1)
    prefix = parts[0]
    rest = parts[1] if len(parts) > 1 else ""

    if prefix == "t":
        return [_make_event("assistant_delta", {"text": rest}, ctx)]

    if prefix == "th":
        return [_make_event("thought_delta", {"text": rest}, ctx)]

    if prefix == "si":
        return [_make_event("session_started", {"provider_session_id": rest}, ctx)]

    if prefix == "e":
        return [_make_event("error", {"message": rest}, ctx)]

    if prefix == "rl":
        return [_make_event("warning", {"message": rest, "code": "rate_limit"}, ctx)]

    if prefix == "ss":
        return [_make_event("capabilities_changed", {"state": rest}, ctx)]

    if prefix == "tc":
        segs = rest.split("|", 2)
        name     = segs[0] if len(segs) > 0 else ""
        id_      = segs[1] if len(segs) > 1 else ""
        args_raw = segs[2] if len(segs) > 2 else "{}"
        try:
            args = json.loads(args_raw) if args_raw else {}
        except json.JSONDecodeError:
            args = {}
        if not isinstance(args, dict):
            args = {}
        return [_make_event("tool_call_started", {"name": name, "id": id_, "args": args}, ctx)]

    if prefix == "tr":
        segs   = rest.split("|", 2)
        id_    = segs[0] if len(segs) > 0 else ""
        ok_str = (segs[1] if len(segs) > 1 else "true").strip()
        result = segs[2] if len(segs) > 2 else ""
        if ok_str.lower() == "true":
            return [_make_event("tool_call_completed", {"id": id_, "result": result}, ctx)]
        return [_make_event("tool_call_failed", {"id": id_, "error": result}, ctx)]

    if prefix == "d":
        segs = rest.split("|", 3)
        try:
            cost = float(segs[1]) if len(segs) > 1 and segs[1] else 0.0
            inp  = int(segs[2])   if len(segs) > 2 and segs[2] else 0
            out  = int(segs[3])   if len(segs) > 3 and segs[3] else 0
        except ValueError:
            return []
        return [
            _make_event("cost_update", {"cost_usd": cost, "input_tokens": inp, "output_tokens": out}, ctx),
            _make_event("turn_completed", {}, ctx),
        ]

    if prefix == "pp":
        segs      = rest.split("|", 2)
        tool_name = segs[0] if len(segs) > 0 else ""
        rid       = segs[1] if len(segs) > 1 else ""
        inp_raw   = segs[2] if len(segs) > 2 else "{}"
        try:
            inp = json.loads(inp_raw) if inp_raw else {}
        except json.JSONDecodeError:
            inp = {}
        if not isinstance(inp, dict):
            inp = {}
        return [_make_event("permission_requested",
                            {"tool_name": tool_name, "request_id": rid, "input": inp}, ctx)]

    if prefix == "au":
        segs    = rest.split("|", 1)
        rid     = segs[0] if len(segs) > 0 else ""
        inp_raw = segs[1] if len(segs) > 1 else "{}"
        try:
            inp = json.loads(inp_raw) if inp_raw else {}
        except json.JSONDecodeError:
            inp = {}
        if not isinstance(inp, dict):
            inp = {}
        return [_make_event("permission_requested",
                            {"request_id": rid, "input": inp, "is_ask_user": True}, ctx)]

    if prefix == "tp":
        segs    = rest.split("|", 1)
        pct_str = segs[0] if len(segs) > 0 else "0"
        msg     = segs[1] if len(segs) > 1 else ""
        try:
            pct = float(pct_str)
        except ValueError:
            pct = 0.0
        return [_make_event("warning", {"code": "tool_progress", "progress_pct": pct, "message": msg}, ctx)]

    return []
