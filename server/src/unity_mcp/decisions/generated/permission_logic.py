# Generated from dsl/decisions/permission.logic
# source-sha256=fcc95aab0a1953b89c2e7e2e8af446f07bb729c0c3ed7cd287e3f763d3a46645
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class ChatPermissionValues:
    ALLOW = "allow"
    DENY_ASK = "denyAsk"
    DENY_DANGEROUS = "denyDangerous"
    DENY_UNKNOWN_MODE = "denyUnknownMode"


@dataclass(frozen=True, slots=True)
class ChatPermissionInput:
    call_dangerous: bool
    call_mutating: bool
    chat_mode: str


def chat_permission(inp: ChatPermissionInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("call.dangerous", inp.call_dangerous, "bool")
    _check("call.mutating", inp.call_mutating, "bool")
    _check("chat.mode", inp.chat_mode, "enum", values=("external", "ask", "agent",
            "fullAccess", "unknown",))
    return _decide_chat_permission(inp)


__all__ = [
    "LogicInputError",
    "ChatPermissionValues",
    "ChatPermissionInput",
    "chat_permission",
]


# Unchecked evaluators: reachable only through the checked functions above.
def _check(
    fact_id: str,
    value: object,
    kind: str,
    lo: float | None = None,
    hi: float | None = None,
    integer: bool = False,
    values: tuple[str, ...] = (),
) -> None:
    if kind == "bool":
        ok = isinstance(value, bool)
    elif kind == "number":
        ok = (
            isinstance(value, (int, float))
            and not isinstance(value, bool)
            and math.isfinite(value)
        )
        number = _typing.cast("float", value)
        ok = ok and (lo is None or number >= lo) and (hi is None or number <= hi)
        if integer:
            ok = ok and float(number).is_integer() and abs(number) <= 9007199254740991
    elif kind == "enum":
        ok = isinstance(value, str) and value in values
    else:
        ok = isinstance(value, str)
    if not ok:
        raise LogicInputError(f"LOGIC_INPUT_CONTRACT: {fact_id}={value!r}")


def _decide_chat_permission(s: ChatPermissionInput) -> str:
    # rule: external (priority 10)
    if s.chat_mode == "external":
        return "allow"
    # rule: unknownMode (priority 11)
    if s.chat_mode == "unknown":
        return "denyUnknownMode"
    # rule: fullAccess (priority 12)
    if s.chat_mode == "fullAccess":
        return "allow"
    # rule: agentDangerous (priority 20)
    if (s.chat_mode == "agent") and (s.call_dangerous is True):
        return "denyDangerous"
    # rule: askBlocked (priority 30)
    if ((s.chat_mode == "ask") and ((s.call_mutating is True) or (s.call_dangerous is
            True))):
        return "denyAsk"
    return "allow"
