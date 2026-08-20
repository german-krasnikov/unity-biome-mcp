"""Suite verdict layer separation (MCP-SUITE-006).

Separates inner (per-file assertion) verdicts from outer (lifecycle/transport)
verdicts so cleanup failures don't mask passing test results.
"""
import re
from dataclasses import dataclass


@dataclass
class FileVerdict:
    filename: str
    passed: int
    expected: int
    state: str  # "pass" | "fail" | "error" | "no_verdict"


@dataclass
class AggregateResult:
    total_passed: int
    total_expected: int


@dataclass
class SuiteVerdict:
    inner_verdicts: list[FileVerdict]
    outer_verdict: str  # "pass" | "fail"
    aggregate: AggregateResult


_COUNT_RE = re.compile(r"PLAYTEST:\s*(\d+)\s*/\s*(\d+)")


def _is_outer_row(filepath: str) -> bool:
    """Outer rows are lifecycle markers like <suite cleanup> / <suite startup>."""
    return filepath.startswith("<suite ")


def _parse_counts(raw: str) -> tuple[int, int]:
    m = _COUNT_RE.search(raw)
    if not m:
        return 0, 0
    return int(m.group(1)), int(m.group(2))


def _classify_state(passed: int, expected: int, raw: str) -> str:
    if expected == 0:
        return "no_verdict"
    if passed == expected:
        return "pass"
    if "ERROR" in raw:
        return "error"
    return "fail"


def compute_suite_verdict(results: list) -> SuiteVerdict:
    """Build layered SuiteVerdict from (filepath, raw, elapsed, ok) results."""
    inner: list[FileVerdict] = []
    outer_failed = False

    for filepath, raw, _elapsed, ok in results:
        if _is_outer_row(filepath):
            if not ok:
                outer_failed = True
            continue
        passed, expected = _parse_counts(raw)
        state = _classify_state(passed, expected, raw)
        inner.append(FileVerdict(
            filename=filepath,
            passed=passed,
            expected=expected,
            state=state,
        ))

    aggregate = AggregateResult(
        total_passed=sum(fv.passed for fv in inner),
        total_expected=sum(fv.expected for fv in inner),
    )
    return SuiteVerdict(
        inner_verdicts=inner,
        outer_verdict="fail" if outer_failed else "pass",
        aggregate=aggregate,
    )


def format_layered_verdict(verdict: SuiteVerdict) -> str:
    """Format: SUITE_RESULT|INNER_STATE|OUTER_STATE|aggregate:X/Y."""
    agg = verdict.aggregate
    inner_states = {fv.state for fv in verdict.inner_verdicts}

    if not inner_states or inner_states == {"no_verdict"}:
        inner_label = "NO_VERDICT"
    elif "fail" in inner_states or "error" in inner_states:
        inner_label = "INNER_FAIL"
    else:
        inner_label = "INNER_PASS"

    outer_label = "OUTER_FAIL" if verdict.outer_verdict == "fail" else "OUTER_PASS"
    return f"SUITE_RESULT|{inner_label}|{outer_label}|aggregate:{agg.total_passed}/{agg.total_expected}"
