"""Transform Claude/Kimi stream-json NDJSON lines → pipe-format strings.

Pure stateful transformer. Never raises. Unknown input → empty list.
"""
import json
from dataclasses import dataclass, field

_MAX_TOOL_RESULT_LEN = 200


@dataclass
class _ToolCallAcc:
    """Buffer for a tool_use block spanning start → N deltas → stop."""
    active: bool      = False
    muted:  bool      = False  # suppress text_delta in tool_result blocks
    name:   str       = ""
    id:     str       = ""
    args:   list[str] = field(default_factory=list)

    def reset(self) -> None:
        self.active = self.muted = False
        self.name = self.id = ""
        self.args = []

    def complete_args(self) -> str:
        return "".join(self.args)


def _transform_line(line: str, acc: _ToolCallAcc) -> list[str]:
    """Convert one NDJSON line → 0..N pipe-format strings. Mutates acc."""
    if not line or not line.strip():
        return []
    try:
        obj = json.loads(line)
    except (json.JSONDecodeError, ValueError):
        return []

    t = obj.get("type", "")

    if t == "system":
        sub = obj.get("subtype", "")
        if sub == "init":
            return [f"si|{obj.get('session_id', '')}"]
        if sub == "api_retry":
            return []  # transient — SDK auto-retries, don't show as error
        return []

    if t == "stream_event":
        return _handle_stream_event(obj, acc)

    if t == "result":
        if obj.get("is_error"):
            return [f"e|{obj.get('error', 'Unknown error')}"]
        sid   = obj.get("session_id", "")
        cost  = obj.get("total_cost_usd", 0) or 0
        usage = obj.get("usage") or {}
        inp   = usage.get("input_tokens", 0) or 0
        out   = usage.get("output_tokens", 0) or 0
        return [f"d|{sid}|{cost}|{inp}|{out}"]

    if t in ("control_request", "sdk_control_request"):
        return _parse_control_request(obj)

    if t == "tool_progress":
        return [f"tp|{obj.get('percentage', 0)}|{obj.get('message', '')}"]

    if t == "rate_limit_event":
        return [f"rl|{obj.get('message', 'Rate limited')}"]

    if t == "session_state_changed":
        return [f"ss|{obj.get('state', '')}"]

    return []


def _handle_stream_event(obj: dict, acc: _ToolCallAcc) -> list[str]:
    ev = obj.get("event") or {}
    et = ev.get("type", "")

    if et == "content_block_start":
        blk = ev.get("content_block") or {}
        bt = blk.get("type", "")
        if bt == "tool_use":
            acc.reset()
            acc.active = True
            acc.name = blk.get("name", "")
            acc.id   = blk.get("id", "")
        elif bt == "tool_result":
            acc.muted = True
        else:
            acc.muted = False
        return []

    if et == "content_block_delta":
        d  = ev.get("delta") or {}
        dt = d.get("type", "")
        if dt == "text_delta":
            if acc.active or acc.muted:
                return []
            return [f"t|{d.get('text', '')}"]
        if dt == "input_json_delta":
            acc.args.append(d.get("partial_json", ""))
        return []

    if et == "content_block_stop":
        if acc.active:
            out = f"tc|{acc.name}|{acc.id}|{acc.complete_args()}"
            acc.reset()
            return [out]
        acc.muted = False
        return []

    return []  # message_start/delta/stop and future events


def _transform_plain_text_line(line: str, acc: _ToolCallAcc) -> list[str]:
    """Wrap plain-text backend stdout lines as pipe-format text events."""
    stripped = line.strip() if line else ""
    if not stripped:
        return []
    return [f"t|{stripped}"]


def _transform_codex_line(line: str, acc: _ToolCallAcc) -> list[str]:
    """Convert Codex CLI NDJSON (codex exec --json) → pipe-format."""
    if not line or not line.strip():
        return []
    try:
        obj = json.loads(line)
    except (json.JSONDecodeError, ValueError):
        return [f"t|{line.strip()}"] if line.strip() else []

    t = obj.get("type", "")

    if t == "item.started":
        item = obj.get("item") or {}
        if item.get("type") == "mcp_tool_call":
            name = item.get("tool", "")
            id_ = item.get("id", "")
            args = item.get("arguments", {})
            if isinstance(args, dict):
                args = json.dumps(args, ensure_ascii=False, separators=(",", ":"))
            return [f"tc|{name}|{id_}|{args}"] if name else []
        return []

    if t == "item.completed":
        item = obj.get("item") or {}
        kind = item.get("type", "")
        if kind == "agent_message":
            text = item.get("text", "")
            return [f"t|{text}"] if text else []
        if kind in ("tool_call", "function_call"):
            name = item.get("name", "")
            id_ = item.get("call_id") or item.get("id", "")
            args = item.get("arguments", "")
            if isinstance(args, dict):
                args = json.dumps(args, ensure_ascii=False, separators=(",", ":"))
            return [f"tc|{name}|{id_}|{args}"] if name else []
        if kind == "mcp_tool_call":
            # chip was already emitted on item.started; emit result now
            id_ = item.get("id", "")
            result = item.get("result") or {}
            content = result.get("content") or []
            text = " ".join(c.get("text", "") for c in content if c.get("type") == "text")
            ok = item.get("status", "") != "error"
            return [f"tr|{id_}|{'true' if ok else 'false'}|{text[:_MAX_TOOL_RESULT_LEN]}"] if id_ else []
        return []

    if t == "turn.completed":
        usage = obj.get("usage") or {}
        inp = usage.get("input_tokens", 0) or 0
        out = (usage.get("output_tokens", 0) or 0) + (usage.get("reasoning_output_tokens", 0) or 0)
        return [f"d||0|{inp}|{out}"]

    if t == "error":
        return [f"e|{obj.get('message', 'Codex error')}"]

    return []


