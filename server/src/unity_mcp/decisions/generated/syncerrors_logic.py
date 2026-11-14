# Generated from dsl/decisions/syncerrors.logic
# source-sha256=379ada9c086b49b6f99aeb4752301f6042dde085862d568dde72f4868c23f30a
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class SyncErrorsValues:
    CLEAN = "clean"
    ERRORS = "errors"
    FAIL_STALE_DLL = "failStaleDll"
    FAILED_NO_DETAILS = "failedNoDetails"
    UNKNOWN = "unknown"
    UNKNOWN_STATE = "unknownState"
    VERDICT = "verdict"


@dataclass(frozen=True, slots=True)
class SyncErrorsInput:
    sync_diagnostic_issue: bool
    sync_has_cs_errors: bool
    sync_has_errors: bool
    sync_has_stale_dlls: bool
    sync_is_warn_only: bool
    sync_state: str


def sync_errors(inp: SyncErrorsInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("sync.diagnosticIssue", inp.sync_diagnostic_issue, "bool")
    _check("sync.hasCsErrors", inp.sync_has_cs_errors, "bool")
    _check("sync.hasErrors", inp.sync_has_errors, "bool")
    _check("sync.hasStaleDlls", inp.sync_has_stale_dlls, "bool")
    _check("sync.isWarnOnly", inp.sync_is_warn_only, "bool")
    _check("sync.state", inp.sync_state, "enum", values=("idle", "idleFailed",
            "other",))
    return _decide_sync_errors(inp)


__all__ = ["LogicInputError", "SyncErrorsValues", "SyncErrorsInput", "sync_errors"]


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


def _decide_sync_errors(s: SyncErrorsInput) -> str:
    # rule: idleFailedErrors (priority 10)
    if (s.sync_state == "idleFailed") and (s.sync_has_cs_errors is True):
        return "errors"
    # rule: idleFailedNoErrors (priority 11)
    if s.sync_state == "idleFailed":
        return "failedNoDetails"
    # rule: warnOnly (priority 20)
    if (s.sync_has_errors is True) and (s.sync_is_warn_only is True):
        return "unknown"
    # rule: hasErrors (priority 21)
    if s.sync_has_errors is True:
        return "errors"
    # rule: notIdle (priority 30)
    if s.sync_state != "idle":
        return "unknownState"
    # rule: diagnosticIssue (priority 40)
    if s.sync_diagnostic_issue is True:
        return "verdict"
    # rule: staleDlls (priority 50)
    if s.sync_has_stale_dlls is True:
        return "failStaleDll"
    # rule: idle (priority 60)
    if s.sync_state == "idle":
        return "clean"
    return "unknown"
