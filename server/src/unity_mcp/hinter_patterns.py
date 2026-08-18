"""Pattern data for ToolHinter — predicates, match tables, and static lookup data."""
import os
from collections import deque  # noqa: TC003
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass


@dataclass(frozen=True)
class Call:
    cmd: str
    key: tuple
    mutates: bool


@dataclass(frozen=True)
class Pattern:
    id: str
    predicate: Callable[[deque, Call], bool]
    suggested_cmd: str
    hint: str
    trigger_cmd: str


def _key(cmd: str, args: dict) -> tuple:
    if cmd in ("get_component", "inspect", "get_object_detail"):
        return ("read", args.get("path", ""), args.get("type") or args.get("component", ""))
    if cmd in ("set_property", "set_property_delta"):
        return ("write", args.get("path", ""), args.get("component", ""),
                args.get("prop") or args.get("field", ""))
    if cmd == "set_active":
        return ("active", args.get("path", ""))
    if cmd == "delete_object":
        return ("delete", args.get("path", ""))
    if cmd == "screenshot":
        return ("snap",)
    if cmd in ("get_console", "recompile", "get_compile_errors"):
        return (cmd,)
    return (cmd,)


def _count_cmd_in_window(recent: deque, cmd: str, n: int) -> int:
    return sum(1 for c in list(recent)[-n:] if c.cmd == cmd)


def _count_key_in_window(recent: deque, key_prefix: tuple, n: int) -> int:
    return sum(1 for c in list(recent)[-n:] if c.key[:len(key_prefix)] == key_prefix)


def _cmd_in_window(recent: deque, cmd: str, n: int) -> bool:
    return any(c.cmd == cmd for c in list(recent)[-n:])


# ── Pattern predicates ────────────────────────────────────────────────────────

def _pred_inspect_loop(recent: deque, call: Call) -> bool:
    if call.cmd != "get_component":
        return False
    return _count_cmd_in_window(recent, "get_component", 8) >= 2


def _pred_batch_writes(recent: deque, call: Call) -> bool:
    if call.cmd != "set_property":
        return False
    path = call.key[1]
    component = call.key[2]
    same_target = ("write", path, component)
    return _count_key_in_window(recent, same_target, 6) >= 2


def _pred_find_then_read(recent: deque, call: Call) -> bool:
    if call.cmd != "get_component":
        return False
    return _cmd_in_window(recent, "find_objects", 4)


def _pred_screenshot_spam(recent: deque, call: Call) -> bool:
    if call.cmd != "screenshot":
        return False
    window = list(recent)[-8:]
    # Screenshot writes an artifact but does not change the scene whose visual
    # state this hint tracks. A non-capture write resets the sequence, so only
    # screenshots after the most recent such write contribute to the hint.
    last_write = max(
        (
            index
            for index, recent_call in enumerate(window)
            if recent_call.mutates and recent_call.cmd != "screenshot"
        ),
        default=-1,
    )
    return sum(
        1 for recent_call in window[last_write + 1 :]
        if recent_call.cmd == "screenshot"
    ) >= 2


def _pred_console_poll(recent: deque, call: Call) -> bool:
    if call.cmd != "get_console":
        return False
    window = list(recent)[-6:]
    has_recompile = any(c.cmd == "recompile" for c in window)
    console_count = sum(1 for c in window if c.cmd == "get_console")
    return has_recompile and console_count >= 2


def _pred_redundant_verify(recent: deque, call: Call) -> bool:
    if call.cmd != "get_component":
        return False
    if os.environ.get("UNITY_MCP_REFLECT", "1") == "0":
        return False
    path = call.key[1]
    component = call.key[2] if len(call.key) > 2 else ""
    # Look for set_property on same (path, component) in last 3
    return any(
        c.cmd == "set_property" and c.key[1] == path and c.key[2] == component
        for c in list(recent)[-3:]
    )


_PATTERNS: list[Pattern] = [
    Pattern(
        id="inspect-loop",
        predicate=_pred_inspect_loop,
        suggested_cmd="inspect",
        hint="[HINT: 3+ get_component calls — use inspect(paths=...) for one trip — saves ~80tok]",
        trigger_cmd="get_component",
    ),
    Pattern(
        id="batch-writes",
        predicate=_pred_batch_writes,
        suggested_cmd="batch",
        hint="[HINT: 3+ writes to same component — use batch or set_property_delta — saves ~120tok]",
        trigger_cmd="set_property",
    ),
    Pattern(
        id="find-then-read",
        predicate=_pred_find_then_read,
        suggested_cmd="inspect",
        hint="[HINT: find_objects → get_component pattern — use inspect to fold both — saves ~60tok]",
        trigger_cmd="get_component",
    ),
    Pattern(
        id="screenshot-spam",
        predicate=_pred_screenshot_spam,
        suggested_cmd="fingerprint",
        hint="[HINT: repeated screenshot without writes — try fingerprint for cheap state diff — saves ~500tok]",
        trigger_cmd="screenshot",
    ),
    Pattern(
        id="console-poll",
        predicate=_pred_console_poll,
        suggested_cmd="wait_until",
        hint="[HINT: post-recompile console polling — use wait_until or get_compile_errors — saves ~300tok]",
        trigger_cmd="get_console",
    ),
    Pattern(
        id="redundant-verify-read",
        predicate=_pred_redundant_verify,
        suggested_cmd="",
        hint="[HINT: set_property already returns snapshot via reflect — skip the verify read — saves ~50tok]",
        trigger_cmd="get_component",
    ),
]
