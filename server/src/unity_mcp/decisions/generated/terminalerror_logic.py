# Generated from dsl/decisions/terminalerror.logic
# source-sha256=ef62e765ac3cd5f0b940d98715cea575d4c0ab3064ee5026e31b87e4a87952a3
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class TerminalErrorValues:
    BUILD_INCOHERENT = "buildIncoherent"
    CLEANUP_BOUNDARY_MISSING = "cleanupBoundaryMissing"
    EXECUTION_BOUNDARY_MISSING = "executionBoundaryMissing"
    EXPECTED_COUNT_INVALID = "expectedCountInvalid"
    HEALTH_NO_PROGRESS = "healthNoProgress"
    HEALTH_UNRESPONSIVE = "healthUnresponsive"
    INFRASTRUCTURE_ERRORS = "infrastructureErrors"
    INVALID_TERMINAL_OUTCOME = "invalidTerminalOutcome"
    ISSUES_EVIDENCE_INVALID = "issuesEvidenceInvalid"
    LIFECYCLE_MISMATCH = "lifecycleMismatch"
    MANIFEST_BOUNDARY_MISSING = "manifestBoundaryMissing"
    OK = "ok"
    REQUEST_INTENT_MISMATCH = "requestIntentMismatch"
    RUN_FINISH_BOUNDARY_MISSING = "runFinishBoundaryMissing"
    RUN_START_BOUNDARY_MISSING = "runStartBoundaryMissing"
    RUN_ZERO_MATCH_FILTER = "runZeroMatchFilter"
    RUN_ZERO_MATCH_METACHAR = "runZeroMatchMetachar"
    TERMINAL_COUNT_INVARIANT = "terminalCountInvariant"
    TERMINAL_FLAG_MISSING = "terminalFlagMissing"
    TERMINAL_STATUS_COUNT_INVALID = "terminalStatusCountInvalid"
    TERMINAL_STATUS_TOTAL_MISMATCH = "terminalStatusTotalMismatch"
    UTF_GUID_MISSING = "utfGuidMissing"
    UTF_XML_SCOPE_MISSING = "utfXmlScopeMissing"


@dataclass(frozen=True, slots=True)
class TerminalErrorInput:
    terminal_build_coherent: bool
    terminal_cleanup_complete: bool
    terminal_execution_finished: bool
    terminal_expected_count_case: str
    terminal_expected_count_value: float
    terminal_filter_has_metachar: bool
    terminal_has_expected: bool
    terminal_has_infrastructure_error: bool
    terminal_health_no_progress: bool
    terminal_health_unresponsive: bool
    terminal_intent_matches: bool
    terminal_is_terminal_flag: bool
    terminal_issues_valid: bool
    terminal_lifecycle_ok: bool
    terminal_manifest_complete: bool
    terminal_outcome_valid: bool
    terminal_run_finished_observed: bool
    terminal_run_started_observed: bool
    terminal_status_counts_valid: bool
    terminal_status_total: float
    terminal_utf_guid_present: bool
    terminal_utf_xml_scope_valid: bool


