"""Tests for cmd_pull (git pull for local installs) in install/commands.py."""
import subprocess
from unittest.mock import patch, MagicMock

import pytest

import install.commands as cmds


class FakeUI:
    def __init__(self):
        self.oks = []
        self.errors = []
        self.infos = []

    def ok(self, msg):    self.oks.append(msg)
    def error(self, msg): self.errors.append(msg)
    def info(self, msg):  self.infos.append(msg)
    def fail(self, msg):  self.errors.append(msg)
    def box(self, lines): pass


def test_cmd_pull_git_success(tmp_path):
    # Arrange: fake git repo root (has .git dir)
    (tmp_path / ".git").mkdir()
    ui = FakeUI()

    with patch("subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(returncode=0)
        result = cmds.cmd_pull(tmp_path, ui)

    assert result == 0
    mock_run.assert_called_once()
    call_args = mock_run.call_args
    assert "git" in call_args[0][0]
    assert "pull" in call_args[0][0]
    assert "--tags" in call_args[0][0]
    assert len(ui.oks) == 1


def test_cmd_pull_no_git_dir(tmp_path):
    # No .git directory — not a clone
    ui = FakeUI()

    result = cmds.cmd_pull(tmp_path, ui)

    assert result == 1
    assert len(ui.errors) == 1
    assert "git" in ui.errors[0].lower() or "clone" in ui.errors[0].lower()


def test_cmd_pull_git_failure(tmp_path):
    (tmp_path / ".git").mkdir()
    ui = FakeUI()

    with patch("subprocess.run") as mock_run:
        mock_run.return_value = MagicMock(returncode=1)
        result = cmds.cmd_pull(tmp_path, ui)

    assert result == 1
    assert len(ui.errors) == 1


def test_cmd_pull_subprocess_exception(tmp_path):
    (tmp_path / ".git").mkdir()
    ui = FakeUI()

    with patch("subprocess.run", side_effect=FileNotFoundError("git not found")):
        result = cmds.cmd_pull(tmp_path, ui)

    assert result == 1
    assert len(ui.errors) == 1
