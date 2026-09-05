"""Unit tests for playtest_async.run_via_start_poll — the E04 dispatch/poll
branch that run_playtest(timeout > _RUN_PLAYTEST_SYNC_CEILING_S) delegates to.

No mock_bridge fixture here: run_via_start_poll takes `send` as a plain
parameter (not a bound module global), so these tests exercise it directly
with a recording stub. The full run_playtest() branching + wire-args shape is
covered by test_server_playtest.py's mock_bridge-based integration tests.
"""
import re
from pathlib import Path
from unittest.mock import AsyncMock

import pytest

from unity_mcp.tools import playtest_async

_PROJECT = Path(__file__).parents[2]
_MCP_SERVER_CS = _PROJECT / "unity-plugin/Editor/MCPServer.cs"


class _RecordingSend:
    """Records every (cmd, args, timeout) call; returns queued responses in order."""

    def __init__(self, responses):
        self._responses = list(responses)
        self.calls = []

    async def __call__(self, cmd, args, timeout=0):
        self.calls.append((cmd, args, timeout))
        return self._responses.pop(0)


# ===========================================================================
# Group A: dispatch + poll happy path
# ===========================================================================

async def test_run_via_start_poll_dispatches_start_then_polls_until_terminal(monkeypatch):
    monkeypatch.setattr(playtest_async.asyncio, "sleep", AsyncMock())
    send = _RecordingSend([
        "run_id=abc123",
        "phase=running|step=1/3|elapsed_ms=500",
        "phase=running|step=2/3|elapsed_ms=1500",
        "PLAYTEST: 3/3 (5.0s) OK",
    ])

    result = await playtest_async.run_via_start_poll(send, {"script": "WAIT 1"}, 300.0, 20.0)

    assert result == "PLAYTEST: 3/3 (5.0s) OK"
    assert [c[0] for c in send.calls] == [
        "start_playtest", "get_playtest_run", "get_playtest_run", "get_playtest_run",
    ]
    assert send.calls[0][1] == {"script": "WAIT 1"}
    assert all(c[1] == {"run_id": "abc123"} for c in send.calls[1:])


async def test_run_via_start_poll_uses_tcp_buffer_as_call_timeout(monkeypatch):
    monkeypatch.setattr(playtest_async.asyncio, "sleep", AsyncMock())
    send = _RecordingSend(["run_id=x", "PLAYTEST: 1/1 OK"])

    await playtest_async.run_via_start_poll(send, {}, 300.0, 42.0)

    assert all(c[2] == 42.0 for c in send.calls)


# ===========================================================================
# Group B: bounded poll count (double-red target: interval ignored / literal bound)
# ===========================================================================

async def test_run_via_start_poll_bounded_by_default_interval(monkeypatch):
    sleep_mock = AsyncMock()
    monkeypatch.setattr(playtest_async.asyncio, "sleep", sleep_mock)
    responses = ["run_id=x"] + ["phase=running|step=1/1|elapsed_ms=1"] * 1000
    send = _RecordingSend(responses)

    with pytest.raises(TimeoutError):
        await playtest_async.run_via_start_poll(send, {}, 5.0, 20.0)

    expected_polls = int(5.0 / playtest_async._PLAYTEST_POLL_INTERVAL_S) + 1
    poll_calls = [c for c in send.calls if c[0] == "get_playtest_run"]
    assert len(poll_calls) == expected_polls
    sleep_mock.assert_awaited_with(playtest_async._PLAYTEST_POLL_INTERVAL_S)


async def test_run_via_start_poll_bound_scales_with_patched_interval(monkeypatch):
    sleep_mock = AsyncMock()
    monkeypatch.setattr(playtest_async.asyncio, "sleep", sleep_mock)
    monkeypatch.setattr(playtest_async, "_PLAYTEST_POLL_INTERVAL_S", 2.5)
    responses = ["run_id=x"] + ["phase=running|step=1/1|elapsed_ms=1"] * 1000
    send = _RecordingSend(responses)

    with pytest.raises(TimeoutError):
        await playtest_async.run_via_start_poll(send, {}, 5.0, 20.0)

    poll_calls = [c for c in send.calls if c[0] == "get_playtest_run"]
    assert len(poll_calls) == 3  # int(5.0 / 2.5) + 1
    sleep_mock.assert_awaited_with(2.5)


# ===========================================================================
# Group C: malformed start_playtest response
# ===========================================================================

async def test_run_via_start_poll_raises_when_start_response_has_no_run_id(monkeypatch):
    monkeypatch.setattr(playtest_async.asyncio, "sleep", AsyncMock())
    send = _RecordingSend(["err: something else entirely"])

    with pytest.raises(RuntimeError, match="did not return a run_id"):
        await playtest_async.run_via_start_poll(send, {}, 300.0, 20.0)


# ===========================================================================
# Group D: cross-language contract — Python ceiling < C# hard dispatch timeout
# ===========================================================================

def _parse_run_playtest_timeout_seconds() -> int:
    src = _MCP_SERVER_CS.read_text(encoding="utf-8")
    match = re.search(r"RunPlaytestTimeoutSeconds\s*=\s*(\d+)\s*;", src)
    if not match:
        raise AssertionError(
            "RunPlaytestTimeoutSeconds constant not found in MCPServer.cs — "
            "cross-language contract cannot be verified"
        )
    return int(match.group(1))


def test_sync_ceiling_stays_below_csharp_hard_dispatch_timeout():
    assert playtest_async._RUN_PLAYTEST_SYNC_CEILING_S < _parse_run_playtest_timeout_seconds()


def test_csharp_run_playtest_timeout_constant_is_130():
    """Pins the exact value — a change to either side must be a deliberate, reviewed edit."""
    assert _parse_run_playtest_timeout_seconds() == 130