def terminal_error(inp: TerminalErrorInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("terminal.buildCoherent", inp.terminal_build_coherent, "bool")
    _check("terminal.cleanupComplete", inp.terminal_cleanup_complete, "bool")
    _check("terminal.executionFinished", inp.terminal_execution_finished, "bool")
    _check("terminal.expectedCountCase", inp.terminal_expected_count_case, "enum",
            values=("ok", "invalidType", "zero", "negative", "invariantBroken",))
    _check("terminal.expectedCountValue", inp.terminal_expected_count_value, "number",
            lo=0, hi=2000000000, integer=True)
    _check("terminal.filterHasMetachar", inp.terminal_filter_has_metachar, "bool")
    _check("terminal.hasExpected", inp.terminal_has_expected, "bool")
    _check("terminal.hasInfrastructureError", inp.terminal_has_infrastructure_error,
            "bool")
    _check("terminal.healthNoProgress", inp.terminal_health_no_progress, "bool")
    _check("terminal.healthUnresponsive", inp.terminal_health_unresponsive, "bool")
    _check("terminal.intentMatches", inp.terminal_intent_matches, "bool")
    _check("terminal.isTerminalFlag", inp.terminal_is_terminal_flag, "bool")
    _check("terminal.issuesValid", inp.terminal_issues_valid, "bool")
    _check("terminal.lifecycleOk", inp.terminal_lifecycle_ok, "bool")
    _check("terminal.manifestComplete", inp.terminal_manifest_complete, "bool")
    _check("terminal.outcomeValid", inp.terminal_outcome_valid, "bool")
    _check("terminal.runFinishedObserved", inp.terminal_run_finished_observed, "bool")
    _check("terminal.runStartedObserved", inp.terminal_run_started_observed, "bool")
    _check("terminal.statusCountsValid", inp.terminal_status_counts_valid, "bool")
    _check("terminal.statusTotal", inp.terminal_status_total, "number", lo=0,
            hi=2000000000, integer=True)
    _check("terminal.utfGuidPresent", inp.terminal_utf_guid_present, "bool")
    _check("terminal.utfXmlScopeValid", inp.terminal_utf_xml_scope_valid, "bool")
    return _decide_terminal_error(inp)


__all__ = [
    "LogicInputError",
    "TerminalErrorValues",
    "TerminalErrorInput",
    "terminal_error",
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


def _decide_terminal_error(s: TerminalErrorInput) -> str:
    # rule: lifecycleMismatch (priority 10)
    if s.terminal_lifecycle_ok is not True:
        return "lifecycleMismatch"
    # rule: intentMismatch (priority 20)
    if s.terminal_intent_matches is not True:
        return "requestIntentMismatch"
    # rule: healthNoProgress (priority 30)
    if s.terminal_health_no_progress is True:
        return "healthNoProgress"
    # rule: healthUnresponsive (priority 31)
    if s.terminal_health_unresponsive is True:
        return "healthUnresponsive"
    # rule: terminalFlagMissing (priority 40)
    if s.terminal_is_terminal_flag is not True:
        return "terminalFlagMissing"
    # rule: executionBoundaryMissing (priority 41)
    if s.terminal_execution_finished is not True:
        return "executionBoundaryMissing"
    # rule: cleanupBoundaryMissing (priority 42)
    if s.terminal_cleanup_complete is not True:
        return "cleanupBoundaryMissing"
    # rule: runStartBoundaryMissing (priority 43)
    if s.terminal_run_started_observed is not True:
        return "runStartBoundaryMissing"
    # rule: manifestBoundaryMissing (priority 44)
    if s.terminal_manifest_complete is not True:
        return "manifestBoundaryMissing"
    # rule: runFinishBoundaryMissing (priority 45)
    if s.terminal_run_finished_observed is not True:
        return "runFinishBoundaryMissing"
    # rule: buildIncoherent (priority 46)
    if s.terminal_build_coherent is not True:
        return "buildIncoherent"
    # rule: invalidOutcome (priority 50)
    if s.terminal_outcome_valid is not True:
        return "invalidTerminalOutcome"
    # rule: utfGuidMissing (priority 60)
    if s.terminal_utf_guid_present is not True:
        return "utfGuidMissing"
    # rule: utfXmlScopeMissing (priority 70)
    if s.terminal_utf_xml_scope_valid is not True:
        return "utfXmlScopeMissing"
    # rule: expectedInvalidType (priority 80)
    if ((s.terminal_has_expected is True) and (s.terminal_expected_count_case ==
            "invalidType")):
        return "expectedCountInvalid"
    # rule: zeroMatchMetachar (priority 81)
    if ((s.terminal_has_expected is True) and (s.terminal_expected_count_case == "zero")
            and (s.terminal_filter_has_metachar is True)):
        return "runZeroMatchMetachar"
    # rule: zeroMatchFilter (priority 82)
    if (s.terminal_has_expected is True) and (s.terminal_expected_count_case == "zero"):
        return "runZeroMatchFilter"
    # rule: expectedNegative (priority 83)
    if ((s.terminal_has_expected is True) and (s.terminal_expected_count_case ==
            "negative")):
        return "expectedCountInvalid"
    # rule: countInvariantBroken (priority 84)
    if ((s.terminal_has_expected is True) and (s.terminal_expected_count_case ==
            "invariantBroken")):
        return "terminalCountInvariant"
    # rule: statusCountInvalid (priority 90)
    if s.terminal_status_counts_valid is not True:
        return "terminalStatusCountInvalid"
    # rule: statusTotalMismatch (priority 100)
    if ((s.terminal_has_expected is True) and (s.terminal_status_total !=
            s.terminal_expected_count_value)):
        return "terminalStatusTotalMismatch"
    # rule: issuesInvalid (priority 110)
    if s.terminal_issues_valid is not True:
        return "issuesEvidenceInvalid"
    # rule: infrastructureError (priority 111)
    if s.terminal_has_infrastructure_error is True:
        return "infrastructureErrors"
    return "ok"
