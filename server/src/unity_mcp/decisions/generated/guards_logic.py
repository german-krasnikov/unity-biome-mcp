# Generated from dsl/decisions/guards.logic
# source-sha256=cf256335259db6c22be663a214b209a2d6b557d2481bec10c87535561d64a82c
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class GuardVerdictValues:
    ASYNC_ONLY = "asyncOnly"
    AUTH_DENIED = "authDenied"
    COMPILING = "compiling"
    DISABLED = "disabled"
    INITIALIZING = "initializing"
    NOT_IN_PLAY = "notInPlay"
    OK = "ok"
    PLAY_BLOCKED = "playBlocked"
    PYTHON_ONLY = "pythonOnly"
    READ_ONLY_BLOCKED = "readOnlyBlocked"


@dataclass(frozen=True, slots=True)
class GuardVerdictInput:
    guard_allowed_during_compile: bool
    guard_allowed_in_play: bool
    guard_always_allowed: bool
    guard_auth_denied: bool
    guard_batchable: bool
    guard_compiling: bool
    guard_mutating: bool
    guard_play_known: bool
    guard_play_mode: bool
    guard_python_only: bool
    guard_read_only: bool
    guard_ready: bool
    guard_runtime: bool
    guard_site: str
    guard_tool_enabled: bool


def guard_verdict(inp: GuardVerdictInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("guard.allowedDuringCompile", inp.guard_allowed_during_compile, "bool")
    _check("guard.allowedInPlay", inp.guard_allowed_in_play, "bool")
    _check("guard.alwaysAllowed", inp.guard_always_allowed, "bool")
    _check("guard.authDenied", inp.guard_auth_denied, "bool")
    _check("guard.batchable", inp.guard_batchable, "bool")
    _check("guard.compiling", inp.guard_compiling, "bool")
    _check("guard.mutating", inp.guard_mutating, "bool")
    _check("guard.playKnown", inp.guard_play_known, "bool")
    _check("guard.playMode", inp.guard_play_mode, "bool")
    _check("guard.pythonOnly", inp.guard_python_only, "bool")
    _check("guard.readOnly", inp.guard_read_only, "bool")
    _check("guard.ready", inp.guard_ready, "bool")
    _check("guard.runtime", inp.guard_runtime, "bool")
    _check("guard.site", inp.guard_site, "enum", values=("top", "batch", "py",))
    _check("guard.toolEnabled", inp.guard_tool_enabled, "bool")
    return _decide_guard_verdict(inp)


__all__ = [
    "LogicInputError",
    "GuardVerdictValues",
    "GuardVerdictInput",
    "guard_verdict",
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


def _decide_guard_verdict(s: GuardVerdictInput) -> str:
    if (s.guard_site == "top") and (s.guard_ready is not True):
        return "initializing"
    if (s.guard_site == "top") and (s.guard_python_only is True):
        return "pythonOnly"
    if (s.guard_site == "top") and (s.guard_auth_denied is True):
        return "authDenied"
    if (((s.guard_site == "top") and (s.guard_compiling is True)) and (
            s.guard_allowed_during_compile is not True)):
        return "compiling"
    if ((((s.guard_site == "top") and (s.guard_play_mode is True)) and (s.guard_mutating
            is True)) and (s.guard_allowed_in_play is not True)):
        return "playBlocked"
    if (((s.guard_site == "top") and (s.guard_play_mode is not True)) and (
            s.guard_runtime is True)):
        return "notInPlay"
    if (((s.guard_site == "top") and (s.guard_read_only is True)) and (s.guard_mutating
            is True)):
        return "readOnlyBlocked"
    if (((s.guard_site == "top") and (s.guard_always_allowed is not True)) and (
            s.guard_tool_enabled is not True)):
        return "disabled"
    if (s.guard_site == "batch") and (s.guard_batchable is not True):
        return "asyncOnly"
    if ((((s.guard_site == "batch") and (s.guard_play_mode is True)) and (
            s.guard_mutating is True)) and (s.guard_allowed_in_play is not True)):
        return "playBlocked"
    if (((s.guard_site == "batch") and (s.guard_play_mode is not True)) and (
            s.guard_runtime is True)):
        return "notInPlay"
    if ((((s.guard_site == "batch") and (s.guard_compiling is True)) and (
            s.guard_mutating is True)) and (s.guard_allowed_during_compile is not
            True)):
        return "compiling"
    if (((s.guard_site == "batch") and (s.guard_read_only is True)) and (
            s.guard_mutating is True)):
        return "readOnlyBlocked"
    if (((s.guard_site == "batch") and (s.guard_always_allowed is not True)) and (
            s.guard_tool_enabled is not True)):
        return "disabled"
    if (s.guard_site == "batch") and (s.guard_ready is not True):
        return "initializing"
    if (((s.guard_site == "py") and (s.guard_read_only is True)) and (s.guard_mutating
            is True)):
        return "readOnlyBlocked"
    if ((((s.guard_site == "py") and (s.guard_play_known is True)) and (
            s.guard_play_mode is not True)) and (s.guard_runtime is True)):
        return "notInPlay"
    if (s.guard_site == "py") and (s.guard_python_only is True):
        return "pythonOnly"
    return "ok"
