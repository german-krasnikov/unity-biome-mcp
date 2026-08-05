"""Tests for collect_test_results.py."""
from __future__ import annotations

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
        capture_output=True, text=True, encoding="utf-8",
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
        capture_output=True, text=True, encoding="utf-8",
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
        capture_output=True, text=True, encoding="utf-8",
    )
    assert r.returncode == 0
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["suites"][0]["passed"] == 284


def test_multiple_suites_accumulate(junit_xml, nunit_xml, tmp_path):
    out = tmp_path / "tests.json"
    subprocess.run(
        [sys.executable, SCRIPT, "--add-pytest", str(junit_xml),
         "--suite", "Python", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8",
    )
    subprocess.run(
        [sys.executable, SCRIPT, "--add-nunit", str(nunit_xml),
         "--suite", "C#", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8",
    )
    data = json.loads(out.read_text(encoding="utf-8"))
    assert len(data["suites"]) == 2
    names = {s["name"] for s in data["suites"]}
    assert names == {"Python", "C#"}


def test_duplicate_suite_replaces(junit_xml, tmp_path):
    out = tmp_path / "tests.json"
    subprocess.run(
        [sys.executable, SCRIPT, "--add-manual",
         "--suite", "Python", "--passed", "100", "--failed", "5",
         "--skipped", "0", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8",
    )
    subprocess.run(
        [sys.executable, SCRIPT, "--add-pytest", str(junit_xml),
         "--suite", "Python", "--out", str(out)],
        capture_output=True, text=True, encoding="utf-8",
    )
    data = json.loads(out.read_text(encoding="utf-8"))
    assert len(data["suites"]) == 1
    assert data["suites"][0]["passed"] == 92
