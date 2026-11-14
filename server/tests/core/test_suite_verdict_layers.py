"""Tests for suite verdict layer separation (MCP-SUITE-006).

Separates inner (per-file assertion) from outer (lifecycle/transport) verdicts
so cleanup failures don't mask passing test results.
"""
import pytest
from unity_mcp.suite_verdict import (
    FileVerdict,
    AggregateResult,
    SuiteVerdict,
    compute_suite_verdict,
    format_layered_verdict,
)


def _raw_pass(n=3):
    return f"PLAYTEST: {n}/{n} (1.0s) OK"


def _raw_fail(passed=1, total=3):
    return f"PLAYTEST: {passed}/{total} (1.0s)\n[2] ASSERT x==1 — FAIL"


def _raw_zero():
    return "PLAYTEST: 0/0 ERROR: some error"


def _raw_cleanup_error():
    return "PLAYTEST: 0/1 ERROR: cleanup failed: RuntimeError: stop failed"


# ── tests ─────────────────────────────────────────────────────────────────────

def test_suite_result_separates_inner_outer_verdicts():
    """SuiteVerdict has inner_verdicts, outer_verdict, and aggregate fields."""
    results = [
        ("Playtests/a.playtest", _raw_pass(3), 1.0, True),
        ("<suite cleanup>", _raw_cleanup_error(), 0.0, False),
    ]
    verdict = compute_suite_verdict(results)
    assert isinstance(verdict, SuiteVerdict)
    assert hasattr(verdict, "inner_verdicts")
    assert hasattr(verdict, "outer_verdict")
    assert hasattr(verdict, "aggregate")


def test_inner_pass_outer_fail_not_flat_fail():
    """Cleanup fail with passing tests: formatted as INNER_PASS + OUTER_FAIL, not flat FAIL."""
    results = [
        ("Playtests/a.playtest", _raw_pass(3), 1.0, True),
        ("Playtests/b.playtest", _raw_pass(2), 0.5, True),
        ("<suite cleanup>", _raw_cleanup_error(), 0.0, False),
    ]
    verdict = compute_suite_verdict(results)

    assert verdict.inner_verdicts[0].state == "pass"
    assert verdict.inner_verdicts[1].state == "pass"
    assert verdict.outer_verdict == "fail"

    formatted = format_layered_verdict(verdict)
    assert "INNER_PASS" in formatted
    assert "OUTER_FAIL" in formatted


def test_aggregate_sums_children():
    """Aggregate = sum of children's pass/expected; outer rows excluded."""
    results = [
        ("Playtests/a.playtest", _raw_pass(3), 1.0, True),
        ("Playtests/b.playtest", "PLAYTEST: 2/2 (0.5s) OK", 0.5, True),
        ("<suite cleanup>", _raw_cleanup_error(), 0.0, False),
    ]
    verdict = compute_suite_verdict(results)
    assert verdict.aggregate.total_passed == 5
    assert verdict.aggregate.total_expected == 5


def test_zero_zero_result_classified_as_no_verdict():
    """Children all returning 0/0 → NO_VERDICT in formatted output."""
    results = [
        ("Playtests/a.playtest", _raw_zero(), 0.1, False),
        ("Playtests/b.playtest", _raw_zero(), 0.1, False),
    ]
    verdict = compute_suite_verdict(results)

    for fv in verdict.inner_verdicts:
        assert fv.state == "no_verdict", f"Expected no_verdict but got {fv.state!r}"

    formatted = format_layered_verdict(verdict)
    assert "NO_VERDICT" in formatted


def test_inner_verdicts_per_file():
    """Each file in suite gets its own FileVerdict with correct state and counts."""
    results = [
        ("Playtests/a.playtest", _raw_pass(3), 1.0, True),
        ("Playtests/b.playtest", _raw_fail(1, 3), 2.0, False),
        ("Playtests/c.playtest", _raw_zero(), 0.1, False),
    ]
    verdict = compute_suite_verdict(results)

    assert len(verdict.inner_verdicts) == 3

    a = verdict.inner_verdicts[0]
    assert a.filename == "Playtests/a.playtest"
    assert a.state == "pass"
    assert a.passed == 3
    assert a.expected == 3

    b = verdict.inner_verdicts[1]
    assert b.filename == "Playtests/b.playtest"
    assert b.state == "fail"
    assert b.passed == 1
    assert b.expected == 3

    c = verdict.inner_verdicts[2]
    assert c.filename == "Playtests/c.playtest"
    assert c.state == "no_verdict"
    assert c.passed == 0
    assert c.expected == 0
