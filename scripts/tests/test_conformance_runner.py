"""Tests for conformance_runner CLI."""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import conformance_runner  # noqa: E402

RUNNER = Path(__file__).resolve().parent.parent / "conformance_runner.py"


def test_runner_rejects_missing_project():
    """--project pointing to nonexistent dir returns error."""
    result = subprocess.run(
        [sys.executable, str(RUNNER), "--project", "/nonexistent/path/xyz"],
        capture_output=True, text=True, timeout=60,
    )
    assert result.returncode != 0
    assert "does not look like a Unity project" in result.stderr


def test_runner_requires_project_arg():
    """Missing --project arg returns error."""
    result = subprocess.run(
        [sys.executable, str(RUNNER)],
        capture_output=True, text=True, timeout=60,
    )
    assert result.returncode != 0


def test_runner_help():
    """--help works."""
    result = subprocess.run(
        [sys.executable, str(RUNNER), "--help"],
        capture_output=True, text=True, timeout=60,
    )
    assert result.returncode == 0
    assert "conformance" in result.stdout.lower()


def test_record_flag_passes_env_var(tmp_path):
    """--record sets UNITY_MCP_TRACE_FILE in the subprocess environment."""
    project = tmp_path / "UnityProject"
    (project / "Assets").mkdir(parents=True)
    args = argparse.Namespace(
        port=9500,
        project=str(project),
        second_port=0,
        record="trace.jsonl",
    )
    env = conformance_runner.build_env(args)
    assert env["UNITY_MCP_TRACE_FILE"] == "trace.jsonl"
    assert env["UNITY_MCP_PORT"] == "9500"


def test_default_conformance_marker_excludes_graphics_lane():
    assert "requires_graphics" in conformance_runner.DEFAULT_MARKERS
    assert "not requires_graphics" in conformance_runner.DEFAULT_MARKERS


def test_runner_fails_closed_when_live_pytest_only_skips(
    tmp_path,
    monkeypatch,
    capsys,
):
    project = tmp_path / "UnityProject"
    (project / "Assets").mkdir(parents=True)

    def fake_run(cmd, **_kwargs):
        junit_arg = next(item for item in cmd if item.startswith("--junitxml="))
        Path(junit_arg.split("=", 1)[1]).write_text(
            """<testsuite name="pytest" tests="1" failures="0" errors="0" skipped="1">
  <testcase classname="conformance.test_connect" name="test_unreachable">
    <skipped message="Unity unreachable" />
  </testcase>
</testsuite>""",
            encoding="utf-8",
        )
        return SimpleNamespace(returncode=0)

    monkeypatch.setattr(subprocess, "run", fake_run)

    rc = conformance_runner.main(["--project", str(project), "--port", "9500"])

    captured = capsys.readouterr()
    assert rc == 1
    assert "skipped" in captured.err.lower()


def test_runner_writes_and_assesses_junit_before_success(
    tmp_path,
    monkeypatch,
    capsys,
):
    project = tmp_path / "UnityProject"
    (project / "Assets").mkdir(parents=True)

    def fake_run(cmd, **_kwargs):
        assert any(item.startswith("--junitxml=") for item in cmd)
        junit_arg = next(item for item in cmd if item.startswith("--junitxml="))
        Path(junit_arg.split("=", 1)[1]).write_text(
            """<testsuite name="pytest" tests="1" failures="0" errors="0" skipped="0">
  <testcase classname="conformance.test_connect" name="test_tcp_roundtrip" />
</testsuite>""",
            encoding="utf-8",
        )
        return SimpleNamespace(returncode=0)

    monkeypatch.setattr(subprocess, "run", fake_run)

    rc = conformance_runner.main(["--project", str(project), "--port", "9500"])

    captured = capsys.readouterr()
    assert rc == 0
    assert "CONFORMANCE PASS: 1/1" in captured.out


def test_runner_does_not_print_pass_when_junit_has_failures(
    tmp_path,
    monkeypatch,
    capsys,
):
    project = tmp_path / "UnityProject"
    (project / "Assets").mkdir(parents=True)

    def fake_run(cmd, **_kwargs):
        junit_arg = next(item for item in cmd if item.startswith("--junitxml="))
        Path(junit_arg.split("=", 1)[1]).write_text(
            """<testsuite name="pytest" tests="1" failures="1" errors="0" skipped="0">
  <testcase classname="conformance.test_connect" name="test_dirty">
    <failure message="scene dirty" />
  </testcase>
</testsuite>""",
            encoding="utf-8",
        )
        return SimpleNamespace(returncode=1)

    monkeypatch.setattr(subprocess, "run", fake_run)

    rc = conformance_runner.main(["--project", str(project), "--port", "9500"])

    captured = capsys.readouterr()
    assert rc == 1
    assert "failed" in captured.err.lower()
    assert "CONFORMANCE PASS" not in captured.out
