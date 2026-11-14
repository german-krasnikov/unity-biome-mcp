# Generated from dsl/decisions/starvation.logic
# source-sha256=359d3620cf607cc5a41b0e5b3d70e2373ea867103b15a2ca01f21056985a73e6
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class StarvationWarnValues:
    OK = "ok"
    WARN = "warn"


@dataclass(frozen=True, slots=True)
class StarvationWarnInput:
    starv_all_same: bool
    starv_window_full: bool


def starvation_warn(inp: StarvationWarnInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("starv.allSame", inp.starv_all_same, "bool")
    _check("starv.windowFull", inp.starv_window_full, "bool")
    return _decide_starvation_warn(inp)


__all__ = [
    "LogicInputError",
    "StarvationWarnValues",
    "StarvationWarnInput",
    "starvation_warn",
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


def _decide_starvation_warn(s: StarvationWarnInput) -> str:
    # rule: warn (priority 10)
    if (s.starv_window_full is True) and (s.starv_all_same is True):
        return "warn"
    return "ok"
