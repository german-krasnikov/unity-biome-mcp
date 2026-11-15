# Generated from dsl/decisions/consolehint.logic
# source-sha256=fd38519d9dd97f1d0ff7b49926705fb706a503eac0bb2b4b0076e7fe88004160
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class ConsoleHintValues:
    FORMAT_ERROR = "format_error"
    MISSING_COMPONENT = "missing_component"
    NONE = "none"
    NULL_REF = "null_ref"


@dataclass(frozen=True, slots=True)
class ConsoleHintInput:
    con_has_format_error: bool
    con_has_missing_component: bool
    con_has_null_ref: bool


def console_hint(inp: ConsoleHintInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("con.hasFormatError", inp.con_has_format_error, "bool")
    _check("con.hasMissingComponent", inp.con_has_missing_component, "bool")
    _check("con.hasNullRef", inp.con_has_null_ref, "bool")
    return _decide_console_hint(inp)


__all__ = ["LogicInputError", "ConsoleHintValues", "ConsoleHintInput", "console_hint"]


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


def _decide_console_hint(s: ConsoleHintInput) -> str:
    # rule: nullRef (priority 10)
    if s.con_has_null_ref is True:
        return "null_ref"
    # rule: missingComp (priority 20)
    if s.con_has_missing_component is True:
        return "missing_component"
    # rule: formatErr (priority 30)
    if s.con_has_format_error is True:
        return "format_error"
    return "none"
