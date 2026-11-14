# Generated from dsl/decisions/taint.logic
# source-sha256=d09248e0d7850920c1112ccd430b3efbd90aff8be934f67a553cf5a808e26077
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class TaintWarnValues:
    OK = "ok"
    WARN = "warn"


@dataclass(frozen=True, slots=True)
class TaintWarnInput:
    taint_is_set_property: bool
    taint_prop_ends_with_reference: bool
    taint_value_is_clean: bool
    taint_value_is_null: bool
    taint_value_is_special: bool


def taint_warn(inp: TaintWarnInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("taint.isSetProperty", inp.taint_is_set_property, "bool")
    _check("taint.propEndsWithReference", inp.taint_prop_ends_with_reference, "bool")
    _check("taint.valueIsClean", inp.taint_value_is_clean, "bool")
    _check("taint.valueIsNull", inp.taint_value_is_null, "bool")
    _check("taint.valueIsSpecial", inp.taint_value_is_special, "bool")
    return _decide_taint_warn(inp)


__all__ = ["LogicInputError", "TaintWarnValues", "TaintWarnInput", "taint_warn"]


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


def _decide_taint_warn(s: TaintWarnInput) -> str:
    # rule: notSetProperty (priority 10)
    if s.taint_is_set_property is not True:
        return "ok"
    # rule: notRefProp (priority 20)
    if s.taint_prop_ends_with_reference is not True:
        return "ok"
    # rule: nullValue (priority 30)
    if s.taint_value_is_null is True:
        return "ok"
    # rule: specialValue (priority 31)
    if s.taint_value_is_special is True:
        return "ok"
    # rule: cleanValue (priority 40)
    if s.taint_value_is_clean is True:
        return "ok"
    # rule: unreadRef (priority 50)
    if (s.taint_is_set_property is True) and (s.taint_prop_ends_with_reference is True):
        return "warn"
    return "ok"
