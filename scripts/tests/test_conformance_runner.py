"""Tests for conformance_runner CLI."""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

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
