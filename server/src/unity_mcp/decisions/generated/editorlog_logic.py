# Generated from dsl/decisions/editorlog.logic
# source-sha256=f12d9bccad489f6cfd638feb87fb6daca90d4c08613f819ebbc3db5c46867b68
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class FailureCurrencyValues:
    CURRENT = "current"
    STALE = "stale"


@dataclass(frozen=True, slots=True)
class FailureCurrencyInput:
    log_failure_offset: float
    log_success_offset: float


def failure_currency(inp: FailureCurrencyInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("log.failureOffset", inp.log_failure_offset, "number", lo=-1, hi=2000000000,
            integer=True)
    _check("log.successOffset", inp.log_success_offset, "number", lo=-1, hi=2000000000,
            integer=True)
    return _decide_failure_currency(inp)


class LogPickValues:
    PREV = "prev"
    PRIMARY = "primary"


@dataclass(frozen=True, slots=True)
class LogPickInput:
    log_has_prev: bool
    log_has_primary: bool
    log_prev_currency: str
    log_prev_newer: bool
    log_primary_currency: str


def log_pick(inp: LogPickInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("log.hasPrev", inp.log_has_prev, "bool")
    _check("log.hasPrimary", inp.log_has_primary, "bool")
    _check("log.prevCurrency", inp.log_prev_currency, "enum", values=("current",
            "stale",))
    _check("log.prevNewer", inp.log_prev_newer, "bool")
    _check("log.primaryCurrency", inp.log_primary_currency, "enum", values=("current",
            "stale",))
    return _decide_log_pick(inp)


class WedgeKindValues:
    BUILD_FAILED_WEDGE = "buildFailedWedge"
    CLEAN = "clean"
    STALE_CACHE = "staleCache"


@dataclass(frozen=True, slots=True)
class WedgeKindInput:
    log_currency: str
    wedge_cs_errors: float
    wedge_still_missing: float
    wedge_unparsable: float


def wedge_kind(inp: WedgeKindInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("log.currency", inp.log_currency, "enum", values=("current", "stale",))
    _check("wedge.csErrors", inp.wedge_cs_errors, "number", lo=0, hi=1000000,
            integer=True)
    _check("wedge.stillMissing", inp.wedge_still_missing, "number", lo=0, hi=1000000,
            integer=True)
    _check("wedge.unparsable", inp.wedge_unparsable, "number", lo=0, hi=1000000,
            integer=True)
    return _decide_wedge_kind(inp)


class ErrorOnDiskValues:
    MATCHES = "matches"
    STALE_ON_DISK = "staleOnDisk"


@dataclass(frozen=True, slots=True)
class ErrorOnDiskInput:
    err_complete: bool
    err_file_readable: bool
    err_member_in_code: bool
    err_type_found: bool


def error_on_disk(inp: ErrorOnDiskInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("err.complete", inp.err_complete, "bool")
    _check("err.fileReadable", inp.err_file_readable, "bool")
    _check("err.memberInCode", inp.err_member_in_code, "bool")
    _check("err.typeFound", inp.err_type_found, "bool")
    return _decide_error_on_disk(inp)


__all__ = [
    "LogicInputError",
    "FailureCurrencyValues",
    "FailureCurrencyInput",
    "failure_currency",
    "LogPickValues",
    "LogPickInput",
    "log_pick",
    "WedgeKindValues",
    "WedgeKindInput",
    "wedge_kind",
    "ErrorOnDiskValues",
    "ErrorOnDiskInput",
    "error_on_disk",
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


def _decide_failure_currency(s: FailureCurrencyInput) -> str:
    # rule: noFailure (priority 10)
    if s.log_failure_offset < 0:
        return "stale"
    # rule: noSuccess (priority 20)
    if s.log_success_offset < 0:
        return "current"
    # rule: failureLast (priority 30)
    if s.log_failure_offset > s.log_success_offset:
        return "current"
    return "stale"


def _decide_log_pick(s: LogPickInput) -> str:
    # rule: onlyPrev (priority 10)
    if (s.log_has_primary is not True) and (s.log_has_prev is True):
        return "prev"
    # rule: onlyPrimary (priority 11)
    if s.log_has_prev is not True:
        return "primary"
    # rule: primaryCurrent (priority 20)
    if s.log_primary_currency == "current":
        return "primary"
    # rule: prevCurrent (priority 21)
    if s.log_prev_currency == "current":
        return "prev"
    # rule: prevNewer (priority 30)
    if s.log_prev_newer is True:
        return "prev"
    return "primary"


def _decide_wedge_kind(s: WedgeKindInput) -> str:
    # rule: clean (priority 10)
    if s.log_currency == "stale":
        return "clean"
    # rule: noErrors (priority 20)
    if s.wedge_cs_errors == 0:
        return "buildFailedWedge"
    # rule: unparsable (priority 30)
    if s.wedge_unparsable > 0:
        return "buildFailedWedge"
    # rule: stillMissing (priority 40)
    if s.wedge_still_missing > 0:
        return "buildFailedWedge"
    return "staleCache"


def _decide_error_on_disk(s: ErrorOnDiskInput) -> str:
    # rule: fixedOnDisk (priority 10)
    if ((s.err_complete is True) and (s.err_file_readable is True) and (s.err_type_found
            is True) and (s.err_member_in_code is True)):
        return "staleOnDisk"
    return "matches"
