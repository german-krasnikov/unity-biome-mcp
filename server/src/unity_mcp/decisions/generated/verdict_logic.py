# Generated from dsl/decisions/verdict.logic
# source-sha256=19b8bf67b026382459d636d81e58f5c321b78cee4ea78b8caf3e5988469b967a
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


@dataclass(frozen=True, slots=True)
class PassesInput:
    ledger_failed: float
    ledger_has_failed: bool
    ledger_steps: float
    ledger_steps_not_ok: float
    ledger_teardown_ok: bool
    report_blank: bool
    report_format: str
    report_plane: str
    text_has_aborted: bool
    text_has_blocked: bool
    text_has_console_err: bool
    text_has_error: bool
    text_has_fail: bool
    text_has_timeout: bool
    text_header: str
    text_passed: float
    text_total: float


def passes(inp: PassesInput) -> bool:
    """Checks every input against the contract, then decides."""
    _check("ledger.failed", inp.ledger_failed, "number", lo=None, hi=None, integer=True)
    _check("ledger.hasFailed", inp.ledger_has_failed, "bool")
    _check("ledger.steps", inp.ledger_steps, "number", lo=0, hi=1000000, integer=True)
    _check("ledger.stepsNotOk", inp.ledger_steps_not_ok, "number", lo=0, hi=1000000,
            integer=True)
    _check("ledger.teardownOk", inp.ledger_teardown_ok, "bool")
    _check("report.blank", inp.report_blank, "bool")
    _check("report.format", inp.report_format, "enum", values=("json", "text",))
    _check("report.plane", inp.report_plane, "enum", values=("server", "editor",))
    _check("text.hasAborted", inp.text_has_aborted, "bool")
    _check("text.hasBlocked", inp.text_has_blocked, "bool")
    _check("text.hasConsoleErr", inp.text_has_console_err, "bool")
    _check("text.hasError", inp.text_has_error, "bool")
    _check("text.hasFail", inp.text_has_fail, "bool")
    _check("text.hasTimeout", inp.text_has_timeout, "bool")
    _check("text.header", inp.text_header, "enum", values=("none", "first", "later",))
    _check("text.passed", inp.text_passed, "number", lo=0, hi=1000000, integer=True)
    _check("text.total", inp.text_total, "number", lo=0, hi=1000000, integer=True)
    return _decide_passes(inp)


class OutcomeValues:
    ERROR = "error"
    FAIL = "fail"
    PASS = "pass"


@dataclass(frozen=True, slots=True)
class OutcomeInput:
    ledger_failed: float
    ledger_failed_matches_steps: bool
    ledger_has_failed: bool
    ledger_readable: bool
    ledger_steps: float
    ledger_steps_not_ok: float
    ledger_teardown_ok: bool
    report_blank: bool
    report_format: str
    report_plane: str
    report_zero_of_zero: bool
    text_has_aborted: bool
    text_has_blocked: bool
    text_has_console_err: bool
    text_has_error: bool
    text_has_fail: bool
    text_has_timeout: bool
    text_header: str
    text_passed: float
    text_total: float


