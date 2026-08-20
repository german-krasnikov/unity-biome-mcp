"""Generation-aware compile fence tests.

Problem: await_compile returns 'clean' before compile starts because Unity's
file monitor has 3-10s latency after a .cs write.

Solution: await_compile(expected_generation=N) waits until generation N
compiles, not just until the current compile state is idle.

Generation is reported by Unity in sync_status responses as gen=N.
"""
import pytest
from unittest.mock import AsyncMock, patch

import unity_mcp.tools.code_intel as _ci


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original = _ci._send
    yield
    _ci._send = original


def _sync_status_send(responses: list[str], errors_response: str = ""):
    """Mock send that returns sync_status responses in sequence, then repeats last.

    Attaches a .call_count list to the returned coroutine so tests can assert
    how many times sync_status was polled.
    """
    it = iter(responses)
    last = [responses[-1]]
    counter = [0]

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            counter[0] += 1
            try:
                val = next(it)
            except StopIteration:
                val = last[0]
            last[0] = val
            return val
        if cmd == "get_compile_errors":
            return errors_response
        if cmd == "compile_status":
            return "idle|0.0"
        raise AssertionError(f"Unexpected cmd: {cmd}")

    _send.call_count = counter
    return _send


# ---------------------------------------------------------------------------
# Test 1: fence waits until target generation compiles
# ---------------------------------------------------------------------------

async def test_compile_fence_waits_for_generation():
    """await_compile(expected_generation=5) waits until gen 5 compile finishes.

    Sequence: gen=4 compiling → gen=5 compiling → gen=5 ready → clean.
    The fence must poll through all 4 state transitions; call_count proves it
    didn't short-circuit on the first response.
    """
    mock = _sync_status_send([
        "epoch=1|state=compiling|gen=4",   # gen 4 in progress, not gen 5 yet
        "epoch=1|state=compiling|gen=4",   # still gen 4
        "epoch=2|state=compiling|gen=5",   # gen 5 started
        "epoch=2|state=ready|gen=5",       # gen 5 done
    ])
    _ci._send = mock
    result = await _ci.await_compile(timeout=60.0, expected_generation=5)
    assert "compile clean" in result
    assert mock.call_count[0] >= 4, (
        f"Expected >= 4 sync_status polls to traverse all state transitions, "
        f"got {mock.call_count[0]}"
    )


# ---------------------------------------------------------------------------
# Test 2: idle before compile starts is NOT ready
# ---------------------------------------------------------------------------

async def test_compile_fence_clean_before_start_is_not_ready():
    """Idle state with gen < expected_generation must NOT return clean.

    The compiler hasn't been triggered yet — we must keep waiting.
    call_count proves the fence didn't treat the first idle response as done.
    """
    mock = _sync_status_send([
        "epoch=0|state=idle|gen=4",        # idle but gen 4 < 5 → not done yet
        "epoch=0|state=idle|gen=4",        # still waiting
        "epoch=3|state=compiling|gen=5",   # gen 5 triggered
        "epoch=3|state=ready|gen=5",       # gen 5 complete
    ])
    _ci._send = mock
    result = await _ci.await_compile(timeout=60.0, expected_generation=5)
    assert "compile clean" in result
    assert mock.call_count[0] >= 4, (
        f"Expected >= 4 sync_status polls (idle/gen4 must NOT short-circuit), "
        f"got {mock.call_count[0]}"
    )


# ---------------------------------------------------------------------------
# Test 3: already at target generation → returns clean immediately
# ---------------------------------------------------------------------------

async def test_compile_fence_clean_after_completion_is_ready():
    """If gen >= expected_generation and state is idle, return clean immediately.

    No waiting needed — target generation already compiled.
    """
    call_count = [0]

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            call_count[0] += 1
            return "epoch=0|state=idle|gen=5"
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected cmd: {cmd}")

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0, expected_generation=5)
    assert "compile clean" in result
    assert call_count[0] == 1, "Should return after first sync_status poll"


# ---------------------------------------------------------------------------
# Test 4: timeout returns typed error with generation info
# ---------------------------------------------------------------------------

async def test_compile_fence_timeout_returns_typed_error():
    """Timeout gives generation-specific error message, not generic timeout.

    Error format: 'Compile generation N not reached within Xs (current: M)'
    """
    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            return "epoch=0|state=idle|gen=4"
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected cmd: {cmd}")

    _ci._send = _send
    result = await _ci.await_compile(timeout=0.001, expected_generation=5)
    assert "Compile generation 5" in result
    assert "current: 4" in result


# ---------------------------------------------------------------------------
# Test 5: backward compat — no expected_generation, current behavior
# ---------------------------------------------------------------------------

async def test_compile_fence_backward_compat():
    """Without expected_generation, await_compile behaves as before.

    idle compile_status → returns clean without waiting for generation.
    """
    import os
    with patch.dict(os.environ, {"UNITY_MCP_COMPILE_SETTLE_SECS": "0"}):
        _ci._send = _sync_status_send(
            ["epoch=0|state=idle|gen=3"],  # sync_status available but no expected_gen
            errors_response="",
        )
        result = await _ci.await_compile(timeout=60.0)
    # Should complete without generation gate
    assert "compile clean" in result
