"""Fail-closed tests for release-gating JUnit ingestion."""


import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from gauntlet.junit import (  # noqa: E402
    JUnitError,
    parse_attested_pytest_junit,
    parse_pytest_junit,
)


def _write(tmp_path: Path, body: str) -> Path:
    path = tmp_path / "junit.xml"
    path.write_text(body, encoding="utf-8")
    return path


def test_parser_aggregates_every_suite_and_leaf(tmp_path: Path) -> None:
    path = _write(
        tmp_path,
        """<?xml version="1.0"?>
<testsuites name="pytest tests">
  <testsuite name="alpha" tests="2" failures="1" errors="0" skipped="0">
    <testcase classname="tests.test_alpha" name="test_pass" />
    <testcase classname="tests.test_alpha" name="test_fail">
      <failure message="expected failure" />
    </testcase>
  </testsuite>
  <testsuite name="beta" tests="2" failures="0" errors="1" skipped="1">
    <testcase classname="tests.test_beta" name="test_skip">
      <skipped message="unsupported" />
    </testcase>
    <testcase classname="tests.test_beta" name="test_error">
      <error message="fixture error" />
    </testcase>
  </testsuite>
</testsuites>
""",
    )

    result = parse_pytest_junit(path)

    assert result.total == 4
    assert result.passed == 1
    assert result.failed == 2
    assert result.skipped == 1
    assert result.scenario_ids == (
        "tests.test_alpha::test_fail",
        "tests.test_alpha::test_pass",
        "tests.test_beta::test_error",
        "tests.test_beta::test_skip",
    )


def test_parser_accepts_single_testsuite_root(tmp_path: Path) -> None:
    path = _write(
        tmp_path,
        """<testsuite name="pytest" tests="1" failures="0" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_ok" />
</testsuite>""",
    )

    result = parse_pytest_junit(path)

    assert result.total == 1
    assert result.passed == 1
    assert result.scenario_ids == ("tests.test_one::test_ok",)


def test_attested_parser_uses_reserved_scenario_and_pytest_node_properties(tmp_path: Path) -> None:
    path = _write(
        tmp_path,
        """<testsuite name="pytest" tests="1" failures="0" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_display_name">
    <properties>
      <property name="gauntlet_scenario_id" value="identity-handshake" />
      <property name="gauntlet_pytest_node_id" value="server/tests/test_one.py::test_actual" />
    </properties>
  </testcase>
</testsuite>""",
    )

    result = parse_attested_pytest_junit(path)

    assert result.scenario_ids == ("identity-handshake",)
    assert result.scenario_nodes == (
        ("identity-handshake", "server/tests/test_one.py::test_actual"),
    )


@pytest.mark.parametrize(
    ("properties", "message"),
    [
        ("", "reserved"),
        (
            '<property name="gauntlet_scenario_id" value="identity" />',
            "pytest node",
        ),
        (
            """<property name="gauntlet_scenario_id" value="identity" />
      <property name="gauntlet_scenario_id" value="replacement" />
      <property name="gauntlet_pytest_node_id" value="server/tests/test_one.py::test_ok" />""",
            "duplicate",
        ),
    ],
)
def test_attested_parser_rejects_missing_or_duplicate_reserved_properties(
    tmp_path: Path,
    properties: str,
    message: str,
) -> None:
    path = _write(
        tmp_path,
        f"""<testsuite tests="1" failures="0" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_ok">
    <properties>{properties}</properties>
  </testcase>
</testsuite>""",
    )

    with pytest.raises(JUnitError, match=message):
        parse_attested_pytest_junit(path)


@pytest.mark.parametrize(
    ("body", "message"),
    [
        ("<not-junit />", "root"),
        ("<testsuites />", "testcase"),
        (
            """<testsuite tests="2" failures="0" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_ok" />
</testsuite>""",
            "declared test count",
        ),
        (
            """<testsuite tests="1" failures="1" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_ok" />
</testsuite>""",
            "declared outcome counts",
        ),
        (
            """<testsuite tests="-1" failures="0" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_ok" />
</testsuite>""",
            "non-negative",
        ),
        (
            """<testsuite tests="1" failures="0" errors="0" skipped="0">
  <testcase name="test_ok" />
</testsuite>""",
            "classname",
        ),
        (
            """<testsuite tests="2" failures="0" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_ok" />
  <testcase classname="tests.test_one" name="test_ok" />
</testsuite>""",
            "duplicate scenario",
        ),
        (
            """<testsuite tests="1" failures="1" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_bad">
    <failure /><skipped />
  </testcase>
</testsuite>""",
            "multiple outcomes",
        ),
        (
            """<testsuites>
  <testsuite tests="1" failures="0" errors="0" skipped="0">
    <testcase classname="tests.test_one" name="test_owned" />
  </testsuite>
  <testcase classname="tests.test_one" name="test_orphan" />
</testsuites>""",
            "owned by exactly one leaf suite",
        ),
        (
            """<testsuite tests="1" failures="0" errors="0" skipped="0">
  <wrapper><testcase classname="tests.test_one" name="test_wrapped" /></wrapper>
</testsuite>""",
            "unsupported child",
        ),
        (
            """<testsuites tests="1" failures="1" errors="0" skipped="0">
  <testsuite tests="1" failures="0" errors="0" skipped="0">
    <testcase classname="tests.test_one" name="test_ok" />
  </testsuite>
</testsuites>""",
            "testsuites declared outcome counts",
        ),
        (
            """<testsuite tests="1" failures="0" errors="0" skipped="0">
  <testcase classname="tests.test_one" name="test_hidden_failure">
    <wrapper><failure /></wrapper>
  </testcase>
</testsuite>""",
            "testcase contains an unsupported child",
        ),
    ],
)
def test_parser_rejects_ambiguous_or_inconsistent_xml(
    tmp_path: Path,
    body: str,
    message: str,
) -> None:
    with pytest.raises(JUnitError, match=message):
        parse_pytest_junit(_write(tmp_path, body))


def test_parser_rejects_malformed_or_missing_xml(tmp_path: Path) -> None:
    with pytest.raises(JUnitError, match="does not exist"):
        parse_pytest_junit(tmp_path / "missing.xml")
    with pytest.raises(JUnitError, match="valid XML"):
        parse_pytest_junit(_write(tmp_path, "<testsuite>"))
