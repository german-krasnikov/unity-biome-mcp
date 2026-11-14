"""Subprocess lifecycle: kill called when returncode is None, regardless of exception class.

Regression: sampling.py zombie was memorial-fixed. This test pins ALL exception paths.

TimeoutError vs Exception kill contract:
  - TimeoutError branch: kills UNCONDITIONALLY (timed-out proc is always a zombie)
  - Exception branch: kills ONLY if returncode is None (avoid EPERM on already-dead proc)
"""
import asyncio
from unittest.mock import AsyncMock, MagicMock, patch

import pytest


# Exception branch: kill ONLY if returncode is None (avoid EPERM on already-dead proc)
@pytest.mark.parametrize("error_type", [OSError, RuntimeError, ValueError])
async def test_sampling_kills_subprocess_on_exception_when_alive(error_type, monkeypatch):
    """For non-timeout exceptions, proc.kill() fires only when returncode is None."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    proc = MagicMock()
    proc.returncode = None  # still alive — Exception path checks this before kill
    proc.communicate = AsyncMock(side_effect=error_type("boom"))
    proc.kill = MagicMock()
    proc.wait = AsyncMock(return_value=0)

    create_mock = AsyncMock(return_value=proc)
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec", create_mock):
        result = await SamplingService()._run(["echo"], timeout=1.0)

    assert result is None
    proc.kill.assert_called_once()  # Exception path checks returncode is None before kill
    proc.wait.assert_awaited()


# TimeoutError branch: kill UNCONDITIONALLY (timed-out proc is always zombie)
async def test_sampling_kills_unconditionally_on_timeout(monkeypatch):
    """TimeoutError handler kills proc regardless of returncode (unlike Exception path)."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    proc = MagicMock()
    proc.returncode = 0  # already exited but timed out — STILL killed per contract
    proc.communicate = AsyncMock(side_effect=asyncio.TimeoutError("boom"))
    proc.kill = MagicMock()
    proc.wait = AsyncMock(return_value=0)

    create_mock = AsyncMock(return_value=proc)
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec", create_mock):
        result = await SamplingService()._run(["echo"], timeout=1.0)

    assert result is None
    proc.kill.assert_called_once()  # unconditional in TimeoutError branch


async def test_sampling_no_kill_when_already_exited(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    proc = MagicMock()
    proc.returncode = 1
    proc.communicate = AsyncMock(side_effect=RuntimeError("post-exit"))
    proc.kill = MagicMock()
    proc.wait = AsyncMock(return_value=1)

    create_mock = AsyncMock(return_value=proc)
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec", create_mock):
        result = await SamplingService()._run(["echo"], timeout=1.0)

    assert result is None
    proc.kill.assert_not_called()


async def test_sampling_create_failure_no_proc(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService

    create_mock = AsyncMock(side_effect=OSError("ENOENT"))
    with patch("unity_mcp.sampling.asyncio.create_subprocess_exec", create_mock):
        result = await SamplingService()._run(["nonexistent"], timeout=1.0)
    assert result is None
