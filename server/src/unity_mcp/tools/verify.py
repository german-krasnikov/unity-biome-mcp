"""Single verification gate after code/scene changes (P1.7).

Additive gates — only enabled ones run:
  1. await_compile  (always)
  2. get_compile_errors  (always)
  3. get_console_since mark_id  (if mark_id provided)
  4. run_tests_wait mode filter  (if run_tests_mode provided)
  5. run_playtest_suite paths  (if playtests provided)

Returns PASS when all enabled gates pass, FAIL at the first gate that fails.
"""
from __future__ import annotations

import contextlib
import json
import re

from . import code_intel as _ci
from . import console as _con
from . import runtime as _rt
from . import testing as _test
from ._common import bind

_DROPPED_RE = re.compile(r"\[\+\d+ older problem entries dropped\]")

_send = None
_args = None


def _is_compile_clean(result: str) -> bool:
    r = result.strip().lower()
    return r.startswith("compile clean") or "no compilation errors" in r or r in ("", "no errors")


def _is_errors_clean(result: str) -> bool:
    r = result.strip()
    return not r or "error cs" not in r.lower()


def _snapshot_count(snapshot: dict, *names: str) -> int | None:
    counts = snapshot.get("counts")
    sources = (counts, snapshot) if isinstance(counts, dict) else (snapshot,)
    for source in sources:
        for name in names:
            value = source.get(name)
            if isinstance(value, int) and not isinstance(value, bool):
                return value
    return None


def _is_snapshot_pass(snapshot: dict) -> bool:
    if not isinstance(snapshot.get("request_id"), str) or not snapshot["request_id"]:
        return False
    if not isinstance(snapshot.get("run_id"), str) or not snapshot["run_id"]:
        return False
    if not isinstance(snapshot.get("utf_guid"), str) or not snapshot["utf_guid"]:
        return False
    state = snapshot.get("state")
    lifecycle = snapshot.get("lifecycle")
    if state is not None and lifecycle is not None and state != lifecycle:
        return False
    if (state or lifecycle) != "terminal" or snapshot.get("outcome") != "passed":
        return False
    if snapshot.get("is_terminal") is not True:
        return False
    if snapshot.get("execution_finished") is not True:
        return False
    if snapshot.get("cleanup_complete") is not True:
        return False
    if snapshot.get("build_coherent") is not True:
        return False
    if snapshot.get("utf_version") != "1.6.0":
        return False
    if snapshot.get("manifest_complete") is not True:
        return False
    if snapshot.get("run_started_observed") is not True:
        return False
    if snapshot.get("run_finished_observed") is not True:
        return False

    expected = _snapshot_count(snapshot, "expected", "expected_count")
    completed = _snapshot_count(
        snapshot, "completed", "finished", "completed_expected_count"
    )
    missing = _snapshot_count(snapshot, "missing", "missing_count")
    unexpected = _snapshot_count(snapshot, "unexpected", "unexpected_count")
    conflicts = _snapshot_count(snapshot, "conflicts", "conflict", "conflict_count")
    if expected is None or completed is None or expected <= 0:
        return False
    if completed != expected:
        return False
    if (missing, unexpected, conflicts) != (0, 0, 0):
        return False

    errors = snapshot.get("errors")
    if errors not in (None, []):
        return False
    issues = snapshot.get("issues")
    return isinstance(issues, list) and not any(
        isinstance(issue, dict) and issue.get("severity") == "error"
        for issue in issues
    )


def _is_tests_pass(result: str) -> bool:
    stripped = result.strip()
    if stripped.startswith((
        "BLOCKED:", "TIMEOUT", "START-UNKNOWN", "PROTOCOL-ERROR", "tests-started",
    )):
        return False
    try:
        snapshot = json.loads(stripped)
    except (TypeError, ValueError, json.JSONDecodeError):
        return False
    return isinstance(snapshot, dict) and _is_snapshot_pass(snapshot)


def _is_suite_pass(result: str) -> bool:
    if not result.startswith("SUITE:"):
        return False
    m = re.search(r"(\d+)/(\d+)", result)
    if m:
        return m.group(1) == m.group(2)
    return "FAIL" not in result


