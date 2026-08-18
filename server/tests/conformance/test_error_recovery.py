"""Gate 7: Error Recovery — graceful error handling and state safety."""

import pytest

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


async def test_invalid_command_returns_error(conformance_worker):
    """Unknown command returns ok=false with descriptive error."""
    worker, bridge = conformance_worker
    resp = await bridge.send("nonexistent_cmd_xyz_123", {})
    assert not resp.get("ok", True), "unknown command should return ok=false"
    err = resp.get("err", "")
    assert "not registered" in err.lower() or "unknown" in err.lower(), \
        f"error message not descriptive: {err}"


async def test_python_only_command_returns_clear_error(conformance_worker):
    """Python-only tool via TCP returns specific error message."""
    worker, bridge = conformance_worker
    resp = await bridge.send("mcp_status", {})
    assert not resp.get("ok", True), "Python-only command should fail via TCP"
    err = resp.get("err", "")
    assert "python-only" in err.lower() or "python" in err.lower(), \
        f"error doesn't mention Python-only: {err}"


async def test_error_does_not_leak_state(conformance_worker):
    """After an error, subsequent valid commands still work."""
    worker, bridge = conformance_worker
    await bridge.send("nonexistent_cmd_xyz_123", {})

    resp = await bridge.send("get_status", {})
    assert resp["ok"], f"valid command failed after error: {resp}"
    assert "data" in resp


async def test_get_compile_errors_after_error(conformance_worker):
    """Compile errors command works after a prior error."""
    worker, bridge = conformance_worker
    await bridge.send("nonexistent_cmd_xyz_123", {})

    resp = await bridge.send("get_compile_errors", {})
    assert resp["ok"], f"get_compile_errors failed: {resp}"
