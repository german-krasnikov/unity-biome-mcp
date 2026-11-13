"""Pattern data for ToolHinter — window folds (hinter.contract's win.* facts) and the static Pattern table.

The 6 known patterns' firing logic now lives in dsl/decisions/hinter.logic (hintPattern DECISION, via
decisions/hinter_facts.py). `Pattern.predicate` stays on the dataclass only so a caller can still register a
custom pattern of its own (hinter.py:observe() falls back to calling it for any pattern id outside the known 6).
"""
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
    return sum(1 for c in list(recent)[-n:] if c.key[: len(key_prefix)] == key_prefix)


def _screenshots_since_write(recent: deque, n: int) -> int:
    """Screenshots in the last `n` calls after the latest non-screenshot write.

    A screenshot writes an artifact but does not change the scene whose visual state this hint
    tracks, so it does not reset the sequence — only a non-capture write does.
    """
    window = list(recent)[-n:]
    last_write = max(
        (index for index, recent_call in enumerate(window) if recent_call.mutates and recent_call.cmd != "screenshot"),
        default=-1,
    )
    return sum(1 for recent_call in window[last_write + 1 :] if recent_call.cmd == "screenshot")


def _same_target_set_property(recent: deque, path: str, component: str, n: int) -> int:
    """set_property calls (not set_property_delta) on the same (path, component) in the last `n` calls."""
    return sum(
        1
        for c in list(recent)[-n:]
        if c.cmd == "set_property" and len(c.key) > 2 and c.key[1] == path and c.key[2] == component
    )


def window_facts(recent: deque, call: Call) -> dict:
    """The win.* facts of `call` against `recent` (computed BEFORE `call` is appended to it)."""
    path = call.key[1] if len(call.key) > 1 else ""
    component = call.key[2] if len(call.key) > 2 else ""
    return {
        "get_component8": _count_cmd_in_window(recent, "get_component", 8),
        "same_target_writes6": _count_key_in_window(recent, ("write", path, component), 6),
        "find_objects4": _count_cmd_in_window(recent, "find_objects", 4),
        "screenshots_since_write8": _screenshots_since_write(recent, 8),
        "recompile6": _count_cmd_in_window(recent, "recompile", 6),
        "get_console6": _count_cmd_in_window(recent, "get_console", 6),
        "same_target_set_property3": _same_target_set_property(recent, path, component, 3),
    }


def reflect_on() -> bool:
    """env.reflectOn: UNITY_MCP_REFLECT defaults to on, '0' turns it off (hinter_patterns.py:109 historically)."""
    return os.environ.get("UNITY_MCP_REFLECT", "1") != "0"


# id -> hintPattern's enum outcome, and back. Order = priority (hinter.logic's AT order).
KNOWN_PATTERN_OUTCOMES: dict[str, str] = {
    "inspect-loop": "inspect_loop",
    "batch-writes": "batch_writes",
    "find-then-read": "find_then_read",
    "screenshot-spam": "screenshot_spam",
    "console-poll": "console_poll",
    "redundant-verify-read": "redundant_verify_read",
}

_UNUSED_PREDICATE: Callable[[deque, Call], bool] = lambda recent, call: False  # noqa: E731,ARG005

_PATTERNS: list[Pattern] = [
    Pattern(
        id="inspect-loop",
        predicate=_UNUSED_PREDICATE,
        suggested_cmd="inspect",
        hint="[HINT: 3+ get_component calls — use inspect(paths=...) for one trip — saves ~80tok]",
        trigger_cmd="get_component",
    ),
    Pattern(
        id="batch-writes",
        predicate=_UNUSED_PREDICATE,
        suggested_cmd="batch",
        hint="[HINT: 3+ writes to same component — use batch or set_property_delta — saves ~120tok]",
        trigger_cmd="set_property",
    ),
    Pattern(
        id="find-then-read",
        predicate=_UNUSED_PREDICATE,
        suggested_cmd="inspect",
        hint="[HINT: find_objects → get_component pattern — use inspect to fold both — saves ~60tok]",
        trigger_cmd="get_component",
    ),
    Pattern(
        id="screenshot-spam",
        predicate=_UNUSED_PREDICATE,
        suggested_cmd="fingerprint",
        hint="[HINT: repeated screenshot without writes — try fingerprint for cheap state diff — saves ~500tok]",
        trigger_cmd="screenshot",
    ),
    Pattern(
        id="console-poll",
        predicate=_UNUSED_PREDICATE,
        suggested_cmd="wait_until",
        hint="[HINT: post-recompile console polling — use wait_until or get_compile_errors — saves ~300tok]",
        trigger_cmd="get_console",
    ),
    Pattern(
        id="redundant-verify-read",
        predicate=_UNUSED_PREDICATE,
        suggested_cmd="",
        hint="[HINT: set_property already returns snapshot via reflect — skip the verify read — saves ~50tok]",
        trigger_cmd="get_component",
    ),
]
