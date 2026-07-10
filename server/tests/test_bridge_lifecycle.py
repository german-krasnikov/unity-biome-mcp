"""TCP connection lifecycle tests — CLOSE_WAIT prevention, reconnect races, dead detection.

Covers:
- close() calls shutdown(SHUT_RDWR) to send TCP FIN
- close() nulls writer BEFORE awaiting wait_closed (prevents concurrent access)
- close() survives wait_closed timeout
- _reconnect() only assigns self._reader/writer after ping succeeds
- _reconnect() closes new socket on ping failure (no leak)
- connected property: True iff writer is not None and not is_closing()
"""
import asyncio
import socket
import struct
import json
from unittest.mock import AsyncMock, Mock, MagicMock, patch, call

import pytest

from unity_mcp.bridge import UnityBridge
from helpers import make_writer, make_idle_probe, ping_response, reconnect_preamble


# ---------------------------------------------------------------------------
# Fix 1: close() — shutdown + null-first
# ---------------------------------------------------------------------------

async def test_close_calls_socket_shutdown():
    """close() must call shutdown(SHUT_RDWR) on underlying socket."""
    mock_sock = Mock()
    writer = make_writer()
    writer.get_extra_info = Mock(return_value=mock_sock)

    with patch("asyncio.open_connection", return_value=(AsyncMock(), writer)):
        bridge = UnityBridge()
        await bridge.connect()
        await bridge.close()

    mock_sock.shutdown.assert_called_once_with(socket.SHUT_RDWR)


async def test_close_nulls_writer_before_closing():
    """self._writer must be None before wait_closed() is awaited."""
    writer_state_during_wait = []
    writer = make_writer()
    mock_sock = Mock()
    writer.get_extra_info = Mock(return_value=mock_sock)

    async def slow_wait_closed():
        # capture writer state at the moment wait_closed is awaited
        writer_state_during_wait.append(bridge._writer)
        return None

    writer.wait_closed = slow_wait_closed

    with patch("asyncio.open_connection", return_value=(AsyncMock(), writer)):
        bridge = UnityBridge()
        await bridge.connect()
        await bridge.close()

    assert len(writer_state_during_wait) == 1
    assert writer_state_during_wait[0] is None, "writer must be nulled before wait_closed"


async def test_close_survives_wait_closed_timeout():
    """close() returns even if wait_closed hangs (timeout=2s in impl)."""
    writer = make_writer()
    mock_sock = Mock()
    writer.get_extra_info = Mock(return_value=mock_sock)

    async def forever():
        await asyncio.Future()  # never resolves

    writer.wait_closed = forever

    with patch("asyncio.open_connection", return_value=(AsyncMock(), writer)):
        bridge = UnityBridge()
        await bridge.connect()

        # Should complete within 3s even though wait_closed hangs
        await asyncio.wait_for(bridge.close(), timeout=3.0)

    assert bridge._writer is None


# ---------------------------------------------------------------------------
# Fix 2: _reconnect() — assign-after-success
# ---------------------------------------------------------------------------

async def test_reconnect_assigns_after_success():
    """After successful ping, self._writer must be the new writer."""
    ping_hdr, ping_pay = ping_response()
    new_reader = AsyncMock()
    new_reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
    new_writer = make_writer()
    new_writer.get_extra_info = Mock(return_value=Mock())

    with patch("unity_mcp.bridge.asyncio.open_connection",
               return_value=(new_reader, new_writer)):
        bridge = UnityBridge()
        await bridge._reconnect()

    assert bridge._writer is new_writer
    assert bridge._reader is new_reader


async def test_reconnect_closes_new_socket_on_ping_failure():
    """If ping fails, new writer must be closed and self._writer stays None."""
    new_reader = AsyncMock()
    new_reader.readexactly = AsyncMock(
        side_effect=asyncio.TimeoutError("ping timed out")
    )
    new_writer = make_writer()
    mock_sock = Mock()
    new_writer.get_extra_info = Mock(return_value=mock_sock)

    with patch("unity_mcp.bridge.asyncio.open_connection",
               return_value=(new_reader, new_writer)):
        bridge = UnityBridge()
        with pytest.raises((asyncio.TimeoutError, ConnectionError, Exception)):
            await bridge._reconnect()

    # New writer must have been closed (leak prevention)
    new_writer.close.assert_called()
    # self._writer must not point to the leaked socket
    assert bridge._writer is None