def _extract_ratio(result: str) -> str:
    try:
        snapshot = json.loads(result)
    except (TypeError, ValueError, json.JSONDecodeError):
        snapshot = None
    if isinstance(snapshot, dict):
        expected = _snapshot_count(snapshot, "expected", "expected_count")
        completed = _snapshot_count(
            snapshot, "completed", "finished", "completed_expected_count"
        )
        if expected is not None and completed is not None:
            return f"{completed}/{expected}"
    m = re.search(r"\d+/\d+", result)
    if m:
        return m.group(0)
    m2 = re.search(r"(\d+)\s+(?:tests?\s+)?passed", result, re.IGNORECASE)
    return f"{m2.group(1)} passed" if m2 else "ok"


def _fail(gate: str, detail: str, skipped: list[str]) -> str:
    lines = [f"FAIL: {gate} gate failed", f"  {detail.strip()}"]
    if skipped:
        lines.append(f"next gates skipped: {', '.join(skipped)}")
    return "\n".join(lines)


async def _stop_play_mode() -> None:
    """Stop Play Mode before playtests — best-effort."""
    with contextlib.suppress(Exception):
        await _send("editor", _args(action="stop"), timeout=10.0)


async def verify_after_change(
    changed_files: str = "",
    test_filter: str = "",
    run_tests_mode: str = "",
    playtests: str = "",
    mark_id: str = "",
    timeout: float = 300.0,
    restart_between: bool = False,
) -> str:
    """Single verification gate after code/scene changes.
    Gates are additive — only enabled ones run:
    1. await_compile (always)
    2. get_compile_errors (always)
    3. get_console_since mark_id (if mark_id provided)
    4. run_tests_wait mode filter (if run_tests_mode provided)
    5. run_playtest_suite paths (if playtests provided)
    Returns PASS only when ALL enabled gates pass.
    Failure includes which gate failed and recommended next command."""
    passed: list[str] = []

    # Determine which optional gates are enabled (for skip reporting)
    optional_gates: list[str] = []
    if mark_id:
        optional_gates.append("console")
    if run_tests_mode:
        optional_gates.append("tests")
    if playtests:
        optional_gates.append("playtests")

    # Gate 1: await_compile
    compile_timeout = min(timeout, 120.0)
    try:
        compile_result = await _ci.await_compile(timeout=compile_timeout)
        if _is_compile_clean(compile_result):
            passed.append("compile")
        else:
            return _fail("await_compile", compile_result, optional_gates)
    except Exception as e:
        return _fail("await_compile", str(e), optional_gates)

    # Gate 2: get_compile_errors
    try:
        errors = await _con.get_compile_errors()
        if _is_errors_clean(errors):
            passed.append("errors_clean")
        else:
            return _fail("get_compile_errors", errors, optional_gates)
    except Exception as e:
        return _fail("get_compile_errors", str(e), optional_gates)

    # Gate 3: console since mark_id (optional)
    remaining_optional = list(optional_gates)
    if mark_id:
        remaining_optional = [g for g in optional_gates if g != "console"]
        try:
            console_result = await _con.get_console_since(
                mark_id, level="error,exception,assert"
            )
            filtered = _DROPPED_RE.sub("", console_result).strip()
            if filtered and filtered != "no logs":
                return _fail("console_since", console_result, remaining_optional)
            passed.append("console_clean")
        except Exception as e:
            return _fail("console_since", str(e), remaining_optional)

    # Gate 4: run_tests_wait (optional)
    if run_tests_mode:
        remaining_optional = ["playtests"] if playtests else []
        test_result = await _test.run_tests_wait(
            mode=run_tests_mode,
            filter=test_filter,
            timeout=timeout,
        )
        if _is_tests_pass(test_result):
            passed.append(f"tests({_extract_ratio(test_result)})")
        else:
            classes = re.findall(r"^FAIL\s+(\w+)", test_result, re.MULTILINE)
            rec = f'run_tests_wait mode="{run_tests_mode}"'
            if classes:
                rec += f' filter="{"|".join(dict.fromkeys(classes))}"'
            first_line = test_result.split("\n")[0]
            return _fail("tests", f"{first_line}\n  recommended: {rec}", remaining_optional)

    # Gate 5: run_playtest_suite (optional)
    if playtests:
        if restart_between:
            await _stop_play_mode()
        suite_result = await _rt.run_playtest_suite(playtests)
        if _is_suite_pass(suite_result):
            passed.append(f"playtests({_extract_ratio(suite_result)})")
        else:
            return _fail("playtests", suite_result, [])

    return "PASS: " + " + ".join(passed)


def register(mcp, send, args):
    bind(globals(), send, args)
    from ._annotations import RW as _RW
    mcp.tool(annotations=_RW)(verify_after_change)
