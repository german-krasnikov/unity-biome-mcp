"""Tests for conformance_runner CLI."""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

RUNNER = Path(__file__).resolve().parent.parent / "conformance_runner.py"


def test_runner_rejects_missing_project():
    """--project pointing to nonexistent dir returns error."""
    result = subprocess.run(
        [sys.executable, str(RUNNER), "--project", "/nonexistent/path/xyz"],
        capture_output=True, text=True,
    )
    assert result.returncode != 0
    assert "does not look like a Unity project" in result.stderr


def test_runner_requires_project_arg():
    """Missing --project arg returns error."""
    result = subprocess.run(
        [sys.executable, str(RUNNER)],
        capture_output=True, text=True,
    )
    assert result.returncode != 0


def test_runner_help():
    """--help works."""
    result = subprocess.run(
        [sys.executable, str(RUNNER), "--help"],
        capture_output=True, text=True,
    )
    assert result.returncode == 0
    assert "conformance" in result.stdout.lower()