def test_connected_false_when_writer_none():
    """connected returns False when no writer (never connected)."""
    bridge = UnityBridge()
    assert bridge.connected is False


def test_connected_false_when_writer_closing():
    """connected returns False when writer.is_closing() is True."""
    writer = make_writer()
    writer.is_closing = Mock(return_value=True)
    bridge = UnityBridge()
    bridge._writer = writer
    assert bridge.connected is False


# ---------------------------------------------------------------------------
# RC1 — new contract: writer not None + not is_closing()
# ---------------------------------------------------------------------------

def test_connected_true_with_live_writer():
    """connected returns True iff writer is set and not closing.

    RC1 bug: old code did select+MSG_PEEK; if asyncio drained bytes into
    StreamReader the OS buffer looked empty → BlockingIOError → False negative.
    New code: only checks is_closing(), no socket peek.
    """
    mock_sock = Mock()
    mock_sock.recv.side_effect = BlockingIOError()  # would be False negative in old code
    writer = make_writer()
    writer.get_extra_info = Mock(return_value=mock_sock)
    writer.is_closing = Mock(return_value=False)

    bridge = UnityBridge()
    bridge._writer = writer

    with patch("select.select", return_value=([mock_sock], [], [])):
        result = bridge.connected

    assert result is True


def test_connected_false_after_drain_eof():
    """connected returns False once writer.is_closing() is True (CLOSE_WAIT via asyncio)."""
    writer = make_writer()
    writer.is_closing = Mock(return_value=True)
    bridge = UnityBridge()
    bridge._writer = writer
    assert bridge.connected is False


# ---------------------------------------------------------------------------
# Heartbeat lifecycle: close() stops heartbeat, _reconnect() starts it
# ---------------------------------------------------------------------------

async def test_close_stops_heartbeat():
    """close() must cancel and null the heartbeat task (leak fix)."""
    bridge = UnityBridge()
    bridge.start_heartbeat(interval=60.0)
    task = bridge._heartbeat_task
    assert task is not None and not task.done()

    await bridge.close()
    await asyncio.sleep(0)  # let CancelledError propagate through the loop

    assert bridge._heartbeat_task is None
    # The task must have been cancelled
    assert task.cancelled() or task.done()


async def test_reconnect_starts_heartbeat(mock_connection):
    """_reconnect() must call start_heartbeat() so heartbeat runs after reconnect."""
    from helpers import reconnect_preamble
    mock_reader, mock_writer = mock_connection
    mock_reader.readexactly = AsyncMock(side_effect=reconnect_preamble())

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        assert bridge._heartbeat_task is None

        await bridge._reconnect(fire_callbacks=False)

        assert bridge._heartbeat_task is not None
        bridge.stop_heartbeat()


# ---------------------------------------------------------------------------
# MAJOR #2: _ping_stall_failures must reset on reconnect
# ---------------------------------------------------------------------------

async def test_ping_stall_failures_reset_on_reconnect():
    """_ping_stall_failures must be 0 after successful _reconnect().

    Bug: stall budget leaks across sessions — accumulated stalls from a previous
    connection cause premature force-close after reconnect.
    """
    from helpers import make_idle_probe, reconnect_preamble

    probe = make_idle_probe()
    bridge = UnityBridge(probe=probe)
    bridge._ping_stall_failures = 5  # simulate accumulated stalls

    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=reconnect_preamble())

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        await bridge._reconnect(fire_callbacks=False)

    assert bridge._ping_stall_failures == 0, (
        f"_ping_stall_failures must be reset to 0 after _reconnect(), got {bridge._ping_stall_failures}"
    )
    bridge.stop_heartbeat()
