# Generated from dsl/decisions/reflectgate.logic
# source-sha256=7037b330488c6b63eb2cd20f108b44ad258a12575b1f035709ec2aa1c9ab4165
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class ReflectGateValues:
    RUN_REFLECT = "runReflect"
    RUN_VERIFY_SNAPSHOT = "runVerifySnapshot"
    SKIP = "skip"


@dataclass(frozen=True, slots=True)
class ReflectGateInput:
    reflect_cmd_is_set_property: bool
    reflect_cmd_is_write: bool
    reflect_env_on: bool
    reflect_has_prop_and_value: bool
    reflect_no_reflect_flag: bool
    reflect_result_degraded: bool


def reflect_gate(inp: ReflectGateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("reflect.cmdIsSetProperty", inp.reflect_cmd_is_set_property, "bool")
    _check("reflect.cmdIsWrite", inp.reflect_cmd_is_write, "bool")
    _check("reflect.envOn", inp.reflect_env_on, "bool")
    _check("reflect.hasPropAndValue", inp.reflect_has_prop_and_value, "bool")
    _check("reflect.noReflectFlag", inp.reflect_no_reflect_flag, "bool")
    _check("reflect.resultDegraded", inp.reflect_result_degraded, "bool")
    return _decide_reflect_gate(inp)


__all__ = ["LogicInputError", "ReflectGateValues", "ReflectGateInput", "reflect_gate"]


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


def _decide_reflect_gate(s: ReflectGateInput) -> str:
    # rule: verifySnapshotFallback (priority 10)
    if ((s.reflect_env_on is not True) and (s.reflect_cmd_is_set_property is True) and (
            s.reflect_has_prop_and_value is True)):
        return "runVerifySnapshot"
    # rule: skipDegraded (priority 20)
    if (s.reflect_env_on is True) and (s.reflect_result_degraded is True):
        return "skip"
    # rule: skipNoReflectFlag (priority 21)
    if (s.reflect_env_on is True) and (s.reflect_no_reflect_flag is True):
        return "skip"
    # rule: skipNotWrite (priority 22)
    if (s.reflect_env_on is True) and (s.reflect_cmd_is_write is not True):
        return "skip"
    # rule: runReflect (priority 30)
    if (s.reflect_env_on is True) and (s.reflect_cmd_is_write is True):
        return "runReflect"
    return "skip"
