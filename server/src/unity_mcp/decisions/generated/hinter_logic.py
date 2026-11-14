# Generated from dsl/decisions/hinter.logic
# source-sha256=5b50969c2fe4e23d4fdbceceee52ae8c8fdbce8a01fd9f248ddf52ba00e34c6e
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class HintPatternValues:
    BATCH_WRITES = "batch_writes"
    CONSOLE_POLL = "console_poll"
    FIND_THEN_READ = "find_then_read"
    INSPECT_LOOP = "inspect_loop"
    NONE = "none"
    REDUNDANT_VERIFY_READ = "redundant_verify_read"
    SCREENSHOT_SPAM = "screenshot_spam"


@dataclass(frozen=True, slots=True)
class HintPatternInput:
    call_cmd: str
    env_reflect_on: bool
    open_batch_writes: bool
    open_console_poll: bool
    open_find_then_read: bool
    open_inspect_loop: bool
    open_redundant_verify: bool
    open_screenshot_spam: bool
    win_find_objects4: float
    win_get_component8: float
    win_get_console6: float
    win_recompile6: float
    win_same_target_set_property3: float
    win_same_target_writes6: float
    win_screenshots_since_write8: float


def hint_pattern(inp: HintPatternInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("call.cmd", inp.call_cmd, "string")
    _check("env.reflectOn", inp.env_reflect_on, "bool")
    _check("open.batchWrites", inp.open_batch_writes, "bool")
    _check("open.consolePoll", inp.open_console_poll, "bool")
    _check("open.findThenRead", inp.open_find_then_read, "bool")
    _check("open.inspectLoop", inp.open_inspect_loop, "bool")
    _check("open.redundantVerify", inp.open_redundant_verify, "bool")
    _check("open.screenshotSpam", inp.open_screenshot_spam, "bool")
    _check("win.findObjects4", inp.win_find_objects4, "number", lo=0, hi=16,
            integer=True)
    _check("win.getComponent8", inp.win_get_component8, "number", lo=0, hi=16,
            integer=True)
    _check("win.getConsole6", inp.win_get_console6, "number", lo=0, hi=16, integer=True)
    _check("win.recompile6", inp.win_recompile6, "number", lo=0, hi=16, integer=True)
    _check("win.sameTargetSetProperty3", inp.win_same_target_set_property3, "number",
            lo=0, hi=16, integer=True)
    _check("win.sameTargetWrites6", inp.win_same_target_writes6, "number", lo=0, hi=16,
            integer=True)
    _check("win.screenshotsSinceWrite8", inp.win_screenshots_since_write8, "number",
            lo=0, hi=16, integer=True)
    return _decide_hint_pattern(inp)


class HintGateValues:
    CLOSED = "closed"
    OPEN = "open"


@dataclass(frozen=True, slots=True)
class HintGateInput:
    hint_calls_since_emit: float
    hint_enabled: bool
    hint_suppressed: bool


def hint_gate(inp: HintGateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("hint.callsSinceEmit", inp.hint_calls_since_emit, "number", lo=-1000000000,
            hi=1000000000, integer=True)
    _check("hint.enabled", inp.hint_enabled, "bool")
    _check("hint.suppressed", inp.hint_suppressed, "bool")
    return _decide_hint_gate(inp)


class HintAdoptionValues:
    ADOPTED = "adopted"
    IGNORED = "ignored"
    PENDING = "pending"
    SUPPRESS = "suppress"


@dataclass(frozen=True, slots=True)
class HintAdoptionInput:
    adopt_ignored_before: float
    adopt_is_suggested: bool
    adopt_is_trigger: bool


def hint_adoption(inp: HintAdoptionInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("adopt.ignoredBefore", inp.adopt_ignored_before, "number", lo=0, hi=1000,
            integer=True)
    _check("adopt.isSuggested", inp.adopt_is_suggested, "bool")
    _check("adopt.isTrigger", inp.adopt_is_trigger, "bool")
    return _decide_hint_adoption(inp)


__all__ = [
    "LogicInputError",
    "HintPatternValues",
    "HintPatternInput",
    "hint_pattern",
    "HintGateValues",
    "HintGateInput",
    "hint_gate",
    "HintAdoptionValues",
    "HintAdoptionInput",
    "hint_adoption",
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


def _decide_hint_pattern(s: HintPatternInput) -> str:
    # rule: inspectLoop (priority 10)
    if ((s.open_inspect_loop is True) and (s.call_cmd == "get_component") and (
            s.win_get_component8 >= 2)):
        return "inspect_loop"
    # rule: batchWrites (priority 20)
    if ((s.open_batch_writes is True) and (s.call_cmd == "set_property") and (
            s.win_same_target_writes6 >= 2)):
        return "batch_writes"
    # rule: findThenRead (priority 30)
    if ((s.open_find_then_read is True) and (s.call_cmd == "get_component") and (
            s.win_find_objects4 >= 1)):
        return "find_then_read"
    # rule: screenshotSpam (priority 40)
    if ((s.open_screenshot_spam is True) and (s.call_cmd == "screenshot") and (
            s.win_screenshots_since_write8 >= 2)):
        return "screenshot_spam"
    # rule: consolePoll (priority 50)
    if ((s.open_console_poll is True) and (s.call_cmd == "get_console") and (
            s.win_recompile6 >= 1) and (s.win_get_console6 >= 2)):
        return "console_poll"
    # rule: redundantVerify (priority 60)
    if ((s.open_redundant_verify is True) and (s.call_cmd == "get_component") and (
            s.env_reflect_on is True) and (s.win_same_target_set_property3 >= 1)):
        return "redundant_verify_read"
    return "none"


def _decide_hint_gate(s: HintGateInput) -> str:
    # rule: disabled (priority 10)
    if s.hint_enabled is not True:
        return "closed"
    # rule: suppressed (priority 20)
    if s.hint_suppressed is True:
        return "closed"
    # rule: cooldown (priority 30)
    if s.hint_calls_since_emit < 8:
        return "closed"
    return "open"


def _decide_hint_adoption(s: HintAdoptionInput) -> str:
    # rule: adopted (priority 10)
    if s.adopt_is_suggested is True:
        return "adopted"
    # rule: suppress (priority 20)
    if (s.adopt_is_trigger is True) and (s.adopt_ignored_before >= 1):
        return "suppress"
    # rule: ignored (priority 21)
    if s.adopt_is_trigger is True:
        return "ignored"
    return "pending"
