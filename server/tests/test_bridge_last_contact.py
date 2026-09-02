"""last_contact_age_s / pending_queue_depth diagnostic fields on UnityBridge.

`_last_contact_at` marks the last *successful* contact with Unity — a pong on
the heartbeat ping, or a completed send() round-trip. It must NOT be touched
by a failed/timed-out attempt. `pending_queue_depth` is a plain getter over
the existing send-queue depth; no new instrumentation.
"""
import ast
import asyncio
import pathlib
import time
from unittest.mock import MagicMock, patch

from helpers import make_writer
from unity_mcp.bridge import UnityBridge


def test_module_has_no_test_to_test_import():
    """Guard: importing a helper from a sibling test_*.py module makes
    collecting this file also execute that sibling's module-level code,
    coupling the two files' futures (see C1-round2.md #8)."""
    tree = ast.parse(pathlib.Path(__file__).read_text(encoding="utf-8"))
    modules = [n.module for n in ast.walk(tree) if isinstance(n, ast.ImportFrom)]
    assert not any(m and m.startswith("test_") for m in modules), modules


def _make_connected_bridge() -> UnityBridge:
    """Return a UnityBridge with a live writer mock so connected == True."""
    from unity_mcp.compile_state import CompileStateProbe
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()
    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    bridge._writer = mock_writer
    bridge._reader = MagicMock()
    return bridge


async def test_updated_on_successful_ping():
    """_raw_ping() on a confirmed pong sets _last_contact_at to "now", not to a
    stale/construction-time value (Arm B guard: `is not None` alone would also
    pass a bug that sets the field once in __init__)."""
    bridge = _make_connected_bridge()
    bridge._writer = make_writer()
    assert bridge.last_contact_age_s is None

    async def echo_ping_id():
        return {"id": f"hb{bridge._counter:04x}", "ok": True, "data": "pong"}

    before = time.monotonic()
    with patch.object(bridge, "_read_response", new=echo_ping_id):
        await bridge._raw_ping(timeout=1.0)

    assert bridge._last_contact_at is not None
    assert bridge._last_contact_at >= before
    assert bridge.last_contact_age_s >= 0.0


async def test_not_updated_on_ping_timeout():
    """A timed-out ping must leave _last_contact_at untouched (still None)."""
    bridge = _make_connected_bridge()
    bridge._writer = make_writer()

    async def raise_timeout():
        raise TimeoutError("simulated ping timeout")

    with patch.object(bridge, "_read_response", new=raise_timeout):
        try:
            await bridge._raw_ping(timeout=1.0)
        except TimeoutError:
            pass

    assert bridge._last_contact_at is None
    assert bridge.last_contact_age_s is None


async def test_updated_on_successful_send():
    """A completed send() round-trip (consumer success path) sets _last_contact_at."""
    bridge = _make_connected_bridge()
    before = time.monotonic()

    async def fake_send_with_retry(cmd, payload, msg_id, timeout, deadline, op_id=""):
        return {"id": msg_id, "ok": True, "data": "ok"}

    with patch.object(bridge, "_send_with_retry", new=fake_send_with_retry):
        result = await bridge.send("ping", {})

    assert result["ok"] is True
    assert bridge._last_contact_at is not None
    assert bridge._last_contact_at >= before


async def test_pending_queue_depth_reflects_qsize():
    """pending_queue_depth wraps _send_queue.qsize() — no consumer running."""
    bridge = _make_connected_bridge()
    assert bridge.pending_queue_depth == 0

    for i in range(2):
        bridge._send_queue.put_nowait(
            ("get_status", b"{}", f"{i:04x}", 30.0, time.monotonic() + 30.0, f"op-{i}", asyncio.Future())
        )

    assert bridge.pending_queue_depth == 2
