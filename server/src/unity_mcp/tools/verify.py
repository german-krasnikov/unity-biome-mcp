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
import re
from ._common import bind
from . import code_intel as _ci
from . import console as _con
from . import testing as _test
from . import runtime as _rt

_send = None
_args = None


def _is_compile_clean(result: str) -> bool:
    r = result.strip().lower()
    return r.startswith("compile clean") or "no compilation errors" in r or r in ("", "no errors")


def _is_errors_clean(result: str) -> bool:
    r = result.strip()
    return not r or "error cs" not in r.lower()


def _is_tests_pass(result: str) -> bool:
    if result.startswith(("BLOCKED:", "TIMEOUT:", "tests-started")):
        return False
    m = re.search(r"(\d+) failed", result)
    return not (m and int(m.group(1)) > 0)


def _is_suite_pass(result: str) -> bool:
    if not result.startswith("SUITE:"):
        return False
    m = re.search(r"(\d+)/(\d+)", result)
    if m:
        return m.group(1) == m.group(2)
    return "FAIL" not in result


def _extract_ratio(result: str) -> str:
    m = re.search(r"\d+/\d+", result)
    return m.group(0) if m else "?"


def _fail(gate: str, detail: str, skipped: list[str]) -> str:
    lines = [f"FAIL: {gate} gate failed", f"  {detail.strip()}"]
    if skipped:
        lines.append(f"next gates skipped: {', '.join(skipped)}")
    return "\n".join(lines)


async def verify_after_change(
    changed_files: str = "",
    test_filter: str = "",
    run_tests_mode: str = "",
    playtests: str = "",
    mark_id: str = "",
    timeout: float = 300.0,
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
            if console_result.strip() and console_result.strip() != "no logs":
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
