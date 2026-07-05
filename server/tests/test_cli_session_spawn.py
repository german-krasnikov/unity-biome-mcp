"""Characterization tests pinning CliSession.start()'s create_subprocess_exec kwargs.

These pin 4 shipped crash-fixes (stdin mode, stderr capture, 16 MiB line limit,
login-shell PATH prepend) so a future refactor of start() can't silently revert
one of them while every other test stays green. All tests should currently PASS —
RED here signals a live regression, not a missing feature.

Critical gotcha: login_shell_path is imported *locally inside* start()
(`from .backend_def import login_shell_path` @ cli_session.py:48). That statement
re-resolves unity_mcp.backend_def.login_shell_path fresh on every call, so the
correct monkeypatch target is unity_mcp.backend_def.login_shell_path — patching
unity_mcp.cli_session.login_shell_path silently no-ops (no such module attribute).
"""
import asyncio
import os
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.cli_session import CliSession


def _fake_proc():
    p = MagicMock()
    p.pid, p.returncode = 111, None
    p.stdin, p.stdout = MagicMock(), MagicMock()
    p.stderr = MagicMock()
    p.stderr.read = AsyncMock(return_value=b"")  # avoid TypeError if pump exists (task 5)
    return p


async def _spawn(reads_stdin, monkeypatch, login_path=""):
    monkeypatch.setattr(
        "unity_mcp.backend_def.login_shell_path", AsyncMock(return_value=login_path)
    )
    create_mock = AsyncMock(return_value=_fake_proc())
    with patch("unity_mcp.cli_session.asyncio.create_subprocess_exec", create_mock):
        session = CliSession(
            binary="codex", argv=["exec"], env_set={}, env_strip=[], reads_stdin=reads_stdin
        )
        await session.start()
    return create_mock.call_args.kwargs


@pytest.mark.asyncio
async def test_start_stdin_devnull_when_reads_stdin_false(monkeypatch):
    kwargs = await _spawn(False, monkeypatch)
    assert kwargs["stdin"] == asyncio.subprocess.DEVNULL


@pytest.mark.asyncio
async def test_start_stdin_pipe_when_reads_stdin_true(monkeypatch):
    kwargs = await _spawn(True, monkeypatch)
    assert kwargs["stdin"] == asyncio.subprocess.PIPE


@pytest.mark.asyncio
async def test_start_stderr_always_piped(monkeypatch):
    for reads_stdin in (True, False):
        kwargs = await _spawn(reads_stdin, monkeypatch)
        assert kwargs["stderr"] == asyncio.subprocess.PIPE


@pytest.mark.asyncio
async def test_start_sets_16mb_line_limit(monkeypatch):
    kwargs = await _spawn(True, monkeypatch)
    assert kwargs["limit"] == 16 * 1024 * 1024


@pytest.mark.asyncio
async def test_start_prepends_login_shell_path(monkeypatch):
    monkeypatch.setenv("PATH", "/usr/bin:/bin")
    kwargs = await _spawn(True, monkeypatch, login_path="/opt/homebrew/bin:/usr/local/bin")
    assert kwargs["env"]["PATH"] == "/opt/homebrew/bin:/usr/local/bin" + os.pathsep + "/usr/bin:/bin"


@pytest.mark.asyncio
async def test_start_no_path_mutation_when_login_shell_empty(monkeypatch):
    monkeypatch.setenv("PATH", "/usr/bin:/bin")
    kwargs = await _spawn(True, monkeypatch, login_path="")
    assert kwargs["env"]["PATH"] == "/usr/bin:/bin"
