# Generated from dsl/decisions/writededup.logic
# source-sha256=012b2368355607017528f5a84ea809e8dec5d4d423526535e099641e42f7a672
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class WriteDedupWarnValues:
    OK = "ok"
    WARN = "warn"


@dataclass(frozen=True, slots=True)
class WriteDedupWarnInput:
    dedup_is_screenshot: bool
    dedup_is_write: bool
    dedup_same_generation: bool
    dedup_within_ttl: bool


def write_dedup_warn(inp: WriteDedupWarnInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("dedup.isScreenshot", inp.dedup_is_screenshot, "bool")
    _check("dedup.isWrite", inp.dedup_is_write, "bool")
    _check("dedup.sameGeneration", inp.dedup_same_generation, "bool")
    _check("dedup.withinTtl", inp.dedup_within_ttl, "bool")
    return _decide_write_dedup_warn(inp)


__all__ = [
    "LogicInputError",
    "WriteDedupWarnValues",
    "WriteDedupWarnInput",
    "write_dedup_warn",
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


def _decide_write_dedup_warn(s: WriteDedupWarnInput) -> str:
    # rule: notWrite (priority 10)
    if s.dedup_is_write is not True:
        return "ok"
    # rule: screenshot (priority 11)
    if s.dedup_is_screenshot is True:
        return "ok"
    # rule: ttlExpired (priority 20)
    if s.dedup_within_ttl is not True:
        return "ok"
    # rule: newGeneration (priority 21)
    if s.dedup_same_generation is not True:
        return "ok"
    # rule: duplicate (priority 30)
    if ((s.dedup_is_write is True) and (s.dedup_is_screenshot is not True) and (
            s.dedup_within_ttl is True) and (s.dedup_same_generation is True)):
        return "warn"
    return "ok"
