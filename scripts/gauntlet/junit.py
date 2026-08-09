"""Strict JUnit ingestion for release-gating evidence.

The analytics parser intentionally remains tolerant. This module is a separate
fail-closed boundary: missing leaves, malformed counters, and ambiguous outcomes
are evidence errors rather than empty success results.
"""

from __future__ import annotations

import xml.etree.ElementTree as ET
from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

_OUTCOME_TAGS = frozenset({"failure", "error", "skipped"})
_SUITE_METADATA_TAGS = frozenset({"properties", "system-out", "system-err"})
_COUNT_KEYS = ("tests", "failures", "errors", "skipped")
_MAX_JUNIT_BYTES = 16 * 1024 * 1024


class JUnitError(ValueError):
    """Raised when JUnit cannot prove an exact, coherent test run."""


@dataclass(frozen=True, slots=True)
class JUnitResult:
    passed: int
    failed: int
    skipped: int
    total: int
    scenario_ids: tuple[str, ...]


def parse_pytest_junit(path: Path) -> JUnitResult:
    """Parse all pytest JUnit leaves and validate every declared suite count."""
    try:
        size = path.stat().st_size
    except FileNotFoundError as exc:
        raise JUnitError("JUnit file does not exist") from exc
    except OSError as exc:
        raise JUnitError("JUnit file metadata cannot be read") from exc
    if size > _MAX_JUNIT_BYTES:
        raise JUnitError("JUnit file exceeds the release evidence size limit")

    try:
        payload = path.read_bytes()
    except OSError as exc:
        raise JUnitError("JUnit file cannot be read") from exc
    return parse_pytest_junit_bytes(payload)


def parse_pytest_junit_bytes(payload: bytes) -> JUnitResult:
    """Parse a single already-verified JUnit byte payload."""
    if len(payload) > _MAX_JUNIT_BYTES:
        raise JUnitError("JUnit file exceeds the release evidence size limit")
    try:
        root = ET.fromstring(payload)
    except ET.ParseError as exc:
        raise JUnitError("JUnit file is not valid XML") from exc

    root_tag = _local_name(root.tag)
    if root_tag not in {"testsuite", "testsuites"}:
        raise JUnitError("JUnit root must be testsuite or testsuites")

    suites = [element for element in root.iter() if _local_name(element.tag) == "testsuite"]
    if not suites:
        raise JUnitError("JUnit contains no testcase suites")
    leaf_cases: list[ET.Element] = []
    for suite in suites:
        leaf_cases.extend(_validate_suite(suite))

    cases = [element for element in root.iter() if _local_name(element.tag) == "testcase"]
    if not cases:
        raise JUnitError("JUnit contains no testcase leaves")
    if {id(case) for case in cases} != {id(case) for case in leaf_cases}:
        raise JUnitError("JUnit testcase is not owned by exactly one leaf suite")
    if root_tag == "testsuites":
        unexpected = [child for child in root if _local_name(child.tag) not in {"testsuite", *_SUITE_METADATA_TAGS}]
        if unexpected:
            raise JUnitError("JUnit testsuites root contains an unsupported child")
        _validate_testsuites_counts(root, cases)

    scenario_ids: list[str] = []
    passed = failed = skipped = 0
    for case in cases:
        scenario_ids.append(_scenario_id(case))
        outcome = _case_outcome(case)
        if outcome == "passed":
            passed += 1
        elif outcome == "skipped":
            skipped += 1
        else:
            failed += 1

    if len(set(scenario_ids)) != len(scenario_ids):
        raise JUnitError("JUnit contains a duplicate scenario ID")
    scenario_ids.sort()
    total = len(cases)
    if passed + failed + skipped != total:
        raise JUnitError("JUnit leaf outcomes do not match the total")
    return JUnitResult(
        passed=passed,
        failed=failed,
        skipped=skipped,
        total=total,
        scenario_ids=tuple(scenario_ids),
    )


