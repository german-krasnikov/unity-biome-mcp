"""Tests for collect_test_results.py."""

import json
import subprocess
import sys

import pytest

SCRIPT = "scripts/collect_test_results.py"


@pytest.fixture()
def junit_xml(tmp_path):
    xml = tmp_path / "junit.xml"
    xml.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<testsuites><testsuite name="tests" tests="100" failures="2" errors="1" skipped="5">'
        "</testsuite></testsuites>",
        encoding="utf-8",
    )
    return xml


@pytest.fixture()
def nunit_xml(tmp_path):
    xml = tmp_path / "nunit.xml"
    xml.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<test-run total="3290" passed="3286" failed="4" skipped="0" inconclusive="0" />',
        encoding="utf-8",
    )
    return xml


def test_parse_pytest_junit(junit_xml, tmp_path):
    out = tmp_path / "tests.json"
    r = subprocess.run(
        [sys.executable, SCRIPT, "--add-pytest", str(junit_xml),
         "--suite", "Server", "--platform", "linux", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    assert r.returncode == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    s = data["suites"][0]
    assert s["name"] == "Server"
    assert s["passed"] == 92
    assert s["failed"] == 3
    assert s["skipped"] == 5
    assert s["total"] == 100


def test_parse_nunit(nunit_xml, tmp_path):
    out = tmp_path / "tests.json"
    r = subprocess.run(
        [sys.executable, SCRIPT, "--add-nunit", str(nunit_xml),
         "--suite", "C# EditMode", "--platform", "linux", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    assert r.returncode == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    s = data["suites"][0]
    assert s["passed"] == 3286
    assert s["failed"] == 4


def test_add_manual(tmp_path):
    out = tmp_path / "tests.json"
    r = subprocess.run(
        [sys.executable, SCRIPT, "--add-manual",
         "--suite", "Live", "--passed", "284", "--failed", "0", "--skipped", "0",
         "--platform", "macos", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    assert r.returncode == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["suites"][0]["passed"] == 284


def test_multiple_suites_accumulate(junit_xml, nunit_xml, tmp_path):
    out = tmp_path / "tests.json"
    subprocess.run(
        [sys.executable, SCRIPT, "--add-pytest", str(junit_xml),
         "--suite", "Python", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    subprocess.run(
        [sys.executable, SCRIPT, "--add-nunit", str(nunit_xml),
         "--suite", "C#", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    data = json.loads(out.read_text(encoding="utf-8"))
    assert len(data["suites"]) == 2
    names = {s["name"] for s in data["suites"]}
    assert names == {"Python", "C#"}


@pytest.fixture()
def junit_xml_with_duration(tmp_path):
    xml = tmp_path / "junit_duration.xml"
    xml.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<testsuites><testsuite name="tests" tests="10" failures="0" errors="0" '
        'skipped="0" time="12.5"></testsuite></testsuites>',
        encoding="utf-8",
    )
    return xml


@pytest.fixture()
def nunit_xml_with_duration(tmp_path):
    xml = tmp_path / "nunit_duration.xml"
    xml.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<test-run total="2" passed="2" failed="0" skipped="0" inconclusive="0">'
        '<test-suite type="TestSuite">'
        '<test-case classname="FixtureA" fullname="FixtureA.T1" name="T1" duration="1.5" />'
        '<test-case classname="FixtureA" fullname="FixtureA.T2" name="T2" duration="2.75" />'
        "</test-suite></test-run>",
        encoding="utf-8",
    )
    return xml


def test_collected_results_retain_duration_field(
    junit_xml_with_duration, nunit_xml_with_duration, tmp_path
):
    """Persisted suite record carries a numeric `duration`: JUnit sources it
    from the testsuite `time` attribute, NUnit sums per-case `duration`
    attributes via test_timeline.parse_nunit_case_durations (reused, not
    re-walked).

    Double-red: fails if `duration` is dropped from the record (KeyError),
    and fails if the wrong source attribute is read for either format --
    a wrong attribute name silently defaults to 0.0, which does not match
    either fixture's nonzero expected value.
    """
    out = tmp_path / "tests.json"
    subprocess.run(
        [sys.executable, SCRIPT, "--add-pytest", str(junit_xml_with_duration),
         "--suite", "Python", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    subprocess.run(
        [sys.executable, SCRIPT, "--add-nunit", str(nunit_xml_with_duration),
         "--suite", "C#", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    data = json.loads(out.read_text(encoding="utf-8"))
    suites = {s["name"]: s for s in data["suites"]}
    assert suites["Python"]["duration"] == pytest.approx(12.5)
    assert suites["C#"]["duration"] == pytest.approx(4.25)


def test_duplicate_suite_replaces(junit_xml, tmp_path):
    out = tmp_path / "tests.json"
    subprocess.run(
        [sys.executable, SCRIPT, "--add-manual",
         "--suite", "Python", "--passed", "100", "--failed", "5",
         "--skipped", "0", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    subprocess.run(
        [sys.executable, SCRIPT, "--add-pytest", str(junit_xml),
         "--suite", "Python", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8", timeout=60,
    )
    data = json.loads(out.read_text(encoding="utf-8"))
    assert len(data["suites"]) == 1
    assert data["suites"][0]["passed"] == 92
