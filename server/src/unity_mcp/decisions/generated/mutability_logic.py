# Generated from dsl/decisions/mutability.logic
# source-sha256=bbb7f17ec975588d28322ea95ba3d8df008e3a102e89e7d2f7426afbf7ef3722
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


@dataclass(frozen=True, slots=True)
class IsMutatingInput:
    args_abort_on_fail: str
    args_action: str
    args_clear: str
    args_fix: str
    args_has_enable: bool
    args_reset: str
    call_action_read: bool
    call_base: str
    call_cmd: str
    call_unknown_is_write: bool


def is_mutating(inp: IsMutatingInput) -> bool:
    """Checks every input against the contract, then decides."""
    _check("args.abortOnFail", inp.args_abort_on_fail, "enum", values=("absent", "off",
            "on",))
    _check("args.action", inp.args_action, "string")
    _check("args.clear", inp.args_clear, "enum", values=("absent", "off", "on",))
    _check("args.fix", inp.args_fix, "enum", values=("absent", "off", "on",))
    _check("args.hasEnable", inp.args_has_enable, "bool")
    _check("args.reset", inp.args_reset, "enum", values=("absent", "off", "on",))
    _check("call.actionRead", inp.call_action_read, "bool")
    _check("call.base", inp.call_base, "enum", values=("read", "write", "unknown",))
    _check("call.cmd", inp.call_cmd, "string")
    _check("call.unknownIsWrite", inp.call_unknown_is_write, "bool")
    return _decide_is_mutating(inp)


__all__ = ["LogicInputError", "IsMutatingInput", "is_mutating"]


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


def _decide_is_mutating(s: IsMutatingInput) -> bool:
    # rule: readBase (priority 10)
    if s.call_base == "read":
        return False
    # rule: unknownWrites (priority 11)
    if (s.call_base == "unknown") and (s.call_unknown_is_write is True):
        return True
    # rule: unknownReads (priority 12)
    if s.call_base == "unknown":
        return False
    # rule: doctorIdle (priority 20)
    if (s.call_cmd == "doctor") and (s.args_fix != "on"):
        return False
    # rule: metricsKept (priority 21)
    if (s.call_cmd == "get_metrics") and (s.args_reset != "on"):
        return False
    # rule: waitPolls (priority 22)
    if (s.call_cmd == "wait_until") and (s.args_abort_on_fail != "on"):
        return False
    # rule: changesKept (priority 23)
    if (s.call_cmd == "get_changes") and (s.args_clear == "off"):
        return False
    # rule: modeQuery (priority 24)
    if ((s.call_cmd == "editor") and (s.args_action == "mutation_mode") and (
            s.args_has_enable is not True)):
        return False
    return s.call_action_read is not True