def _transform_opencode_line(line: str, acc: _ToolCallAcc) -> list[str]:
    """Convert OpenCode NDJSON (opencode run --format json) → pipe-format."""
    if not line or not line.strip():
        return []
    try:
        obj = json.loads(line)
    except (json.JSONDecodeError, ValueError):
        return [f"t|{line.strip()}"] if line.strip() else []

    t = obj.get("type", "")

    if t == "text":
        part = obj.get("part") or {}
        text = part.get("text", "")
        return [f"t|{text}"] if text else []

    if t == "step_finish":
        part = obj.get("part") or {}
        tokens = part.get("tokens") or {}
        inp = tokens.get("input", 0)
        out = tokens.get("output", 0)
        sid = obj.get("sessionID", "")
        cost = part.get("cost", 0) or 0
        return [f"d|{sid}|{cost}|{inp}|{out}"]

    if t == "error":
        part = obj.get("part") or {}
        return [f"e|{part.get('error', 'OpenCode error')}"]

    if t == "tool_start":
        part = obj.get("part") or {}
        name = part.get("name", "")
        tid = part.get("id", "")
        args = json.dumps(part.get("input", {}), ensure_ascii=False, separators=(",", ":"))
        return [f"tc|{name}|{tid}|{args}"] if name else []

    return []  # step_start, tool_finish, etc.


def _transform_kimi_line(line: str, acc: _ToolCallAcc) -> list[str]:
    """Convert Kimi NDJSON (kimi -p --output-format stream-json) → pipe-format."""
    if not line or not line.strip():
        return []
    try:
        obj = json.loads(line)
    except (json.JSONDecodeError, ValueError):
        return [f"t|{line.strip()}"] if line.strip() else []

    role = obj.get("role", "")

    if role == "assistant":
        text = obj.get("content", "")
        return [f"t|{text}"] if text else []

    if role == "meta":
        if obj.get("type") == "session.resume_hint":
            sid = obj.get("session_id", "")
            return [f"d|{sid}|0|0|0"]
        return []

    return []


def _parse_control_request(obj: dict) -> list[str]:
    req = obj.get("request") or {}
    sub = req.get("subtype", "")

    if sub == "can_use_tool":
        rid   = req.get("request_id", "")
        tname = req.get("tool_name", "")
        inp   = req.get("input") or {}
        inp_s = json.dumps(inp, ensure_ascii=False, separators=(",", ":"))
        if tname == "AskUserQuestion":
            return [f"au|{rid}|{inp_s}"]
        return [f"pp|{tname}|{rid}|{inp_s}"]

    if sub == "hook_callback":
        rid   = obj.get("request_id", "")   # top-level, not inside request
        inp   = req.get("input") or {}
        tname = inp.get("tool_name", "")
        tinp  = inp.get("tool_input") or {}
        tinp_s = json.dumps(tinp, ensure_ascii=False, separators=(",", ":"))
        if tname == "AskUserQuestion":
            return [f"au|{rid}|{tinp_s}"]
        return [f"pp|{tname}|{rid}|{tinp_s}"]

    if sub == "permission":
        rid   = req.get("request_id", "")
        tname = req.get("tool_name", "")
        tinp  = req.get("tool_input") or {}
        tinp_s = json.dumps(tinp, ensure_ascii=False, separators=(",", ":"))
        return [f"pp|{tname}|{rid}|{tinp_s}"]

    if sub == "elicitation":
        rid   = req.get("request_id", "")
        elab  = req.get("elicitation") or {}
        elab_s = json.dumps(elab, ensure_ascii=False, separators=(",", ":"))
        return [f"au|{rid}|{elab_s}"]

    return []
