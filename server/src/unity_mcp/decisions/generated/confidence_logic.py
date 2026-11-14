# Generated from dsl/decisions/confidence.logic
# source-sha256=8f51141da469edc5a560582cbfcdcec6b51f4f03852fe0c573fdb381de39bd9a
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class ConfidenceBandValues:
    LOW = "low"
    OK = "ok"
    WARN = "warn"


@dataclass(frozen=True, slots=True)
class ConfidenceBandInput:
    conf_is_write: bool
    conf_updated_value: float


def confidence_band(inp: ConfidenceBandInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("conf.isWrite", inp.conf_is_write, "bool")
    _check("conf.updatedValue", inp.conf_updated_value, "number", lo=0, hi=1,
            integer=False)
    return _decide_confidence_band(inp)


class ShouldVerifyValues:
    SKIP = "skip"
    VERIFY = "verify"


@dataclass(frozen=True, slots=True)
class ShouldVerifyInput:
    conf_updated_value: float


def should_verify(inp: ShouldVerifyInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("conf.updatedValue", inp.conf_updated_value, "number", lo=0, hi=1,
            integer=False)
    return _decide_should_verify(inp)


__all__ = [
    "LogicInputError",
    "ConfidenceBandValues",
    "ConfidenceBandInput",
    "confidence_band",
    "ShouldVerifyValues",
    "ShouldVerifyInput",
    "should_verify",
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


def _decide_confidence_band(s: ConfidenceBandInput) -> str:
    # rule: okBand (priority 10)
    if s.conf_updated_value >= 0.5:
        return "ok"
    # rule: lowBand (priority 20)
    if s.conf_updated_value < 0.3:
        return "low"
    # rule: warnBand (priority 30)
    if s.conf_updated_value >= 0.3:
        return "warn"
    return "ok"


def _decide_should_verify(s: ConfidenceBandInput | ShouldVerifyInput) -> str:
    # rule: skip (priority 10)
    if s.conf_updated_value >= 0.5:
        return "skip"
    return "verify"