def outcome(inp: OutcomeInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("ledger.failed", inp.ledger_failed, "number", lo=None, hi=None, integer=True)
    _check("ledger.failedMatchesSteps", inp.ledger_failed_matches_steps, "bool")
    _check("ledger.hasFailed", inp.ledger_has_failed, "bool")
    _check("ledger.readable", inp.ledger_readable, "bool")
    _check("ledger.steps", inp.ledger_steps, "number", lo=0, hi=1000000, integer=True)
    _check("ledger.stepsNotOk", inp.ledger_steps_not_ok, "number", lo=0, hi=1000000,
            integer=True)
    _check("ledger.teardownOk", inp.ledger_teardown_ok, "bool")
    _check("report.blank", inp.report_blank, "bool")
    _check("report.format", inp.report_format, "enum", values=("json", "text",))
    _check("report.plane", inp.report_plane, "enum", values=("server", "editor",))
    _check("report.zeroOfZero", inp.report_zero_of_zero, "bool")
    _check("text.hasAborted", inp.text_has_aborted, "bool")
    _check("text.hasBlocked", inp.text_has_blocked, "bool")
    _check("text.hasConsoleErr", inp.text_has_console_err, "bool")
    _check("text.hasError", inp.text_has_error, "bool")
    _check("text.hasFail", inp.text_has_fail, "bool")
    _check("text.hasTimeout", inp.text_has_timeout, "bool")
    _check("text.header", inp.text_header, "enum", values=("none", "first", "later",))
    _check("text.passed", inp.text_passed, "number", lo=0, hi=1000000, integer=True)
    _check("text.total", inp.text_total, "number", lo=0, hi=1000000, integer=True)
    return _decide_outcome(inp)


class FileStateValues:
    ERROR = "error"
    FAIL = "fail"
    NO_VERDICT = "no_verdict"
    PASS = "pass"


@dataclass(frozen=True, slots=True)
class FileStateInput:
    file_error_text: bool
    file_expected: float
    file_passed: float


def file_state(inp: FileStateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("file.errorText", inp.file_error_text, "bool")
    _check("file.expected", inp.file_expected, "number", lo=0, hi=1000000, integer=True)
    _check("file.passed", inp.file_passed, "number", lo=0, hi=1000000, integer=True)
    return _decide_file_state(inp)


class SuiteInnerValues:
    INNER_FAIL = "INNER_FAIL"
    INNER_PASS = "INNER_PASS"
    NO_VERDICT = "NO_VERDICT"


@dataclass(frozen=True, slots=True)
class SuiteInnerInput:
    suite_all_no_verdict: bool
    suite_any_error: bool
    suite_any_fail: bool


def suite_inner(inp: SuiteInnerInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("suite.allNoVerdict", inp.suite_all_no_verdict, "bool")
    _check("suite.anyError", inp.suite_any_error, "bool")
    _check("suite.anyFail", inp.suite_any_fail, "bool")
    return _decide_suite_inner(inp)


class SuiteOuterValues:
    OUTER_FAIL = "OUTER_FAIL"
    OUTER_PASS = "OUTER_PASS"


@dataclass(frozen=True, slots=True)
class SuiteOuterInput:
    suite_outer_failed: bool


def suite_outer(inp: SuiteOuterInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("suite.outerFailed", inp.suite_outer_failed, "bool")
    return _decide_suite_outer(inp)


__all__ = [
    "LogicInputError",
    "PassesInput",
    "passes",
    "OutcomeValues",
    "OutcomeInput",
    "outcome",
    "FileStateValues",
    "FileStateInput",
    "file_state",
    "SuiteInnerValues",
    "SuiteInnerInput",
    "suite_inner",
    "SuiteOuterValues",
    "SuiteOuterInput",
    "suite_outer",
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


def _decide_passes(s: PassesInput | OutcomeInput) -> bool:
    if s.report_blank is True:
        return False
    if (s.report_format == "text") and (s.text_has_aborted is True):
        return False
    if (s.report_format == "text") and (s.text_header == "none"):
        return False
    if (s.report_format == "text") and (s.text_total == 0):
        return False
    if (s.report_format == "text") and (s.text_passed != s.text_total):
        return False
    if (((s.report_format == "text") and (s.report_plane == "server")) and (((((
            s.text_has_fail is True) or (s.text_has_error is True)) or (
            s.text_has_console_err is True)) or (s.text_has_blocked is True)) or (
            s.text_has_timeout is True))):
        return False
    if s.report_format == "text":
        return True
    if s.ledger_teardown_ok is not True:
        return False
    if (s.ledger_has_failed is not True) or (s.ledger_failed != 0):
        return False
    if s.ledger_steps == 0:
        return False
    if s.ledger_steps_not_ok > 0:
        return False
    return s.report_format == "json"


def _decide_outcome(s: OutcomeInput) -> str:
    if _decide_passes(s) is True:
        return "pass"
    if (s.report_blank is True) or (s.report_zero_of_zero is True):
        return "error"
    if (s.report_format == "json") and (s.ledger_readable is not True):
        return "error"
    if (s.report_format == "json") and (s.ledger_steps == 0):
        return "error"
    if (s.report_format == "json") and (s.ledger_failed_matches_steps is not True):
        return "error"
    return "fail"


def _decide_file_state(s: FileStateInput) -> str:
    if s.file_expected == 0:
        return "no_verdict"
    if s.file_passed == s.file_expected:
        return "pass"
    if s.file_error_text is True:
        return "error"
    return "fail"


def _decide_suite_inner(s: SuiteInnerInput) -> str:
    if s.suite_all_no_verdict is True:
        return "NO_VERDICT"
    if (s.suite_any_fail is True) or (s.suite_any_error is True):
        return "INNER_FAIL"
    return "INNER_PASS"


def _decide_suite_outer(s: SuiteOuterInput) -> str:
    if s.suite_outer_failed is True:
        return "OUTER_FAIL"
    return "OUTER_PASS"