def _validate_suite(suite: ET.Element) -> list[ET.Element]:
    direct_cases = [child for child in suite if _local_name(child.tag) == "testcase"]
    nested_suites = [child for child in suite if _local_name(child.tag) == "testsuite"]
    unexpected = [
        child for child in suite if _local_name(child.tag) not in {"testcase", "testsuite", *_SUITE_METADATA_TAGS}
    ]
    if unexpected:
        raise JUnitError("JUnit suite contains an unsupported child")
    if direct_cases and nested_suites:
        raise JUnitError("JUnit suite mixes direct testcase leaves and nested suites")

    descendant_cases = [element for element in suite.iter() if _local_name(element.tag) == "testcase"]
    if not descendant_cases:
        raise JUnitError("JUnit suite contains no testcase leaves")
    if direct_cases and len(direct_cases) != len(descendant_cases):
        raise JUnitError("JUnit testcase must be a direct child of its leaf suite")

    declared = {key: _required_count(suite, key) for key in _COUNT_KEYS}
    if declared["tests"] != len(descendant_cases):
        raise JUnitError("JUnit declared test count does not match testcase leaves")

    actual = {"failures": 0, "errors": 0, "skipped": 0}
    outcome_keys = {"failure": "failures", "error": "errors", "skipped": "skipped"}
    for case in descendant_cases:
        outcome = _case_outcome(case)
        if outcome in outcome_keys:
            actual[outcome_keys[outcome]] += 1
    if any(declared[key] != actual[key] for key in actual):
        raise JUnitError("JUnit declared outcome counts do not match testcase leaves")
    return direct_cases


def _scenario_id(case: ET.Element) -> str:
    classname = case.get("classname")
    name = case.get("name")
    if not classname:
        raise JUnitError("JUnit testcase classname must be non-empty")
    if not name:
        raise JUnitError("JUnit testcase name must be non-empty")
    return f"{classname}::{name}"


def _case_outcome(case: ET.Element) -> str:
    direct_children = list(case)
    unexpected = [
        child for child in direct_children if _local_name(child.tag) not in {*_OUTCOME_TAGS, *_SUITE_METADATA_TAGS}
    ]
    if unexpected:
        raise JUnitError("JUnit testcase contains an unsupported child")
    direct_outcomes = [child for child in direct_children if _local_name(child.tag) in _OUTCOME_TAGS]
    nested_outcomes = [
        element for element in case.iter() if element is not case and _local_name(element.tag) in _OUTCOME_TAGS
    ]
    if len(nested_outcomes) != len(direct_outcomes):
        raise JUnitError("JUnit testcase contains a nested outcome")
    outcomes = [_local_name(child.tag) for child in direct_outcomes]
    if len(outcomes) > 1:
        raise JUnitError("JUnit testcase has multiple outcomes")
    return outcomes[0] if outcomes else "passed"


def _validate_testsuites_counts(root: ET.Element, cases: list[ET.Element]) -> None:
    present = tuple(key for key in _COUNT_KEYS if root.get(key) is not None)
    if not present:
        return
    if present != _COUNT_KEYS:
        raise JUnitError("JUnit testsuites aggregate counts must be complete")
    declared = {key: _required_count(root, key) for key in _COUNT_KEYS}
    actual = {"tests": len(cases), "failures": 0, "errors": 0, "skipped": 0}
    outcome_keys = {"failure": "failures", "error": "errors", "skipped": "skipped"}
    for case in cases:
        outcome = _case_outcome(case)
        if outcome in outcome_keys:
            actual[outcome_keys[outcome]] += 1
    if declared["tests"] != actual["tests"]:
        raise JUnitError("JUnit testsuites declared test count does not match testcase leaves")
    if any(declared[key] != actual[key] for key in ("failures", "errors", "skipped")):
        raise JUnitError("JUnit testsuites declared outcome counts do not match testcase leaves")


def _required_count(suite: ET.Element, key: str) -> int:
    raw = suite.get(key)
    try:
        value = int(raw) if raw is not None else None
    except ValueError as exc:
        raise JUnitError(f"JUnit {key} count must be an integer") from exc
    if value is None:
        raise JUnitError(f"JUnit suite is missing the {key} count")
    if value < 0:
        raise JUnitError("JUnit counts must be non-negative")
    return value


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]
